using UnityEngine;
using UnityEditor;

/// <summary>
/// 菜单工具：一键创建生存数值 HUD
/// 使用：菜单栏 → Game Tools → Create Survival HUD
/// </summary>
public static class SetupSurvivalHUD
{
    [MenuItem("Game Tools/Create Survival HUD")]
    private static void Create()
    {
        // 查找是否已存在
        var existing = Object.FindObjectOfType<_Game.UI.SurvivalHUD>();
        if (existing != null)
        {
            Debug.Log("SurvivalHUD 已存在于场景中");
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        // 创建并挂载
        var go = new GameObject("SurvivalHUD", typeof(_Game.UI.SurvivalHUD));
        Undo.RegisterCreatedObjectUndo(go, "Create Survival HUD");

        Debug.Log("✅ 已创建 SurvivalHUD（屏幕左下角显示生存数值）");
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }
}
