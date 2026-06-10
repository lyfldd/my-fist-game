using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 一键设置玩家角色模型 + 动画导入（Mixamo 骨骼）
/// 用法：Unity 菜单 → Tools → Characters → Setup Player / Import Animation
/// </summary>
public class SetupPlayerCharacter
{
    private const string PlayerDir = "Assets/_Game/Config/Models/Characters/Player";
    private const string AnimDir = PlayerDir + "/Animations";
    private const string ModelPath = PlayerDir + "/Female Start Walking.fbx";
    private const string MatPath = PlayerDir + "/Player.mat";
    private const string PrefabPath = PlayerDir + "/Female Start Walking.prefab";

    [MenuItem("Tools/Characters/Setup Player (Full)", false, 0)]
    public static void Setup()
    {
        AssetDatabase.Refresh();

        // 1. 设置模型 FBX 为 Humanoid（强制 Mixamo 骨骼映射）
        if (!SetupModelHumanoid())
        {
            Debug.LogError("[SetupPlayer] 模型 FBX 配置失败");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 2. 配置动画
        SetupAllAnimations();

        // 3. 创建/更新材质
        Material[] mats = CreateMaterials();

        // 4. 创建 Prefab + Animator
        CreatePrefab(mats);

        // 5. 创建 Animator Controller（Blend Tree）
        CreateAnimatorController();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPlayer] ✅ 全部完成！请打开 MainScene，拖入 Player_Rigged，改名 Player + Tag=Player");
    }

    // ==================== 模型导入 ====================

