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

        // ============================================================
        // 存档系统接口
        // ============================================================

        /// <summary> 导出所有容器记录（存档用） </summary>
        public System.Collections.Generic.List<SaveLoad.ContainerSaveRecord> GetAllRecords()
        {
            var result = new System.Collections.Generic.List<SaveLoad.ContainerSaveRecord>();
            foreach (var kv in _records)
            {
                var r = kv.Value;
                var csr = new SaveLoad.ContainerSaveRecord
                {
                    containerId = r.containerId,
                    isOpened = r.isOpened,
                    openedGameDay = r.openedGameDay,
                    profileName = r.profile != null ? r.profile.name : "",
                };

                // 导出容器内物品
                if (r.container != null && r.container.placedItems.Count > 0)
                {
                    csr.items = new System.Collections.Generic.List<SaveLoad.SlotSaveData>();
                    foreach (var pi in r.container.placedItems)
                    {
                        csr.items.Add(new SaveLoad.SlotSaveData
                        {
                            instanceId = pi.instanceId,
                            itemName = (pi.isGhost || pi.itemData == null) ? null : pi.itemData.itemName,
                            count = pi.count,
                            gridX = pi.gridX,
                            gridY = pi.gridY,
                            rotated = pi.rotated,
                            isGhost = pi.isGhost,
                            itemDurability = pi.itemDurability,
                            repairCount = pi.repairCount,
                        });
                    }
                }
                // csr.items = null 表示未生成过 loot，读档后由 profile 重新生成

                result.Add(csr);
            }
            return result;
        }

        /// <summary> 从存档恢复单个容器记录 </summary>
        public void RestoreRecord(SaveLoad.ContainerSaveRecord csr, SaveLoad.ItemCatalog itemCatalog)
        {
            if (csr == null) return;
            if (!_records.TryGetValue(csr.containerId, out var record)) return;

            record.isOpened = csr.isOpened;
            record.openedGameDay = csr.openedGameDay;

            if (csr.items != null)
            {
                // 确保容器已创建
                if (record.container == null && record.profile != null)
                {
                    record.container = new Inventory.InventoryContainer
                    {
                        containerName = record.profile.displayName,
                        gridWidth = record.profile.gridWidth,
                        gridHeight = record.profile.gridHeight,
                    };
                }

                if (record.container != null)
                {
                    record.container.placedItems.Clear();
                    foreach (var s in csr.items)
                    {
                        if (string.IsNullOrEmpty(s.itemName) || itemCatalog == null)
                            continue;
                        var itemData = itemCatalog.Find(s.itemName);
                        if (itemData != null)
                        {
                            record.container.placedItems.Add(new Inventory.PlacedItem
                            {
                                instanceId = s.instanceId,
                                itemData = itemData,
                                count = s.count,
                                gridX = s.gridX,
                                gridY = s.gridY,
                                rotated = s.rotated,
                            });
                        }
                    }
                }
            }
        }
    }
}
