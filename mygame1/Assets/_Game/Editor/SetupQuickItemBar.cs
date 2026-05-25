using UnityEngine;
using UnityEditor;

/// <summary>
/// 菜单工具：一键创建快捷物品栏
/// 使用：菜单栏 → Game Tools → Create Quick Item Bar
/// </summary>
public static class SetupQuickItemBar
{
    [MenuItem("Game Tools/Create Quick Item Bar")]
    private static void Create()
    {
        var existing = Object.FindObjectOfType<_Game.UI.QuickItemBar>();
        if (existing != null)
        {
            Debug.Log("QuickItemBar 已存在于场景中");
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var go = new GameObject("QuickItemBar", typeof(_Game.UI.QuickItemBar));
        Undo.RegisterCreatedObjectUndo(go, "Create Quick Item Bar");

        Debug.Log("✅ 已创建 QuickItemBar（屏幕中下方显示快捷物品）");
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }
}
