using System.Collections;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using Inv = _Game.Systems.Inventory.Inventory;

namespace _Game.Systems.Building
{
    /// <summary>
    /// 建造模式控制器 — 状态机 + 输入处理 + 材料扣除 + 实例化
    ///
    /// 状态流转：
    ///   B键 → MenuOnly（菜单可见，未选中物品）
    ///   点击物品/滚轮 → Preview（虚影预览）
    ///   左键（菜单外）→ Building → Preview（可连续建造）
    ///   右键/Esc → Preview→MenuOnly→Inactive（逐级退出）
    ///
    /// 按键：
    ///   B      = 进入/退出建造模式
    ///   左键    = 确认放置（菜单外的点击，由BuildMenuUI.OnGUI处理）
    ///   右键/Esc = 取消预览 / 退出建造模式
    ///   滚轮    = 切换建造物
    ///   数字键1~9 = 快捷切换
    /// </summary>
    public class BuildModeController : MonoBehaviour
    {
        [Header("当前建造物")]
        [Tooltip("Phase 1: Inspector 手动指定。后续由 BuildMenuUI 动态设置。")]
        public BuildableData activeBuildable;

        [Header("按键配置")]
        public KeyCode toggleKey = KeyCode.B;
        public KeyCode cancelKey = KeyCode.Escape;

        [Header("建造进度")]
        [Tooltip("建造进度条持续时间 = buildable.buildDuration（秒）")]
        public float buildDurationMultiplier = 1f;

        [Header("生成")]
        [Tooltip("建造物生成时的 Y 偏移")]
        public float spawnYOffset = 0f;

        // 运行时引用
        private BuildModeState _state = BuildModeState.Inactive;
        private Inv _inventory;
        private GhostPreview _ghostPreview;
        private Coroutine _buildCoroutine;
        private _Game.Systems.Character.StaminaSystem _staminaSystem;

        // 公开状态查询
        public BuildModeState CurrentState => _state;
        public bool IsBuildMode => _state != BuildModeState.Inactive;
        public float BuildProgress { get; private set; } // 0~1, 仅在 Building 状态有效

        private void Awake()
        {
            _inventory = GetComponent<Inv>();
            _ghostPreview = GetComponent<GhostPreview>();
            _staminaSystem = GetComponent<_Game.Systems.Character.StaminaSystem>();
        }

        void OnEnable()
        {
            InputRouter.BindKey(toggleKey, InputPriority.Action, HandleToggleKey, this);
            InputRouter.BindMouse(1, InputPriority.Action, HandleCancel, this);
            InputRouter.BindKey(cancelKey, InputPriority.Action, HandleCancel, this);
        }

        void OnDisable() { InputRouter.UnbindAll(this); }

        bool HandleToggleKey()
        {
            if (_state == BuildModeState.Inactive)
            {
                EnterBuildMode();
                return true;
            }
            ExitBuildMode();
            return true;
        }

        bool HandleCancel()
        {
            if (_state == BuildModeState.Preview)
            {
                CancelPreview();
                return true;
            }
            if (_state == BuildModeState.MenuOnly)
            {
                ExitBuildMode();
                return true;
            }
            return false;
        }

        private void EnterBuildMode()
        {
            _state = BuildModeState.MenuOnly;
            EventBus.Publish(new BuildModeEnteredEvent(null));

            Debug.Log("[BuildModeController] 进入建造模式（待选择建造物）");
        }

