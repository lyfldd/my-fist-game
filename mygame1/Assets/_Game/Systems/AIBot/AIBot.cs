using UnityEngine;
using UnityEngine.AI;
using _Game.Core;
using _Game.Systems.Character;
using _Game.Systems.Combat;

namespace _Game.Systems.AIBot
{
    public enum AIBotCommand { Follow, Guard, Patrol, Pilot }
    public enum EnergyMode { Battery, Uranium }

    /// <summary>
    /// AI机器人主控制器。状态机 + 双能量 + 血量 + 防自杀协调。
    /// 由 AIBotBuildable 在放置后初始化。
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AIBot : MonoBehaviour, IDamageable
    {
        [Header("血量")]
        [SerializeField] private float maxHP = 500f;
        [SerializeField] private float currentHP = 500f;

        [Header("双能量")]
        [SerializeField] private float batteryMax = 200f;
        [SerializeField] private float batteryCurrent = 200f;
        [SerializeField] private float uraniumMax = 200f;
        [SerializeField] private float uraniumCurrent;

        [Header("当前状态")]
        [SerializeField] private AIBotCommand currentCommand = AIBotCommand.Follow;
        [SerializeField] private EnergyMode energyMode = EnergyMode.Battery;
        [SerializeField] private bool isDead;

        [Header("跟随设置")]
        public float followDistance = 3f;
        public float followDistanceMin = 1f;
        public float followDistanceMax = 10f;

        [Header("驻守设置")]
        public float guardChaseRange = 8f;
        public float guardAutoRecallDistance = 100f;
        public bool guardAutoRecallEnabled = true;
        public Vector3 guardPosition;

        [Header("巡逻设置")]
        public float patrolRadius = 30f;
        public float patrolRadiusMin = 10f;
        public float patrolRadiusMax = 80f;
        public float patrolAutoRecallDistance = 200f;
        public bool patrolAutoRecallEnabled = true;
        public bool patrolAroundPlayer = true;
        public Vector3 patrolCenterPoint;

        [Header("移动")]
        public float moveSpeed = 5f;

        // 能量消耗速率 (/h)
        public const float ENERGY_GUARD_PER_H = 3f;
        public const float ENERGY_FOLLOW_PER_H = 6f;
        public const float ENERGY_PATROL_PER_H = 10f;
        public const float ENERGY_LASER_PER_SHOT = 2f;
        public const float ENERGY_RIGHTARM_PER_SHOT = 1f;
        public const float ENERGY_LEFTARM_PER_SWING = 0.5f;

        // 太阳能充电速率 (/h，仅白天)
        public float solarRechargeRate = 120f;

        // 组件引用
        private NavMeshAgent _agent;
        private Transform _player;

        // 传送冷却
        private float _teleportCooldownTimer;
        public const float TELEPORT_COOLDOWN = 30f;

        // 卡住检测
        private Vector3 _lastPosition;
        private float _stuckTimer;
        private const float STUCK_RESET_TIME = 3f;
        private const float STUCK_TELEPORT_TIME = 5f;

        // 巡逻内部状态
        private Vector3 _patrolTarget;
        private float _patrolPauseTimer;
        private bool _patrolPaused;
        private const float PATROL_FIND_INTERVAL = 2f;
        private float _patrolFindTimer;
        private const float PATROL_PAUSE_MIN = 3f;
        private const float PATROL_PAUSE_MAX = 5f;

        // ============================================================
        // 属性
        // ============================================================

        public float HP => currentHP;
        public float MaxHP => maxHP;
        public float HealthPercent => maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
        public bool IsDead => isDead;
        public bool IsLowHP => HealthPercent < 0.3f;
        public bool IsCriticalHP => HealthPercent < 0.1f;
        public bool IsPiloted { get; set; }
        public AIBotCommand CurrentCommand => currentCommand;
        public EnergyMode CurrentEnergyMode => energyMode;

        public float BatteryCurrent => batteryCurrent;
        public float BatteryMax => batteryMax;
        public float BatteryPercent => batteryMax > 0f ? Mathf.Clamp01(batteryCurrent / batteryMax) : 0f;
        public float UraniumCurrent => uraniumCurrent;
        public float UraniumMax => uraniumMax;
        public float UraniumPercent => uraniumMax > 0f ? Mathf.Clamp01(uraniumCurrent / uraniumMax) : 0f;

        public bool IsBatteryEmpty => batteryCurrent <= 0f;
        public bool IsUraniumEmpty => uraniumCurrent <= 0f;
        public bool IsShutdown => IsBatteryEmpty && IsUraniumEmpty;

        public NavMeshAgent Agent => _agent;
        public Transform PlayerTransform => _player;

        // ============================================================
        // Unity 生命周期
        // ============================================================

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = moveSpeed;
            _agent.acceleration = 12f;
            _agent.angularSpeed = 360f;
            _agent.radius = 0.5f;
            _agent.height = 2f;
            _agent.stoppingDistance = followDistance;
            _agent.autoBraking = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            // 自动挂载驾驶组件
            if (GetComponent<AIBotPilot>() == null)
                gameObject.AddComponent<AIBotPilot>();

            currentHP = maxHP;
            batteryCurrent = batteryMax;
            solarRechargeRate = 120f;
        }

