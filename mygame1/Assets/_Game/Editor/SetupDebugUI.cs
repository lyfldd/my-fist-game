using UnityEngine;
using UnityEditor;

public static class SetupDebugUI
{
    [MenuItem("Game Tools/Create Debug UI (DecibelHUD + DebugPanel)")]
    private static void Create()
    {
        // DecibelHUD
        var decibel = Object.FindObjectOfType<_Game.UI.DecibelHUD>();
        if (decibel == null)
        {
            var go = new GameObject("DecibelHUD", typeof(_Game.UI.DecibelHUD));
            Undo.RegisterCreatedObjectUndo(go, "Create DecibelHUD");
            Debug.Log("已创建 DecibelHUD（左上角分贝显示）");
        }
        else
        {
            Debug.Log("DecibelHUD 已存在，跳过");
        }

        // DebugPanel
        var debug = Object.FindObjectOfType<_Game.UI.DebugPanel>();
        if (debug == null)
        {
            var go = new GameObject("DebugPanel", typeof(_Game.UI.DebugPanel));
            Undo.RegisterCreatedObjectUndo(go, "Create DebugPanel");
            Debug.Log("已创建 DebugPanel（按 Y 切换开发者面板）");
        }
        else
        {
            Debug.Log("DebugPanel 已存在，跳过");
        }
    }
}
