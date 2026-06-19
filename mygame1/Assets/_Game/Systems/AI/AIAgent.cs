using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Threat;
using UnityTime = UnityEngine.Time;
using Random = UnityEngine.Random;

namespace _Game.Systems.AI
{
    public enum AIState
    {
        Idle,
        Investigate,
        Combat,
        Flee,
        Return
    }

    public enum VisionLevel
    {
        Blind,              // 看不见
        PeripheralMotion,   // 余光，仅检测移动目标
        Peripheral,         // 周边视野，概率识别
        Central             // 中心视野，100% 识别
    }

    /// <summary>
    /// AIAgent 抽象基类 — 所有 NPC/僵尸/AIBot 的通用寻路+感知+战斗骨架。
    /// 子类只需覆写 CanSee/DoAttack/GetHomePosition。
    /// </summary>
    public abstract class AIAgent : MonoBehaviour
    {
        [SerializeField] protected AIAgentData _data;
        public AIAgentData Data => _data;

        // ═══ 组件引用 ═══
        protected NavMeshAgent _agent;
        protected FactionComponent _factionComp;

        // ═══ 状态机 ═══
        protected AIState _currentState = AIState.Idle;
        public AIState CurrentState => _currentState;
        AIState? _pendingTransition;
        bool _transitionLock;

        // ═══ 目标 ═══
        protected int? _currentTargetId;
        protected Transform _currentTarget;
        protected float _lostSightTimer;
        protected float _lastAttackTime;

        // ═══ 导航 ═══
        protected Vector3 _activityCenter;
        protected Vector3? _currentDestination;

        // ═══ Idle 恢复 ═══
        protected Vector3? _resumeWanderTarget;
        protected int _resumePatrolIndex = -1;
        int _currentPatrolIndex;

        // ═══ 导航卡住 ═══
        float _stuckCheckTimer;
        int _stuckCount;
        Vector3 _lastStuckPos;

        // ═══ 感知 ═══
        float _perceptionTimer;
        static int _tickGroupCounter;
        int _tickGroup;
        const int TICK_GROUP_COUNT = 10;

        // ═══ 状态计时器 ═══
        float _investigateTimer;
        float _targetReassessTimer;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _factionComp = GetComponent<FactionComponent>();
            _tickGroup = _tickGroupCounter++ % TICK_GROUP_COUNT;
        }

