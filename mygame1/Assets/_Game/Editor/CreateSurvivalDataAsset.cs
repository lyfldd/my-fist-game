using UnityEngine;
using UnityEditor;
using _Game.Config;

/// <summary>
/// 工具菜单：一键创建 SurvivalData_Default 配置资产
/// 使用：菜单栏 → Game Tools → Create Survival Data Asset
/// </summary>
public static class CreateSurvivalDataAsset
{
    [MenuItem("Game Tools/Create Survival Data Asset")]
    private static void CreateAsset()
    {
        string path = "Assets/_Game/Resources/SurvivalData_Default.asset";

        // 检查是否已存在
        var existing = AssetDatabase.LoadAssetAtPath<SurvivalData>(path);
        if (existing != null)
        {
            Debug.Log("SurvivalData_Default 已存在，路径：" + path);
            EditorGUIUtility.PingObject(existing);
            return;
        }

        // 确保 Resources 文件夹存在
        string dir = "Assets/_Game/Resources";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            AssetDatabase.CreateFolder("Assets/_Game", "Resources");
        }

        // 创建资产
        var data = ScriptableObject.CreateInstance<SurvivalData>();
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("✅ 已创建 SurvivalData_Default.asset，路径：" + path);
        EditorGUIUtility.PingObject(data);
    }
}
