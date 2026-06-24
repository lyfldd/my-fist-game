using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Inventory
{
    /// <summary>
    /// 单个存储容器 — 对内委托给 GridInventory2D，对外 API 不变。
    /// </summary>
    [System.Serializable]
    public class InventoryContainer
    {
        [Header("容器信息")]
        public string containerName = "容器";
        public EquipSlot equipSlot = EquipSlot.None;
        public KeyCode toggleKey = KeyCode.None;

        [Header("网格尺寸")]
        public int gridWidth = 4;
        public int gridHeight = 3;

        [Header("物品列表")]
        public List<PlacedItem> placedItems = new List<PlacedItem>();

        [System.NonSerialized] GridInventory2D _grid;
        [System.NonSerialized] bool _gridDirty;

        GridInventory2D EnsureGrid()
        {
            if (_grid != null && !_gridDirty) return _grid;
            _grid = new GridInventory2D(gridWidth, gridHeight);
            // 从 placedItems 迁移
            foreach (var p in placedItems)
                if (p.itemData != null)
                    _grid.Place(p.itemData, p.count, p.gridX, p.gridY, p.rotated,
                        p.instanceId, p.itemDurability, p.repairCount);
            _gridDirty = false;
            return _grid;
        }

        void SyncPlacedItems()
        {
            placedItems.Clear();
            var g = EnsureGrid();
            foreach (var s in g.SlotList)
            {
                if (s.isGhost) { placedItems.Add(PlacedItem.Ghost(s.x, s.y, s.ghostSourceSlot)); continue; }
                placedItems.Add(new PlacedItem(s.itemData, s.count, s.x, s.y)
                {
                    instanceId = s.instanceId, rotated = s.rotated,
                    itemDurability = s.itemDurability, repairCount = s.repairCount,
                });
            }
            _gridDirty = false;
        }

        /// <summary> 调整网格尺寸，溢出物品返回列表 </summary>
        public List<PlacedItem> Resize(int newW, int newH)
        {
            gridWidth = newW; gridHeight = newH;
            var overflow = EnsureGrid().Resize(newW, newH);
            var result = new List<PlacedItem>();
            foreach (var s in overflow)
                result.Add(new PlacedItem(s.itemData, s.count, 0, 0)
                {
                    instanceId = s.instanceId, rotated = s.rotated,
                    itemDurability = s.itemDurability, repairCount = s.repairCount,
                });
            return result;
        }

        /// <summary> 外部直接读写 placedItems 后需调此方法标记脏 </summary>
        public void MarkDirty() { _gridDirty = true; }

        // ===== 属性 ===

        public PlacedItem? GetItemAt(int x, int y)
        {
            var s = EnsureGrid().GetSlotAt(x, y);
            if (s.HasValue && s.Value.itemData != null && !s.Value.isGhost)
                return new PlacedItem(s.Value.itemData, s.Value.count, s.Value.x, s.Value.y)
                {
                    instanceId = s.Value.instanceId, rotated = s.Value.rotated,
                    itemDurability = s.Value.itemDurability, repairCount = s.Value.repairCount,
                };
            return null;
        }

        public int TotalCells => gridWidth * gridHeight;

        public int UsedCells
        {
            get
            {
                int cells = 0;
                foreach (var s in EnsureGrid().SlotList)
                    if (!s.isGhost) cells += s.w * s.h;
                return cells;
            }
        }

        public float CurrentWeight => EnsureGrid().TotalWeight;

        // ===== 网格检测 ===

        public bool IsSpaceFree(int x, int y, int w, int h)
            => EnsureGrid().CanPlace(x, y, w, h);

        public bool IsSpaceFreeFor(int x, int y, int w, int h, int excludeX, int excludeY, int excludeW, int excludeH)
        {
            // 找到排除位置的 slotId
            int excludeId = 0;
            var s = EnsureGrid().GetSlotAt(excludeX, excludeY);
            if (s.HasValue) excludeId = s.Value.slotId;
            return EnsureGrid().CanPlace(x, y, w, h, excludeId);
        }

        public bool FindSpace(int w, int h, out int ox, out int oy)
            => EnsureGrid().FindSpace(w, h, out ox, out oy);

        public void MoveItem(int fromX, int fromY, int toX, int toY, bool rotated)
        {
            var s = EnsureGrid().GetSlotAt(fromX, fromY);
            if (s.HasValue) EnsureGrid().Move(s.Value.slotId, toX, toY);
        }

        // ===== 增删 ===

        public int AddItem(ItemData item, int count, float overloadWeight)
        {
            if (item == null || count <= 0) return 0;
            if (CurrentWeight + count * item.weight > overloadWeight) return 0;
            return EnsureGrid().TryAdd(item, count, overloadWeight);
        }

        public bool RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;
            int remaining = count;
            foreach (var s in EnsureGrid().SlotList)
            {
                if (s.isGhost || s.itemData != item) continue;
                int take = Mathf.Min(s.count, remaining);
                if (take < s.count)
                {
                    var slot = s; slot.count -= take;
                    EnsureGrid().SlotDict[slot.slotId] = slot;
                }
                else EnsureGrid().Remove(s.slotId);
                remaining -= take;
                if (remaining <= 0) break;
            }
            return count - remaining > 0;
        }

        public bool RemoveItemAt(int gridX, int gridY, int count)
        {
            var s = EnsureGrid().GetSlotAt(gridX, gridY);
            if (!s.HasValue || s.Value.itemData == null) return false;
            if (s.Value.count > count)
            {
                var slot = s.Value; slot.count -= count;
                EnsureGrid().SlotDict[slot.slotId] = slot;
            }
            else EnsureGrid().Remove(s.Value.slotId);
            return true;
        }

        public int GetItemCount(ItemData item)
        {
            int total = 0;
            foreach (var s in EnsureGrid().SlotList)
                if (s.itemData == item) total += s.count;
            return total;
        }
    }
}
