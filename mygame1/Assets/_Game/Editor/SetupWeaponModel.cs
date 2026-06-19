using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键设置武器模型：创建 Prefab + 绑定 ItemData
/// 支持 .fbx 和 .glb 格式
/// 用法：Unity 菜单 → Tools → Weapons → Setup From Model
/// </summary>
public class SetupWeaponModel
{
    private const string PrefabPath = "Assets/_Game/Models/Weapons/LongSword.prefab";
    private const string ItemDataPath = "Assets/_Game/Config/Weapons/LongSword.asset";

    [MenuItem("Tools/Weapons/Setup From Model")]
    public static void Setup()
    {
        AssetDatabase.Refresh();

        // 自动查找模型文件（先找 fbx，再找 glb）
        string modelPath = FindModelFile();
        if (modelPath == null)
        {
            Debug.LogError("未找到模型文件！请把 LongSword.fbx 或 LongSword.glb 放到 Assets/_Game/Models/Weapons/");
            return;
        }

        var model = AssetDatabase.LoadMainAssetAtPath(modelPath) as GameObject;
        if (model == null)
        {
            Debug.LogError($"模型加载失败: {modelPath}");
            return;
        }

        // 创建 Prefab
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (instance == null) { Debug.LogError("实例化失败"); return; }

        instance.transform.localScale = Vector3.one * 0.01f;

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);

        // 绑定 ItemData
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var itemData = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDataPath);
        if (itemData != null && prefab != null)
        {
            itemData.worldPrefab = prefab;

            var icon = AssetPreview.GetAssetPreview(prefab);
            if (icon != null)
                itemData.icon = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), Vector2.one * 0.5f);

            EditorUtility.SetDirty(itemData);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static string FindModelFile()
    {
        string folder = "Assets/_Game/Models/Weapons/";
        string[] exts = { ".fbx", ".glb", ".obj" };
        foreach (var ext in exts)
        {
            string path = folder + "LongSword" + ext;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                return path;
        }
        return null;
    }
}