        private void TryStartBuilding()
        {
            // 0. 未选择建造物
            if (activeBuildable == null)
            {
                Debug.LogWarning("[BuildModeController] 未选择建造物，请从菜单中选取");
                return;
            }

            // 1. 位置有效性检查
            if (_ghostPreview != null && !_ghostPreview.IsValidPlacement)
            {
                Debug.Log("[BuildModeController] 当前位置不可放置（有阻挡物）");
                return;
            }

            // 2. 材料检查（DevTools 自由建造模式跳过）
            if (!_Game.UI.DevTools.FreeBuildEnabled && !HasRequiredMaterials())
            {
                Debug.LogWarning($"[BuildModeController] 材料不足，无法建造 {activeBuildable.displayName}");
                return;
            }

            // 3. 工具检查（自由建造模式跳过）
            if (!_Game.UI.DevTools.FreeBuildEnabled && activeBuildable.requiredTool != null)
            {
                if (!IsRequiredToolEquipped())
                {
                    Debug.LogWarning($"[BuildModeController] 需要装备 {activeBuildable.requiredTool.itemName} 才能建造");
                    return;
                }
            }

            // 4. 技能等级检查（自由建造模式跳过）
            if (!_Game.UI.DevTools.FreeBuildEnabled && activeBuildable.skillRequirements != null && activeBuildable.skillRequirements.Length > 0)
            {
                var pc = ServiceLocator.Get<_Game.Systems.Character.PlayerCharacter>();
                foreach (var req in activeBuildable.skillRequirements)
                {
                    int skillLevel = pc != null ? pc.GetSkillLevel(req.skill) : 0;
                    if (skillLevel < req.level)
                    {
                        Debug.LogWarning($"[BuildModeController] 建造 {activeBuildable.displayName} 需要 {req.skill} Lv{req.level}，当前 Lv{skillLevel}");
                        return;
                    }
                }
            }

            // 4.5 AI机器人限1台/玩家
            if (activeBuildable.category == BuildableCategory.ElectronicsIndustry
                && activeBuildable.displayName.Contains("AI"))
            {
                var existing = ServiceLocator.Get<_Game.Systems.AIBot.AIBot>();
                if (existing != null && !existing.IsDead)
                {
                    Debug.LogWarning("[BuildModeController] 已有AI机器人，限1台/玩家");
                    return;
                }
            }

            // 5. 开始建造
            _state = BuildModeState.Building;
            _buildCoroutine = StartCoroutine(BuildRoutine());
        }

        // ============================================================
        // Building 状态
        // ============================================================

        private IEnumerator BuildRoutine()
        {
            float duration = _Game.UI.DevTools.InstantBuildEnabled
                ? 0f
                : activeBuildable.buildDuration * buildDurationMultiplier;
            float elapsed = 0f;

            Debug.Log($"[BuildModeController] 开始建造 {activeBuildable.displayName}（{duration:F1}秒）");

            while (elapsed < duration)
            {
                float dt = UnityEngine.Time.deltaTime;
                elapsed += dt;
                BuildProgress = Mathf.Clamp01(elapsed / duration);

                // 体力消耗
                if (activeBuildable.staminaDrainPerSec > 0f && _staminaSystem != null)
                    _staminaSystem.Consume(activeBuildable.staminaDrainPerSec * dt);

                // 饥饿/口渴消耗通过事件发布，由 SurvivalSystem 订阅处理
                if (activeBuildable.hungerDrainPerSec > 0f || activeBuildable.thirstDrainPerSec > 0f)
                {
                    EventBus.Publish(new BuildProgressTickEvent(
                        activeBuildable.hungerDrainPerSec * dt,
                        activeBuildable.thirstDrainPerSec * dt));
                }

                yield return null;
            }

            // 建造完成
            CompleteBuilding();
        }

