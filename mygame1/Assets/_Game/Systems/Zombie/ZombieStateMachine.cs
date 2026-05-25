using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Systems.Combat;
using _Game.Systems.WorldGen;
using _Game.Core;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// 僵尸状态机。挂每只僵尸上，持有组件引用和运行时数据。
    /// 状态类为全局单例，所有数据存在此 ctx 上。
    /// </summary>
    public class ZombieStateMachine : MonoBehaviour
    {
        // ── 运行时参数（由 ZombieController 或 ZombieSpawner 通过 ApplyZombieData 写入）──
        public float moveSpeed;
        public float wanderSpeedMultiplier = 0.5f;
        public float detectRange;
        public float loseRange = 30f;
        public float visionAngle;
        public float attackRange;
        public int attackDamage;
        public float attackCooldown;

        // 组件引用
        [HideInInspector] public NavMeshAgent agent;
        [HideInInspector] public DamageableZombie damageable;

        // 当前状态
        public ZombieState currentState { get; private set; }

        // 感知（由 ZombieAwarenessSystem 写入）
        [HideInInspector] public Transform playerTarget;
        [HideInInspector] public bool playerDetected;

        // Idle 用
        [HideInInspector] public float idleTimer;
        [HideInInspector] public float idleDuration;

        // Wander 用
        [HideInInspector] public Vector3 wanderTarget;

        // Attack 用
        [HideInInspector] public float attackTimer;

        // 声音
        [HideInInspector] public Vector3 lastHeardPosition;

        // 内部
        private float _destinationUpdateTimer;
        private float _lastHeardTime;
        private float _lastHeardRadius;
        private const float ZombieHearCooldown = 2f;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();
            agent.enabled = false; // 默认禁用，由 ChunkManager 激活

            damageable = GetComponent<DamageableZombie>();
        }

        void Start()
        {
            // 仅当尚未被 ZombieSpawner 注册时才注册（避免重复）
            int chunkId = ChunkManager.GetChunkId(transform.position);
            var chunk = ChunkManager.Instance?.GetChunk(chunkId);
            if (chunk != null && !chunk.zombieInstances.Contains(this))
                ChunkManager.Instance?.RegisterZombie(this, chunkId);

            if (currentState == null)
                TransitionTo(ZombieState_Idle.Instance);
        }

        /// <summary>从 ZombieData SO 或预制体数据批量应用参数。</summary>
        public void ApplyZombieData(ZombieData data)
        {
            if (data == null) return;
            moveSpeed = data.moveSpeed;
            detectRange = data.detectRange;
            loseRange = data.loseRange;
            visionAngle = data.visionAngle;
            attackRange = data.attackRange;
            attackDamage = data.attackDamage;
            attackCooldown = data.attackCooldown;

            if (agent != null)
            {
                agent.speed = moveSpeed;
                agent.stoppingDistance = attackRange * 0.8f;
                agent.acceleration = 8f;
                agent.angularSpeed = 360f;
            }
        }

        void Update()
        {
            currentState?.Update(this);
        }

        void OnDestroy()
        {
            if (ZombieAwarenessSystem.Instance != null)
                ZombieAwarenessSystem.Instance.Unregister(this);
        }

        /// <summary>切换状态：Exit 旧 → Enter 新。</summary>
        public void TransitionTo(ZombieState newState)
        {
            if (currentState != null)
                currentState.Exit(this);
            currentState = newState;
            if (currentState != null)
                currentState.Enter(this);
        }

        /// <summary>由 ZombieAwarenessSystem 调用。</summary>
        public void OnPlayerDetected(Transform player)
        {
            playerTarget = player;
            playerDetected = true;
        }

        /// <summary>由 ZombieAwarenessSystem 调用。</summary>
        public void OnPlayerLost()
        {
            playerTarget = null;
            playerDetected = false;
        }

        /// <summary>由 DecibelSystem 调用。节流3: 冷却内忽略更小声音，只有更大的才切目标。</summary>
        public void OnSoundHeard(NoiseEvent noise)
        {
            if ((noise.Source & (SoundSource.Zombie | SoundSource.Environment)) != 0)
                return;

            float now = UnityEngine.Time.time;
            float oldRadius = _lastHeardRadius;

            bool inCooldown = now - _lastHeardTime < ZombieHearCooldown;
            if (inCooldown && noise.Radius <= oldRadius)
                return;

            _lastHeardTime = now;
            _lastHeardRadius = noise.Radius;
            lastHeardPosition = noise.Position;

            bool isBigger = noise.Radius > oldRadius;

            if (currentState is ZombieState_Idle || currentState is ZombieState_Wander)
            {
                if (noise.SourceObject != null)
                {
                    playerTarget = noise.SourceObject.transform;
                    playerDetected = true;
                }
                else
                {
                    playerTarget = null;
                    playerDetected = false;
                }
                TransitionTo(ZombieState_Chase.Instance);
            }
            else if (isBigger)
            {
                lastHeardPosition = noise.Position;
                if (noise.SourceObject != null)
                {
                    playerTarget = noise.SourceObject.transform;
                    playerDetected = true;
                }
            }
        }

        /// <summary>更新 NavMeshAgent 目标点，按 interval 节流。</summary>
        public void UpdateDestination(Vector3 target, float interval = 0.5f)
        {
            _destinationUpdateTimer -= UnityEngine.Time.deltaTime;
            if (_destinationUpdateTimer <= 0f)
            {
                _destinationUpdateTimer = interval;
                if (agent.enabled && agent.isOnNavMesh)
                    agent.SetDestination(target);
            }
        }

        /// <summary>设置移动速度。</summary>
        public void SetSpeed(float speed)
        {
            if (agent.enabled)
                agent.speed = speed;
        }

        /// <summary>找一个 NavMesh 上的随机点，radius 半径内。</summary>
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
    }
}
