using UnityEditor;
using UnityEngine;

public static class SetupZombieSpawnDebug
{
    [MenuItem("Game Tools/Create Zombie Spawn Debug Window (F1)")]
    private static void Create()
    {
        var existing = Object.FindObjectOfType<_Game.UI.ZombieSpawnDebugWindow>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var go = new GameObject("ZombieSpawnDebugWindow", typeof(_Game.UI.ZombieSpawnDebugWindow));
        Undo.RegisterCreatedObjectUndo(go, "Create ZombieSpawnDebugWindow");

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }
}
