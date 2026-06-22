using System;
using System.Collections;
using _Game.Config;
using _Game.Core;
using UnityEngine;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 存档系统入口管理器。
    /// DontDestroyOnLoad 单例，挂在 Managers GameObject 上。
    ///
    /// 核心职责：
    ///   - 接收保存/加载请求（手动 & 自动）
    ///   - 主线程采集各系统数据 → ICloneable 深拷贝 → 后台线程写盘
    ///   - 并发保存合并策略（_pendingSave）
    ///   - 加载流程编排（Phase -1 → Phase 0~7）
    ///   - InputRouter 高优先级拦截（加载时屏蔽玩家输入）
    ///   - 发布存档事件（SaveCompleted / LoadCompleted 等）
    /// </summary>
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; private set; }

        [Header("存档槽")]
        [SerializeField] private int _currentSlotIndex = 0;

        [Header("自动保存")]
        [SerializeField] private float _autoSaveIntervalMinutes = 10f;
        [SerializeField] private float _manualSaveCooldownSeconds = 30f;

        // ═══ 状态标记 ═══

        /// <summary> 是否正在序列化/写盘（阻塞并发保存请求） </summary>
        private bool _serializingLock;

        /// <summary> 当前保存期间收到新请求时设为 true，完成后合并执行一次 </summary>
        private bool _pendingSave;

        /// <summary> 正在加载存档（全局冻结标记） </summary>
        public bool IsLoading { get; private set; }

        /// <summary> 世界是否正在生成 </summary>
        public bool IsWorldGenerating { get; set; }

        // ═══ 冷却 ═══
        private float _lastManualSaveTime = -999f;

        // ═══ 组件引用 ═══
        private ItemCatalog _itemCatalog;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SaveService.Initialize();

            Debug.Log($"[SaveLoadManager] 存档路径: {Application.persistentDataPath}/Saves/");
            Debug.Log("[SaveLoadManager] 🔧 调试快捷键: F5=保存, F9=加载");

            // 加载 ItemCatalog（由编辑器脚本自动创建，见 ItemCatalogInitializer.cs）
            _itemCatalog = Resources.Load<ItemCatalog>("ItemCatalog");
            if (_itemCatalog != null)
                _itemCatalog.Build();
            else
                Debug.LogError("[SaveLoadManager] ❌ 未找到 ItemCatalog.asset！物品存档/恢复将不可用。请重新导入项目或手动在 Resources 下 Create→Game→Item Catalog");
        }

        void Start()
        {
            // 订阅事件
            EventBus.Subscribe<RequestSaveGame>(OnRequestSaveGame);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<RequestSaveGame>(OnRequestSaveGame);
            // 正常退出时自动保存
            AutoSaveOnQuit();
        }

        void Update()
        {
            // ═══════════════════════════════════════════
            // 🔧 Phase 1 调试快捷键（后续由 UI 替代）
            // ═══════════════════════════════════════════
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Debug.Log("[SaveLoadManager] 🔧 F5 手动保存");
                ManualSave();
            }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                Debug.Log($"[SaveLoadManager] 🔧 F9 加载存档 slot={_currentSlotIndex}");
                LoadGame(_currentSlotIndex);
            }
        }

        // ═══════════════════════════════════════════
        // 公开 API
        // ═══════════════════════════════════════════

        /// <summary> 手动保存（ESC 菜单） </summary>
        public void ManualSave()
        {
            if (UnityEngine.Time.unscaledTime - _lastManualSaveTime < _manualSaveCooldownSeconds)
            {
                Debug.Log("[SaveLoadManager] 手动保存冷却中，请稍后再试");
                return;
            }
            _lastManualSaveTime = UnityEngine.Time.unscaledTime;
            RequestSave(_currentSlotIndex, false);
        }

        /// <summary> 自动保存 </summary>
        public void AutoSave()
        {
            RequestSave(_currentSlotIndex, true);
        }

        /// <summary> 手动加载 </summary>
        public void LoadGame(int slotIndex)
        {
            if (!SaveService.SlotExists(slotIndex))
            {
                Debug.LogWarning($"[SaveLoadManager] 槽位 {slotIndex} 无存档");
                return;
            }
            StartCoroutine(LoadCoroutine(slotIndex));
        }

        // ═══════════════════════════════════════════
        // 保存流程
        // ═══════════════════════════════════════════

        private void RequestSave(int slotIndex, bool isAutoSave)
        {
            if (_serializingLock)
            {
                _pendingSave = true; // 合并策略
                return;
            }

            if (IsLoading)
            {
                Debug.Log("[SaveLoadManager] 加载中，跳过保存");
                return;
            }

            if (IsWorldGenerating)
            {
                _pendingSave = true; // 世界生成完后保存
                return;
            }

            StartCoroutine(SaveCoroutine(slotIndex, isAutoSave));
        }

        private IEnumerator SaveCoroutine(int slotIndex, bool isAutoSave)
        {
            _serializingLock = true;

            // 1. 主线程采集数据
            var saveData = new SaveData
            {
                version = 1,
                worldGenVersion = 1,
                worldSeed = 0, // TODO: 从 WorldGenerator 获取
                saveDateTime = DateTime.Now.ToString("O"),
                totalPlayTime = UnityEngine.Time.time,
                player = CollectPlayerData(),
                inventory = CollectInventoryData(),
                world = CollectWorldData(),
                productions = CollectProductionData(),
                timeWeather = CollectTimeWeatherData(),
                research = CollectResearchData(),
            };

            // 2. 深拷贝（ICloneable，~1-2ms）
            var snapshot = saveData.Clone() as SaveData;

            // 3. 后台线程写盘
            var task = SaveService.SaveAsync(slotIndex, snapshot);

            // 4. 主线程写 meta
            var meta = new SaveSlotMeta
            {
                slotIndex = slotIndex,
                saveVersion = saveData.version,
                worldGenVersion = saveData.worldGenVersion,
                saveDateTime = saveData.saveDateTime,
                totalPlayTime = saveData.totalPlayTime,
                gameDays = saveData.timeWeather?.totalGameDays ?? 0f,
                playerLevel = 1,
            };
            SaveService.SaveMeta(slotIndex, meta);

            // 5. 等待后台线程完成
            while (!task.IsCompleted)
                yield return null;

            try
            {
                task.Wait(); // 已经完成，不会阻塞
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadManager] 保存失败: {ex.Message}");
                EventBus.Publish(new SaveCompleted(slotIndex, false));
                _serializingLock = false;
                yield break;
            }

            Debug.Log($"[SaveLoadManager] 保存完成 slot={slotIndex} auto={isAutoSave}");
            EventBus.Publish(new SaveCompleted(slotIndex, true));

            _serializingLock = false;

            // 6. 检查是否有待合并的保存
            if (_pendingSave)
            {
                _pendingSave = false;
                StartCoroutine(SaveCoroutine(slotIndex, true));
            }
        }

        // ═══════════════════════════════════════════
        // 加载流程
        // ═══════════════════════════════════════════

        private IEnumerator LoadCoroutine(int slotIndex)
        {
            // Phase 0: 全局冻结
            IsLoading = true;
            UnityEngine.Time.timeScale = 0f;

            // InputRouter 高优先级拦截所有输入
            InputRouter.BindKey(KeyCode.F1, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F2, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F3, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F4, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F5, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F6, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F7, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F8, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F9, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F10, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F11, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.F12, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.Escape, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.E, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.Tab, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.I, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.B, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.T, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.P, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.V, InputPriority.Debug, () => true, this);
            // 鼠标
            InputRouter.BindMouse(0, InputPriority.Debug, () => true, this);
            InputRouter.BindMouse(1, InputPriority.Debug, () => true, this);
            InputRouter.BindMouse(2, InputPriority.Debug, () => true, this);

            EventBus.Publish(new GameLoadStarted(slotIndex));

            // Phase -1: 卸载当前世界
            yield return StartCoroutine(UnloadCurrentWorld());

            // Phase 1: 读取存档
            var saveData = SaveService.Load(slotIndex);
            if (saveData == null)
            {
                Debug.LogError($"[SaveLoadManager] 加载失败 slot={slotIndex}");
                Unfreeze();
                EventBus.Publish(new LoadCompleted(slotIndex, false));
                yield break;
            }

            // 版本校验
            if (saveData.worldGenVersion != 1)
            {
                Debug.LogError($"[SaveLoadManager] 世界版本不兼容: 存档={saveData.worldGenVersion}, 当前=1。请开始新游戏。");
                Unfreeze();
                EventBus.Publish(new LoadCompleted(slotIndex, false));
                yield break;
            }

            yield return null;

            // Phase 2: 设置世界种子 → 触发 WorldGenerator
            // TODO: WorldGenerator.SetSeed(saveData.worldSeed);
            // yield return new WaitUntil(() => ... WorldGenCompleted event);
            yield return null;

            // Phase 3: 放置玩家
            var player = PlayerRegistry.Transform;
            if (player != null && saveData.player != null)
            {
                player.SetPositionAndRotation(
                    new Vector3(saveData.player.posX, saveData.player.posY, saveData.player.posZ),
                    Quaternion.Euler(0, saveData.player.rotY, 0));
            }

            // Phase 4: 恢复玩家内部状态
            RestorePlayerState(saveData.player);
            RestoreInventory(saveData.inventory);

            yield return null;

            // Phase 5: 恢复世界实体
            RestoreWorldEntities(saveData.world);

            // 发世界实体就绪事件
            EventBus.Publish(new WorldEntitiesRestored());

            yield return null;

            // Phase 6: 恢复系统实体状态
            RestoreProductionDevices(saveData.productions);
            // TODO: P4 — 电力网/AIBot/车辆/僵尸

            // Phase 7: 恢复全局状态
            RestoreTimeWeather(saveData.timeWeather);
            RestoreResearch(saveData.research);

            yield return null;

            // Phase 8: 解冻
            Unfreeze();
            Debug.Log($"[SaveLoadManager] 加载完成 slot={slotIndex}");
            EventBus.Publish(new LoadCompleted(slotIndex, true));
        }

        private void Unfreeze()
        {
            IsLoading = false;
            UnityEngine.Time.timeScale = 1f;
            InputRouter.UnbindAll(this);
        }

        // ═══════════════════════════════════════════
        // Phase -1: 卸载当前世界
        // ═══════════════════════════════════════════

        private IEnumerator UnloadCurrentWorld()
        {
            Debug.Log("[SaveLoadManager] Phase -1: 卸载当前世界...");

            // 销毁所有持久化实体
            DestroyEntitiesOfType<PersistentGUID>();

            // 清空 GUID 注册表
            PersistentGUIDRegistry.Clear();

            // 重置各种 ID 分配器（需在各系统中暴露 Reset 方法）
            // TODO: WorldItemManager.ResetInstanceCounter();
            // TODO: Inventory.ResetInstanceCounter();

            yield return null;
        }

        private void DestroyEntitiesOfType<T>() where T : Component
        {
            var entities = FindObjectsOfType<T>();
            foreach (var entity in entities)
            {
                if (entity != null && entity.gameObject != null)
                    Destroy(entity.gameObject);
            }
        }

        // ═══════════════════════════════════════════
        // 数据采集（主线程同步，各系统 GetSaveData）
        // ═══════════════════════════════════════════

        private PlayerSaveData CollectPlayerData()
        {
            var player = PlayerRegistry.Transform;
            if (player == null) return null;

            var pd = new PlayerSaveData
            {
                posX = player.position.x,
                posY = player.position.y,
                posZ = player.position.z,
                rotY = player.eulerAngles.y,
            };

            // SurvivalSystem
            var survival = player.GetComponent<_Game.Systems.Survival.SurvivalSystem>();
            if (survival != null)
                pd = survival.GetSaveData();
            // 补回 Transform 数据（GetSaveData 可能覆盖了 pd）
            pd.posX = player.position.x;
            pd.posY = player.position.y;
            pd.posZ = player.position.z;
            pd.rotY = player.eulerAngles.y;

            // SurvivalXPSystem
            var xp = _Game.Systems.Character.SurvivalXPSystem.Instance;
            xp?.PopulateSaveData(pd);

            // StaminaSystem
            var stamina = player.GetComponent<_Game.Systems.Character.StaminaSystem>();
            stamina?.PopulateSaveData(pd);

            return pd;
        }

        private InventorySaveData CollectInventoryData()
        {
            var player = PlayerRegistry.GameObject;
            if (player == null) return null;

            var inventory = player.GetComponent<_Game.Systems.Inventory.Inventory>();
            if (inventory == null) return null;

            var inv = inventory.GetSaveData();

            // 采集武器弹药
            var switcher = player.GetComponent<_Game.Systems.Weapon.WeaponSwitcher>();
            var shooting = player.GetComponent<_Game.Systems.Weapon.WeaponShooting>();
            if (switcher != null && shooting != null)
            {
                var activeSlot = switcher.ActiveSlot;
                if (activeSlot != Config.EquipSlot.None)
                {
                    inv.ammoReserves[activeSlot.ToString()] = shooting.GetCurrentMag(activeSlot);
                }
            }

            return inv;
        }

        private WorldSaveData CollectWorldData()
        {
            var ws = new WorldSaveData();

            // 建筑
            ws.buildings = new System.Collections.Generic.List<PlacedStructureSaveData>();
            var registry = PlacedStructureRegistry.Instance;
            if (registry != null)
            {
                foreach (var ps in registry.AllStructures)
                {
                    if (ps == null || ps.buildableData == null) continue;
                    // 排除会移动的独立实体（AI机器人、车辆）——它们将来走 Phase 4 实体保存
                    if (ps.GetComponent<_Game.Systems.AIBot.AIBot>() != null) continue;
                    if (ps.GetComponent<_Game.Systems.Vehicle.VehicleController>() != null) continue;
                    // 工作台/熔炉/电力设备等——虽然是实体但通过建造系统放置，走建筑路径保存
                    var guid = ps.GetComponent<PersistentGUID>();
                    ws.buildings.Add(new PlacedStructureSaveData
                    {
                        guid = guid != null ? guid.Guid : "",
                        buildableName = ps.buildableData.displayName,
                        posX = ps.transform.position.x,
                        posY = ps.transform.position.y,
                        posZ = ps.transform.position.z,
                        rotY = ps.transform.eulerAngles.y,
                        currentHealth = ps.HealthPercent * (ps.buildableData?.maxHealth ?? 100f),
                    });
                }
            }

            // 地面物品
            ws.groundItems = new System.Collections.Generic.List<WorldItemSaveData>();
            var worldItems = FindObjectsOfType<_Game.Systems.WorldContainer.WorldItem>();
            foreach (var wi in worldItems)
            {
                if (wi == null || wi.itemData == null) continue;
                // 分配 instanceId（如果尚未分配）
                if (wi.instanceId == 0)
                    wi.instanceId = WorldItemManager.Instance?.AllocateId() ?? 0;
                ws.groundItems.Add(new WorldItemSaveData
                {
                    instanceId = wi.instanceId,
                    itemName = wi.itemData.itemName,
                    count = wi.count,
                    posX = wi.transform.position.x,
                    posY = wi.transform.position.y,
                    posZ = wi.transform.position.z,
                });
            }

            // 容器
            var crateReg = _Game.Systems.WorldContainer.ContainerRegistry.Instance;
            ws.containers = crateReg?.GetAllRecords() ?? new System.Collections.Generic.List<ContainerSaveRecord>();

            // 阵营
            var faction = FindObjectOfType<_Game.Systems.Threat.FactionSystem>();
            ws.factionDeltas = faction?.GetFactionDeltas() ?? new System.Collections.Generic.List<FactionDeltaSaveData>();

            Debug.Log($"[SaveLoadManager] 📦 采集世界实体: 建筑={ws.buildings.Count}, 地面物品={ws.groundItems.Count}, 容器={ws.containers.Count}");

            return ws;
        }

        private System.Collections.Generic.List<ProductionSaveData> CollectProductionData()
        {
            var list = new System.Collections.Generic.List<ProductionSaveData>();
            var structures = FindObjectsOfType<_Game.Systems.Crafting.ProductionDevice>();
            foreach (var pd in structures)
            {
                if (pd == null) continue;
                var psd = pd.GetSaveData();
                if (!string.IsNullOrEmpty(psd.guid)) list.Add(psd);
            }
            Debug.Log($"[SaveLoadManager] 📦 采集生产设备: {list.Count} 个");
            return list;
        }

        private ResearchSaveData CollectResearchData()
        {
            var rs = new ResearchSaveData();
            var player = PlayerRegistry.GameObject;
            if (player != null)
            {
                var chem = player.GetComponent<_Game.Systems.Crafting.ChemicalResearchManager>();
                if (chem != null) rs.completedResearchIds = chem.GetCompletedIds();
            }
            return rs;
        }

        private void RestoreResearch(ResearchSaveData rs)
        {
            if (rs == null) return;
            var player = PlayerRegistry.GameObject;
            if (player != null)
            {
                var chem = player.GetComponent<_Game.Systems.Crafting.ChemicalResearchManager>();
                chem?.RestoreCompletedIds(rs.completedResearchIds);
            }
        }

        private TimeWeatherSaveData CollectTimeWeatherData()
        {
            var tw = new TimeWeatherSaveData();
            var tm = ServiceLocator.Get<_Game.Systems.Time.TimeManager>();
            if (tm != null) tw.totalGameDays = tm.GetTotalGameDays();
            var wm = _Game.Systems.Weather.WeatherManager.Instance;
            if (wm != null) tw = wm.GetSaveData();
            // 补回 totalGameDays（被 GetSaveData 覆盖了）
            if (tm != null) tw.totalGameDays = tm.GetTotalGameDays();
            return tw;
        }

        // ═══════════════════════════════════════════
        // 数据恢复
        // ═══════════════════════════════════════════

        private void RestorePlayerState(PlayerSaveData pd)
        {
            if (pd == null) return;
            var player = PlayerRegistry.GameObject;
            if (player == null) return;

            var survival = player.GetComponent<_Game.Systems.Survival.SurvivalSystem>();
            survival?.RestoreFromSave(pd);

            var xp = _Game.Systems.Character.SurvivalXPSystem.Instance;
            xp?.RestoreFromSave(pd);

            var stamina = player.GetComponent<_Game.Systems.Character.StaminaSystem>();
            stamina?.RestoreFromSave(pd);
        }

        private void RestoreInventory(InventorySaveData inv)
        {
            if (inv == null) return;
            var player = PlayerRegistry.GameObject;
            if (player == null) return;

            var inventory = player.GetComponent<_Game.Systems.Inventory.Inventory>();
            inventory?.RestoreFromSave(inv, _itemCatalog);
        }

        private void RestoreWorldEntities(WorldSaveData world)
        {
            if (world == null) return;

            // 1. 恢复建筑（复用建造系统的 PlaceStructureDirect）
            int restoredBuildings = 0;
            if (world.buildings != null && world.buildings.Count > 0)
            {
                Debug.Log($"[SaveLoadManager] 🏗️ 开始恢复 {world.buildings.Count} 个建筑...");
                var buildableCatalog = Resources.Load<BuildableCatalog>("BuildableCatalog");
#if UNITY_EDITOR
                if (buildableCatalog == null)
                    buildableCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildableCatalog>(
                        "Assets/_Game/Config/BuildableCatalog.asset");
#endif
                Debug.Log($"[SaveLoadManager] BuildableCatalog: {(buildableCatalog != null ? buildableCatalog.buildables?.Length.ToString() ?? "null数组" : "null")}");
                var buildController = PlayerRegistry.Get<_Game.Systems.Building.BuildModeController>();
                Debug.Log($"[SaveLoadManager] BuildModeController: {(buildController != null ? "✅" : "❌ null")}");

                if (buildController == null)
                    Debug.LogError("[SaveLoadManager] ❌ 无法恢复建筑：BuildModeController 为 null。请确认玩家身上有 BuildModeController 组件。");

                foreach (var bsd in world.buildings)
                {
                    if (string.IsNullOrEmpty(bsd.buildableName)) continue;

                    // 从 catalog 查找 BuildableData
                    BuildableData bd = null;
                    if (buildableCatalog != null && buildableCatalog.buildables != null)
                    {
                        foreach (var b in buildableCatalog.buildables)
                        {
                            if (b != null && b.displayName == bsd.buildableName) { bd = b; break; }
                        }
                    }

                    if (bd == null)
                    {
                        Debug.LogWarning($"[SaveLoadManager] 恢复建筑跳过，catalog 中无此 BuildableData: {bsd.buildableName}");
                        continue;
                    }

                    // ✅ 复用建造系统：无 prefab 时自动用 Cube 方块代替
                    Vector3 pos = new Vector3(bsd.posX, bsd.posY, bsd.posZ);
                    GameObject go;
                    if (buildController != null)
                        go = buildController.PlaceStructureDirect(bd, pos);
                    else
                        continue; // 没有 BuildModeController 无法放置

                    if (go == null) continue;

                    // 设置旋转（PlaceStructureDirect 默认用 Quaternion.identity）
                    go.transform.rotation = Quaternion.Euler(0, bsd.rotY, 0);

                    // 覆盖 PersistentGUID 为存档中的值
                    var guid = go.GetComponent<PersistentGUID>();
                    if (guid != null && !string.IsNullOrEmpty(bsd.guid))
                        guid.Initialize(bsd.guid, "PlacedStructure");

                    restoredBuildings++;
                }
            }

            Debug.Log($"[SaveLoadManager] 🏗️ 恢复建筑: {restoredBuildings}/{world.buildings?.Count ?? 0}");

            // 2. 恢复地面物品
            if (world.groundItems != null)
            {
                int maxId = 0;
                foreach (var wis in world.groundItems)
                {
                    var itemData = _itemCatalog?.Find(wis.itemName);
                    if (itemData == null)
                    {
                        Debug.LogWarning($"[SaveLoadManager] 恢复地面物品失败，找不到: {wis.itemName}");
                        continue;
                    }

                    var go = new GameObject($"World_{wis.itemName}");
                    go.transform.position = new Vector3(wis.posX, wis.posY, wis.posZ);
                    var wi = go.AddComponent<_Game.Systems.WorldContainer.WorldItem>();
                    wi.itemData = itemData;
                    wi.count = wis.count;
                    wi.instanceId = wis.instanceId;

                    if (wis.instanceId > maxId) maxId = wis.instanceId;
                }

                // 重置 instanceId 计数器
                var wim = WorldItemManager.Instance;
                if (wim != null && maxId > 0) wim.ResetCounter(maxId + 1);
            }

            // 3. 恢复容器
            if (world.containers != null)
            {
                var crateReg = _Game.Systems.WorldContainer.ContainerRegistry.Instance;
                if (crateReg != null)
                {
                    foreach (var csr in world.containers)
                        crateReg.RestoreRecord(csr, _itemCatalog);
                }
            }

            // 4. 恢复阵营关系
            if (world.factionDeltas != null && world.factionDeltas.Count > 0)
            {
                var faction = FindObjectOfType<_Game.Systems.Threat.FactionSystem>();
                faction?.ApplyDeltas(world.factionDeltas);
            }
        }

        /// <summary> 恢复生产设备内部状态（Phase 4，在建筑和 GUID 注册之后） </summary>
        public void RestoreProductionDevices(System.Collections.Generic.List<ProductionSaveData> productions)
        {
            if (productions == null || productions.Count == 0) return;

            var allDevices = FindObjectsOfType<_Game.Systems.Crafting.ProductionDevice>();
            int restored = 0;
            foreach (var psd in productions)
            {
                if (string.IsNullOrEmpty(psd.guid)) continue;

                // 按 GUID 找到对应设备
                var go = PersistentGUIDRegistry.Find(psd.guid);
                if (go == null) continue;

                var device = go.GetComponent<_Game.Systems.Crafting.ProductionDevice>();
                if (device == null) continue;

                device.RestoreFromSave(psd, _itemCatalog);
                restored++;
            }

            // 第二阶段：恢复设备间链接（依赖所有设备已恢复）
            foreach (var psd in productions)
            {
                if (string.IsNullOrEmpty(psd.outputDestinationGuid)) continue;
                var srcGo = PersistentGUIDRegistry.Find(psd.guid);
                var destGo = PersistentGUIDRegistry.Find(psd.outputDestinationGuid);
                if (srcGo != null && destGo != null)
                {
                    var srcDevice = srcGo.GetComponent<_Game.Systems.Crafting.ProductionDevice>();
                    var destDevice = destGo.GetComponent<_Game.Systems.Crafting.ProductionDevice>();
                    if (srcDevice != null && destDevice != null)
                        srcDevice.OutputDestination = destDevice;
                }
            }

            Debug.Log($"[SaveLoadManager] 🏭 恢复生产设备: {restored}/{productions.Count}");
        }

        private void RestoreTimeWeather(TimeWeatherSaveData tw)
        {
            if (tw == null) return;
            var tm = ServiceLocator.Get<_Game.Systems.Time.TimeManager>();
            if (tm != null) tm.SetTotalGameDays(tw.totalGameDays);
            var wm = _Game.Systems.Weather.WeatherManager.Instance;
            wm?.RestoreFromSave(tw);
        }

        // ═══════════════════════════════════════════
        // 退出自动保存
        // ═══════════════════════════════════════════

        private void AutoSaveOnQuit()
        {
            if (IsLoading) return;

            // 同步保存（退出时不能异步）
            var saveData = new SaveData
            {
                version = 1,
                worldGenVersion = 1,
                worldSeed = 0,
                saveDateTime = DateTime.Now.ToString("O"),
                totalPlayTime = UnityEngine.Time.time,
                player = CollectPlayerData(),
                inventory = CollectInventoryData(),
                world = CollectWorldData(),
                productions = CollectProductionData(),
                timeWeather = CollectTimeWeatherData(),
                research = CollectResearchData(),
            };

            try
            {
                var snapshot = saveData.Clone() as SaveData;
                SaveService.SaveAsync(_currentSlotIndex, snapshot).Wait();
                var meta = new SaveSlotMeta
                {
                    slotIndex = _currentSlotIndex,
                    saveVersion = saveData.version,
                    worldGenVersion = saveData.worldGenVersion,
                    saveDateTime = saveData.saveDateTime,
                    totalPlayTime = saveData.totalPlayTime,
                };
                SaveService.SaveMeta(_currentSlotIndex, meta);
                Debug.Log("[SaveLoadManager] 退出时自动保存完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadManager] 退出保存失败: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════
        // EventBus 回调
        // ═══════════════════════════════════════════

        private void OnRequestSaveGame(RequestSaveGame evt)
        {
            RequestSave(evt.SlotIndex, evt.IsAutoSave);
        }
    }
}