    static bool SetupModelHumanoid()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"未找到模型: {ModelPath}");
            return false;
        }

        // Mixamo 模型已自带 Humanoid 骨骼映射，只需确保设置正确
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;

        // 命名 FBX 内嵌动画 clip
        var clips = importer.defaultClipAnimations;
        if (clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                // 提取 FBX 自带动画，命名为 Walking
                clips[i].name = (clips.Length == 1) ? "Walking" : $"Walking_{i}";
                clips[i].loopTime = true;
            }
            importer.clipAnimations = clips;
        }

        importer.SaveAndReimport();

        // 验证
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var avatar = GetPlayerAvatar();
        if (avatar != null && avatar.isHuman)
            Debug.Log($"[SetupPlayer] ✅ 模型 Humanoid Avatar 就绪: {avatar.name}");
        else
            Debug.LogError("[SetupPlayer] ❌ Avatar 生成失败！请在 Unity 中手动检查 Female Start Walking.fbx 的 Rig 配置");

        return avatar != null;
    }

    // ==================== 动画导入 ====================

    static void SetupAllAnimations()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string absAnimDir = Path.Combine(projectRoot, AnimDir);

        if (!Directory.Exists(absAnimDir))
        {
            Debug.LogWarning($"[SetupPlayer] Animations 目录不存在: {absAnimDir}");
            return;
        }

        var playerAvatar = GetPlayerAvatar();
        if (playerAvatar == null)
        {
            Debug.LogError("[SetupPlayer] ❌ 无法获取 Player Avatar，请先确保 Player_Rigged.fbx 的 Avatar 已生成");
            return;
        }

        string[] fbxFiles = Directory.GetFiles(absAnimDir, "*.fbx");
        Debug.Log($"[SetupPlayer] 扫描到 {fbxFiles.Length} 个动画 FBX");

        foreach (var fbxPath in fbxFiles)
        {
            string fileName = Path.GetFileName(fbxPath);
            string relPath = AnimDir + "/" + fileName;
            SetupAnimationFBX(relPath, playerAvatar);
        }
    }

    static void SetupAnimationFBX(string fbxPath, Avatar playerAvatar)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[SetupPlayer] 无法找到 ModelImporter: {fbxPath}");
            return;
        }

        // 动画 FBX：Humanoid，从模型 Copy Avatar
        importer.animationType = ModelImporterAnimationType.Human;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.optimizeGameObjects = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;

        // 设置 Avatar Definition = Copy From Other Avatar
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        // 使用 SerializedObject 设置 sourceAvatar
        SetAvatarCopyFrom(importer, playerAvatar);

        // 自动命名 clip
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
        Debug.Log($"[SetupPlayer] ✅ 动画: {clipName}");
    }

    static void SetAvatarCopyFrom(ModelImporter importer, Avatar sourceAvatar)
    {
        try
        {
            // 尝试公开 API
            var sourceProp = typeof(ModelImporter).GetProperty("sourceAvatar");
            if (sourceProp != null)
            {
                sourceProp.SetValue(importer, sourceAvatar);
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.SaveAndReimport();
                return;
            }
        }
        catch { }

        // 兜底：SerializedObject
        var so = new SerializedObject(importer);
        so.Update();
        so.FindProperty("m_AvatarSetup").intValue = 2; // CopyFromOther

        string[] fields = { "m_LastHumanDescriptionAvatarSource", "m_SourceAvatar", "m_Avatar" };
        foreach (var f in fields)
        {
            var prop = so.FindProperty(f);
            if (prop != null)
            {
                prop.objectReferenceValue = sourceAvatar;
                so.ApplyModifiedProperties();
                importer.SaveAndReimport();
                Debug.Log($"[SetupPlayer] Avatar CopyFrom 通过字段 {f}");
                return;
            }
        }

        so.ApplyModifiedProperties();
        importer.SaveAndReimport();
        Debug.LogWarning("[SetupPlayer] 无法自动设置 Avatar CopyFrom，请在 Inspector 中手动设置 Rig → Avatar Definition → Copy From Other Avatar");
    }

    // ==================== 材质 ====================

    static Material[] CreateMaterials()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("[SetupPlayer] FBX 加载失败");
            return new Material[0];
        }

        var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError("[SetupPlayer] 模型无 SkinnedMeshRenderer");
            return new Material[0];
        }

        int slotCount = smr.sharedMaterials.Length;
        var result = new Material[slotCount];

        for (int i = 0; i < slotCount; i++)
            result[i] = CreateOrUpdateMaterial(smr.sharedMaterials[i], i);

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
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = matName;
            AssetDatabase.CreateAsset(mat, matAssetPath);
        }

        // 贴贴图
        TrySetTexture(mat, "_BaseMap", PlayerDir + "/Player_BaseColor.png");
        TrySetTexture(mat, "_MainTex", PlayerDir + "/Player_BaseColor.png");
        TrySetTexture(mat, "_MetallicGlossMap", PlayerDir + "/Player_Metallic.png");
        TrySetTexture(mat, "_BumpMap", PlayerDir + "/Player_Normal.png");
        if (mat.HasProperty("_BumpMap"))
            mat.EnableKeyword("_NORMALMAP");
        TrySetTexture(mat, "_EmissionMap", PlayerDir + "/Player_Emission.png");
        if (mat.HasProperty("_EmissionMap"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.white * 0.5f);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void TrySetTexture(Material mat, string prop, string texPath)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex != null && mat.HasProperty(prop))
            mat.SetTexture(prop, tex);
    }

    // ==================== Prefab ====================

    static void CreatePrefab(Material[] mats)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) { Debug.LogError("FBX 加载失败"); return; }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (instance == null) { Debug.LogError("实例化失败"); return; }

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

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerDir + "/Player.controller");
        var anim = instance.GetComponent<Animator>();
        if (anim != null && controller != null)
            anim.runtimeAnimatorController = controller;

        // 删除旧 Prefab 再保存
        if (File.Exists(PrefabPath))
            AssetDatabase.DeleteAsset(PrefabPath);

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        Debug.Log("[SetupPlayer] ✅ Prefab: " + PrefabPath);
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

        // 清空 base layer
        var baseLayer = controller.layers[0];
        foreach (var s in baseLayer.stateMachine.states)
            baseLayer.stateMachine.RemoveState(s.state);
        foreach (var sm in baseLayer.stateMachine.stateMachines)
            baseLayer.stateMachine.RemoveStateMachine(sm.stateMachine);

        // 确保 Speed 参数
        var speedParam = controller.parameters.FirstOrDefault(p => p.name == "Speed");
        if (speedParam == null)
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        var rootSM = controller.layers[0].stateMachine;

        // ── 单一 Blend Tree: 0=Idle, 1=Walk, 1.5=SlowRun, 2=FastRun ──
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
        AssetDatabase.AddObjectToAsset(blendTree, controller);

        AddClipToBlendTree(blendTree, "Standing Idle", 0f);
        AddClipToBlendTree(blendTree, "Walking", 1f);
        AddClipToBlendTree(blendTree, "Slow Run", 1.5f);
        AddClipToBlendTree(blendTree, "Fast Run", 2f);

        var locState = rootSM.AddState("Locomotion");
        locState.motion = blendTree;
        locState.writeDefaultValues = true;
        rootSM.defaultState = locState;

        Debug.Log("[SetupPlayer] Animator: Blend Tree Idle(0)→Walk(1)→SlowRun(1.5)→FastRun(2)");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPlayer] ✅ Animator Controller (Blend Tree: Idle→Walk→SlowRun→FastRun)");
    }

    static void AddClipToBlendTree(BlendTree tree, string clipName, float threshold)
    {
        // 查找动画 clip
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimDir}/{clipName}.anim");
        if (clip == null)
        {
            var guids = AssetDatabase.FindAssets($"{clipName} t:AnimationClip");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (path.Contains("Player") || path.Contains("Animations"))
                {
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip.name == clipName || clip.name.StartsWith(clipName))
                        break;
                }
            }
        }

        if (clip != null)
        {
            tree.AddChild(clip, threshold);
            Debug.Log($"[SetupPlayer] BlendTree: {clipName} @ threshold={threshold}");
        }
        else
        {
            Debug.LogWarning($"[SetupPlayer] ⚠️ 未找到动画 clip: {clipName}");
        }
    }

    // ==================== 辅助 ====================

    static Avatar GetPlayerAvatar()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
        foreach (var a in assets)
        {
            if (a is Avatar avatar && avatar.isHuman)
                return avatar;
        }
        return null;
    }

    static bool ShouldLoop(string clipName)
    {
        string lower = clipName.ToLower();
        if (lower.Contains("attack") || lower.Contains("hit") ||
            lower.Contains("death") || lower.Contains("fire") ||
            lower.Contains("shoot") || lower.Contains("reload") ||
            lower.Contains("equip"))
            return false;
        return true;
    }

    // ==================== 编辑模式下挂组件 ====================

    [MenuItem("Tools/Characters/Apply Player Components", false, 10)]
    public static void ApplyPlayerComponents()
    {
        var player = GameObject.FindWithTag("Player")
                  ?? GameObject.Find("Player")
                  ?? GameObject.Find("player");

        if (player == null)
        {
            Debug.LogError("[SetupPlayer] 场景中没有 tag=Player 或名叫 Player 的对象！请先创建 Player 父对象并设置 Tag");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Apply Player Components");

        // Rigidbody
        var rb = player.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(player);
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // CapsuleCollider
        var col = player.GetComponent<CapsuleCollider>();
        if (col == null) col = Undo.AddComponent<CapsuleCollider>(player);
        col.center = new Vector3(0, 1f, 0);
        col.height = 2f;
        col.radius = 0.3f;

        // Animator 兜底
        if (player.GetComponent<Animator>() == null && player.GetComponentInChildren<Animator>() == null)
            Undo.AddComponent<Animator>(player);

        // 所有游戏脚本
        string[] components = {
            "_Game.Systems.Player.PlayerController",
            "_Game.Systems.Inventory.Inventory",
            "_Game.Systems.Interaction.PlayerInteraction",
            "_Game.Systems.Character.PlayerCharacter",
            "_Game.Systems.Survival.SurvivalSystem",
            "_Game.Systems.Combat.PlayerCombat",
            "_Game.Systems.Weapon.WeaponShooting",
            "_Game.Systems.Weapon.WeaponAiming",
            "_Game.Systems.Weapon.WeaponHolder",
            "_Game.Systems.Building.BuildMenuUI",
            "_Game.Systems.Building.GhostPreview",
            "_Game.Systems.Crafting.CraftingUI",
            "_Game.Systems.Crafting.ProductionDeviceUI",
            "_Game.Systems.Crafting.ChemicalResearchManager",
            "_Game.Systems.Crafting.ChemicalResearchUI",
            "_Game.Systems.PlayerInput.MouseGroundProjector",
            "_Game.Systems.Character.StaminaSystem",
            "_Game.Systems.Character.SurvivalXPSystem",
            "_Game.Systems.Building.BuildModeController",
            "_Game.Systems.Building.BuildModeInputLock",
            "_Game.Systems.Combat.DamageablePlayer",
            "_Game.Systems.Threat.FactionComponent",
            "_Game.Systems.Weapon.WeaponSwitcher",
            "_Game.Systems.Character.ProfessionApplier",
            "_Game.Systems.Vehicle.VehicleInputLock",
            "_Game.Systems.Inventory.InventoryTest",
        };

        int added = 0;
        foreach (var typeName in components)
        {
            var type = System.Type.GetType(typeName + ", Assembly-CSharp");
            if (type == null) continue;
            if (player.GetComponent(type) != null) continue;
            Undo.AddComponent(player, type);
            added++;
        }

        // 摄像机跟随
        var cam = Camera.main;
        if (cam != null && cam.GetComponent<CameraFollow>() == null)
            Undo.AddComponent(cam.gameObject, typeof(CameraFollow));

        Debug.Log($"[SetupPlayer] ✅ 已添加 {added} 个组件 + Rigidbody/Collider/CameraFollow");
    }
}
