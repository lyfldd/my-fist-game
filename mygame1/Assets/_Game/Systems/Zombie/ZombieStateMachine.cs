using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Core;
using _Game.Systems.AI;
using _Game.Systems.Combat;
using _Game.Systems.Threat;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// 僵尸 AI — 继承 AIAgent 基类，使用 ThreatSystem + 感知 + FSM。
    /// 替代旧 ZombieState 单例模式。
    /// </summary>
    [RequireComponent(typeof(FactionComponent))]
    public class ZombieStateMachine : AIAgent
    {
        // 公共访问器（ChunkManager / DamageableZombie 兼容）
        public NavMeshAgent Agent => _agent;

        DamageableZombie _damageable;
        bool _isDead;

        public bool IsDead => _isDead;

        // ═══ 从 ZombieData 初始化 ═══
        public void ApplyFromZombieData(ZombieData data)
        {
            if (data == null) return;

            _damageable = GetComponent<DamageableZombie>();

            // 运行时创建 AIAgentData
            if (_data == null)
            {
                _data = ScriptableObject.CreateInstance<AIAgentData>();
                _data.name = data.zombieName;
            }

            _data.displayName = data.zombieName;
            _data.factionType = FactionType.Zombie;
            _data.idleSpeed = data.moveSpeed * 0.5f;
            _data.combatSpeed = data.moveSpeed;
            _data.fleeSpeed = data.moveSpeed;
            _data.activityRadius = 100f;
            _data.hasActivityConstraint = false;
            _data.perceptionRange = data.detectRange;
            _data.visionConeAngle = data.visionAngle;
            _data.centralVisionAngle = data.visionAngle;
            _data.peripheralVisionAngle = 0f;
            _data.peripheralRecognitionChance = 0f;
            _data.motionThreshold = 0.5f;
            _data.attackRange = data.attackRange;
            _data.attackCooldown = data.attackCooldown;
            _data.attackDamage = data.attackDamage;
            _data.idleBehavior = IdleBehavior.Wander;
            _data.wanderRadius = 10f;
            _data.wanderInterval = 4f;
            _data.investigateTime = 3f;
            _data.canFlee = false;
            _data.fleeThreshold = 0.3f;
            _data.fleeDistance = 10f;
            _data.canAllyAlert = true;
            _data.allyAlertRange = 15f;
            _data.targetLostTimeout = 5f;
            _data.targetReassessInterval = 2f;
            _data.targetSwitchThreshold = 1.5f;
            _data.angularSpeed = 360f;
            _data.obstacleMask = LayerMask.GetMask("Default", "Building");
            _data.reactsToSoundTags = new List<SoundTag>
                { SoundTag.Footstep, SoundTag.Combat, SoundTag.Gunshot, SoundTag.Building, SoundTag.Impact };

            if (_agent != null)
            {
                _agent.speed = _data.idleSpeed;
                _agent.stoppingDistance = _data.attackRange * 0.8f;
                _agent.acceleration = 8f;
                _agent.angularSpeed = 360f;
            }
        }

        // ═══ AIAgent 抽象方法覆写 ═══

        protected override void DoAttack(GameObject target)
        {
            if (_isDead) return;
            if (_data == null) return;

            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(_data.attackDamage);
                EventBus.Publish(new ThreatReportEvent(
                    gameObject.GetInstanceID(),
                    target.GetInstanceID(),
                    _data.attackDamage));
            }
        }

        protected override Vector3? GetHomePosition() => null;

        // ═══ 简单视觉（僵尸不使用三档视觉） ═══
        protected override bool CanSee(Transform target)
        {
            if (target == null || _data == null) return false;

            Vector3 eyes = GetEyesPosition();
            Vector3 targetEyes = target.position + Vector3.up * 1.5f;
            float dist = Vector3.Distance(eyes, targetEyes);

            if (dist > _data.perceptionRange) return false;

            // 视线遮蔽
            if (_data.obstacleMask.value != 0 &&
                Physics.Linecast(eyes, targetEyes, _data.obstacleMask))
                return false;

            // 视觉锥
            if (_data.visionConeAngle > 0f && _data.visionConeAngle < 360f)
            {
                Vector3 toTarget = (targetEyes - eyes).normalized;
                toTarget.y = 0f;
                Vector3 forward = transform.forward;
                forward.y = 0f;
                float halfAngle = _data.visionConeAngle * 0.5f;
                float angle = Vector3.Angle(forward, toTarget);
                if (angle > halfAngle) return false;
            }

            return true;
        }

        // ═══ 死亡 ═══
        public void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // enabled=false 触发 AIAgent.OnDisable() 自动取消事件订阅
            enabled = false;

            if (_agent != null && _agent.enabled)
            {
                _agent.isStopped = true;
                _agent.enabled = false;
            }

            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
        }

        // ═══ 导航辅助（保持兼容） ═══
        public bool SampleRandomPoint(float radius, out Vector3 result)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 random = transform.position + Random.insideUnitSphere * radius;
                random.y = transform.position.y;
                if (NavMesh.SamplePosition(random, out NavMeshHit hit, radius, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }
            result = transform.position;
            return false;
        }

        protected override Vector3 GetEyesPosition()
            => transform.position + Vector3.up * 1.5f;

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            if (_data == null) return;
            base.OnDrawGizmosSelected();
        }
#endif
    }
}
