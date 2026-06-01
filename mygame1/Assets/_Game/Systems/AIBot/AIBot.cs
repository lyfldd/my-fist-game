using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Character;
using _Game.Systems.Combat;
using _Game.Systems.Threat;

namespace _Game.Systems.AIBot
{
    public enum AIBotCommand { Follow, Guard, Patrol, Pilot }
    public enum EnergyMode { EnergySaving, Electric, Uranium, Burst }

    [System.Serializable]
    public struct FusionCoreSlot
    {
        public string itemName;       // "聚变核心(小)" / "聚变核心(大)"
        public ItemData itemData;     // 物品引用（用于取出归还背包）
        public float burnTime;        // 总燃耗时(h)
        public float burnRemaining;   // 剩余燃耗时(h)
        public float outputRate;      // 铀产出/h

        public bool IsEmpty => string.IsNullOrEmpty(itemName) || burnRemaining <= 0f;
        public bool IsSmall => itemName == "聚变核心(小)";
        public bool IsLarge => itemName == "聚变核心(大)";
    }

    /// <summary>
    /// AI机器人主控制器。状态机 + 四能量模式 + 血量 + 防自杀协调。
    /// 由 AIBotBuildable 在放置后初始化。
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(FactionComponent))]
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
        [SerializeField] private EnergyMode energyMode = EnergyMode.Electric;
        [SerializeField] private bool isDead;

        [Header("节能模式")]
        public bool ecoModeEnabled;

        [Header("爆发模式")]
        public bool burstModeEnabled;
        public bool shieldActive;
        public float shieldMaxHP = 500f;
        public float shieldCurrentHP;
        public float shieldStartupTimer;
        public const float SHIELD_STARTUP_TIME = 5f;
        public const float SHIELD_STARTUP_COST = 30f;
        public const float SHIELD_MAINTENANCE_FULL = 0.1f;    // 铀/秒（满盾时）
        public const float SHIELD_MAINTENANCE_RECHARGE = 0.5f; // 铀/秒（未满时）
        public const float SHIELD_REGEN_PER_SEC = 5f;         // 盾回复/秒（未满时）

        [Header("移动")]
        public float moveSpeed = 5f;
        public float pilotBaseSpeed = 8f;
        [Range(0.25f, 1f)] public float speedSliderValue = 1f;

        // 移动耗能 (/h)
        public const float MOVE_ENERGY_PER_H = 5f;

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

        // 能量消耗速率 (/h)
        public const float ENERGY_GUARD_PER_H = 3f;
        public const float ENERGY_FOLLOW_PER_H = 6f;
        public const float ENERGY_PATROL_PER_H = 10f;
        public const float ENERGY_LASER_PER_SHOT = 2f;
        public const float ENERGY_RIGHTARM_PER_SHOT = 1f;
        public const float ENERGY_LEFTARM_PER_SWING = 0.5f;

        // 太阳能充电速率 (/h，仅白天)
        public float solarRechargeRate = 120f;

        // 微型核反应堆 (3×3 聚变核心槽位)
        public FusionCoreSlot[] reactorSlots = new FusionCoreSlot[9];
        public const float NUCLEAR_RECHARGE_BASE = 30f; // 小核心基础铀产出/h

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

        // 模式倍率属性
        public float SpeedMultiplier => energyMode switch
        {
            EnergyMode.EnergySaving => 0.75f,
            EnergyMode.Electric => 1f,
            EnergyMode.Uranium => 1.25f,
            EnergyMode.Burst => 1.5f,
            _ => 1f
        };
        public float ConsumptionMultiplier => energyMode switch
        {
            EnergyMode.EnergySaving => 0.5f,
            EnergyMode.Electric => 1f,
            EnergyMode.Uranium => 1f,
            EnergyMode.Burst => 1.25f,
            _ => 1f
        };
        public float CooldownMultiplier => energyMode switch
        {
            EnergyMode.EnergySaving => 0.5f,
            EnergyMode.Electric => 1f,
            EnergyMode.Uranium => 2f,
            EnergyMode.Burst => 2.5f,
            _ => 1f
        };
        public bool IsLaserEnabled => energyMode != EnergyMode.EnergySaving;
        public bool IsAIAssistEnabled => energyMode != EnergyMode.EnergySaving;
        public bool IsAIWeaponOverrideEnabled => energyMode != EnergyMode.EnergySaving;
        public bool IsShieldAvailable => energyMode == EnergyMode.Burst;

        // 能量来源判定
        public bool UsesBattery => energyMode == EnergyMode.EnergySaving || energyMode == EnergyMode.Electric;
        public bool UsesUranium => energyMode == EnergyMode.Uranium || energyMode == EnergyMode.Burst;

        public float BatteryCurrent => batteryCurrent;
        public float BatteryMax => batteryMax;
        public float BatteryPercent => batteryMax > 0f ? Mathf.Clamp01(batteryCurrent / batteryMax) : 0f;
        public float UraniumCurrent => uraniumCurrent;
        public float UraniumMax => uraniumMax;
        public float UraniumPercent => uraniumMax > 0f ? Mathf.Clamp01(uraniumCurrent / uraniumMax) : 0f;

        public float ShieldCurrentHP => shieldCurrentHP;
        public float ShieldMaxHP => shieldMaxHP;
        public float ShieldPercent => shieldMaxHP > 0f ? Mathf.Clamp01(shieldCurrentHP / shieldMaxHP) : 0f;
        public bool IsShieldActive => shieldActive && shieldCurrentHP > 0f;

        public bool IsBatteryEmpty => batteryCurrent <= 0f;
        public bool IsUraniumEmpty => uraniumCurrent <= 0f;
        public bool IsShutdown => IsBatteryEmpty && IsUraniumEmpty;

        /// <summary>当前太阳能实际充电速率 (/h)，0 表示未在充电</summary>
        public float CurrentSolarRate { get; private set; }
        public bool IsSolarActive => CurrentSolarRate > 0f;

        /// <summary>当前核反应堆充电速率 (/h)，0 表示未在充电</summary>
        public float CurrentNuclearRate { get; private set; }
        public bool IsNuclearActive => CurrentNuclearRate > 0f;

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

            // 设置阵营（RequireComponent 保证 FactionComponent 已存在，必须在 Start 前设好）
            var factionComp = GetComponent<FactionComponent>();
            if (factionComp != null)
                factionComp.SetFaction(FactionType.AIBot);

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
        }

        void Update()
        {
            if (isDead) return;

            // 模式回退检查
            CheckEnergyFallback();

            // 护盾维护（爆发模式下，驾驶中也生效）
            if (burstModeEnabled && shieldActive)
                UpdateShield();

            // 护盾启动计时
            if (shieldStartupTimer > 0f)
                shieldStartupTimer -= UnityEngine.Time.deltaTime;

            // 能量消耗（驾驶中也消耗）
            ConsumeEnergy();

            // 核反应堆充电（铀/爆发模式，全天候，驾驶中也生效）
            NuclearRecharge();

            // 太阳能充电（白天缓慢恢复电池，驾驶中也生效）
            SolarRecharge();

            if (IsPiloted) return;
            if (_agent == null || !_agent.enabled) return;

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

            float activityRate = GetActivityRate();
            float moveRate = IsMoving() ? MOVE_ENERGY_PER_H : 0f;
            float totalRate = (activityRate + moveRate) * ConsumptionMultiplier;
            float ratePerSec = totalRate / 3600f;
            float dt = UnityEngine.Time.deltaTime;
            float consumed = ratePerSec * dt;

            if (UsesBattery)
            {
                batteryCurrent -= consumed;
                if (batteryCurrent <= 0f)
                {
                    batteryCurrent = 0f;
                    if (uraniumCurrent > 0f)
                    {
                        energyMode = EnergyMode.Uranium;
                        burstModeEnabled = false;
                    }
                }
            }
            else
            {
                uraniumCurrent -= consumed;
                if (uraniumCurrent <= 0f)
                {
                    uraniumCurrent = 0f;
                    burstModeEnabled = false;
                    shieldActive = false;
                    if (batteryCurrent > 0f)
                        energyMode = EnergyMode.Electric;
                }
            }
        }

        bool IsMoving()
        {
            if (_agent == null || !_agent.enabled) return false;
            return !_agent.isStopped && _agent.hasPath && _agent.velocity.magnitude > 0.1f;
        }

        /// <summary>模式能量耗尽时的回退检查（每帧调用）</summary>
        void CheckEnergyFallback()
        {
            if (ecoModeEnabled && energyMode != EnergyMode.EnergySaving)
            {
                if (batteryCurrent > 0f)
                {
                    energyMode = EnergyMode.EnergySaving;
                }
                else
                {
                    ecoModeEnabled = false;
                }
            }
            if (burstModeEnabled && energyMode != EnergyMode.Burst)
            {
                if (uraniumCurrent > 0f)
                {
                    energyMode = EnergyMode.Burst;
                }
                else
                {
                    burstModeEnabled = false;
                    shieldActive = false;
                }
            }
        }

        /// <summary>护盾维护逻辑：维持消耗 + 回复 + 启动</summary>
        void UpdateShield()
        {
            if (!burstModeEnabled || energyMode != EnergyMode.Burst) { shieldActive = false; return; }

            if (shieldStartupTimer > 0f) return; // 还在启动中

            if (shieldCurrentHP >= shieldMaxHP)
            {
                // 满盾：低功耗维持
                shieldCurrentHP = shieldMaxHP;
                ConsumeUraniumForShield(SHIELD_MAINTENANCE_FULL * UnityEngine.Time.deltaTime);
            }
            else
            {
                // 未满：消耗 + 回复
                ConsumeUraniumForShield(SHIELD_MAINTENANCE_RECHARGE * UnityEngine.Time.deltaTime);
                shieldCurrentHP = Mathf.Min(shieldCurrentHP + SHIELD_REGEN_PER_SEC * UnityEngine.Time.deltaTime, shieldMaxHP);
            }
        }

        void ConsumeUraniumForShield(float amount)
        {
            uraniumCurrent = Mathf.Max(0f, uraniumCurrent - amount);
            if (uraniumCurrent <= 0f)
            {
                uraniumCurrent = 0f;
                shieldActive = false;
                burstModeEnabled = false;
                if (batteryCurrent > 0f) energyMode = EnergyMode.Electric;
            }
        }

        /// <summary>开启/重启能量盾</summary>
        public void ActivateShield()
        {
            if (!IsShieldAvailable || shieldActive) return;
            if (uraniumCurrent < SHIELD_STARTUP_COST) return;

            uraniumCurrent -= SHIELD_STARTUP_COST;
            shieldStartupTimer = SHIELD_STARTUP_TIME;
            shieldActive = true;
            shieldCurrentHP = 0f;
        }

        /// <summary>关闭能量盾</summary>
        public void DeactivateShield()
        {
            shieldActive = false;
            shieldStartupTimer = 0f;
        }

        /// <summary>核反应堆被动充电（铀/爆发模式，全天候）</summary>
        void NuclearRecharge()
        {
            CurrentNuclearRate = 0f;
            if (!UsesUranium) return;
            if (uraniumCurrent >= uraniumMax) return;
            if (IsShutdown) return;

            float totalRate = 0f;
            for (int i = 0; i < reactorSlots.Length; i++)
            {
                if (!reactorSlots[i].IsEmpty)
                {
                    totalRate += reactorSlots[i].outputRate;
                    reactorSlots[i].burnRemaining -= UnityEngine.Time.deltaTime / 3600f;
                    if (reactorSlots[i].burnRemaining <= 0f)
                        reactorSlots[i] = default;
                }
            }

            if (totalRate <= 0f) return;

            float recharge = totalRate / 3600f * UnityEngine.Time.deltaTime;
            uraniumCurrent = Mathf.Min(uraniumCurrent + recharge, uraniumMax);
            CurrentNuclearRate = totalRate;
        }

        private _Game.Systems.Time.TimeManager _cachedTimeManager;
        private _Game.Systems.Weather.WeatherManager _cachedWeatherManager;
        private float _solarDebugTimer;

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

            float adjustedAmount = amount * ConsumptionMultiplier;

            if (UsesBattery)
            {
                batteryCurrent = Mathf.Max(0f, batteryCurrent - adjustedAmount);
                if (batteryCurrent <= 0f && uraniumCurrent > 0f)
                {
                    energyMode = EnergyMode.Uranium;
                    burstModeEnabled = false;
                }
            }
            else
            {
                uraniumCurrent = Mathf.Max(0f, uraniumCurrent - adjustedAmount);
                if (uraniumCurrent <= 0f)
                {
                    burstModeEnabled = false;
                    shieldActive = false;
                    if (batteryCurrent > 0f)
                        energyMode = EnergyMode.Electric;
                }
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

        /// <summary>切换基础能量模式。会自动关闭节能/爆发开关。</summary>
        public void SetEnergyMode(EnergyMode mode)
        {
            ecoModeEnabled = false;
            burstModeEnabled = false;
            shieldActive = false;
            shieldStartupTimer = 0f;

            if (mode == EnergyMode.EnergySaving)
            {
                ecoModeEnabled = true;
                energyMode = batteryCurrent > 0f ? EnergyMode.EnergySaving : EnergyMode.Electric;
                if (batteryCurrent <= 0f && uraniumCurrent > 0f) energyMode = EnergyMode.Uranium;
                return;
            }
            if (mode == EnergyMode.Burst)
            {
                burstModeEnabled = true;
                energyMode = uraniumCurrent > 0f ? EnergyMode.Burst : EnergyMode.Uranium;
                if (uraniumCurrent <= 0f && batteryCurrent > 0f) energyMode = EnergyMode.Electric;
                return;
            }

            if (mode == EnergyMode.Electric && batteryCurrent > 0f)
            {
                energyMode = EnergyMode.Electric;
                return;
            }
            if (mode == EnergyMode.Uranium && uraniumCurrent > 0f)
            {
                energyMode = EnergyMode.Uranium;
                return;
            }

            if (batteryCurrent > 0f)
                energyMode = EnergyMode.Electric;
            else if (uraniumCurrent > 0f)
                energyMode = EnergyMode.Uranium;
            else
                energyMode = EnergyMode.Electric;
        }

        /// <summary>切换节能模式开关</summary>
        public void ToggleEcoMode()
        {
            if (ecoModeEnabled)
            {
                SetEnergyMode(EnergyMode.Electric);
            }
            else
            {
                SetEnergyMode(EnergyMode.EnergySaving);
            }
        }

        /// <summary>切换爆发模式开关</summary>
        public void ToggleBurstMode()
        {
            if (burstModeEnabled)
            {
                SetEnergyMode(EnergyMode.Uranium);
            }
            else
            {
                SetEnergyMode(EnergyMode.Burst);
            }
        }

        // ============================================================
        // 血量系统
        // ============================================================

        public void TakeDamage(float damage)
        {
            if (isDead) return;

            // 护盾吸收优先
            if (IsShieldActive && shieldStartupTimer <= 0f)
            {
                if (shieldCurrentHP >= damage)
                {
                    shieldCurrentHP -= damage;
                    return;
                }
                damage -= shieldCurrentHP;
                shieldCurrentHP = 0f;
                shieldActive = false;
            }

            currentHP -= damage;
            if (currentHP <= 0f)
            {
                currentHP = 0f;
                OnDeath();
            }
        }

        public float DamageMultiplier => 1f;

        public void RepairHP(float amount)
        {
            currentHP = Mathf.Min(currentHP + amount, maxHP);
        }

        void OnDeath()
        {
            isDead = true;

            // 通知 ThreatSystem 清理
            EventBus.Publish(new EntityDeathEvent(gameObject.GetInstanceID()));

            // 如果被驾驶中，强制退出（必须在禁用Agent之前）
            if (IsPiloted && GetComponent<AIBotPilot>() is AIBotPilot pilot && pilot.IsPiloting)
                pilot.ExitPilot();

            if (_agent != null && _agent.enabled)
                _agent.enabled = false;

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