        private void CompleteBuilding()
        {
            // 1. 扣除材料（DevTools 自由建造模式跳过）
            if (!_Game.UI.DevTools.FreeBuildEnabled)
                DeductMaterials();

            // 2. 获取放置位置
            Vector3 placePos = _ghostPreview != null
                ? _ghostPreview.PreviewPosition
                : transform.position + transform.forward * 3f;

            placePos.y += spawnYOffset;

            // 3. 实例化建造物
            GameObject structure;
            if (activeBuildable.builtPrefab != null)
            {
                structure = Instantiate(activeBuildable.builtPrefab, placePos, Quaternion.identity);
                structure.name = activeBuildable.displayName;
            }
            else
            {
                // 兜底：无 builtPrefab 时生成默认方块
                structure = GameObject.CreatePrimitive(PrimitiveType.Cube);
                structure.transform.position = placePos;
                structure.transform.localScale = activeBuildable.placementSize;
                structure.name = $"[NoPrefab] {activeBuildable.displayName}";
            }

            // 3.5 AI机器人特殊处理：挂载 AIBot + AIBotBuildable
            bool isAIBot = activeBuildable.category == BuildableCategory.ElectronicsIndustry
                && structure.GetComponent<_Game.Systems.AIBot.AIBot>() == null
                && structure.GetComponent<_Game.Systems.AIBot.AIBotBuildable>() == null
                && activeBuildable.displayName.Contains("AI");

            if (isAIBot)
            {
                var bot = structure.AddComponent<_Game.Systems.AIBot.AIBot>();
                var botCombat = structure.AddComponent<_Game.Systems.AIBot.AIBotCombat>();
                var botInventory = structure.AddComponent<_Game.Systems.AIBot.AIBotInventory>();
                var botBuildable = structure.AddComponent<_Game.Systems.AIBot.AIBotBuildable>();
                botBuildable.buildableData = activeBuildable;

                // 注册弹药消耗回调
                botCombat.AmmoConsumeCallback = botInventory.ConsumeAmmo;

                // 确保有 Rigidbody（NavMeshAgent 需要）
                var rb = structure.GetComponent<Rigidbody>();
                if (rb == null) rb = structure.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                Debug.Log($"[BuildModeController] AI机器人已部署: {activeBuildable.displayName}");
            }
            else
            {
                // 3.6 挂载 PlacedStructure 组件
                var placed = structure.GetComponent<PlacedStructure>();
                if (placed == null)
                    placed = structure.AddComponent<PlacedStructure>();
                placed.buildableData = activeBuildable;

                // 3.7 工作台：挂载 WorkstationInteract
                if (activeBuildable.isWorkstation)
                {
                    var ws = structure.GetComponent<WorkstationInteract>();
                    if (ws == null)
                        ws = structure.AddComponent<WorkstationInteract>();
                    ws.workstationTier = activeBuildable.workstationTier;
                }

                // 3.8 生产设备：挂载 ProductionDevice
                if (activeBuildable.productionDeviceRef != null)
                {
                    var pd = structure.GetComponent<Crafting.ProductionDevice>();
                    if (pd == null)
                        pd = structure.AddComponent<Crafting.ProductionDevice>();
                    pd.Init(activeBuildable.productionDeviceRef);
                }

                // 3.9 用电终端：挂载 PowerTerminal
                if (activeBuildable.powerSupplyRadius > 0f)
                {
                    var pt = structure.GetComponent<Power.PowerTerminal>();
                    if (pt == null)
                        pt = structure.AddComponent<Power.PowerTerminal>();
                    pt.supplyRadius = activeBuildable.powerSupplyRadius;
                }

                // 3.10 发电端：挂载 PowerSource
                if (activeBuildable.powerOutput > 0f)
                {
                    var ps = structure.GetComponent<Power.PowerSource>();
                    if (ps == null) ps = structure.AddComponent<Power.PowerSource>();
                    ps.sourceType = activeBuildable.powerSourceType;
                    ps.maxOutput = activeBuildable.powerOutput;
                    ps.requiresFuel = activeBuildable.powerRequiresFuel;
                    ps.fuelPerHour = activeBuildable.powerFuelPerHour;
                    ps.fuelItemName = activeBuildable.powerFuelItemName;
                    ps.fuelItemData = activeBuildable.powerFuelItemData;
                    ps.daytimeOnly = activeBuildable.powerDaytimeOnly;
                    ps.requiresOpenAir = activeBuildable.powerRequiresOpenAir;
                    ps.requiresWater = activeBuildable.powerRequiresWater;
                    ps.noiseRadius = activeBuildable.powerNoiseRadius;
                }

                // 3.11 设备用电：挂载 PowerConsumer
                if (activeBuildable.powerRequired > 0f)
                {
                    var pc = structure.GetComponent<Power.PowerConsumer>();
                    if (pc == null) pc = structure.AddComponent<Power.PowerConsumer>();
                    pc.requiredPower = activeBuildable.powerRequired;
                    pc.allowCoal = activeBuildable.powerAllowCoal;
                    pc.coalPower = activeBuildable.powerCoalPower;
                    pc.electricSpeedMultiplier = activeBuildable.powerElectricSpeedMul;
                }
            }

            // 4. 发布事件
            EventBus.Publish(new StructurePlacedEvent(activeBuildable, placePos, structure));
            EventBus.Publish(new SurvivalXpGained(GameConstants.XP_PLACE_BUILDING, "build"));

            Debug.Log($"[BuildModeController] 建造完成: {activeBuildable.displayName} @ {placePos}");

            // 5. 保持预览状态，可连续建造同一物品
            BuildProgress = 0f;
            _state = BuildModeState.Preview;
            EventBus.Publish(new BuildModeEnteredEvent(activeBuildable));
        }