        void Start()
        {
            var pc = FindObjectOfType<PlayerCharacter>();
            if (pc != null) _player = pc.transform;

            _lastPosition = transform.position;
            guardPosition = transform.position;

            // 注册为僵尸目标
            if (Zombie.ZombieAwarenessSystem.Instance != null)
                Zombie.ZombieAwarenessSystem.Instance.RegisterAIBot(transform);
        }

        void Update()
        {
            if (IsPiloted) return;
            if (isDead) return;
            if (_agent == null || !_agent.enabled) return;

            // 能量消耗
            ConsumeEnergy();

            // 太阳能充电（白天缓慢恢复电池）
            SolarRecharge();

            // 血量检查（防自杀：切跟随+紧贴，但不阻断移动）
            CheckLowHealthRetreat();

            // 卡住检测
            CheckStuck();

            // 传送冷却计时
            if (_teleportCooldownTimer > 0f)
                _teleportCooldownTimer -= UnityEngine.Time.deltaTime;

            // 执行当前指令
            switch (currentCommand)
            {
                case AIBotCommand.Follow:
                    UpdateFollow();
                    break;
                case AIBotCommand.Guard:
                    UpdateGuard();
                    break;
                case AIBotCommand.Patrol:
                    UpdatePatrol();
                    break;
            }
        }

        // ============================================================
        // 能量系统
        // ============================================================

        void ConsumeEnergy()
        {
            if (IsShutdown) return;

            float ratePerSec = GetActivityRate() / 3600f;

            if (energyMode == EnergyMode.Battery)
            {
                batteryCurrent -= ratePerSec * UnityEngine.Time.deltaTime;
                if (batteryCurrent <= 0f)
                {
                    batteryCurrent = 0f;
                    if (uraniumCurrent > 0f)
                        energyMode = EnergyMode.Uranium;
                }
            }
            else
            {
                uraniumCurrent -= ratePerSec * UnityEngine.Time.deltaTime;
                if (uraniumCurrent <= 0f)
                {
                    uraniumCurrent = 0f;
                    if (batteryCurrent > 0f)
                        energyMode = EnergyMode.Battery;
                }
            }
        }

        private _Game.Systems.Time.TimeManager _cachedTimeManager;
        private _Game.Systems.Weather.WeatherManager _cachedWeatherManager;
        private float _solarDebugTimer;

        // 当前太阳能实际充电速率 (/h)，0 表示未在充电
        public float CurrentSolarRate { get; private set; }
        public bool IsSolarActive => CurrentSolarRate > 0f;

