using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 编辑器工具：将 Player.controller 从 1D BlendTree 升级到 2D Simple Directional
/// 自动从旧 BlendTree 提取动画剪辑并放置到 2D 平面
/// </summary>
public class UpgradeAnimator2D
{
    private const string ControllerPath = "Assets/_Game/Config/Models/Characters/Player/Player.controller";
    private const string ClipSearchDir = "Assets/_Game/Config/Models/Characters/Player/Animations";

    [MenuItem("Tools/动画/升级到 2D 混合树")]
    public static void Upgrade()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[2D升级] 未找到 Controller: {ControllerPath}");
            return;
        }

        // ── 1. 搜索动画剪辑（FBX 内嵌 clip 需要全项目搜索）──
        var clips = new Dictionary<string, AnimationClip>();
        var allClipGuids = AssetDatabase.FindAssets("t:AnimationClip");
        foreach (var guid in allClipGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            // 只取 Player 相关路径的动画
            if (!path.Contains("Player") && !path.Contains("Animations")) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null && !clips.ContainsKey(clip.name))
                clips[clip.name] = clip;
        }
        // ── 2. 清空 base layer ──
        var baseLayer = controller.layers[0];
        var rootSM = baseLayer.stateMachine;

        foreach (var s in rootSM.states.ToArray())
            rootSM.RemoveState(s.state);
        foreach (var sm in rootSM.stateMachines.ToArray())
            rootSM.RemoveStateMachine(sm.stateMachine);

        // ── 3. 重建参数 ──
        RemoveParam(controller, "Speed");
        RemoveParam(controller, "MoveX");
        RemoveParam(controller, "MoveZ");
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
        // ── 4. 创建 2D Simple Directional BlendTree ──
        var blend = new BlendTree
        {
            name = "Locomotion2D",
            blendParameter = "MoveX",
            blendParameterY = "MoveZ",
            blendType = BlendTreeType.SimpleDirectional2D,
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(blend, controller);

        // ── 5. 放置动画到 2D 平面（同一组前向动画镜像到四个方向）──
        // 目前只有前进动画，但把它们映射到四个方向，至少不会卡 Idle
        // 后续替换为真正的横移/后退动画时，只需改对应位置的 clip 即可

        // 中心
        TryAddClip2D(blend, clips, "Standing Idle", 0f, 0f);

        // 前方 (W)
        TryAddClip2D(blend, clips, "Walking",   0f, 1f);
        TryAddClip2D(blend, clips, "Slow Run",  0f, 1.5f);
        TryAddClip2D(blend, clips, "Fast Run",  0f, 2f);

        // 后方 (S) — 镜像同一组动画
        TryAddClip2D(blend, clips, "Walking",   0f, -1f);
        TryAddClip2D(blend, clips, "Slow Run",  0f, -1.5f);
        TryAddClip2D(blend, clips, "Fast Run",  0f, -2f);

        // 右方 (D) — 用 Walk/Run 做横移（后续换 Strafe 动画）
        TryAddClip2D(blend, clips, "Walking",   1f, 0f);
        TryAddClip2D(blend, clips, "Fast Run",  2f, 0f);

        // 左方 (A)
        TryAddClip2D(blend, clips, "Walking",   -1f, 0f);
        TryAddClip2D(blend, clips, "Fast Run",  -2f, 0f);

        // ── 6. 创建新状态 ──
        var state = rootSM.AddState("Locomotion");
        state.motion = blend;
        state.writeDefaultValues = true;
        rootSM.defaultState = state;

        // ── 7. 清理孤儿子资源（旧 BlendTree / 无效引用） ──
        CleanOrphans(controller);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

    }

    // ═══════════════════════════════════════════
    // 辅助
    // ═══════════════════════════════════════════

    private static void RemoveParam(AnimatorController ctrl, string name)
    {
        for (int i = ctrl.parameters.Length - 1; i >= 0; i--)
            if (ctrl.parameters[i].name == name)
                ctrl.RemoveParameter(i);
    }

    private static void TryAddClip2D(BlendTree tree, Dictionary<string, AnimationClip> clips,
        string clipName, float posX, float posZ)
    {
        if (clips.TryGetValue(clipName, out var clip))
        {
            tree.AddChild(clip, new Vector2(posX, posZ));
        }
    }

    private static void CleanOrphans(AnimatorController controller)
    {
        // 遍历 controller 的所有 sub-assets，清理无引用的 BlendTree
        var orphans = new List<Object>();
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(ControllerPath);

        foreach (var obj in subAssets)
        {
            if (obj is BlendTree bt && bt.name == "Locomotion")
                orphans.Add(bt);
        }

        foreach (var obj in orphans)
        {
            AssetDatabase.RemoveObjectFromAsset(obj);
        }
    }

    [MenuItem("Tools/动画/还原到 1D 混合树")]
    public static void RevertTo1D()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[1D还原] 未找到 Controller: {ControllerPath}");
            return;
        }

        // 清空
        var rootSM = controller.layers[0].stateMachine;
        foreach (var s in rootSM.states.ToArray())
            rootSM.RemoveState(s.state);

        // 参数
        RemoveParam(controller, "MoveX");
        RemoveParam(controller, "MoveZ");
        RemoveParam(controller, "Speed");
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // 1D BlendTree: Idle(0)→Walk(1)→SlowRun(1.5)→FastRun(2)
        var blend = new BlendTree
        {
            name = "Locomotion",
            blendParameter = "Speed",
            blendType = BlendTreeType.Simple1D,
            useAutomaticThresholds = false,
            minThreshold = 0f,
            maxThreshold = 2f,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(blend, controller);

        // 搜索动画
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("Player") && !path.Contains("Animations")) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null && !clips.ContainsKey(clip.name))
                clips[clip.name] = clip;
        }

        TryAddClip1D(blend, clips, "Standing Idle", 0f);
        TryAddClip1D(blend, clips, "Walking", 1f);
        TryAddClip1D(blend, clips, "Slow Run", 1.5f);
        TryAddClip1D(blend, clips, "Fast Run", 2f);

        var state = rootSM.AddState("Locomotion");
        state.motion = blend;
        state.writeDefaultValues = true;
        rootSM.defaultState = state;

        CleanOrphans(controller);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void TryAddClip1D(BlendTree tree, Dictionary<string, AnimationClip> clips,
        string clipName, float threshold)
    {
        if (clips.TryGetValue(clipName, out var clip))
        {
            tree.AddChild(clip, threshold);
        }
    }

    [MenuItem("Tools/动画/还原到 1D 混合树", validate = true)]
    private static bool ValidateRevert()
    {
        return AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null;
    }
}
