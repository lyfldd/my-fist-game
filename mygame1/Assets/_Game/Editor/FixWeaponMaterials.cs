using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// 为模型自动创建材质（按命名规则匹配贴图）
/// 扫描 Weapons/ 和 Buildings/ 下所有 FBX + Prefab
/// 用法：Tools → Models → Create Materials for All
/// </summary>
public class FixWeaponMaterials
{
    private static readonly string[] SearchFolders = {
        "Assets/_Game/Models/Weapons",
        "Assets/_Game/Models/Buildings",
        "Assets/_Game/Models/Vehicles",
    };

    [MenuItem("Tools/Models/Create Materials for All")]
    static void CreateAllMaterials()
    {
        int count = 0;
        foreach (string folder in SearchFolders)
        {
            var modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            foreach (string guid in modelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                string matFolder = GetMatFolder(path);
                if (CreateMatForModel(name, matFolder))
                {
                    ApplyMatToModel(path, name, matFolder);
                    count++;
                }
            }
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab")) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                string matFolder = GetMatFolder(path);
                if (CreateMatForModel(name, matFolder))
                {
                    ApplyMatToPrefab(path, name, matFolder);
                    count++;
                }
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"材质创建完成，共处理 {count} 个模型");
    }

    /// <summary> 根据模型路径推断材质贴图文件夹
    /// 例: Weapons/AK47.fbx → Weapons/ (同级)
    /// 例: Buildings/Commercial/ShopNarrow/ShopNarrow.fbx 
    ///   → 先查同目录, 再查 Materials/BuildingMaterials/{Name}/
    /// </summary>
    static string GetMatFolder(string fbxPath)
    {
        string dir = Path.GetDirectoryName(fbxPath).Replace("\\", "/");
        string name = Path.GetFileNameWithoutExtension(fbxPath);
        
        // 1. 同目录下是否有贴图？
        if (HasTextures(dir, name)) return dir;
        
        // 2. 同目录下是否有同名子文件夹？
        string sub = dir + "/" + name;
        if (Directory.Exists(sub) && HasTextures(sub, name)) return sub;
        
        // 3. 建筑的贴图可能在 Materials/BuildingMaterials/{Name}/
        if (dir.Contains("Buildings/"))
        {
            string matDir = "Assets/_Game/Models/Materials/BuildingMaterials/" + name;
            if (Directory.Exists(matDir)) return matDir;
        }
        
        // 4. 最后兜底：用同目录
        return dir;
    }

    static bool HasTextures(string folder, string prefix)
    {
        string[] guids = AssetDatabase.FindAssets(prefix + "_BaseColor t:texture2D", new[] { folder });
        return guids.Length > 0;
    }

    /// <summary> 创建材质（已存在但无贴图则重建）</summary>
    static bool CreateMatForModel(string modelName, string matFolder)
    {
        string matPath = matFolder + "/" + modelName + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        
        // 已有且有贴图 → 跳过
        if (mat != null && mat.mainTexture != null) return true;
        
        // 已有但空的 → 删除重建
        if (mat != null)
        {
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.Refresh();
        }

        var baseColor = LoadTex(modelName + "_BaseColor", matFolder);
        var normal = LoadTex(modelName + "_Normal", matFolder);
        var metallic = LoadTex(modelName + "_Metallic", matFolder);

        mat = new Material(Shader.Find("Standard"));
        if (baseColor != null) mat.SetTexture("_MainTex", baseColor);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (metallic != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallic);
            mat.SetFloat("_Metallic", 1f);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }
        mat.SetFloat("_Glossiness", 0.5f);

        if (!AssetDatabase.IsValidFolder(matFolder))
            Directory.CreateDirectory(matFolder);
        AssetDatabase.CreateAsset(mat, matPath);
        Debug.Log($"创建材质: {modelName}");
        return true;
    }

    static void ApplyMatToModel(string fbxPath, string modelName, string matFolder)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null) return;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matFolder + "/" + modelName + ".mat");
        if (mat == null) return;

        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            var instance = UnityEngine.Object.Instantiate(model);
            renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    static void ApplyMatToPrefab(string prefabPath, string modelName, string matFolder)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matFolder + "/" + modelName + ".mat");
        if (mat == null) return;

        var prefabGO = PrefabUtility.LoadPrefabContents(prefabPath);
        var renderers = prefabGO.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.sharedMaterial = mat;
        PrefabUtility.SaveAsPrefabAsset(prefabGO, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabGO);
        Debug.Log($"材质已应用到 Prefab: {modelName}");
    }

    static Texture2D LoadTex(string name, string folder)
    {
        string[] guids = AssetDatabase.FindAssets(name + " t:texture2D", new[] { folder });
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
