using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Game.Editor
{
    /// <summary>
    /// 一键将 Player GameObject 预配置好所有必要组件。
    /// 运行后 GameBootstrap 的 AddIfMissing 就不需要在运行时补救了。
    ///
    /// 用法：菜单 Tools → Preconfigure Player
    /// </summary>
    public static class PreconfigurePlayer
    {
        // GameBootstrap.OnAfterSceneLoad 中的组件顺序（保持一致性）
        static readonly string[] RequiredComponents = new[]
        {
            "Rigidbody",                                          // Unity 基础
            "CapsuleCollider",
            "Animator",

            "_Game.Systems.Player.PlayerController",
            "_Game.Systems.Inventory.Inventory",
            "_Game.Systems.Interaction.PlayerInteraction",
            "_Game.Systems.Character.PlayerCharacter",
            "_Game.Systems.Survival.SurvivalSystem",
            "_Game.Systems.Combat.PlayerCombat",
            "_Game.Systems.Weapon.WeaponShooting",
            "_Game.Systems.Weapon.WeaponAiming",
            "_Game.Systems.Weapon.WeaponHolder",
            "_Game.Systems.Weapon.SpreadVisualizer",
            // HUD UI
            "_Game.UI.SurvivalHUD",
            "_Game.UI.QuickItemBar",
            "_Game.UI.CrosshairUI",
            "_Game.UI.DecibelHUD",
            "_Game.UI.WeatherHUD",
            "_Game.UI.TopLeftHUD",
            "_Game.Systems.Building.BuildMenuUI",
            "_Game.Systems.Building.GhostPreview",
            "_Game.Systems.Crafting.CraftingUI",
            "_Game.Systems.Crafting.ChemicalResearchManager",
            "_Game.Systems.Crafting.ChemicalResearchUI",
            "_Game.Systems.PlayerInput.MouseGroundProjector",
            "_Game.Systems.Character.StaminaSystem",
            "_Game.Systems.Character.SurvivalXPSystem",
            "_Game.Systems.Building.BuildModeController",
            "_Game.Systems.Building.BuildModeInputLock",
            "_Game.Systems.Combat.DamageablePlayer",
            "_Game.Systems.Threat.FactionComponent",
            "_Game.Systems.Weapon.WeaponSwitcher",
            "_Game.Systems.Character.ProfessionApplier",
            "_Game.Systems.Vehicle.VehicleInputLock",
            "_Game.Systems.Inventory.InventoryTest",
        };

        [MenuItem("Tools/Preconfigure Player")]
        public static void Run()
        {
            // 确保 MainScene 已打开
            var mainScenePath = "Assets/Scenes/MainScene.scene";
            var currentScene = SceneManager.GetActiveScene();
            if (currentScene.path != mainScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(mainScenePath);
                }
                else
                {
                    Debug.LogWarning("[PreconfigurePlayer] 请先保存当前场景，然后手动打开 MainScene");
                    return;
                }
            }

            // 找 Player
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[PreconfigurePlayer] ❌ MainScene 中没有 tag=Player 的对象！");
                return;
            }

            int added = 0, existed = 0;

            // Rigidbody 特殊处理
            var rb = player.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = player.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                added++;
            }
            else existed++;

            // CapsuleCollider
            var col = player.GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = player.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 1f, 0);
                col.height = 2f;
                col.radius = 0.3f;
                added++;
            }
            else existed++;

            // Animator — 只在模型子对象上保留一个（Parent 不需要，避免冲突）
            var anim = player.GetComponentInChildren<Animator>();
            if (anim == null)
            {
                anim = player.AddComponent<Animator>();
                added++;
            }
            else existed++;
            // 如果 Animator 在子对象上，且 Player 本身也有一个多余的，删除它
            var selfAnim = player.GetComponent<Animator>();
            if (selfAnim != null && selfAnim != anim)
            {
                Object.DestroyImmediate(selfAnim);
                Debug.Log("[PreconfigurePlayer] 🧹 删除 Player 上多余 Animator，模型子对象已有");
            }
            // 赋值 AnimatorController + Avatar（仅当缺少时）
            if (anim.runtimeAnimatorController == null)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Game/Config/Models/Characters/Player/Player.controller");
                if (ctrl != null) { anim.runtimeAnimatorController = ctrl; existed++; }
            }
            if (anim.avatar == null)
            {
                var modelAssets = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/_Game/Config/Models/Characters/Player/Player_Rigged.fbx");
                foreach (var a in modelAssets)
                {
                    if (a is Avatar av) { anim.avatar = av; break; }
                }
            }

            // 游戏组件（跳过已处理的前3个）
            for (int i = 3; i < RequiredComponents.Length; i++)
            {
                var typeName = RequiredComponents[i];
                var type = System.Type.GetType(typeName + ", Assembly-CSharp");
                if (type == null)
                {
                    Debug.LogWarning($"[PreconfigurePlayer] ⚠️ 类型未找到: {typeName}");
                    continue;
                }

                if (player.GetComponent(type) == null)
                {
                    player.AddComponent(type);
                    added++;
                }
                else existed++;
            }

            // 标记场景脏
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.SetDirty(player);

            Debug.Log($"[PreconfigurePlayer] ✅ 完成！新增 {added} 个组件，已有 {existed} 个组件。请 Ctrl+S 保存场景。");
        }
    }
}