        void SolarRecharge()
        {
            CurrentSolarRate = 0f;
            if (solarRechargeRate <= 0f)
            {
                if (UnityEngine.Time.frameCount % 300 == 0) Debug.LogWarning("[AIBot] 太阳能速率=0，跳过充电");
                return;
            }
            if (batteryCurrent >= batteryMax) return;
            if (IsShutdown) return;

            // 白天检查
            if (_cachedTimeManager == null)
                _cachedTimeManager = FindObjectOfType<_Game.Systems.Time.TimeManager>();
            if (_cachedTimeManager == null)
            {
                if (UnityEngine.Time.frameCount % 300 == 0) Debug.LogWarning("[AIBot] TimeManager未找到");
                return;
            }
            float hour = _cachedTimeManager.CurrentHour;
            if (hour < 6f || hour >= 18f) return;

            // 天气检查
            if (_cachedWeatherManager == null)
                _cachedWeatherManager = FindObjectOfType<_Game.Systems.Weather.WeatherManager>();
            if (_cachedWeatherManager == null)
            {
                if (UnityEngine.Time.frameCount % 300 == 0) Debug.LogWarning("[AIBot] WeatherManager未找到");
                return;
            }
            var weather = _cachedWeatherManager.CurrentWeather;
            float weatherMult;
            switch (weather)
            {
                case _Game.Config.WeatherType.Sunny:  weatherMult = 1f; break;
                case _Game.Config.WeatherType.Cloudy: weatherMult = 0.5f; break;
                default: return; // 雨/暴雨不充电
            }

            float rate = solarRechargeRate * weatherMult;
            float recharge = rate / 3600f * UnityEngine.Time.deltaTime;
            batteryCurrent = Mathf.Min(batteryCurrent + recharge, batteryMax);
            CurrentSolarRate = rate;

            // 每5秒打印一次充电状态
            _solarDebugTimer -= UnityEngine.Time.deltaTime;
            if (_solarDebugTimer <= 0f)
            {
                _solarDebugTimer = 5f;
                Debug.Log($"[AIBot] 太阳能充电中: +{rate:F1}/h, 电池 {batteryCurrent:F0}/{batteryMax:F0}, 天气={weather}");
            }
        }

        float GetActivityRate()
        {
            switch (currentCommand)
            {
                case AIBotCommand.Follow: return ENERGY_FOLLOW_PER_H;
                case AIBotCommand.Guard: return ENERGY_GUARD_PER_H;
                case AIBotCommand.Patrol: return ENERGY_PATROL_PER_H;
                default: return ENERGY_GUARD_PER_H;
            }
        }

        public void ConsumeEnergyForAction(float amount)
        {
            if (IsShutdown) return;

            if (energyMode == EnergyMode.Battery)
            {
                batteryCurrent = Mathf.Max(0f, batteryCurrent - amount);
                if (batteryCurrent <= 0f && uraniumCurrent > 0f)
                    energyMode = EnergyMode.Uranium;
            }
            else
            {
                float halfAmount = amount * 0.5f;
                uraniumCurrent = Mathf.Max(0f, uraniumCurrent - halfAmount);
                if (uraniumCurrent <= 0f && batteryCurrent > 0f)
                    energyMode = EnergyMode.Battery;
            }
        }

        public void AddBatteryEnergy(float amount)
        {
            batteryCurrent = Mathf.Min(batteryCurrent + amount, batteryMax);
        }

        public void AddUraniumFuel()
        {
            uraniumCurrent = uraniumMax;
        }

        /// <summary>切换能量模式。目标模式无能源时自动回退到有能源的模式，都没有则默认电池。</summary>
        public void SetEnergyMode(EnergyMode mode)
        {
            // 优先尝试目标模式
            if (mode == EnergyMode.Battery && batteryCurrent > 0f)
            {
                energyMode = EnergyMode.Battery;
                return;
            }
            if (mode == EnergyMode.Uranium && uraniumCurrent > 0f)
            {
                energyMode = EnergyMode.Uranium;
                return;
            }

            // 目标模式无能源 → 回退到另一种
            if (batteryCurrent > 0f)
                energyMode = EnergyMode.Battery;
            else if (uraniumCurrent > 0f)
                energyMode = EnergyMode.Uranium;
            else
                energyMode = EnergyMode.Battery; // 都没有，默认电池
        }

        // ============================================================
        // 血量系统
        // ============================================================

        public void TakeDamage(float damage)
        {
            if (isDead) return;
            currentHP -= damage;
            if (currentHP <= 0f)
            {
                currentHP = 0f;
                OnDeath();
            }
        }

        public void RepairHP(float amount)
        {
            currentHP = Mathf.Min(currentHP + amount, maxHP);
        }

