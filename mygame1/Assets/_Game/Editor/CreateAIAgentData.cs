using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Editor
{
    /// <summary>
    /// 一键生成6类 AIAgentData SO
    /// </summary>
    public static class CreateAIAgentData
    {
        const string OUTPUT_PATH = "Assets/_Game/Config/AIAgent/";

        [MenuItem("Tools/创建默认AIAgent数据")]
        public static void CreateAll()
        {
            if (!AssetDatabase.IsValidFolder(OUTPUT_PATH))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Config", "AIAgent");
            }

            var specs = new[]
            {
                ("AIAgent_Zombie", "僵尸", FactionType.Zombie,
                    idleSpeed: 1.5f, combatSpeed: 2.5f, fleeSpeed: 3f,
                    activityRadius: 50f, hasActivityConstraint: true,
                    perceptionRange: 30f, visionConeAngle: 120f,
                    centralVision: 30f, peripheralVision: 60f, peripheralRecog: 0.5f,
                    attackRange: 1.5f, attackCooldown: 1f, attackDamage: 10f,
                    idleBehavior: IdleBehavior.Wander, wanderRadius: 10f,
                    canFlee: false, fleeThreshold: 0f, canAllyAlert: true, allyAlertRange: 15f,
                    soundTags: new[] { SoundTag.Footstep, SoundTag.Combat, SoundTag.Gunshot, SoundTag.Building, SoundTag.Impact }),

                ("AIAgent_Bandit", "黑恶势力", FactionType.Bandit,
                    idleSpeed: 3f, combatSpeed: 4.5f, fleeSpeed: 6f,
                    activityRadius: 100f, hasActivityConstraint: true,
                    perceptionRange: 40f, visionConeAngle: 90f,
                    centralVision: 30f, peripheralVision: 45f, peripheralRecog: 0.8f,
                    attackRange: 15f, attackCooldown: 2f, attackDamage: 15f,
                    idleBehavior: IdleBehavior.StandBy, wanderRadius: 5f,
                    canFlee: true, fleeThreshold: 0.3f,
                    canAllyAlert: true, allyAlertRange: 20f,
                    soundTags: new[] { SoundTag.Footstep, SoundTag.Combat, SoundTag.Gunshot }),

                ("AIAgent_Survivor", "幸存者", FactionType.Survivor,
                    idleSpeed: 2.5f, combatSpeed: 3.5f, fleeSpeed: 5f,
                    activityRadius: 80f, hasActivityConstraint: true,
                    perceptionRange: 25f, visionConeAngle: 360f,
                    centralVision: 360f, peripheralVision: 360f, peripheralRecog: 1f,
                    attackRange: 1.5f, attackCooldown: 1.2f, attackDamage: 12f,
                    idleBehavior: IdleBehavior.FollowEntity, wanderRadius: 10f,
                    canFlee: true, fleeThreshold: 0.4f,
                    canAllyAlert: true, allyAlertRange: 25f,
                    soundTags: new[] { SoundTag.Footstep, SoundTag.Combat, SoundTag.Gunshot, SoundTag.Voice }),

                ("AIAgent_Military", "军方", FactionType.Military,
                    idleSpeed: 3f, combatSpeed: 5f, fleeSpeed: 7f,
                    activityRadius: 120f, hasActivityConstraint: true,
                    perceptionRange: 45f, visionConeAngle: 90f,
                    centralVision: 30f, peripheralVision: 45f, peripheralRecog: 0.7f,
                    attackRange: 20f, attackCooldown: 1f, attackDamage: 20f,
                    idleBehavior: IdleBehavior.Patrol, wanderRadius: 5f,
                    canFlee: false, fleeThreshold: 0f,
                    canAllyAlert: true, allyAlertRange: 30f,
                    soundTags: new[] { SoundTag.Footstep, SoundTag.Combat, SoundTag.Gunshot }),

                ("AIAgent_Animal", "中立动物", FactionType.Neutral,
                    idleSpeed: 3f, combatSpeed: 6f, fleeSpeed: 8f,
                    activityRadius: 60f, hasActivityConstraint: true,
                    perceptionRange: 25f, visionConeAngle: 180f,
                    centralVision: 45f, peripheralVision: 90f, peripheralRecog: 0.4f,
                    attackRange: 1.5f, attackCooldown: 1.5f, attackDamage: 5f,
                    idleBehavior: IdleBehavior.Wander, wanderRadius: 15f,
                    canFlee: true, fleeThreshold: 0.6f,
                    canAllyAlert: false, allyAlertRange: 0f,
                    soundTags: new[] { SoundTag.Footstep, SoundTag.Combat, SoundTag.Gunshot }),

                ("AIAgent_AIBot", "AI机器人", FactionType.AIBot,
                    idleSpeed: 0f, combatSpeed: 8f, fleeSpeed: 10f,
                    activityRadius: 0f, hasActivityConstraint: false,
                    perceptionRange: 50f, visionConeAngle: 360f,
                    centralVision: 360f, peripheralVision: 360f, peripheralRecog: 1f,
                    attackRange: 15f, attackCooldown: 0.5f, attackDamage: 0f, // 伤害由 AIBotCombat 武器决定
                    idleBehavior: IdleBehavior.StandBy, wanderRadius: 0f,
                    canFlee: false, fleeThreshold: 0f,
                    canAllyAlert: false, allyAlertRange: 0f,
                    soundTags: new[] { SoundTag.Footstep, SoundTag.Combat, SoundTag.Gunshot, SoundTag.Building, SoundTag.Mechanical }),
            };

            foreach (var spec in specs)
            {
                var asset = ScriptableObject.CreateInstance<AIAgentData>();
                asset.displayName = spec.Item2;
                asset.factionType = spec.Item3;
                asset.idleSpeed = spec.idleSpeed;
                asset.combatSpeed = spec.combatSpeed;
                asset.fleeSpeed = spec.fleeSpeed;
                asset.activityRadius = spec.activityRadius;
                asset.hasActivityConstraint = spec.hasActivityConstraint;
                asset.perceptionRange = spec.perceptionRange;
                asset.visionConeAngle = spec.visionConeAngle;
                asset.centralVisionAngle = spec.centralVision;
                asset.peripheralVisionAngle = spec.peripheralVision;
                asset.peripheralRecognitionChance = spec.peripheralRecog;
                asset.attackRange = spec.attackRange;
                asset.attackCooldown = spec.attackCooldown;
                asset.attackDamage = spec.attackDamage;
                asset.idleBehavior = spec.idleBehavior;
                asset.wanderRadius = spec.wanderRadius;
                asset.canFlee = spec.canFlee;
                asset.fleeThreshold = spec.fleeThreshold;
                asset.canAllyAlert = spec.canAllyAlert;
                asset.allyAlertRange = spec.allyAlertRange;
                asset.reactsToSoundTags = new List<SoundTag>(spec.soundTags);
                asset.obstacleMask = LayerMask.GetMask("Default", "Building");

                AssetDatabase.CreateAsset(asset, OUTPUT_PATH + spec.Item1 + ".asset");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreateAIAgentData] {specs.Length} 个 AIAgentData SO 创建完成 → " + OUTPUT_PATH);
        }
    }
}
