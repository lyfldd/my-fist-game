using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 一键设置玩家角色模型 + 动画导入
/// 用法：Unity 菜单 → Tools → Characters → Setup Player / Import Animation
/// </summary>
public class SetupPlayerCharacter
{
    private const string PlayerDir = "Assets/_Game/Config/Models/Characters/Player";
    private const string AnimDir = PlayerDir + "/Animations";
    private const string ModelPath = PlayerDir + "/Player.fbx";
    private const string MatPath = PlayerDir + "/Player.mat";
    private const string PrefabPath = PlayerDir + "/Player.prefab";

    [MenuItem("Tools/Characters/Setup Player (Full)", false, 0)]
    public static void Setup()
    {
        AssetDatabase.Refresh();

        // 1. 设置模型 FBX 为 Humanoid
        if (!SetupModelHumanoid())
        {
            Debug.LogError("[SetupPlayer] 模型 FBX 配置失败，检查 Player.fbx 是否存在");
            return;
        }

        // 确保模型完全导入后再读取
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 2. 配置所有动画 FBX（Animations 目录下）
        SetupAllAnimations();

        // 3. 创建/更新材质
        Material[] mats = CreateMaterials();

        // 4. 创建 Prefab + Animator
        CreatePrefab(mats);

        // 5. 创建/更新 Animator Controller
        CreateAnimatorController();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPlayer] 全部完成！");
    }

    // ==================== 模型导入 ====================

    [MenuItem("Tools/Characters/Setup Model (Humanoid)", false, 1)]
    public static void SetupModelOnly()
    {
        SetupModelHumanoid();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPlayer] 模型 Humanoid 设置完成");
    }

    static bool SetupModelHumanoid()
    {
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"未找到模型: {ModelPath}");
            return false;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.optimizeGameObjects = true;
        // 让 Unity 自动生成材质（之后用贴图替换）
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;

        // 清除旧的 clip 配置（新模型可能有不同动画）
        importer.clipAnimations = new ModelImporterClipAnimation[0];

        importer.SaveAndReimport();
        Debug.Log("[SetupPlayer] 模型 Humanoid rig 已设置，Avatar 已生成，材质已自动提取");
        return true;
    }

    // ==================== 动画导入 ====================

    [MenuItem("Tools/Characters/Import All Animations", false, 2)]
    public static void ImportAllAnimations()
    {
        SetupAllAnimations();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPlayer] 所有动画导入完成");
    }

    static void SetupAllAnimations()
    {
        // AnimDir 是相对路径，转成绝对路径给 Directory API
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string absAnimDir = Path.Combine(projectRoot, AnimDir);

        if (!Directory.Exists(absAnimDir))
        {
            Debug.LogWarning($"[SetupPlayer] Animations 目录不存在: {absAnimDir}");
            return;
        }

        // 先确保 Player Avatar 已生成
        var playerAvatar = GetPlayerAvatar();
        if (playerAvatar == null)
        {
            Debug.LogWarning("[SetupPlayer] Player Avatar 未就绪，先跑一次 Setup Model");
            SetupModelHumanoid();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            playerAvatar = GetPlayerAvatar();
        }

        if (playerAvatar == null)
        {
            Debug.LogError("[SetupPlayer] 无法获取 Player Avatar，动画导入中止");
            return;
        }

        string[] fbxFiles = Directory.GetFiles(absAnimDir, "*.fbx");
        Debug.Log($"[SetupPlayer] 动画目录扫描到 {fbxFiles.Length} 个 FBX");

        foreach (var fbxPath in fbxFiles)
        {
            // 直接用文件名拼接相对路径，避免 FileUtil.GetProjectRelativePath 处理反斜杠的问题
            string fileName = Path.GetFileName(fbxPath);
            string relPath = AnimDir + "/" + fileName;
            Debug.Log($"[SetupPlayer] 处理动画: {relPath}");
            SetupAnimationFBX(relPath, playerAvatar);
        }

        Debug.Log($"[SetupPlayer] 共配置 {fbxFiles.Length} 个动画 FBX");
    }

    static void SetupAnimationFBX(string fbxPath, Avatar playerAvatar)
    {
        // 诊断：先检查文件是否被 Unity 识别
        var asset = AssetDatabase.LoadAssetAtPath<Object>(fbxPath);
        if (asset == null)
        {
            Debug.LogError($"[SetupPlayer] AssetDatabase 无法加载: {fbxPath}，文件是否被 Unity 正确导入？尝试重新拖入文件。");
            return;
        }
        Debug.Log($"[SetupPlayer] 找到资源: {asset.name} (类型={asset.GetType().Name})");

        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[SetupPlayer] 未找到 ModelImporter: {fbxPath}（资源类型={asset.GetType().Name}）");
            return;
        }

        // 动画 FBX 设为 Humanoid，不带模型
        importer.animationType = ModelImporterAnimationType.Human;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.optimizeGameObjects = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        // 禁用 Mesh 导入（只要动画数据）
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;

        // 自动命名 clip：用文件名
        string clipName = Path.GetFileNameWithoutExtension(fbxPath);
        var clips = importer.defaultClipAnimations;
        if (clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = (clips.Length == 1) ? clipName : $"{clipName}_{i}";
                clips[i].loopTime = ShouldLoop(clipName);
            }
            importer.clipAnimations = clips;
        }

        importer.SaveAndReimport();

        // 设置 Avatar 为 Copy From Player（需要用 SerializedObject 操作）
        SetAvatarCopyFrom(importer, playerAvatar);

        Debug.Log($"[SetupPlayer] 动画导入完成: {clipName}");
    }

    static void SetAvatarCopyFrom(ModelImporter importer, Avatar sourceAvatar)
    {
        // 方式 1：先用公开 API（Unity 2019.3+ / 团结引擎可能支持）
        try
        {
            var sourceProp = typeof(ModelImporter).GetProperty("sourceAvatar");
            if (sourceProp != null)
            {
                sourceProp.SetValue(importer, sourceAvatar);
                importer.avatarSetup = (ModelImporterAvatarSetup)2; // CopyFromOther
                importer.SaveAndReimport();
                Debug.Log($"[SetupPlayer] Avatar CopyFrom 设置成功（公开 API）");
                return;
            }
        }
        catch { /* fall through */ }

        // 方式 2：SerializedObject 操作（兜底）
        var so = new SerializedObject(importer);
        so.Update();

        var avatarSetupProp = so.FindProperty("m_AvatarSetup");
        avatarSetupProp.intValue = 2; // CopyFromOther

        // 尝试多个可能的 Avatar 引用字段名
        string[] avatarFieldNames = {
            "m_LastHumanDescriptionAvatarSource",
            "m_SourceAvatar",
            "m_Avatar"
        };

        bool found = false;
        foreach (var fieldName in avatarFieldNames)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = sourceAvatar;
                found = true;
                Debug.Log($"[SetupPlayer] Avatar CopyFrom 设置成功（字段: {fieldName}）");
                break;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[SetupPlayer] 无法找到 Avatar Source 字段，请在 Unity 编辑器中手动设置 Rig → Avatar Definition → Copy From Other Avatar → 选择 PlayerAvatar");
        }

        so.ApplyModifiedProperties();
        importer.SaveAndReimport();
    }

    // ==================== 材质 ====================

    static Material[] CreateMaterials()
    {
        // 找到 Unity 从 FBX 自动生成的材质
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError($"[SetupPlayer] FBX 加载失败: {ModelPath}，请确认文件存在且 Unity 已完成导入");
            return new Material[0];
        }

        // 列出所有子物体上的 Renderer 组件，方便调试
        var allRenderers = model.GetComponentsInChildren<Renderer>();
        Debug.Log($"[SetupPlayer] 模型包含 {allRenderers.Length} 个 Renderer: {string.Join(", ", allRenderers.Select(r => $"{r.name}({r.GetType().Name})"))}");

        var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null)
        {
            // 有些模型用 MeshRenderer（不带骨骼），这也可以
            var mr = model.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                Debug.LogWarning($"[SetupPlayer] 模型使用 MeshRenderer（无骨骼），尝试处理...");
                int count = mr.sharedMaterials.Length;
                var mrMats = new Material[count];
                for (int i = 0; i < count; i++)
                {
                    mrMats[i] = CreateOrUpdateMaterial(mr.sharedMaterials[i], i);
                }
                AssetDatabase.SaveAssets();
                return mrMats;
            }
            Debug.LogError("[SetupPlayer] 模型无任何 Renderer，请检查 FBX 是否包含网格");
            return new Material[0];
        }

        int slotCount = smr.sharedMaterials.Length;
        var result = new Material[slotCount];

        Debug.Log($"[SetupPlayer] SkinnedMeshRenderer 有 {slotCount} 个材质槽");

        for (int i = 0; i < slotCount; i++)
        {
            result[i] = CreateOrUpdateMaterial(smr.sharedMaterials[i], i);
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    static Material CreateOrUpdateMaterial(Material srcMat, int index)
    {
        string matName = srcMat != null ? srcMat.name : $"Player_mat_{index}";
        string matAssetPath = $"{PlayerDir}/{matName}.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
        if (mat == null && srcMat != null)
        {
            mat = new Material(srcMat);
            mat.name = matName;
            AssetDatabase.CreateAsset(mat, matAssetPath);
        }
        else if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = matName;
            AssetDatabase.CreateAsset(mat, matAssetPath);
        }

        // 贴贴图
        var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerDir + "/Player_BaseColor.png");
        var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerDir + "/Player_Metallic.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerDir + "/Player_Normal.png");
        var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerDir + "/Player_Roughness.png");
        var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerDir + "/Player_Emission.png");

        TrySetTexture(mat, "_BaseMap", baseColor);
        TrySetTexture(mat, "_MainTex", baseColor);
        TrySetTexture(mat, "_MetallicGlossMap", metallic);
        TrySetTexture(mat, "_BumpMap", normal);
        if (normal != null && mat.HasProperty("_BumpMap"))
            mat.EnableKeyword("_NORMALMAP");
        TrySetTexture(mat, "_SpecGlossMap", roughness);
        TrySetTexture(mat, "_Smoothness", roughness);
        TrySetTexture(mat, "_EmissionMap", emission);
        if (emission != null && mat.HasProperty("_EmissionMap"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.white * 0.5f);
        }

        EditorUtility.SetDirty(mat);
        Debug.Log($"[SetupPlayer] 材质 [{index}] {matName} 贴图已更新 (shader={mat.shader.name})");
        return mat;
    }

    static void TrySetTexture(Material mat, string prop, Texture2D tex)
    {
        if (tex != null && mat.HasProperty(prop))
            mat.SetTexture(prop, tex);
    }

    // ==================== Prefab ====================

    static void CreatePrefab(Material[] mats)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) { Debug.LogError("FBX 模型加载失败"); return; }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (instance == null) { Debug.LogError("实例化失败"); return; }

        // 处理所有 Renderer（SkinnedMeshRenderer 或 MeshRenderer）
        foreach (var r in instance.GetComponentsInChildren<Renderer>())
        {
            if (mats.Length > 0)
            {
                var assigned = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < assigned.Length; i++)
                    assigned[i] = (i < mats.Length) ? mats[i] : mats[0];
                r.materials = assigned;
            }
        }

        if (instance.GetComponent<Animator>() == null)
            instance.AddComponent<Animator>();

        // 绑定 Animator Controller
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerDir + "/Player.controller");
        var anim = instance.GetComponent<Animator>();
        if (anim != null && controller != null)
            anim.runtimeAnimatorController = controller;

        AssetDatabase.DeleteAsset(PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        Debug.Log("[SetupPlayer] Prefab 已更新: " + PrefabPath);
    }

    // ==================== Animator Controller ====================

    static void CreateAnimatorController()
    {
        var controllerPath = PlayerDir + "/Player.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            Debug.Log("[SetupPlayer] 新建 Animator Controller");
        }

        // 清空已有 layers
        var layers = controller.layers;
        if (layers.Length > 0)
        {
            var baseLayer = layers[0];
            foreach (var s in baseLayer.stateMachine.states)
                baseLayer.stateMachine.RemoveState(s.state);
            foreach (var sm in baseLayer.stateMachine.stateMachines)
                baseLayer.stateMachine.RemoveStateMachine(sm.stateMachine);
        }

        // 确保 Speed 参数存在
        var speedParam = controller.parameters.FirstOrDefault(p => p.name == "Speed");
        if (speedParam == null)
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // 创建 Blend Tree 并保存为 Controller 的子资产
        var blendTree = new BlendTree
        {
            name = "Locomotion",
            blendParameter = "Speed",
            blendType = BlendTreeType.Simple1D,
            useAutomaticThresholds = false,
            minThreshold = 0f,
            maxThreshold = 2f,
            hideFlags = HideFlags.HideInHierarchy
        };

        // 关键：把 BlendTree 保存为 Controller 的子资产，否则不会持久化
        AssetDatabase.AddObjectToAsset(blendTree, controller);

        // 添加动画 clip 到 Blend Tree
        AddClipToBlendTree(blendTree, "Walking", 1f);
        AddClipToBlendTree(blendTree, "Slow Run", 1.5f);
        AddClipToBlendTree(blendTree, "Fast Run", 2f);

        var rootStateMachine = controller.layers[0].stateMachine;
        var locomotionState = rootStateMachine.AddState("Locomotion");
        locomotionState.motion = blendTree;
        locomotionState.writeDefaultValues = true;
        rootStateMachine.defaultState = locomotionState;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPlayer] Animator Controller 已更新（Locomotion Blend Tree + Speed 参数）");
    }

    static void AddClipToBlendTree(BlendTree tree, string clipName, float threshold)
    {
        // 查找动画 clip：先找单独的 .anim 文件，再找 FBX 内嵌 clip
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimDir}/{clipName}.anim");
        if (clip == null)
        {
            // 从 FBX 中找
            var guids = AssetDatabase.FindAssets($"{clipName} t:AnimationClip");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (path.Contains("Player") || path.Contains("Animations"))
                {
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip.name == clipName) break;
                }
            }
        }

        if (clip != null)
        {
            tree.AddChild(clip, threshold);
            Debug.Log($"[SetupPlayer] BlendTree 添加: {clipName} @ threshold={threshold}");
        }
        else
        {
            Debug.LogWarning($"[SetupPlayer] 未找到动画 clip: {clipName}，BlendTree 跳过");
        }
    }

    // ==================== 辅助 ====================

    static Avatar GetPlayerAvatar()
    {
        // 从 Player.fbx 获取生成的 Avatar
        var assets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
        foreach (var a in assets)
        {
            if (a is Avatar avatar)
                return avatar;
        }
        return null;
    }

    // 判断动画是否应该循环
    static bool ShouldLoop(string clipName)
    {
        string lower = clipName.ToLower();
        // 攻击、死亡、受击不循环
        if (lower.Contains("attack") || lower.Contains("hit") ||
            lower.Contains("death") || lower.Contains("dead") ||
            lower.Contains("fire") || lower.Contains("shoot") ||
            lower.Contains("reload") || lower.Contains("equip"))
            return false;
        // 移动、待机循环
        return true;
    }
}
