using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一次性修复脚本：自动补全 Player 上所有缺失的组件。
/// 用法：Unity 菜单 → Tools → Fix Missing Components
/// 运行后即可删除本脚本，组件已写入场景。
/// </summary>
public class FixMissingComponents
{
    [MenuItem("Tools/Fix Missing Components")]
    public static void Fix()
    {
        // 查找 Player
        var player = GameObject.FindWithTag("Player")
                  ?? GameObject.Find("player")
                  ?? GameObject.Find("Player");

        if (player == null)
        {
            Debug.LogError("[FixMissingComponents] 场景中找不到 Player，请确认存在且 Tag=Player 或名称为 player/Player");
            return;
        }

        Debug.Log($"[FixMissingComponents] 找到 Player: {player.name}");

        // ── Player 上需要的所有组件 ──
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
        AddIfMissing(player, "_Game.Systems.PlayerInput.MouseGroundProjector");
        AddIfMissing(player, "_Game.Systems.Character.StaminaSystem");
        AddIfMissing(player, "_Game.Systems.Character.SurvivalXPSystem");
        AddIfMissing(player, "_Game.Systems.Building.BuildModeController");
        AddIfMissing(player, "_Game.Systems.Building.BuildModeInputLock");
        AddIfMissing(player, "_Game.Systems.Combat.DamageablePlayer");
        AddIfMissing(player, "_Game.Systems.Weapon.WeaponSwitcher");
        AddIfMissing(player, "_Game.Systems.Character.ProfessionApplier");
        AddIfMissing(player, "_Game.Systems.Vehicle.VehicleInputLock");
        AddIfMissing(player, "_Game.Systems.ItemUsage.ItemUsageSystem");
        AddIfMissing(player, "_Game.Systems.Inventory.InventoryTest");

        // ── InputRouter：挂到 Managers 上 ──
        var managers = GameObject.Find("Managers");
        if (managers == null)
        {
            managers = new GameObject("Managers");
            Debug.Log("[FixMissingComponents] 创建 Managers GameObject");
        }
        AddIfMissing(managers, "_Game.Core.InputRouter");

        // ── Canvas 上需要的 UI 组件 ──
        var canvas = FindCanvas();
        if (canvas != null)
        {
            AddIfMissing(canvas, "_Game.UI.InteractionPromptUI");
            AddIfMissing(canvas, "_Game.UI.InteractionToast");
            AddIfMissing(canvas, "_Game.UI.TopLeftHUD");
        }

        // ── 标记场景已修改 ──
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(managers);
        if (canvas != null) EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[FixMissingComponents] ✅ 完成！所有缺失组件已补全。现在可以安全删除 FixMissingComponents.cs");
    }

    static void AddIfMissing(GameObject go, string fullTypeName)
    {
        var type = System.Type.GetType(fullTypeName + ", Assembly-CSharp");
        if (type == null)
        {
            Debug.LogWarning($"[FixMissingComponents] 找不到类型: {fullTypeName}");
            return;
        }

        if (go.GetComponent(type) == null)
        {
            go.AddComponent(type);
            Debug.Log($"[FixMissingComponents] ✓ 添加 [{go.name}] ← {type.Name}");
        }
    }

    static GameObject FindCanvas()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas != null) return canvas;

        // 尝试在场景中找到任何 Canvas
        var c = Object.FindObjectOfType<Canvas>();
        return c != null ? c.gameObject : null;
    }
}
