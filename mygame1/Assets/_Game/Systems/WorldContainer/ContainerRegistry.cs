using System.Collections.Generic;
using _Game.Config;
using _Game.Systems.Inventory;
using UnityEngine;

namespace _Game.Systems.WorldContainer
{
    /// <summary>
    /// 容器注册中心 — 全量 ContainerRecord 字典 + 懒加载。
    /// 挂载到场景单例 GameObject。
    /// </summary>
    public class ContainerRegistry : MonoBehaviour
    {
        public static ContainerRegistry Instance { get; private set; }

        private readonly Dictionary<int, ContainerRecord> _records = new Dictionary<int, ContainerRecord>();
        private readonly Dictionary<int, List<ContainerRecord>> _chunkIndex = new Dictionary<int, List<ContainerRecord>>();

        private int _nextId;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public int Register(int chunkId, ContainerLootProfile profile, int existingId = -1)
        {
            int id = existingId >= 0 ? existingId : _nextId++;

            var record = new ContainerRecord
            {
                containerId = id,
                isOpened = false,
                openedGameDay = 0,
                chunkId = chunkId,
                profile = profile,
                container = null
            };

            _records[id] = record;

            if (!_chunkIndex.TryGetValue(chunkId, out var list))
            {
                list = new List<ContainerRecord>();
                _chunkIndex[chunkId] = list;
            }
            list.Add(record);

            return id;
        }

        public InventoryContainer GetOrCreate(int containerId)
        {
            if (!_records.TryGetValue(containerId, out var record))
            {
                Debug.LogWarning($"ContainerRegistry: 未知容器 id={containerId}");
                return null;
            }

            if (record.container == null && record.profile != null)
            {
                record.container = new InventoryContainer
                {
                    containerName = record.profile.displayName,
                    gridWidth = record.profile.gridWidth,
                    gridHeight = record.profile.gridHeight
                };
            }

            return record.container;
        }

        public void MarkOpened(int containerId, float gameDay)
        {
            if (_records.TryGetValue(containerId, out var record))
            {
                record.isOpened = true;
                record.openedGameDay = gameDay;
            }
        }

        public List<ContainerRecord> GetChunkRecords(int chunkId)
        {
            return _chunkIndex.TryGetValue(chunkId, out var list) ? list : null;
        }

        public bool IsOpened(int containerId)
        {
            return _records.TryGetValue(containerId, out var record) && record.isOpened;
        }

        public ContainerRecord GetRecord(int containerId)
        {
            _records.TryGetValue(containerId, out var record);
            return record;
        }

        public void ReleaseContainer(int containerId)
        {
            if (_records.TryGetValue(containerId, out var record))
            {
                record.container = null;
            }
        }
    }
}