        // ============================================================
        // 退出建造模式
        // ============================================================

        private void ExitBuildMode()
        {
            if (_buildCoroutine != null)
            {
                StopCoroutine(_buildCoroutine);
                _buildCoroutine = null;
            }

            BuildProgress = 0f;
            _state = BuildModeState.Inactive;
            EventBus.Publish(new BuildModeExitedEvent());
            Debug.Log("[BuildModeController] 退出建造模式");
        }

        // ============================================================
        // 材料/工具检查
        // ============================================================

        private bool HasRequiredMaterials()
        {
            if (activeBuildable.materials == null || activeBuildable.materials.Length == 0)
                return true; // 无需材料

            if (_inventory == null)
            {
                Debug.LogError("[BuildModeController] Inventory 引用缺失");
                return false;
            }

            foreach (var req in activeBuildable.materials)
            {
                if (!_inventory.HasItem(req.itemData, req.count))
                    return false;
            }
            return true;
        }

        private void DeductMaterials()
        {
            if (activeBuildable.materials == null || _inventory == null)
                return;

            foreach (var req in activeBuildable.materials)
            {
                _inventory.RemoveItem(req.itemData, req.count);
            }
        }

        private bool IsRequiredToolEquipped()
        {
            if (_inventory == null) return false;
            if (_inventory.equipped.TryGetValue(EquipSlot.RightHand, out var equippedItem))
                return equippedItem == activeBuildable.requiredTool;
            return false;
        }

        // ============================================================
        // 公开 API（供 BuildMenuUI / 外部调用）
        // ============================================================

        /// <summary>
        /// 切换当前建造物类型（BuildMenuUI 调用）
        /// </summary>
        public void SwitchBuildable(BuildableData newBuildable)
        {
            if (_state == BuildModeState.Building)
            {
                Debug.LogWarning("[BuildModeController] 建造中无法切换建造物");
                return;
            }

            activeBuildable = newBuildable;

            // 切换到新物品预览
            if (_state == BuildModeState.Preview || _state == BuildModeState.MenuOnly)
            {
                if (_ghostPreview != null)
                    _ghostPreview.HidePreview();
                _state = BuildModeState.Preview;
                EventBus.Publish(new BuildModeEnteredEvent(activeBuildable));
            }
        }

        /// <summary>
        /// 取消当前物品选中（保留建造模式和菜单）
        /// </summary>
        public void CancelPreview()
        {
            if (_state == BuildModeState.Building) return;
            if (_state != BuildModeState.Preview) return;

            _state = BuildModeState.MenuOnly;
            BuildProgress = 0f;
            if (_ghostPreview != null)
                _ghostPreview.HidePreview();
            // 不清除 activeBuildable，记住上次选择
        }

        /// <summary>
        /// 确认建造（由 BuildMenuUI.OnGUI 在菜单外的点击时调用）
        /// </summary>
        public void TryConfirmBuild()
        {
            if (_state != BuildModeState.Preview) return;
            TryStartBuilding();
        }

        /// <summary>
        /// 强制退出（供外部调用，如在死亡/场景切换时清理）
        /// </summary>
        public void ForceExit()
        {
            if (_state != BuildModeState.Inactive)
                ExitBuildMode();
        }

