using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Combat;
using _Game.Systems.WorldGen;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// 两阶段僵尸刷新系统。
    /// Phase A（初始）: Chunk 进入 Preloaded → 按 Profile 随机刷 initialMin~initialMax 只
    /// Phase B（持续）: Chunk 处于 Loaded → 每 respawnInterval 检查，当前存活 < maxPerChunk 时补刷
    /// 僵尸死亡 → 预算 +1；Chunk 卸载 → 重置
    /// </summary>
    public class ZombieSpawner : MonoBehaviour
    {
        public static ZombieSpawner Instance { get; private set; }

        [Header("地段刷怪配置（多份 = 不同地段）")]
        public ZoneSpawnProfile[] zoneProfiles;

        [Header("全局昼夜倍率")]
        public float nightMultiplier = 2f;

        // 内部
        private readonly Dictionary<int, int> _alivePerChunk = new();       // chunkId → 当前存活
        private readonly Dictionary<int, int> _maxPerChunk = new();         // chunkId → 上限（晚上×倍率）
        private readonly Dictionary<int, ZoneSpawnProfile> _chunkProfile = new(); // chunkId → 地段配置
        private readonly Dictionary<int, float> _nextRespawnTime = new();   // chunkId → 下次检查时间
        private readonly HashSet<int> _initialSpawned = new();             // 已执行过初始刷怪
        private float _spawnCheckTimer;
        private const float SpawnCheckInterval = 10f;
        private int _gridSize;
        private Transform _player;

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
            if (playerObj != null) _player = playerObj.transform;

            EventBus.Subscribe<ZombieDied>(OnZombieDied);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ZombieDied>(OnZombieDied);
        }

        void Update()
        {
            _spawnCheckTimer -= UnityEngine.Time.deltaTime;
            if (_spawnCheckTimer > 0f) return;
            _spawnCheckTimer = SpawnCheckInterval;

            var cm = ChunkManager.Instance;
            if (cm == null || _player == null) return;

            cm.GetChunkCounts(out int loaded, out _, out _);

            // 遍历所有已记录 chunk，检查 Loaded 的是否需要补刷
            bool isNight = IsNight();
            var keys = new List<int>(_alivePerChunk.Keys);
            foreach (var chunkId in keys)
            {
                var chunk = cm.GetChunk(chunkId);
                if (chunk == null) continue;

                // 只对 Loaded 状态的 chunk 持续补刷
                if (chunk.state != ChunkState.Loaded) continue;

                // 检查冷却
                if (_nextRespawnTime.TryGetValue(chunkId, out float nextTime)
                    && UnityEngine.Time.time < nextTime)
                    continue;

                TryRespawn(chunkId, isNight);
            }
        }

        // ============================================================
        // Phase A: 初始刷怪（ChunkManager.Stage2 调用）
        // ============================================================

        /// <summary>在 Chunk 首次预热时刷初始僵尸。</summary>
        public void SpawnInitial(int chunkId, bool isNight)
        {
            if (_initialSpawned.Contains(chunkId)) return;
            _initialSpawned.Add(chunkId);

            var profile = GetProfile(chunkId);
            if (profile == null || profile.typeWeights.Length == 0) return;

            int count = Random.Range(profile.initialMin, profile.initialMax + 1);
            int max = Mathf.RoundToInt(profile.maxPerChunk * (isNight ? nightMultiplier : 1f));

            _maxPerChunk[chunkId] = max;
            _chunkProfile[chunkId] = profile;

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (TryGetSpawnPoint(chunkId, profile.minSpawnDistFromPlayer, out Vector3 pos))
                {
                    SpawnZombie(pos, PickWeightedType(profile), chunkId);
                    spawned++;
                }
            }

            _alivePerChunk[chunkId] = spawned;
            _nextRespawnTime[chunkId] = UnityEngine.Time.time + profile.respawnInterval;
        }

        // ============================================================
        // Phase B: 持续补刷
        // ============================================================

        void TryRespawn(int chunkId, bool isNight)
        {
            if (!_maxPerChunk.TryGetValue(chunkId, out int max)) return;
            if (!_alivePerChunk.TryGetValue(chunkId, out int alive)) return;

            int budget = max - alive;
            if (budget <= 0) return;

            var profile = _chunkProfile[chunkId];
            if (profile == null || profile.typeWeights.Length == 0) return;

            int batch = Mathf.Min(budget, profile.maxPerRespawnBatch);

            int spawned = 0;
            for (int i = 0; i < batch; i++)
            {
                if (TryGetSpawnPoint(chunkId, profile.minSpawnDistFromPlayer, out Vector3 pos))
                {
                    SpawnZombie(pos, PickWeightedType(profile), chunkId);
                    spawned++;
                }
            }

            _alivePerChunk[chunkId] = alive + spawned;
            _nextRespawnTime[chunkId] = UnityEngine.Time.time + profile.respawnInterval;
        }

        // ============================================================
        // 死亡回收预算
        // ============================================================

        void OnZombieDied(ZombieDied evt)
        {
            int chunkId = ChunkManager.GetChunkId(new Vector3(evt.X, 0, evt.Z));
            if (_alivePerChunk.TryGetValue(chunkId, out int alive) && alive > 0)
                _alivePerChunk[chunkId] = alive - 1;
        }

        // ============================================================
        // Chunk 卸载时重置
        // ============================================================

        /// <summary>由 ChunkManager 在 Chunk 进入 Unloaded 时调用。</summary>
        public void OnChunkUnloaded(int chunkId)
        {
            _alivePerChunk.Remove(chunkId);
            _maxPerChunk.Remove(chunkId);
            _chunkProfile.Remove(chunkId);
            _nextRespawnTime.Remove(chunkId);
            _initialSpawned.Remove(chunkId);
        }

        // ============================================================
        // 刷新点选取
        // ============================================================

        /// <summary>在 Chunk 内找一个远离玩家且不在视野前方的 NavMesh 点。</summary>
        bool TryGetSpawnPoint(int chunkId, float minDist, out Vector3 result)
        {
            Vector2 center = ChunkManager.GetChunkCenter(chunkId);
            float halfSize = GameConstants.RUNTIME_CHUNK_SIZE * 0.45f;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float x = center.x + Random.Range(-halfSize, halfSize);
                float z = center.y + Random.Range(-halfSize, halfSize);

                if (!NavMesh.SamplePosition(new Vector3(x, 0, z),
                    out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    continue;

                Vector3 pos = hit.position;

                // 距离玩家检测
                if (_player != null)
                {
                    Vector3 toSpawn = pos - _player.position;
                    toSpawn.y = 0f;
                    float dist = toSpawn.magnitude;
                    if (dist < minDist) continue;

                    // 不在玩家视野前方（后方或侧面优先）
                    Vector3 playerForward = _player.forward;
                    playerForward.y = 0f;
                    float dot = Vector3.Dot(toSpawn.normalized, playerForward.normalized);
                    if (dot > 0.3f) continue; // 太靠前，重试
                }

                result = pos;
                return true;
            }

            result = Vector3.zero;
            return false;
        }

        // ============================================================
        // 辅助
        // ============================================================

        ZombieData PickWeightedType(ZoneSpawnProfile profile)
        {
            var weights = profile.typeWeights;
            if (weights.Length == 1) return weights[0].data;

            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i].weight;
            int roll = Random.Range(0, total);
            int accum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                accum += weights[i].weight;
                if (roll < accum) return weights[i].data;
            }
            return weights[weights.Length - 1].data;
        }

        ZoneSpawnProfile GetProfile(int chunkId)
        {
            if (zoneProfiles == null || zoneProfiles.Length == 0) return null;

            // 伪随机地段分配（后续替换为真实区域数据）
            int cx = chunkId % _gridSize;
            int cz = chunkId / _gridSize;
            int hash = (cx * 73856093) ^ (cz * 19349663);
            int index = Mathf.Abs(hash) % zoneProfiles.Length;
            return zoneProfiles[index];
        }

        GameObject SpawnZombie(Vector3 pos, ZombieData data, int chunkId)
        {
            var go = new GameObject($"Zombie_{data.zombieName}");
            go.transform.position = pos;

            var agent = go.AddComponent<NavMeshAgent>();
            var stateMachine = go.AddComponent<ZombieStateMachine>();
            var damageable = go.AddComponent<DamageableZombie>();
            var controller = go.AddComponent<ZombieController>();
            controller.Initialize(data);

            // 圆柱体外形
            BuildBody(go, data);

            ChunkManager.Instance?.RegisterZombie(stateMachine, chunkId);

            return go;
        }

        /// <summary>创建圆柱体身体（子物体）。</summary>
        public static void BuildBody(GameObject parent, ZombieData data)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(parent.transform);
            body.transform.localPosition = Vector3.zero;
            float r = data.bodyRadius > 0f ? data.bodyRadius : 0.35f;
            float h = data.bodyHeight > 0f ? data.bodyHeight : 1.8f;
            body.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
            var col = body.GetComponent<CapsuleCollider>();
            if (col != null)
            {
                col.radius = r;
                col.height = h;
            }
        }

        bool IsNight()
        {
            var tm = FindObjectOfType<_Game.Systems.Time.TimeManager>();
            if (tm == null) return false;
            float h = tm.CurrentHour;
            return h >= 18f || h < 6f;
        }

        // 公开属性（供 DebugPanel）
        public int GetAliveInChunk(int chunkId) =>
            _alivePerChunk.TryGetValue(chunkId, out int v) ? v : -1;

        public int GetBudgetInChunk(int chunkId)
        {
            if (!_maxPerChunk.TryGetValue(chunkId, out int max)) return -1;
            int alive = _alivePerChunk.TryGetValue(chunkId, out int v) ? v : 0;
            return max - alive;
        }
    }
}