        protected virtual void OnEnable()
        {
            if (_agent == null)
            {
                Debug.LogError($"[AIAgent] {name} 缺少 NavMeshAgent！AI 已禁用。", this);
                enabled = false;
                return;
            }

            EventBus.Subscribe<NoiseEvent>(OnHeardSound);
            EventBus.Subscribe<AllyAlertEvent>(OnAllyAlert);

            _activityCenter = transform.position;
            TransitionTo(AIState.Idle);
        }

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe<NoiseEvent>(OnHeardSound);
            EventBus.Unsubscribe<AllyAlertEvent>(OnAllyAlert);
        }

        IEnumerator Start()
        {
            yield return null; // 等一帧让 FactionComponent.OnEnable 先跑完

            if (_factionComp == null)
            {
                Debug.LogError($"[AIAgent] {name} 缺少 FactionComponent！AI 已禁用。", this);
                enabled = false;
                yield break;
            }

            if (ThreatSystem.Instance == null)
            {
                Debug.LogError($"[AIAgent] {name} ThreatSystem 未就绪！AI 已禁用。", this);
                enabled = false;
                yield break;
            }

            if (!ThreatSystem.Instance.IsEntityRegistered(gameObject.GetInstanceID()))
            {
                yield return null;
                if (!ThreatSystem.Instance.IsEntityRegistered(gameObject.GetInstanceID()))
                {
                    Debug.LogError($"[AIAgent] {name} 注册失败，AI 已禁用。", this);
                    enabled = false;
                }
            }
        }

        // ═══ 主循环 ═══
        void Update()
        {
            if (_data == null) return;

            if (UnityTime.frameCount % TICK_GROUP_COUNT == _tickGroup)
                PerceptionTick();

            StateTick();
        }

        void LateUpdate()
        {
            if (_pendingTransition.HasValue && !_transitionLock)
            {
                ExecuteTransition(_pendingTransition.Value);
                _pendingTransition = null;
            }
        }

        // ═══ 状态切换 ═══
        public void TransitionTo(AIState newState)
        {
            if (_pendingTransition.HasValue)
                _pendingTransition = PriorityHigher(_pendingTransition.Value, newState);
            else
                _pendingTransition = newState;
        }

        static AIState PriorityHigher(AIState a, AIState b)
        {
            int p(AIState s) => s switch
            {
                AIState.Flee => 4,
                AIState.Return => 3,
                AIState.Combat => 2,
                AIState.Investigate => 1,
                _ => 0
            };
            return p(a) >= p(b) ? a : b;
        }

        void ExecuteTransition(AIState newState)
        {
            if (_currentState == newState) return;
            _transitionLock = true;

            OnStateExit(_currentState);
            var oldState = _currentState;
            _currentState = newState;
            OnStateEnter(newState);

            // Combat 进入时发射 AllyAlert
            if (newState == AIState.Combat && _data.canAllyAlert)
            {
                EventBus.Publish(new AllyAlertEvent(
                    gameObject.GetInstanceID(),
                    transform.position,
                    _data.allyAlertRange,
                    _data.factionType
                ));
            }
        }

        // ═══ 状态 Tick ═══
        void StateTick()
        {
            _transitionLock = false;

            // 全局检查
            if (DistanceToActivityCenter() > _data.activityRadius && _data.hasActivityConstraint)
                TransitionTo(AIState.Return);

            switch (_currentState)
            {
                case AIState.Idle:        IdleTick();        break;
                case AIState.Investigate: InvestigateTick(); break;
                case AIState.Combat:      CombatTick();      break;
                case AIState.Flee:        FleeTick();        break;
                case AIState.Return:      ReturnTick();      break;
            }
        }

        float DistanceToActivityCenter()
            => Vector3.Distance(transform.position, _activityCenter);

        // ═══ Idle ═══
        protected virtual void IdleTick()
        {
            // 威胁检查
            if (ThreatSystem.Instance != null && ThreatSystem.Instance.HasThreat(gameObject.GetInstanceID()))
            {
                int? realTarget = ThreatSystem.Instance.GetTopRealTarget(gameObject.GetInstanceID());
                if (realTarget.HasValue)
                {
                    var t = InstanceRegistry.GetTransform(realTarget.Value);
                    if (t != null && CanSee(t))
                    {
                        TransitionTo(AIState.Combat);
                        return;
                    }
                }
                TransitionTo(AIState.Investigate);
                return;
            }

            // Idle 行为
            switch (_data.idleBehavior)
            {
                case IdleBehavior.Wander:
                    WanderUpdate();
                    break;
                case IdleBehavior.StandBy:
                    _agent.ResetPath();
                    break;
                case IdleBehavior.Patrol:
                    PatrolUpdate();
                    break;
                case IdleBehavior.FollowEntity:
                    // 子类覆写 GetFollowTarget
                    break;
            }
        }

        // ═══ Wander ═══
        float _wanderTimer;

        void WanderUpdate()
        {
            _wanderTimer -= UnityTime.deltaTime;
            if (_wanderTimer <= 0f || !_agent.hasPath || _agent.remainingDistance < 0.5f)
            {
                Vector3 randomDir = UnityEngine.Random.insideUnitSphere * _data.wanderRadius;
                randomDir.y = 0;
                Vector3 target = _activityCenter + randomDir;

                if (NavMesh.SamplePosition(target, out var hit, _data.wanderRadius, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                    _agent.speed = _data.idleSpeed;
                    _wanderTimer = _data.wanderInterval;
                }
            }
        }

        // ═══ Patrol ═══
        void PatrolUpdate()
        {
            if (_data.patrolRoute == null || _data.patrolRoute.Count == 0) return;

            if (!_agent.hasPath || _agent.remainingDistance < 0.5f)
            {
                _currentPatrolIndex = (_currentPatrolIndex + 1) % _data.patrolRoute.Count;
                _agent.SetDestination(_data.patrolRoute[_currentPatrolIndex]);
                _agent.speed = _data.idleSpeed;
            }
        }

        // ═══ Investigate ═══
        void InvestigateTick()
        {
            CheckStuck();

            int? topTarget = ThreatSystem.Instance?.GetTopTarget(gameObject.GetInstanceID());
            if (topTarget == null || !ThreatSystem.Instance.HasThreat(gameObject.GetInstanceID()))
            {
                TransitionTo(AIState.Idle);
                return;
            }

            // 门控：直接威胁类型 + 距离近 → 切 Combat
            int? realTarget = ThreatSystem.Instance.GetTopRealTarget(gameObject.GetInstanceID());
            if (realTarget.HasValue)
            {
                var entry = ThreatSystem.Instance.GetAllThreats(gameObject.GetInstanceID())
                    .Find(e => e.targetInstanceId == realTarget.Value);
                bool isDirectThreat = entry.type == ThreatType.Damage
                    || entry.type == ThreatType.Visual
                    || entry.type == ThreatType.Aggression;

                if (isDirectThreat)
                {
                    var t = InstanceRegistry.GetTransform(realTarget.Value);
                    if (t != null)
                    {
                        float dist = Vector3.Distance(transform.position, t.position);
                        if (CanSee(t) && dist <= _data.attackRange * 2f)
                        {
                            TransitionTo(AIState.Combat);
                            return;
                        }
                    }
                }
            }

            // 走向威胁来源位置
            Vector3? lastKnown = ThreatSystem.Instance.GetLastKnownPosition(
                gameObject.GetInstanceID(), topTarget.Value);
            Vector3 dest = lastKnown ?? transform.position;

            _agent.SetDestination(dest);
            _agent.speed = _data.idleSpeed;

            // 到达后观察
            if (_agent.remainingDistance < 1f)
            {
                _investigateTimer += UnityTime.deltaTime;
                if (_investigateTimer >= _data.investigateTime)
                {
                    TransitionTo(AIState.Idle);
                }
            }
            else
            {
                _investigateTimer = 0f;
            }
        }

        // ═══ Combat ═══
        void CombatTick()
        {
            if (_currentTarget == null)
            {
                TransitionTo(AIState.Idle);
                return;
            }

            CheckStuck();

            // 定期重评估目标
            _targetReassessTimer += UnityTime.deltaTime;
            if (_targetReassessTimer >= _data.targetReassessInterval)
            {
                _targetReassessTimer = 0f;
                ReassessTarget();
            }

            // 丢失视线计时
            if (!CanSee(_currentTarget))
                _lostSightTimer += UnityTime.deltaTime;
            else
                _lostSightTimer = 0f;

            // 丢失太久 → Investigate 或 Idle
            if (_lostSightTimer > _data.targetLostTimeout)
            {
                if (ThreatSystem.Instance.HasThreat(gameObject.GetInstanceID()))
                    TransitionTo(AIState.Investigate);
                else
                    TransitionTo(AIState.Idle);
                return;
            }

            float dist = Vector3.Distance(transform.position, _currentTarget.position);

            if (dist > _data.attackRange)
            {
                // 追击
                _agent.SetDestination(_currentTarget.position);
                _agent.speed = _data.combatSpeed;
            }
            else
            {
                _agent.ResetPath();
                FaceTarget(_currentTarget);

                if (UnityTime.time - _lastAttackTime >= _data.attackCooldown)
                {
                    DoAttack(_currentTarget.gameObject);
                    _lastAttackTime = UnityTime.time;
                }
            }
        }

        void ReassessTarget()
        {
            int? newTopId = ThreatSystem.Instance.GetTopRealTarget(gameObject.GetInstanceID());
            if (newTopId == null || newTopId == _currentTargetId) return;

            float currentThreat = ThreatSystem.Instance.GetThreatValue(
                gameObject.GetInstanceID(), _currentTargetId ?? 0);
            float newThreat = ThreatSystem.Instance.GetThreatValue(
                gameObject.GetInstanceID(), newTopId.Value);

            if (newThreat <= currentThreat * _data.targetSwitchThreshold) return;

            // 可达性检查
            Transform newTarget = InstanceRegistry.GetTransform(newTopId.Value);
            if (newTarget == null) return;

            NavMeshPath path = new NavMeshPath();
            if (_agent.CalculatePath(newTarget.position, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                float pathLen = GetPathLength(path);
                float straightDist = Vector3.Distance(transform.position, newTarget.position);
                if (pathLen > straightDist * 3f) return;
            }
            else return;

            // 切换
            _currentTargetId = newTopId.Value;
            _currentTarget = newTarget;
            _lostSightTimer = 0f;
        }

        static float GetPathLength(NavMeshPath path)
        {
            float len = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return len;
        }

        void FaceTarget(Transform target)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(dir), _data.angularSpeed * UnityTime.deltaTime);
        }

        // ═══ Flee ═══
        void FleeTick()
        {
            if (!ThreatSystem.Instance.HasThreat(gameObject.GetInstanceID()) &&
                DistanceToActivityCenter() <= _data.activityRadius)
            {
                TransitionTo(AIState.Idle);
                return;
            }

            // 逃离最大威胁方向
            int? topId = ThreatSystem.Instance.GetTopTarget(gameObject.GetInstanceID());
            Vector3 fleeDir = transform.forward;
            if (topId.HasValue)
            {
                var threatPos = ThreatSystem.Instance.GetLastKnownPosition(
                    gameObject.GetInstanceID(), topId.Value);
                if (threatPos.HasValue)
                {
                    fleeDir = (transform.position - threatPos.Value).normalized;
                    fleeDir.y = 0;
                }
            }

            Vector3 fleeTarget = transform.position + fleeDir * _data.fleeDistance;
            if (NavMesh.SamplePosition(fleeTarget, out var hit, _data.fleeDistance, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
                _agent.speed = _data.fleeSpeed;
            }
        }

        // ═══ Return ═══
        void ReturnTick()
        {
            _agent.SetDestination(_activityCenter);
            _agent.speed = _data.idleSpeed;

            if (_agent.remainingDistance < 1f)
            {
                TransitionTo(AIState.Idle);
                return;
            }

            // 途中遇到敌人 → 先打
            if (ThreatSystem.Instance != null && ThreatSystem.Instance.HasThreat(gameObject.GetInstanceID()))
            {
                int? realTarget = ThreatSystem.Instance.GetTopRealTarget(gameObject.GetInstanceID());
                if (realTarget.HasValue)
                {
                    var t = InstanceRegistry.GetTransform(realTarget.Value);
                    if (t != null && CanSee(t))
                    {
                        TransitionTo(AIState.Combat);
                    }
                }
            }
        }

        // ═══ 导航卡住恢复 ═══
        void CheckStuck()
        {
            _stuckCheckTimer += UnityTime.deltaTime;
            if (_stuckCheckTimer < 2f) return;
            _stuckCheckTimer = 0f;

            if (_agent.velocity.magnitude < 0.1f && _agent.hasPath && _agent.remainingDistance > 1f)
            {
                _stuckCount++;

                if (_stuckCount == 1)
                {
                    // 尝试绕路
                    Vector3 detour = FindDetour();
                    if (detour != Vector3.zero)
                        _agent.SetDestination(detour);
                }
                else if (_stuckCount == 2)
                {
                    _agent.ResetPath();
                    transform.position -= transform.forward * 1f;
                    if (_currentDestination.HasValue)
                        _agent.SetDestination(_currentDestination.Value);
                }
                else
                {
                    _agent.ResetPath();
                    _stuckCount = 0;
                    TransitionTo(AIState.Idle);
                }
            }
            else if (_agent.velocity.magnitude > 0.5f)
            {
                _stuckCount = 0;
            }
        }

        Vector3 FindDetour()
        {
            for (int angle = 45; angle <= 135; angle += 45)
            {
                Vector3 left = Quaternion.Euler(0, -angle, 0) * transform.forward * 3f;
                Vector3 right = Quaternion.Euler(0, angle, 0) * transform.forward * 3f;

                if (NavMesh.SamplePosition(transform.position + left, out var hitL, 3f, NavMesh.AllAreas))
                {
                    if (!Physics.Linecast(transform.position, hitL.position, _data.obstacleMask))
                        return hitL.position;
                }
                if (NavMesh.SamplePosition(transform.position + right, out var hitR, 3f, NavMesh.AllAreas))
                {
                    if (!Physics.Linecast(transform.position, hitR.position, _data.obstacleMask))
                        return hitR.position;
                }
            }
            return Vector3.zero;
        }

        // ═══ 感知 ═══
        static bool _perceptionDebugOnce;
        void PerceptionTick()
        {
            if (_data == null)
            {
                if (!_perceptionDebugOnce) { _perceptionDebugOnce = true; Debug.LogError($"[AIAgent] {name} _data 为 null！PerceptionTick 已禁用"); }
                return;
            }
            if (ThreatSystem.Instance == null)
            {
                if (!_perceptionDebugOnce) { _perceptionDebugOnce = true; Debug.LogError($"[AIAgent] {name} ThreatSystem.Instance 为 null！"); }
                return;
            }

            int myId = gameObject.GetInstanceID();
            Vector3 eyes = GetEyesPosition();

            Collider[] hits = new Collider[16];
            // 感知扫描用 AllLayers（障碍物过滤由 CanSee 的 Linecast 负责）
            int count = Physics.OverlapSphereNonAlloc(transform.position, _data.perceptionRange,
                hits, Physics.AllLayers);

            for (int i = 0; i < count; i++)
            {
                var otherFaction = hits[i].GetComponent<FactionComponent>();
                if (otherFaction == null) continue;
                if (otherFaction.gameObject.GetInstanceID() == myId) continue;

                // 只检测敌对阵营
                if (FactionSystem.Instance == null) continue;
                bool isHostile = FactionSystem.Instance.IsHostile(_data.factionType, otherFaction.Faction);
                if (!isHostile) continue;

                if (CanSee(otherFaction.transform))
                {
                    ThreatSystem.Instance.AddThreat(myId,
                        otherFaction.gameObject.GetInstanceID(),
                        50f, ThreatType.Visual,
                        otherFaction.transform.position);
                }
            }
        }

        protected virtual Vector3 GetEyesPosition()
            => transform.position + Vector3.up * 1.5f;

        /// <summary>
        /// 三档视觉检测。子类可覆写。
        /// </summary>
        protected virtual bool CanSee(Transform target)
        {
            if (target == null) return false;

            Vector3 eyes = GetEyesPosition();
            Vector3 targetEyes = target.position + Vector3.up * 1.5f;
            float dist = Vector3.Distance(eyes, targetEyes);

            if (dist > _data.perceptionRange) return false;

            // 视线遮蔽
            if (_data.obstacleMask.value != 0 &&
                Physics.Linecast(eyes, targetEyes, _data.obstacleMask))
                return false;

            // 三档视觉
            Vector3 dirToTarget = (targetEyes - eyes).normalized;
            float angle = Vector3.Angle(transform.forward, dirToTarget);

            if (angle <= _data.centralVisionAngle)
                return true;  // 中心视野 100%

            if (angle <= _data.peripheralVisionAngle)
                return UnityEngine.Random.value < _data.peripheralRecognitionChance;

            if (angle <= _data.visionConeAngle * 0.5f)
            {
                var rb = target.GetComponent<Rigidbody>();
                return rb != null && rb.velocity.magnitude > _data.motionThreshold;
            }

            return false;
        }

        // ═══ 声音反应 ═══
        void OnHeardSound(NoiseEvent e)
        {
            float dist = Vector3.Distance(transform.position, e.Position);
            if (dist > e.Radius) return;
            if (_data.reactsToSoundTags == null || !_data.reactsToSoundTags.Contains(e.Tag)) return;

            ThreatSystem.Instance.AddThreat(
                gameObject.GetInstanceID(),
                e.SourceObject != null ? e.SourceObject.GetInstanceID() : (int?)null,
                e.Radius,  // 半径作为威胁强度（DecibelSystem已做距离衰减）
                ThreatType.Sound,
                e.Position
            );
        }

        // ═══ 友军预警 ═══
        void OnAllyAlert(AllyAlertEvent e)
        {
            if (_data.factionType != e.Faction) return;
            if (Vector3.Distance(transform.position, e.Position) > e.Radius) return;

            ThreatSystem.Instance.AddThreat(
                gameObject.GetInstanceID(),
                null,
                20f,
                ThreatType.AllyDamage,
                e.Position
            );
        }

        // ═══ 保存 Idle 进度（被打断时） ═══
        protected virtual void OnStateExit(AIState oldState)
        {
            if (oldState == AIState.Idle && _data.idleBehavior == IdleBehavior.Wander)
                _resumeWanderTarget = _agent.destination;
            else if (oldState == AIState.Idle && _data.idleBehavior == IdleBehavior.Patrol)
                _resumePatrolIndex = _currentPatrolIndex;
        }

        protected virtual void OnStateEnter(AIState newState)
        {
            if (newState == AIState.Idle)
            {
                _agent.speed = _data.idleSpeed;

                if (_data.idleBehavior == IdleBehavior.Wander && _resumeWanderTarget.HasValue)
                {
                    _agent.SetDestination(_resumeWanderTarget.Value);
                    _resumeWanderTarget = null;
                }
                else if (_data.idleBehavior == IdleBehavior.Patrol && _resumePatrolIndex >= 0)
                {
                    _currentPatrolIndex = _resumePatrolIndex;
                    _resumePatrolIndex = -1;
                }
            }
            else if (newState == AIState.Combat)
            {
                int? topId = ThreatSystem.Instance.GetTopRealTarget(gameObject.GetInstanceID());
                if (topId.HasValue)
                {
                    _currentTargetId = topId.Value;
                    _currentTarget = InstanceRegistry.GetTransform(topId.Value);
                    _lostSightTimer = 0f;
                    _agent.speed = _data.combatSpeed;
                }
            }
            else if (newState == AIState.Investigate)
            {
                _investigateTimer = 0f;
                _agent.speed = _data.idleSpeed;
            }
            else if (newState == AIState.Flee)
            {
                _agent.speed = _data.fleeSpeed;
            }
        }

        // ═══ 虚方法 — 子类覆写 ═══
        /// <summary>攻击行为 — 子类必须覆写</summary>
        protected abstract void DoAttack(GameObject target);

        /// <summary>家园位置 — 有家园的 NPC 覆写</summary>
        protected virtual Vector3? GetHomePosition() => null;

        // ═══ 受伤 — 子类在 TakeDamage 后调此方法 ═══
        public void OnDamaged(int attackerId, float damage, Vector3? position = null)
        {
            EventBus.Publish(new ThreatReportEvent(
                attackerId,
                gameObject.GetInstanceID(),
                damage,
                position
            ));
        }

        // ═══ Gizmos ═══
#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            if (_data == null) return;

            Vector3 pos = transform.position;
            Vector3 eyes = GetEyesPosition();
            Vector3 forward = transform.forward * _data.perceptionRange;

            // 活动范围圈
            UnityEditor.Handles.color = new Color(0, 1, 1, 0.1f);
            UnityEditor.Handles.DrawWireDisc(_activityCenter, Vector3.up, _data.activityRadius);

            // 视觉锥边界
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            float halfAngle = _data.visionConeAngle * 0.5f;
            Gizmos.DrawLine(eyes, eyes + Quaternion.Euler(0, -halfAngle, 0) * forward);
            Gizmos.DrawLine(eyes, eyes + Quaternion.Euler(0, halfAngle, 0) * forward);

            // 中心视野
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            float centralHalf = _data.centralVisionAngle * 0.5f;
            Gizmos.DrawLine(eyes, eyes + Quaternion.Euler(0, -centralHalf, 0) * forward);
            Gizmos.DrawLine(eyes, eyes + Quaternion.Euler(0, centralHalf, 0) * forward);

            // 当前目标
            if (_currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(eyes, _currentTarget.position);
                Gizmos.DrawWireSphere(_currentTarget.position, 0.3f);
            }

            // 导航路径
            if (_agent != null && _agent.hasPath && _agent.path.corners.Length > 1)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < _agent.path.corners.Length - 1; i++)
                    Gizmos.DrawLine(_agent.path.corners[i], _agent.path.corners[i + 1]);
            }

            // 攻击范围
            Gizmos.color = new Color(1, 0, 0, 0.15f);
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, _data.attackRange);

            // 头顶标签
            string label = $"{_data.displayName}\n[{_currentState}]";
            int threatCount = ThreatSystem.Instance?.GetAllThreats(gameObject.GetInstanceID()).Count ?? 0;
            UnityEditor.Handles.Label(pos + Vector3.up * 2.5f,
                $"{label}\nThreats: {threatCount}");
        }
#endif
    }
}
