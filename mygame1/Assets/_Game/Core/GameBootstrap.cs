using UnityEngine;
using _Game.Systems.Threat;

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
            // InputRouter 已自带 EnsureInstance（首次 BindKey 时自动创建），
            // 这里提前创建以便更早可用
            EnsureInputRouter();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            var player = GameObject.FindWithTag("Player")
                      ?? GameObject.Find("player")
                      ?? GameObject.Find("Player");

            if (player == null) return;

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
            AddIfMissing(player, "_Game.Systems.Building.BuildMenuUI");
            AddIfMissing(player, "_Game.Systems.Building.GhostPreview");
            AddIfMissing(player, "_Game.Systems.Crafting.CraftingUI");
            AddIfMissing(player, "_Game.Systems.Crafting.ProductionDeviceUI");
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

            // 设置玩家阵营
            var playerFaction = player.GetComponent<FactionComponent>();
            if (playerFaction != null)
                playerFaction.SetFaction(_Game.Config.FactionType.Player);

            EnsureInputRouter();
        }

        static void EnsureInputRouter()
        {
            if (InputRouter.Instance != null) return;
            var go = GameObject.Find("Managers") ?? new GameObject("Managers");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<InputRouter>();

            // SoundEmitter 从静态类改为 MonoBehaviour，确保实例存在
            if (go.GetComponent<_Game.Systems.Audio.SoundEmitter>() == null)
                go.AddComponent<_Game.Systems.Audio.SoundEmitter>();
            // MuzzleFlashSystem — 枪口火焰 VFX
            if (go.GetComponent<_Game.Systems.VFX.MuzzleFlashSystem>() == null)
                go.AddComponent<_Game.Systems.VFX.MuzzleFlashSystem>();
            // FactionSystem — 阵营系统
            if (go.GetComponent<_Game.Systems.Threat.FactionSystem>() == null)
                go.AddComponent<_Game.Systems.Threat.FactionSystem>();
            // ThreatSystem — 威胁/仇恨系统
            if (go.GetComponent<_Game.Systems.Threat.ThreatSystem>() == null)
                go.AddComponent<_Game.Systems.Threat.ThreatSystem>();
        }

        static void AddIfMissing(GameObject go, string fullTypeName)
        {
            var type = System.Type.GetType(fullTypeName + ", Assembly-CSharp");
            if (type == null) return;
            if (go.GetComponent(type) == null)
                go.AddComponent(type);
        }
    }
}