        void OnDeath()
        {
            isDead = true;

            // 如果被驾驶中，强制退出（必须在禁用Agent之前）
            if (IsPiloted && GetComponent<AIBotPilot>() is AIBotPilot pilot && pilot.IsPiloting)
                pilot.ExitPilot();

            if (_agent != null && _agent.enabled)
                _agent.enabled = false;

            // 从僵尸感知系统移除
            if (Zombie.ZombieAwarenessSystem.Instance != null)
                Zombie.ZombieAwarenessSystem.Instance.UnregisterAIBot(transform);

            Debug.Log("[AIBot] 机器人已报废");
            EventBus.Publish(new AIBotDestroyedEvent(transform.position));
        }

        // ============================================================
        // 跟随逻辑
        // ============================================================

        void UpdateFollow()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _agent.stoppingDistance = followDistance;

            bool canReach = CheckCanReachPlayer();

            if (!canReach)
            {
                HandleUnreachable();
                return;
            }

            if (dist > followDistance + 1f)
            {
                _agent.isStopped = false;
                _agent.SetDestination(_player.position);
            }
            else if (dist < followDistance - 0.5f)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            else
            {
                // 在范围内，面向玩家前方
                _agent.isStopped = true;
                Vector3 lookDir = _player.forward;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 180f * UnityEngine.Time.deltaTime);
                }
            }
        }

        bool CheckCanReachPlayer()
        {
            if (_player == null || _agent == null) return false;
            if (!_agent.isOnNavMesh) return false;

            NavMeshPath path = new NavMeshPath();
            if (!_agent.CalculatePath(_player.position, path))
                return false;

            return path.status == NavMeshPathStatus.PathComplete
                || path.status == NavMeshPathStatus.PathPartial;
        }

        void HandleUnreachable()
        {
            if (_player == null) return;

            // 掉出NavMesh → 立即传送
            if (!_agent.isOnNavMesh)
            {
                TeleportToPlayer();
                return;
            }

            // PathPartial → 等待5s后传送
            NavMeshPath path = new NavMeshPath();
            if (_agent.CalculatePath(_player.position, path))
            {
                if (path.status != NavMeshPathStatus.PathComplete)
                {
                    _stuckTimer += UnityEngine.Time.deltaTime;
                    if (_stuckTimer >= STUCK_TELEPORT_TIME)
                    {
                        TeleportToPlayer();
                        _stuckTimer = 0f;
                    }
                    return;
                }
            }

            _stuckTimer = 0f;
        }

        void TeleportToPlayer()
        {
            if (_teleportCooldownTimer > 0f) return;
            if (_player == null) return;

            // 在玩家周围找有效NavMesh位置
            Vector3 target = _player.position + _player.forward * followDistance;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                if (_agent.enabled)
                    _agent.Warp(hit.position);
                else
                    transform.position = hit.position;

                _teleportCooldownTimer = TELEPORT_COOLDOWN;
                _stuckTimer = 0f;
                Debug.Log("[AIBot] 传送至玩家身边");
            }
        }

        void CheckStuck()
        {
            if (_agent.isStopped) return;
            if (!_agent.hasPath) return;

            float movedDist = Vector3.Distance(transform.position, _lastPosition);
            if (movedDist < 0.1f && _agent.velocity.magnitude < 0.05f)
            {
                _stuckTimer += UnityEngine.Time.deltaTime;
                if (_stuckTimer >= STUCK_RESET_TIME && _stuckTimer < STUCK_TELEPORT_TIME)
                {
                    _agent.ResetPath();
                    if (_player != null)
                        _agent.SetDestination(_player.position);
                }
                else if (_stuckTimer >= STUCK_TELEPORT_TIME)
                {
                    TeleportToPlayer();
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckTimer = 0f;
                _lastPosition = transform.position;
            }
        }

        // ============================================================
        // 驻守逻辑
        // ============================================================

        void UpdateGuard()
        {
            if (_player == null) return;

            // 超距自动解除
            if (guardAutoRecallEnabled)
            {
                float distToPlayer = Vector3.Distance(transform.position, _player.position);
                if (distToPlayer > guardAutoRecallDistance)
                {
                    SetCommand(AIBotCommand.Follow);
                    Debug.Log("[AIBot] 驻守超距，自动切回跟随");
                    return;
                }
            }

            // 如果在驻守点附近，待机
            float distToGuard = Vector3.Distance(transform.position, guardPosition);
            if (distToGuard < 0.5f)
            {
                _agent.isStopped = true;
                return;
            }

            // 返回驻守点
            _agent.isStopped = false;
            _agent.stoppingDistance = 0.1f;
            _agent.SetDestination(guardPosition);
        }

        // ============================================================
        // 巡逻逻辑
        // ============================================================

        void UpdatePatrol()
        {
            if (_player == null) return;

            // 超距自动解除
            if (patrolAutoRecallEnabled)
            {
                float distToPlayer = Vector3.Distance(transform.position, _player.position);
                if (distToPlayer > patrolAutoRecallDistance)
                {
                    SetCommand(AIBotCommand.Follow);
                    Debug.Log("[AIBot] 巡逻超距，自动切回跟随");
                    return;
                }
            }

            Vector3 center = patrolAroundPlayer ? _player.position : patrolCenterPoint;

            if (_patrolPaused)
            {
                _agent.isStopped = true;
                _patrolPauseTimer -= UnityEngine.Time.deltaTime;
                if (_patrolPauseTimer <= 0f)
                {
                    _patrolPaused = false;
                    FindNewPatrolTarget(center);
                }
                return;
            }

            // 到达目标 → 暂停
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
            {
                _patrolPaused = true;
                _patrolPauseTimer = Random.Range(PATROL_PAUSE_MIN, PATROL_PAUSE_MAX);
                return;
            }

            // 定时刷新巡逻点
            _patrolFindTimer -= UnityEngine.Time.deltaTime;
            if (_patrolFindTimer <= 0f)
            {
                _patrolFindTimer = PATROL_FIND_INTERVAL;
                FindNewPatrolTarget(center);
            }
        }

        void FindNewPatrolTarget(Vector3 center)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(1f, patrolRadius);
            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );

            Vector3 candidate = center + offset;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                _patrolTarget = hit.position;
                _agent.isStopped = false;
                _agent.stoppingDistance = 0.1f;
                _agent.SetDestination(_patrolTarget);
            }
        }

        // ============================================================
        // 防自杀
        // ============================================================

        void CheckLowHealthRetreat()
        {
            if (!IsLowHP) return;

            if (IsCriticalHP)
            {
                if (currentCommand != AIBotCommand.Follow)
                    SetCommand(AIBotCommand.Follow);
                followDistance = 1f;
            }
            else if (HealthPercent < 0.3f)
            {
                if (currentCommand != AIBotCommand.Follow)
                    SetCommand(AIBotCommand.Follow);
            }
        }

        /// <summary>瞬间掉血超过30%触发撤退</summary>
        public void OnBurstDamage(float damage)
        {
            if (damage >= maxHP * 0.3f)
            {
                SetCommand(AIBotCommand.Follow);
                followDistance = 1f;
                Debug.Log("[AIBot] 受到重创，触发紧急撤退！");
            }
        }

        // ============================================================
        // 指令切换
        // ============================================================

        public void SetCommand(AIBotCommand cmd)
        {
            currentCommand = cmd;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.isStopped = false;

            switch (cmd)
            {
                case AIBotCommand.Follow:
                    _agent.stoppingDistance = followDistance;
                    break;
                case AIBotCommand.Guard:
                    guardPosition = transform.position;
                    _agent.stoppingDistance = 0.1f;
                    break;
                case AIBotCommand.Patrol:
                    _patrolPaused = true;
                    _patrolPauseTimer = 0.5f;
                    _patrolFindTimer = 0f;
                    _agent.stoppingDistance = 0.1f;
                    break;
                case AIBotCommand.Pilot:
                    _agent.isStopped = true;
                    break;
            }

            Debug.Log($"[AIBot] 切换到 {cmd} 模式");
        }

        public void CycleCommand()
        {
            int next = ((int)currentCommand + 1) % 3;
            SetCommand((AIBotCommand)next);
        }

        // ============================================================
        // Navigable check
        // ============================================================

        public bool CanNavigate()
        {
            return _agent != null && _agent.enabled && _agent.isOnNavMesh && !IsShutdown;
        }
    }
}
