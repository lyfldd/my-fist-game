using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 编辑器工具：重建 Player.controller — 1D BlendTree
/// Idle(0) → Walk(1) → SlowRun(1.5) → FastRun(2)
/// </summary>
public class RebuildPlayerController
{
    private const string CtrlPath = "Assets/_Game/Config/Models/Characters/Player/Player.controller";
    private const string AnimDir = "Assets/_Game/Config/Models/Characters/Player/Animations";

    [MenuItem("Tools/动画/重建 Player Controller (1D)")]
    public static void Rebuild()
    {
        // 删除旧 Controller
        var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (old != null)
        {
            AssetDatabase.DeleteAsset(CtrlPath);
            AssetDatabase.Refresh();
        }

        // 新建 Controller
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        var root = ctrl.layers[0].stateMachine;

        // 参数
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // BlendTree
        var bt = new BlendTree
        {
            name = "Locomotion",
            blendParameter = "Speed",
            blendType = BlendTreeType.Simple1D,
            useAutomaticThresholds = false,
            minThreshold = 0f,
            maxThreshold = 2f,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(bt, ctrl);

        // 找动画
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var g in AssetDatabase.FindAssets("t:AnimationClip"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.Contains("Player") && !p.Contains("Animations")) continue;
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            if (c != null && !clips.ContainsKey(c.name))
                clips[c.name] = c;
        }
        Debug.Log($"[重建] 找到 {clips.Count} 个动画: {string.Join(", ", clips.Keys)}");

        Add(bt, clips, "Standing Idle", 0f);
        Add(bt, clips, "Walking", 1f);
        Add(bt, clips, "Slow Run", 1.5f);
        Add(bt, clips, "Fast Run", 2f);

        var state = root.AddState("Locomotion");
        state.motion = bt;
        state.writeDefaultValues = true;
        root.defaultState = state;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[重建] ✅ Player.controller: 1D BlendTree Idle(0)→Walk(1)→SlowRun(1.5)→FastRun(2)");
    }

    static void Add(BlendTree t, Dictionary<string, AnimationClip> clips, string name, float th)
    {
        if (clips.TryGetValue(name, out var clip))
        {
            t.AddChild(clip, th);
            Debug.Log($"  {name} @ {th}");
        }
        else
            Debug.LogWarning($"[重建] ⚠️ 未找到: {name}");
    }
}
