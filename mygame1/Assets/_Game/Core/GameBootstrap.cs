using UnityEngine;
using _Game.Systems.Threat;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Game.Core
{
    /// <summary>
    /// 运行时启动器 — 确保关键组件存在，不依赖编辑器脚本。
    /// 场景丢失/电脑崩溃后也能自动恢复。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            EnsureInputRouter();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            // 主菜单场景：自动创建 SceneContext 并跳过玩家初始化
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "MainMenu")
            {
                EnsureSceneContext(sceneName);
                return;
            }

            EnsureSceneContext(sceneName);

            var player = GameObject.FindWithTag("Player")
                      ?? GameObject.Find("player")
                      ?? GameObject.Find("Player");

            if (player == null)
            {
                Debug.LogError("[GameBootstrap] ❌ 找不到 Player！请确认场景中有 tag=Player 的对象");
                return;
            }
            // 注册玩家到全局注册表（所有系统通过 PlayerRegistry 访问）
            PlayerRegistry.Register(player);

            // Unity 基础组件兜底
            if (player.GetComponent<Rigidbody>() == null)
            {
                var rb = player.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
            if (player.GetComponent<CapsuleCollider>() == null)
            {
                var col = player.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 1f, 0);
                col.height = 2f;
                col.radius = 0.3f;
            }
            // Animator — 模型子对象已有则不再添加
            if (player.GetComponentInChildren<Animator>() == null)
            {
                var anim = player.AddComponent<Animator>();
                var ctrl = Resources.Load<RuntimeAnimatorController>("Player");
                Avatar avatar = null;
#if UNITY_EDITOR
                if (ctrl == null)
                    ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/_Game/Config/Models/Characters/Player/Player.controller");
                var modelAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/_Game/Config/Models/Characters/Player/Player_Rigged.fbx");
                foreach (var a in modelAssets)
                { if (a is Avatar av) { avatar = av; break; } }
#endif
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                if (avatar != null) anim.avatar = avatar;
            }

            // 按依赖顺序添加组件
            AddIfMissing(player, "_Game.Systems.Player.PlayerController");
            AddIfMissing(player, "_Game.Systems.Inventory.Inventory");
            AddIfMissing(player, "_Game.Systems.Interaction.PlayerInteraction");
            AddIfMissing(player, "_Game.Systems.Character.PlayerCharacter");
            AddIfMissing(player, "_Game.Systems.Survival.SurvivalSystem");
            AddIfMissing(player, "_Game.Systems.Combat.PlayerCombat");
            AddIfMissing(player, "_Game.Systems.Weapon.WeaponShooting");
            AddIfMissing(player, "_Game.Systems.Weapon.WeaponAiming");
            AddIfMissing(player, "_Game.Systems.Weapon.WeaponHolder");
            AddIfMissing(player, "_Game.Systems.Weapon.SpreadVisualizer");
            // ═══ HUD UI 组件（Canvas 结构由 PreconfigureUI 工具预建）═══
            AddIfMissing(player, "_Game.UI.SurvivalHUD");
            AddIfMissing(player, "_Game.UI.QuickItemBar");
            AddIfMissing(player, "_Game.UI.CrosshairUI");
            AddIfMissing(player, "_Game.UI.DecibelHUD");
            AddIfMissing(player, "_Game.UI.WeatherHUD");
            AddIfMissing(player, "_Game.UI.TopLeftHUD");

            // ═══ 系统 UI（非 HUD，有独立逻辑）═══
            AddIfMissing(player, "_Game.Systems.Building.BuildMenuUI");
            AddIfMissing(player, "_Game.Systems.Building.GhostPreview");
            AddIfMissing(player, "_Game.Systems.Crafting.CraftingUI");
            AddIfMissing(player, "_Game.Systems.Crafting.ChemicalResearchManager");
            AddIfMissing(player, "_Game.Systems.Crafting.ChemicalResearchUI");
            AddIfMissing(player, "_Game.Systems.PlayerInput.MouseGroundProjector");
            AddIfMissing(player, "_Game.Systems.Character.StaminaSystem");
            AddIfMissing(player, "_Game.Systems.Character.SurvivalXPSystem");
            AddIfMissing(player, "_Game.Systems.Building.BuildModeController");
            AddIfMissing(player, "_Game.Systems.Building.BuildModeInputLock");
            AddIfMissing(player, "_Game.Systems.Combat.DamageablePlayer");
            AddIfMissing(player, "_Game.Systems.Threat.FactionComponent");
            AddIfMissing(player, "_Game.Systems.Weapon.WeaponSwitcher");
            AddIfMissing(player, "_Game.Systems.Character.ProfessionApplier");
            AddIfMissing(player, "_Game.Systems.Vehicle.VehicleInputLock");
            AddIfMissing(player, "_Game.Systems.Inventory.InventoryTest");
            AddIfMissing(player, "_Game.UI.InventoryUI");          // Tab背包面板

            // 设置玩家阵营
            var playerFaction = player.GetComponent<FactionComponent>();
            if (playerFaction != null)
                playerFaction.SetFaction(_Game.Config.FactionType.Player);

            // 摄像机跟随
            var cam = Camera.main;
            if (cam != null)
            {
                var cf = cam.GetComponent<CameraFollow>();
                if (cf == null)
                    cf = cam.gameObject.AddComponent<CameraFollow>();
                cf.target = player.transform;
            }

            // 自动填充 ScriptableObject 引用
            AutoResolveReferences(player);

            EnsureInputRouter();

            // ═══ 自检 ═══
            var pc = player.GetComponent<_Game.Systems.Character.PlayerCharacter>();
            var fc = player.GetComponent<FactionComponent>();
        }

        // ═══════════════════════════════════════════
        // 自动解析 ScriptableObject 引用
        // ═══════════════════════════════════════════

        static void AutoResolveReferences(GameObject player)
        {
            // SurvivalSystem → survivalData
            var survival = player.GetComponent("_Game.Systems.Survival.SurvivalSystem");
            if (survival != null)
            {
                TrySetField(survival, "survivalData", () => Resources.Load<ScriptableObject>("SurvivalData_Default"));
                TrySetField(survival, "playerCharacter", () => player.GetComponent("_Game.Systems.Character.PlayerCharacter"));
                TrySetField(survival, "timeManager", () => ServiceLocator.Get<_Game.Systems.Time.TimeManager>());
            }

            // PlayerCharacter → characterData
            var pc = player.GetComponent("_Game.Systems.Character.PlayerCharacter");
            if (pc != null)
            {
                TrySetField(pc, "characterData", () =>
                {
                    var so = Resources.Load<ScriptableObject>("DefaultCharacter");
#if UNITY_EDITOR
                    if (so == null)
                        so = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Game/Config/Character/DefaultCharacter.asset");
#endif
                    // 终极兜底：创建空模板（技能/属性为默认值，职业为空）
                    if (so == null)
                    {
                        so = ScriptableObject.CreateInstance<_Game.Config.CharacterData>();
                        Debug.LogWarning("[GameBootstrap] CharacterData 未找到资源/Asset，已创建空模板");
                    }
                    return so;
                });
            }

            // BuildMenuUI → catalog
            var buildMenu = player.GetComponent("_Game.Systems.Building.BuildMenuUI");
            if (buildMenu != null)
            {
                TrySetField(buildMenu, "catalog", () =>
                {
                    var so = Resources.Load<ScriptableObject>("BuildableCatalog");
#if UNITY_EDITOR
                    if (so == null)
                        so = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Game/Config/BuildableCatalog.asset");
#endif
                    return so;
                });
            }

            // ChemicalResearchManager → _researchData
            var chem = player.GetComponent("_Game.Systems.Crafting.ChemicalResearchManager");
            if (chem != null)
            {
                TrySetField(chem, "_researchData", () =>
                {
                    var so = Resources.Load<ScriptableObject>("ChemicalResearchData");
#if UNITY_EDITOR
                    if (so == null)
                        so = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Game/Config/Resources/ChemicalResearchData.asset");
#endif
                    return so;
                });
            }

            // FactionComponent → _factionData（没有 .asset 就跳过，代码里有自动创建）
            var faction = player.GetComponent<FactionComponent>();
            if (faction != null)
            {
                TrySetField(faction, "_factionData", () =>
                {
#if UNITY_EDITOR
                    var guids = AssetDatabase.FindAssets("t:FactionData");
                    if (guids.Length > 0)
                        return AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
                    return null;
                });
            }
        }

        static void TrySetField(object target, string fieldName, System.Func<Object> resolver)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field == null) return;

            var current = field.GetValue(target);
            if (current != null && !(current is UnityEngine.Object uo && uo == null)) return; // 已经设了

            var value = resolver();
            if (value != null)
            {
                field.SetValue(target, value);
            }
        }

        static void EnsureInputRouter()
        {
            var go = GameObject.Find("Managers") ?? new GameObject("Managers");
            Object.DontDestroyOnLoad(go);

            // InputRouter
            if (go.GetComponent<InputRouter>() == null)
                go.AddComponent<InputRouter>();

            // SoundEmitter — 声音事件中转（静态方法 → DecibelSystem）
            if (go.GetComponent<_Game.Systems.Audio.SoundEmitter>() == null)
                go.AddComponent<_Game.Systems.Audio.SoundEmitter>();
            // DecibelSystem — 分贝/噪音系统（僵尸感知 + 声音传播）
            if (go.GetComponent<_Game.Systems.Audio.DecibelSystem>() == null)
                go.AddComponent<_Game.Systems.Audio.DecibelSystem>();
            // MuzzleFlashSystem — 枪口火焰 VFX
            if (go.GetComponent<_Game.Systems.VFX.MuzzleFlashSystem>() == null)
                go.AddComponent<_Game.Systems.VFX.MuzzleFlashSystem>();
            // FactionSystem — 阵营系统
            if (go.GetComponent<_Game.Systems.Threat.FactionSystem>() == null)
                go.AddComponent<_Game.Systems.Threat.FactionSystem>();
            // ThreatSystem — 威胁/仇恨系统
            if (go.GetComponent<_Game.Systems.Threat.ThreatSystem>() == null)
                go.AddComponent<_Game.Systems.Threat.ThreatSystem>();

            // ===== 存档系统 =====
            // SaveLoadManager — 存档入口管理器
            if (go.GetComponent<_Game.Systems.SaveLoad.SaveLoadManager>() == null)
                go.AddComponent<_Game.Systems.SaveLoad.SaveLoadManager>();
            // WorldItemManager — 地面物品实例ID管理器
            if (go.GetComponent<_Game.Systems.SaveLoad.WorldItemManager>() == null)
                go.AddComponent<_Game.Systems.SaveLoad.WorldItemManager>();
            // SaveTriggerSystem — 自动保存触发器
            if (go.GetComponent<_Game.Systems.SaveLoad.SaveTriggerSystem>() == null)
                go.AddComponent<_Game.Systems.SaveLoad.SaveTriggerSystem>();
            // PlacedStructureRegistry — 已放置建筑注册表
            if (go.GetComponent<_Game.Systems.SaveLoad.PlacedStructureRegistry>() == null)
                go.AddComponent<_Game.Systems.SaveLoad.PlacedStructureRegistry>();
            // DurabilitySystem — 耐久系统单例（必须挂载，否则所有 ?. 调用走 null 默认值）
            if (go.GetComponent<_Game.Systems.Durability.DurabilitySystem>() == null)
                go.AddComponent<_Game.Systems.Durability.DurabilitySystem>();
            // ProductionDeviceUI — 工业设备UI单例，挂在 Managers 上任一设备共用
            if (go.GetComponent<_Game.Systems.Crafting.ProductionDeviceUI>() == null)
                go.AddComponent<_Game.Systems.Crafting.ProductionDeviceUI>();
            // SceneIsolationManager — 场景切换生命周期管理
            if (go.GetComponent<_Game.Core.SceneIsolation.SceneIsolationManager>() == null)
                go.AddComponent<_Game.Core.SceneIsolation.SceneIsolationManager>();
            // UIPanelManager — 面板栈管理 + ESC逐层关闭
            if (go.GetComponent<UIPanelManager>() == null)
                go.AddComponent<UIPanelManager>();
        }

        static void EnsureSceneContext(string sceneName)
        {
            var existing = GameObject.Find("SceneContext");
            if (existing != null) return;

            var go = new GameObject("SceneContext");
            var ctx = go.AddComponent<_Game.Core.SceneIsolation.SceneContext>();
            // 用反射设 _sceneLabel（或直接加公开方法）
            var field = typeof(_Game.Core.SceneIsolation.SceneContext).GetField("_sceneLabel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null) field.SetValue(ctx, sceneName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                go, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        static void AddIfMissing(GameObject go, string fullTypeName)
        {
            var type = System.Type.GetType(fullTypeName + ", Assembly-CSharp");
            if (type == null) return;
            if (go.GetComponent(type) == null)
            {
                go.AddComponent(type);
                Debug.LogWarning($"[GameBootstrap] ⚠️ {type.Name} 未预置在 Player 上，已运行时补救。请运行 Tools → Preconfigure Player 固化。");
            }
        }
    }
}