        /// <summary>
        /// 直接放置建筑（跳过建造时间/材料扣除/体力消耗）。用于职业开局等特殊流程。
        /// </summary>
        public GameObject PlaceStructureDirect(BuildableData data, Vector3 position)
        {
            if (data == null) return null;

            position.y += spawnYOffset;

            GameObject structure;
            if (data.builtPrefab != null)
            {
                structure = Instantiate(data.builtPrefab, position, Quaternion.identity);
                structure.name = data.displayName;
            }
            else
            {
                structure = GameObject.CreatePrimitive(PrimitiveType.Cube);
                structure.transform.position = position;
                structure.transform.localScale = data.placementSize;
                structure.name = $"[NoPrefab] {data.displayName}";
            }

            // AI机器人特殊处理
            bool isAIBot = data.category == BuildableCategory.ElectronicsIndustry
                && structure.GetComponent<_Game.Systems.AIBot.AIBot>() == null
                && structure.GetComponent<_Game.Systems.AIBot.AIBotBuildable>() == null
                && data.displayName.Contains("AI");

            if (isAIBot)
            {
                var bot = structure.AddComponent<_Game.Systems.AIBot.AIBot>();
                var botCombat = structure.AddComponent<_Game.Systems.AIBot.AIBotCombat>();
                var botInventory = structure.AddComponent<_Game.Systems.AIBot.AIBotInventory>();
                var botBuildable = structure.AddComponent<_Game.Systems.AIBot.AIBotBuildable>();
                botBuildable.buildableData = data;
                botCombat.AmmoConsumeCallback = botInventory.ConsumeAmmo;

                var rb = structure.GetComponent<Rigidbody>();
                if (rb == null) rb = structure.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                Debug.Log($"[BuildModeController] AI机器人已部署(直接放置): {data.displayName}");
            }
            else
            {
                var placed = structure.GetComponent<PlacedStructure>();
                if (placed == null)
                    placed = structure.AddComponent<PlacedStructure>();
                placed.buildableData = data;

                if (data.isWorkstation)
                {
                    var ws = structure.GetComponent<WorkstationInteract>();
                    if (ws == null)
                        ws = structure.AddComponent<WorkstationInteract>();
                    ws.workstationTier = data.workstationTier;
                }

                if (data.productionDeviceRef != null)
                {
                    var pd = structure.GetComponent<Crafting.ProductionDevice>();
                    if (pd == null)
                        pd = structure.AddComponent<Crafting.ProductionDevice>();
                    pd.Init(data.productionDeviceRef);
                }

                if (data.powerSupplyRadius > 0f)
                {
                    var pt = structure.GetComponent<Power.PowerTerminal>();
                    if (pt == null)
                        pt = structure.AddComponent<Power.PowerTerminal>();
                    pt.supplyRadius = data.powerSupplyRadius;
                }

                if (data.powerOutput > 0f)
                {
                    var ps = structure.GetComponent<Power.PowerSource>();
                    if (ps == null)
                        ps = structure.AddComponent<Power.PowerSource>();
                    ps.sourceType = data.powerSourceType;
                    ps.maxOutput = data.powerOutput;
                    ps.requiresFuel = data.powerRequiresFuel;
                    ps.fuelPerHour = data.powerFuelPerHour;
                    ps.fuelItemName = data.powerFuelItemName;
                    ps.fuelItemData = data.powerFuelItemData;
                    ps.daytimeOnly = data.powerDaytimeOnly;
                    ps.requiresOpenAir = data.powerRequiresOpenAir;
                    ps.requiresWater = data.powerRequiresWater;
                    ps.noiseRadius = data.powerNoiseRadius;
                }

                if (data.powerRequired > 0f)
                {
                    var pc = structure.GetComponent<Power.PowerConsumer>();
                    if (pc == null)
                        pc = structure.AddComponent<Power.PowerConsumer>();
                    pc.requiredPower = data.powerRequired;
                    pc.allowCoal = data.powerAllowCoal;
                    pc.coalPower = data.powerCoalPower;
                    pc.electricSpeedMultiplier = data.powerElectricSpeedMul;
                }
            }

            EventBus.Publish(new StructurePlacedEvent(data, position, structure));
            EventBus.Publish(new SurvivalXpGained(GameConstants.XP_PLACE_BUILDING, "build"));

            Debug.Log($"[BuildModeController] 直接放置完成: {data.displayName} @ {position}");
            return structure;
        }
    }
}
