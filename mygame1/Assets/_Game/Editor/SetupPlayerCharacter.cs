using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键设置玩家角色模型：Humanoid rig + 材质 + Prefab + Animator
/// 用法：Unity 菜单 → Tools → Characters → Setup Player
/// </summary>
public class SetupPlayerCharacter
{
    private const string PlayerDir = "Assets/_Game/Config/Models/Characters/Player";
    private const string ModelPath = PlayerDir + "/Player.fbx";
    private const string MatPath = PlayerDir + "/Player.mat";
    private const string PrefabPath = PlayerDir + "/Player.prefab";

    [MenuItem("Tools/Characters/Setup Player")]
    public static void Setup()
    {
        AssetDatabase.Refresh();

        // 1. 设置 FBX 导入为 Humanoid
        if (!SetupHumanoidRig())
        {
            Debug.LogError("FBX 导入配置失败，请检查 Player.fbx 是否存在");
            return;
        }

        // 2. 创建材质
        Material mat = CreateMaterial();

        // 3. 创建 Prefab
        CreatePrefab(mat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("玩家角色模型设置完成！请在 Player.prefab 上挂载 Animator Controller。");
    }

    static bool SetupHumanoidRig()
    {
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"未找到模型: {ModelPath}");
            return false;
        }

        // Humanoid rig
        importer.animationType = ModelImporterAnimationType.Human;
        importer.optimizeGameObjects = true;

        // 材质导入：用外部材质（不导入内嵌材质）
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        // 动画：保留 clip 并放到 Animations 子目录
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;

        // 复制已有动画 clip 配置，或使用默认
        var clips = importer.defaultClipAnimations;
        if (clips.Length > 0)
        {
            clips[0].name = "Walk";
            clips[0].loopTime = true;
            importer.clipAnimations = clips;
        }

        importer.SaveAndReimport();
        Debug.Log("Humanoid rig 已设置");
        return true;
    }

    static Material CreateMaterial()
    {
        // 查找贴图
        var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerDir + "/Player_BaseColor.png");
        var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerDir + "/Player_Metallic.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerDir + "/Player_Normal.png");
        var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerDir + "/Player_Roughness.png");

        // 使用 Standard shader
        var shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.name = "Player";

        if (baseColor != null) mat.SetTexture("_MainTex", baseColor);
        if (metallic != null) mat.SetTexture("_MetallicGlossMap", metallic);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (roughness != null) mat.SetTexture("_SpecGlossMap", roughness);

        // 保存材质
        // 删掉旧的
        AssetDatabase.DeleteAsset(MatPath);
        AssetDatabase.CreateAsset(mat, MatPath);
        Debug.Log("材质已创建: " + MatPath);
        return mat;
    }

    static void CreatePrefab(Material mat)
    {
        // 加载 FBX 中的模型
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("FBX 模型加载失败");
            return;
        }

        // 实例化 → 设置材质 → 保存 Prefab
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (instance == null) { Debug.LogError("实例化失败"); return; }

        // 给所有 SkinnedMeshRenderer 设置材质
        foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.material = mat;
        }

        // 添加 Animator 组件（如果没有）
        if (instance.GetComponent<Animator>() == null)
            instance.AddComponent<Animator>();

        // 保存
        AssetDatabase.DeleteAsset(PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        Debug.Log("Prefab 已创建: " + PrefabPath);
    }
}
