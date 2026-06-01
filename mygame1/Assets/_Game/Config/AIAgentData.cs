using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Core;

namespace _Game.Config
{
    public enum IdleBehavior
    {
        Wander,        // 随机游荡
        StandBy,       // 站定待命
        Patrol,        // 按 patrolRoute 巡逻
        FollowEntity   // 跟随某实体
    }

    /// <summary>
    /// AI Agent 配置 ScriptableObject — 驱动所有 NPC/僵尸/AIBot 行为
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI Agent Data", fileName = "AIAgent_")]
    public class AIAgentData : ScriptableObject
    {
        [Header("身份")]
        public FactionType factionType;
        public string displayName = "未命名";

        [Header("移动")]
        public float idleSpeed = 2f;
        public float combatSpeed = 4f;
        public float fleeSpeed = 6f;
        public float angularSpeed = 120f;

        [Header("活动范围")]
        public bool hasActivityConstraint = false;
        public float activityRadius = 50f;

        [Header("感知")]
        public float perceptionRange = 30f;
        public float perceptionTickRate = 0.5f;
        [Range(0, 360)] public float visionConeAngle = 120f;
        [Range(0, 90)] public float centralVisionAngle = 30f;
        [Range(0, 90)] public float peripheralVisionAngle = 60f;
        [Range(0, 1)] public float peripheralRecognitionChance = 0.5f;
        public float motionThreshold = 1.5f;
        public LayerMask obstacleMask;
        public List<SoundTag> reactsToSoundTags = new();

        [Header("攻击")]
        public float attackRange = 1.5f;
        public float attackCooldown = 1f;
        public float attackDamage = 10f;
        public float targetReassessInterval = 2f;
        [Range(1f, 3f)] public float targetSwitchThreshold = 1.3f;

        [Header("行为")]
        public IdleBehavior idleBehavior = IdleBehavior.Wander;
        public float wanderRadius = 10f;
        public float wanderInterval = 5f;
        public List<Vector3> patrolRoute = new();
        public float investigateTime = 2f;
        public float targetLostTimeout = 5f;

        [Header("逃跑")]
        public bool canFlee = true;
        [Range(0, 1)] public float fleeThreshold = 0.3f;
        [Range(0, 1)] public float fleeRecoveryThreshold = 0.5f;
        public float fleeDistance = 30f;
        public float safeDistance = 40f;

        [Header("阵营扩散")]
        public bool canAllyAlert = false;
        public float allyAlertRange = 15f;

        [Header("特殊")]
        public bool canOpenDoors = false;
        public bool canHordeSpread = false;
        public float hordeSpreadRange = 15f;
    }
}
