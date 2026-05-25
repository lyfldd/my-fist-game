using System.Collections.Generic;
using UnityEngine;
using _Game.Core;
using _Game.Systems.AIBot;
using _Game.Systems.WorldGen;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// 集中式玩家检测 Phase2。视觉锥 + 听觉联动 + 视线遮蔽 + 逐帧级联扩散。
    /// 同时跟踪AI机器人作为僵尸目标。
    /// </summary>
    public class ZombieAwarenessSystem : MonoBehaviour
    {
        public static ZombieAwarenessSystem Instance { get; private set; }

        [Header("感知参数")]
        public float checkInterval = 0.5f;
        [Tooltip("群体扩散半径（米），新发现目标的僵尸唤醒此范围内其他僵尸")]
        public float cascadeRadius = 15f;

        // Chunk 网格大小，用于空间分区（从 ChunkManager 读取或使用 GameConstants 默认值）
        private int _gridSize;

        private readonly List<ZombieStateMachine> _activeZombies = new List<ZombieStateMachine>();
        private Transform _player;
        private readonly List<Transform> _aiBots = new List<Transform>();
        private float _timer;

        // 级联用
        private readonly List<ZombieStateMachine> _newlyDetected = new List<ZombieStateMachine>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            _gridSize = GameConstants.CHUNK_GRID_SIZE;

            var playerObj = GameObject.FindWithTag("Player")
                ?? GameObject.Find("Player");
            if (playerObj != null)
                _player = playerObj.transform;

            // 查找场景中已有的AI机器人
            var bots = FindObjectsOfType<_Game.Systems.AIBot.AIBot>();
            foreach (var bot in bots)
                if (bot != null && !bot.IsDead)
                    _aiBots.Add(bot.transform);
        }

        void Update()
        {
            _timer -= UnityEngine.Time.deltaTime;
            if (_timer > 0f) return;
            _timer = checkInterval;

            _newlyDetected.Clear();

            // 清理已销毁的AI机器人
            for (int i = _aiBots.Count - 1; i >= 0; i--)
                if (_aiBots[i] == null)
                    _aiBots.RemoveAt(i);

            // ── Step 1: 视觉锥 + 听觉（已有）+ 视线遮蔽 ──
            for (int i = _activeZombies.Count - 1; i >= 0; i--)
            {
                var zombie = _activeZombies[i];
                if (zombie == null)
                {
                    _activeZombies.RemoveAt(i);
                    continue;
                }

                bool wasDetected = zombie.playerDetected;
                Transform bestTarget = FindBestTarget(zombie);
                bool canSee = bestTarget != null;

                if (canSee && !wasDetected)
                {
                    zombie.OnPlayerDetected(bestTarget);
                    _newlyDetected.Add(zombie);
                }
                else if (!canSee && wasDetected)
                {
                    zombie.OnPlayerLost();
                }
            }

            // ── Step 2: 群体级联扩散（仅本轮新发现的僵尸）──
            for (int i = 0; i < _newlyDetected.Count; i++)
            {
                CascadeToNearby(_newlyDetected[i], cascadeRadius);
            }
        }

        // ============================================================
        // 目标选择：优先玩家，再选最近的AI机器人
        // ============================================================

        /// <summary>找到僵尸能看见的最佳目标：玩家优先，其次最近的AI机器人。</summary>
        Transform FindBestTarget(ZombieStateMachine zombie)
        {
            var zombiePos = zombie.transform.position;

            // 优先检查玩家
            if (_player != null && CanSeeTarget(zombie, _player))
                return _player;

            // 再检查AI机器人（选最近的）
            Transform closestBot = null;
            float closestDist = float.MaxValue;
            foreach (var bot in _aiBots)
            {
                if (bot == null) continue;
                float dist = Vector3.Distance(zombiePos, bot.position);
                if (dist < closestDist && CanSeeTarget(zombie, bot))
                {
                    closestDist = dist;
                    closestBot = bot;
                }
            }
            return closestBot;
        }

        // ============================================================
        // 视觉检测
        // ============================================================

        /// <summary>综合判定：距离 + 视觉锥 + 视线遮蔽。</summary>
        bool CanSeeTarget(ZombieStateMachine zombie, Transform target)
        {
            Vector3 targetPos = target.position;
            float dist = Vector3.Distance(zombie.transform.position, targetPos);
            if (dist > zombie.detectRange) return false;

            // 视觉锥检测
            if (zombie.visionAngle > 0f)
            {
                Vector3 toTarget = (targetPos - zombie.transform.position).normalized;
                toTarget.y = 0f;
                Vector3 forward = zombie.transform.forward;
                forward.y = 0f;

                float halfAngle = zombie.visionAngle * 0.5f;
                float angle = Vector3.Angle(forward, toTarget);
                if (angle > halfAngle) return false;
            }
            // visionAngle = 0 → 回退到圆形距离检测（跳过锥形检查）

            // 视线遮蔽
            Vector3 eyesPos = zombie.transform.position + Vector3.up * 1.5f;
            Vector3 targetEyes = targetPos + Vector3.up * 1.5f;
            if (Physics.Linecast(eyesPos, targetEyes))
                return false;

            return true;
        }

        // ============================================================
        // 群体级联扩散
        // ============================================================

        void CascadeToNearby(ZombieStateMachine source, float radius)
        {
            var cm = ChunkManager.Instance;
            if (cm == null) return;

            int sourceChunk = ChunkManager.GetChunkId(source.transform.position);
            int cx = sourceChunk % _gridSize;
            int cz = sourceChunk / _gridSize;

            // 空间分区：只查源僵尸所在 Chunk + 相邻 8 个 Chunk
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = cx + dx;
                    int nz = cz + dz;
                    if (nx < 0 || nx >= _gridSize || nz < 0 || nz >= _gridSize) continue;

                    int chunkId = nz * _gridSize + nx;
                    var chunk = cm.GetChunk(chunkId);
                    if (chunk == null) continue;

                    foreach (var other in chunk.zombieInstances)
                    {
                        if (other == source) continue;
                        if (other == null) continue;

                        // 只唤醒 Idle/Wander 状态的僵尸
                        if (!(other.currentState is ZombieState_Idle) &&
                            !(other.currentState is ZombieState_Wander))
                            continue;

                        float dist = Vector3.Distance(source.transform.position,
                                                      other.transform.position);
                        if (dist <= radius)
                        {
                            other.playerDetected = true;
                            other.playerTarget = source.playerTarget;
                            other.TransitionTo(ZombieState_Chase.Instance);
                        }
                    }
                }
            }
        }

        // ============================================================
        // 公开接口
        // ============================================================

        public void Register(ZombieStateMachine zombie)
        {
            if (!_activeZombies.Contains(zombie))
                _activeZombies.Add(zombie);
        }

        public void Unregister(ZombieStateMachine zombie)
        {
            _activeZombies.Remove(zombie);
        }

        /// <summary>注册AI机器人为潜在僵尸目标。</summary>
        public void RegisterAIBot(Transform aiBot)
        {
            if (aiBot != null && !_aiBots.Contains(aiBot))
                _aiBots.Add(aiBot);
        }

        /// <summary>移除AI机器人。</summary>
        public void UnregisterAIBot(Transform aiBot)
        {
            _aiBots.Remove(aiBot);
        }

        public int ActiveZombieCount => _activeZombies.Count;
    }
}
