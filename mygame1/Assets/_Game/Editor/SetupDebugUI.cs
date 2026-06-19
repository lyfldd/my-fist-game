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
        }

        // DebugPanel
        var debug = Object.FindObjectOfType<_Game.UI.DebugPanel>();
        if (debug == null)
        {
            var go = new GameObject("DebugPanel", typeof(_Game.UI.DebugPanel));
            Undo.RegisterCreatedObjectUndo(go, "Create DebugPanel");
        }
    }
}
