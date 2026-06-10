using System.Collections.Generic;
using _Game.Core;
using _Game.Systems.Zombie;
using UnityEngine;

namespace _Game.Systems.WorldGen
{
    /// <summary>
    /// 三级区块管理器 — 80m Chunk，Loaded/Preloaded/Unloaded 三级体系。
    /// 玩家周围按需激活、异步预热队列、远距离休眠。
    /// 挂载到场景单例 GameObject。
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        public static ChunkManager Instance { get; private set; }

        [Header("性能档位")]
        public ChunkQuality quality = ChunkQuality.Medium;

        [Header("玩家引用")]
        public Transform playerTransform;

        private RuntimeChunk[] _chunks;
        private readonly Queue<int> _preloadQueue = new Queue<int>();
        private Vector3 _lastCheckPosition;
        private float _lastCheckTime;
        private float _lastPlayerSpeed;
        private float _currentDay;

        private int _gridSize;
        private float _chunkSize;
        private _Game.Systems.Time.TimeManager _timeManager;

        public int CurrentPlayerChunk { get; private set; } = -1;

        // 公开属性（供 DebugPanel 读取）
        public int PreloadQueueCount => _preloadQueue.Count;
        public float LastPlayerSpeed => _lastPlayerSpeed;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Initialize();
        }

        void Start()
        {
            if (playerTransform == null)
                playerTransform = PlayerRegistry.Transform;
            _timeManager = ServiceLocator.Get<_Game.Systems.Time.TimeManager>();

            // 注册容器刷新处理器
            if (RefreshHub.Instance != null)
            {
                RefreshHub.Instance.Register(new _Game.Systems.WorldContainer.ContainerRefreshHandler());
            }

            // 自动发现场景中所有 WorldContainer 并注册
            AutoRegisterContainers();
        }

        void Update()
        {
            if (playerTransform == null) return;

            _currentDay = _timeManager != null ? _timeManager.CurrentDay : 0;
            float playerSpeed = _lastPlayerSpeed;

            // 检查条件：移动超过阈值 OR 超过时间间隔
            float distMoved = Vector3.Distance(playerTransform.position, _lastCheckPosition);
            float timeSinceCheck = UnityEngine.Time.time - _lastCheckTime;

            if (distMoved >= GameConstants.CHUNK_CHECK_DISTANCE
                || timeSinceCheck >= GameConstants.CHUNK_CHECK_INTERVAL)
            {
                CheckChunks();
                _lastCheckPosition = playerTransform.position;
                _lastCheckTime = UnityEngine.Time.time;
                _lastPlayerSpeed = distMoved / Mathf.Max(timeSinceCheck, 0.01f);
            }

            // 每帧处理预热队列
            ProcessPreloadQueue();
        }

        /// <summary>
        /// 初始化 100 个 Chunk (10×10 网格, 800m×800m)
        /// </summary>
        public void Initialize()
        {
            _gridSize = GameConstants.CHUNK_GRID_SIZE;
            _chunkSize = GameConstants.RUNTIME_CHUNK_SIZE;
            _chunks = new RuntimeChunk[_gridSize * _gridSize];

            for (int z = 0; z < _gridSize; z++)
            {
                for (int x = 0; x < _gridSize; x++)
                {
                    int id = z * _gridSize + x;
                    var chunk = new RuntimeChunk
                    {
                        chunkId = id,
                        gridPos = new Vector2Int(x, z),
                        state = ChunkState.Unloaded,
                        preloadStage = PreloadStage.Done
                    };

                    // 创建 Chunk 父节点
                    var go = new GameObject($"Chunk_{x}_{z}");
                    go.transform.SetParent(transform);
                    go.SetActive(false);
                    chunk.chunkParent = go.transform;

                    _chunks[id] = chunk;
                }
            }

            _lastCheckPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
            _lastCheckTime = UnityEngine.Time.time;

            Debug.Log($"[ChunkManager] 初始化完成: {_chunks.Length} Chunks ({_gridSize}×{_gridSize}), 覆盖 {_gridSize * _chunkSize}m×{_gridSize * _chunkSize}m");
        }

        // ============================================================
        // 三级切换
        // ============================================================

        void CheckChunks()
        {
            if (_chunks == null) return;

            int playerChunk = GetChunkId(playerTransform.position);
            CurrentPlayerChunk = playerChunk;

            int loadRadius = ChunkQualityConfig.GetLoadRadius(quality);
            int preloadBase = ChunkQualityConfig.GetPreloadBaseRadius(quality);
            int preloadExtra = GetDynamicPreloadExtra(_lastPlayerSpeed);
            int preloadRadius = preloadBase + preloadExtra;
            int unloadRadius = preloadRadius + 1;

            for (int i = 0; i < _chunks.Length; i++)
            {
                int dist = ChunkDistance(playerChunk, i);

                if (dist <= loadRadius)
                {
                    EnterLoaded(i);
                }
                else if (dist <= preloadRadius)
                {
                    EnterPreloaded(i);
                }
                else
                {
                    EnterUnloaded(i);
                }
            }
        }

        void EnterLoaded(int chunkId)
        {
            var chunk = _chunks[chunkId];
            if (chunk.state == ChunkState.Loaded) return;

            // 如果正在预热中，强制完成
            if (chunk.state == ChunkState.Preloaded && chunk.preloadStage != PreloadStage.Done)
            {
                FinishPreload(chunk);
            }

            chunk.state = ChunkState.Loaded;
            chunk.preloadStage = PreloadStage.Done;

            if (chunk.chunkParent != null)
                chunk.chunkParent.gameObject.SetActive(true);

            ActivateZombiesInChunk(chunkId);
            RefreshHub.Instance?.OnChunkLoad(chunkId, _currentDay);
        }

        void EnterPreloaded(int chunkId)
        {
            var chunk = _chunks[chunkId];

            if (chunk.state == ChunkState.Unloaded)
            {
                chunk.state = ChunkState.Preloaded;
                chunk.preloadStage = PreloadStage.Stage0_ActivateGameObjects;

                if (chunk.chunkParent != null)
                    chunk.chunkParent.gameObject.SetActive(true);

                _preloadQueue.Enqueue(chunkId);
            }
            else if (chunk.state == ChunkState.Loaded)
            {
                chunk.state = ChunkState.Preloaded;
                // 从 Loaded 降级: 休眠僵尸，保留容器状态
                DeactivateZombiesInChunk(chunkId);
            }
        }

        void EnterUnloaded(int chunkId)
        {
            var chunk = _chunks[chunkId];
            if (chunk.state == ChunkState.Unloaded) return;

            chunk.state = ChunkState.Unloaded;
            chunk.preloadStage = PreloadStage.Done;

            // 完全卸载：销毁僵尸，重置刷怪预算
            DestroyZombiesInChunk(chunkId);
            Zombie.ZombieSpawner.Instance?.OnChunkUnloaded(chunkId);
            RefreshHub.Instance?.OnChunkUnload(chunkId);

            if (chunk.chunkParent != null)
                chunk.chunkParent.gameObject.SetActive(false);
        }

        // ============================================================
        // 异步预热队列
        // ============================================================

        void ProcessPreloadQueue()
        {
            int stepsThisFrame = 0;
            int maxSteps = GameConstants.PRELOAD_STEPS_PER_FRAME;

            while (_preloadQueue.Count > 0 && stepsThisFrame < maxSteps)
            {
                int chunkId = _preloadQueue.Peek();
                var chunk = _chunks[chunkId];

                // 如果 Chunk 已经离开预加载层，出队
                if (chunk.state != ChunkState.Preloaded)
                {
                    _preloadQueue.Dequeue();
                    continue;
                }

                if (ProcessOnePreloadStep(chunk))
                {
                    // 步骤完成 → 继续下一步
                    stepsThisFrame++;
                }
                else
                {
                    // 当前步骤不能继续 → 等下一帧
                    break;
                }

                if (chunk.preloadStage == PreloadStage.Done)
                {
                    _preloadQueue.Dequeue();
                }
            }
        }

        bool ProcessOnePreloadStep(RuntimeChunk chunk)
        {
            float speed = _lastPlayerSpeed;

            switch (chunk.preloadStage)
            {
                case PreloadStage.Stage0_ActivateGameObjects:
                    // 激活已注册的 GameObject（容器、建筑等）
                    // 子 GameObject 在 SetParent 时已放置在 chunkParent 下
                    // SetActive(true) 已经在 EnterPreloaded 中处理
                    chunk.preloadStage = PreloadStage.Stage1_RebuildContainers;
                    return true;

                case PreloadStage.Stage1_RebuildContainers:
                    RefreshHub.Instance?.OnChunkLoad(chunk.chunkId, _currentDay);
                    chunk.preloadStage = PreloadStage.Stage2_RespawnNPCs;
                    return true;

                case PreloadStage.Stage2_RespawnNPCs:
                    // 高速减载：跳过 NPC 激活
                    if (speed > GameConstants.SPEED_THRESHOLD_FAST)
                    {
                        chunk.preloadStage = PreloadStage.Done;
                        return true;
                    }
                    // Phase A: 初始刷怪（首次进入该 Chunk 时）
                    Zombie.ZombieSpawner.Instance?.SpawnInitial(chunk.chunkId, IsNightTime());
                    ActivateZombiesInChunk(chunk.chunkId);
                    chunk.preloadStage = PreloadStage.Done;
                    return true;

                default:
                    return false;
            }
        }

        void FinishPreload(RuntimeChunk chunk)
        {
            while (chunk.preloadStage != PreloadStage.Done)
            {
                ProcessOnePreloadStep(chunk);
            }
        }

        // ============================================================
        // 动态预热带
        // ============================================================

        int GetDynamicPreloadExtra(float speed)
        {
            if (speed < GameConstants.SPEED_THRESHOLD_NORMAL) return 0;
            if (speed < GameConstants.SPEED_THRESHOLD_FAST) return 1;
            if (speed < GameConstants.SPEED_THRESHOLD_EXTREME) return 2;
            return 3;
        }

        // ============================================================
        // 静态工具
        // ============================================================

        public static int GetChunkId(Vector3 worldPos)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(worldPos.x / GameConstants.RUNTIME_CHUNK_SIZE), 0, GameConstants.CHUNK_GRID_SIZE - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(worldPos.z / GameConstants.RUNTIME_CHUNK_SIZE), 0, GameConstants.CHUNK_GRID_SIZE - 1);
            return z * GameConstants.CHUNK_GRID_SIZE + x;
        }

        public static Vector2 GetChunkCenter(int chunkId)
        {
            int x = chunkId % GameConstants.CHUNK_GRID_SIZE;
            int z = chunkId / GameConstants.CHUNK_GRID_SIZE;
            float half = GameConstants.RUNTIME_CHUNK_SIZE * 0.5f;
            return new Vector2(x * GameConstants.RUNTIME_CHUNK_SIZE + half, z * GameConstants.RUNTIME_CHUNK_SIZE + half);
        }

        public RuntimeChunk GetChunk(int chunkId)
        {
            return (chunkId >= 0 && chunkId < _chunks.Length) ? _chunks[chunkId] : null;
        }

        int ChunkDistance(int a, int b)
        {
            if (a < 0 || b < 0) return int.MaxValue;
            int ax = a % _gridSize, az = a / _gridSize;
            int bx = b % _gridSize, bz = b / _gridSize;
            return Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(az - bz));
        }

        // ============================================================
        // 自动注册（Start 时调用）
        // ============================================================

        void AutoRegisterContainers()
        {
            var registry = _Game.Systems.WorldContainer.ContainerRegistry.Instance;
            if (registry == null) return;

            var allContainers = ServiceLocator.GetAll<_Game.Systems.WorldContainer.WorldContainer>();
            foreach (var wc in allContainers)
            {
                int chunkId = GetChunkId(wc.transform.position);

                // 自动分配 ID（如果未在 Inspector 中设置）
                if (wc.containerId == 0)
                    wc.containerId = registry.Register(chunkId, wc.profile);
                else
                    registry.Register(chunkId, wc.profile, wc.containerId);

                // 将容器 GameObject 移到 Chunk 父节点下
                var chunk = GetChunk(chunkId);
                if (chunk?.chunkParent != null)
                    wc.transform.SetParent(chunk.chunkParent);

                // 注册到 ChunkManager
                RegisterContainer(wc.containerId, chunkId);
            }

            Debug.Log($"[ChunkManager] 自动注册 {allContainers.Length} 个 WorldContainer");
        }

        // ============================================================
        // 对象注册（供各系统在初始化时调用）
        // ============================================================

        public void RegisterContainer(int containerId, int chunkId)
        {
            var chunk = GetChunk(chunkId);
            chunk?.containerIds.Add(containerId);
        }

        public void RegisterBuilding(int buildingId, int chunkId)
        {
            var chunk = GetChunk(chunkId);
            chunk?.buildingIds.Add(buildingId);
        }

        public void RegisterWorldItem(int itemId, int chunkId)
        {
            var chunk = GetChunk(chunkId);
            if (chunk != null && chunk.worldItemIds.Count < GameConstants.PLAYER_DROP_PER_CHUNK_CAP)
                chunk.worldItemIds.Add(itemId);
        }

        public void RegisterZombie(ZombieStateMachine zombie, int chunkId)
        {
            var chunk = GetChunk(chunkId);
            chunk?.zombieInstances.Add(zombie);
        }

        void ActivateZombiesInChunk(int chunkId)
        {
            var chunk = GetChunk(chunkId);
            if (chunk == null) return;
            for (int i = chunk.zombieInstances.Count - 1; i >= 0; i--)
            {
                var zombie = chunk.zombieInstances[i];
                if (zombie == null)
                {
                    chunk.zombieInstances.RemoveAt(i);
                    continue;
                }
                if (zombie.Agent != null) zombie.Agent.enabled = true;
                // AIAgent 通过 EventBus 自行处理感知，不再需要 ZombieAwarenessSystem
            }
        }

        void DeactivateZombiesInChunk(int chunkId)
        {
            var chunk = GetChunk(chunkId);
            if (chunk == null) return;
            for (int i = chunk.zombieInstances.Count - 1; i >= 0; i--)
            {
                var zombie = chunk.zombieInstances[i];
                if (zombie == null)
                {
                    chunk.zombieInstances.RemoveAt(i);
                    continue;
                }
                if (zombie.Agent != null) zombie.Agent.enabled = false;
            }
        }

        void DestroyZombiesInChunk(int chunkId)
        {
            var chunk = GetChunk(chunkId);
            if (chunk == null) return;
            for (int i = chunk.zombieInstances.Count - 1; i >= 0; i--)
            {
                var zombie = chunk.zombieInstances[i];
                if (zombie == null)
                {
                    chunk.zombieInstances.RemoveAt(i);
                    continue;
                }
                if (zombie.gameObject != null)
                    Destroy(zombie.gameObject);
            }
            chunk.zombieInstances.Clear();
        }

        bool IsNightTime()
        {
            if (_timeManager == null) return false;
            float h = _timeManager.CurrentHour;
            return h >= 18f || h < 6f;
        }

        public void GetChunkCounts(out int loaded, out int preloaded, out int unloaded)
        {
            loaded = 0; preloaded = 0; unloaded = 0;
            if (_chunks == null) return;
            for (int i = 0; i < _chunks.Length; i++)
            {
                switch (_chunks[i].state)
                {
                    case ChunkState.Loaded: loaded++; break;
                    case ChunkState.Preloaded: preloaded++; break;
                    case ChunkState.Unloaded: unloaded++; break;
                }
            }
        }
    }
}
