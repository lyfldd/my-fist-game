using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Config;

namespace _Game.Systems.Inventory
{
    /// <summary>
    /// 统一二维网格容器 — 替换 InventoryContainer。
    /// 纯数据结构，不继承 MonoBehaviour，不引用 UI。
    /// int[] 扁平化存储: _grid[y * _width + x] = slotId, 0=空。
    /// </summary>
    [Serializable]
    public class GridInventory2D
    {
        [SerializeField] int _width;
        [SerializeField] int _height;
        [SerializeField] int[] _grid;
        [SerializeField] int _nextSlotId = 1;
        [SerializeField] List<GridSlot> _slotList = new();  // 用于序列化
        [SerializeField] float _totalWeight;

        // 运行时索引（非序列化）
        [NonSerialized] Dictionary<int, GridSlot> _slots;
        Dictionary<int, GridSlot> Slots
        {
            get
            {
                if (_slots == null)
                {
                    _slots = new Dictionary<int, GridSlot>();
                    if (_slotList != null)
                        foreach (var s in _slotList) _slots[s.slotId] = s;
                }
                return _slots;
            }
        }

        public int Width => _width;
        public int Height => _height;
        public int UsedSlots => Slots.Count;
        public int TotalSlots => _width * _height;
        public float TotalWeight => _totalWeight;
        public IReadOnlyList<GridSlot> SlotList => _slotList;
        public Dictionary<int, GridSlot> SlotDict => Slots;

        public GridInventory2D() { }
        public GridInventory2D(int w, int h)
        {
            _width = Mathf.Max(1, w);
            _height = Mathf.Max(1, h);
            _grid = new int[_width * _height];
            _slotList = new List<GridSlot>();
            _slots = new Dictionary<int, GridSlot>();
        }

        // ═══════════════════════════════════════════
        // 空间查询
        // ═══════════════════════════════════════════

        int Idx(int x, int y) => y * _width + x;
        bool InBounds(int x, int y, int w, int h) =>
            x >= 0 && y >= 0 && x + w <= _width && y + h <= _height;

        /// <summary> 指定区域是否空闲。excludeSlotId 用于旋转/移动时排除自身。 </summary>
        public bool CanPlace(int x, int y, int w, int h, int excludeSlotId = 0)
        {
            if (!InBounds(x, y, w, h)) return false;
            for (int cy = y; cy < y + h; cy++)
                for (int cx = x; cx < x + w; cx++)
                {
                    int v = _grid[Idx(cx, cy)];
                    if (v != 0 && v != excludeSlotId) return false;
                }
            return true;
        }

        /// <summary> 找第一个能放下 w×h 的空位 </summary>
        public bool FindSpace(int w, int h, out int ox, out int oy)
        {
            ox = oy = 0;
            for (int y = 0; y <= _height - h; y++)
                for (int x = 0; x <= _width - w; x++)
                    if (CanPlace(x, y, w, h)) { ox = x; oy = y; return true; }
            return false;
        }

        public GridSlot? GetSlotAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height) return null;
            int id = _grid[Idx(x, y)];
            return id > 0 && Slots.TryGetValue(id, out var s) ? s : (GridSlot?)null;
        }

        public GridSlot? GetSlot(int slotId) =>
            slotId > 0 && Slots.TryGetValue(slotId, out var s) ? s : (GridSlot?)null;

        // ═══════════════════════════════════════════
        // 放置
        // ═══════════════════════════════════════════

        /// <summary> 自动添加：堆叠→找空位，限制 maxWeight </summary>
        public int TryAdd(ItemData item, int count, float maxWeight)
        {
            if (item == null || count <= 0) return 0;
            int remaining = count;

            // 1. 堆叠到已有同类
            for (int i = _slotList.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var s = _slotList[i];
                if (s.isGhost || s.itemData != item) continue;
                if (s.count >= item.maxStack) continue;
                int canAdd = Mathf.Min(remaining, item.maxStack - s.count);
                float newWeight = _totalWeight + canAdd * item.weight;
                if (newWeight > maxWeight)
                    canAdd = Mathf.Max(0, (int)((maxWeight - _totalWeight) / item.weight));
                if (canAdd <= 0) break;
                s.count += canAdd;
                _slotList[i] = s;
                Slots[s.slotId] = s;
                _totalWeight += canAdd * item.weight;
                remaining -= canAdd;
            }

            // 2. 放不下就开新堆
            while (remaining > 0)
            {
                if (!FindSpace(item.gridWidth, item.gridHeight, out int x, out int y)) break;
                int perStack = Mathf.Min(remaining, item.maxStack);
                float newWeight = _totalWeight + perStack * item.weight;
                if (newWeight > maxWeight)
                    perStack = Mathf.Max(0, (int)((maxWeight - _totalWeight) / item.weight));
                if (perStack <= 0) break;
                int sid = Place(item, perStack, x, y, false, 0);
                if (sid == 0) break;
                remaining -= perStack;
            }

            return count - remaining;
        }

        /// <summary> 指定位置放置，返回 slotId。跨容器移动时先调此方法目标，成功再 Remove 源。失败返回 0。 </summary>
        public int Place(ItemData item, int count, int x, int y, bool rotated,
                         int instanceId = 0, float durability = 0f, int repairCount = 0)
        {
            if (item == null || count <= 0) return 0;
            int w = rotated ? item.gridHeight : item.gridWidth;
            int h = rotated ? item.gridWidth : item.gridHeight;
            if (!CanPlace(x, y, w, h)) return 0;

            int id = _nextSlotId++;
            var slot = new GridSlot
            {
                slotId = id, itemData = item, count = count,
                x = x, y = y, w = w, h = h, rotated = rotated,
                instanceId = instanceId,
                itemDurability = durability, repairCount = repairCount,
            };

            // 写 grid
            for (int cy = y; cy < y + h; cy++)
                for (int cx = x; cx < x + w; cx++)
                    _grid[Idx(cx, cy)] = id;

            _slotList.Add(slot);
            Slots[id] = slot;
            _totalWeight += item.weight * count;
            return id;
        }

        // ═══════════════════════════════════════════
        // 移除
        // ═══════════════════════════════════════════

        public void Remove(int slotId)
        {
            if (!Slots.TryGetValue(slotId, out var s)) return;
            for (int cy = s.y; cy < s.y + s.h; cy++)
                for (int cx = s.x; cx < s.x + s.w; cx++)
                    if (_grid[Idx(cx, cy)] == slotId) _grid[Idx(cx, cy)] = 0;
            Slots.Remove(slotId);
            for (int i = _slotList.Count - 1; i >= 0; i--)
                if (_slotList[i].slotId == slotId) { _slotList.RemoveAt(i); break; }
            _totalWeight -= s.itemData.weight * s.count;
        }

        public void RemoveByInstanceId(int instanceId)
        {
            if (instanceId <= 0) return;
            foreach (var s in _slotList)
                if (s.instanceId == instanceId) { Remove(s.slotId); return; }
        }

        // ═══════════════════════════════════════════
        // 移动
        // ═══════════════════════════════════════════

        public void Move(int slotId, int toX, int toY)
        {
            if (!Slots.TryGetValue(slotId, out var s)) return;
            if (!CanPlace(toX, toY, s.w, s.h, slotId)) return;
            // 清旧位置
            for (int cy = s.y; cy < s.y + s.h; cy++)
                for (int cx = s.x; cx < s.x + s.w; cx++)
                    if (_grid[Idx(cx, cy)] == slotId) _grid[Idx(cx, cy)] = 0;
            // 写新位置
            s.x = toX; s.y = toY;
            for (int cy = toY; cy < toY + s.h; cy++)
                for (int cx = toX; cx < toX + s.w; cx++)
                    _grid[Idx(cx, cy)] = slotId;
            Slots[slotId] = s;
            for (int i = 0; i < _slotList.Count; i++)
                if (_slotList[i].slotId == slotId) { _slotList[i] = s; break; }
        }

        // ═══════════════════════════════════════════
        // 尺寸调整
        // ═══════════════════════════════════════════

        /// <summary> 缩放网格。缩小导致溢出时返回溢出列表。 </summary>
        public List<GridSlot> Resize(int newW, int newH)
        {
            newW = Mathf.Max(1, newW);
            newH = Mathf.Max(1, newH);
            var overflow = new List<GridSlot>();

            // 找出越界物品
            for (int i = _slotList.Count - 1; i >= 0; i--)
            {
                var s = _slotList[i];
                if (s.x + s.w > newW || s.y + s.h > newH)
                    overflow.Add(s);
            }
            foreach (var s in overflow)
                Remove(s.slotId);

            // 重建 grid（仅当尺寸真的变了）
            if (newW != _width || newH != _height)
            {
                _width = newW;
                _height = newH;
                _grid = new int[_width * _height];
                // 重新写入剩余物品
                foreach (var kv in Slots)
                {
                    var s = kv.Value;
                    for (int cy = s.y; cy < s.y + s.h; cy++)
                        for (int cx = s.x; cx < s.x + s.w; cx++)
                            _grid[Idx(cx, cy)] = s.slotId;
                }
            }
            return overflow;
        }

        // ═══════════════════════════════════════════
        // 幽灵格
        // ═══════════════════════════════════════════

        public int PlaceGhost(int x, int y, EquipSlot source)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height) return 0;
            if (_grid[Idx(x, y)] != 0) return 0;
            int id = _nextSlotId++;
            var ghost = new GridSlot
            {
                slotId = id, itemData = null, count = 0,
                x = x, y = y, w = 1, h = 1,
                isGhost = true, ghostSourceSlot = source,
            };
            _grid[Idx(x, y)] = id;
            _slotList.Add(ghost);
            Slots[id] = ghost;
            return id;
        }

        public void RemoveGhosts(EquipSlot source)
        {
            for (int i = _slotList.Count - 1; i >= 0; i--)
            {
                var s = _slotList[i];
                if (s.isGhost && s.ghostSourceSlot == source)
                    Remove(s.slotId);
            }
        }

        /// <summary> 空闲格数 + 同 source 幽灵格数（幽灵格可复用） </summary>
        public int CountFreeForGhost(EquipSlot source, int needed)
        {
            int free = 0;
            int ghostCount = 0;
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                {
                    int v = _grid[Idx(x, y)];
                    if (v == 0) free++;
                    else if (Slots.TryGetValue(v, out var s) && s.isGhost && s.ghostSourceSlot == source)
                        ghostCount++;
                }
            // 幽灵格可复用 + 真正的空格
            return free + ghostCount >= needed ? free + ghostCount : 0;
        }

        // ═══════════════════════════════════════════
        // 遍历
        // ═══════════════════════════════════════════

        public IEnumerable<GridSlot> AllSlots()
        {
            foreach (var s in _slotList)
                if (!s.isGhost) yield return s;
        }

        public IEnumerable<GridSlot> SlotsFrom(EquipSlot source)
        {
            foreach (var s in _slotList)
                if (s.ghostSourceSlot == source) yield return s;
        }
    }

    /// <summary> 网格物品槽（替代 PlacedItem） </summary>
    [Serializable]
    public struct GridSlot
    {
        public int slotId;
        public ItemData itemData;
        public int count;
        public int x, y, w, h;
        public bool rotated;
        public int instanceId;
        public float itemDurability;
        public int repairCount;
        public bool isGhost;
        public EquipSlot ghostSourceSlot;

        public int GridWidth => rotated ? OriginalHeight : OriginalWidth;
        public int GridHeight => rotated ? OriginalWidth : OriginalHeight;
        public int OriginalWidth => itemData != null ? itemData.gridWidth : 1;
        public int OriginalHeight => itemData != null ? itemData.gridHeight : 1;
        public float TotalWeight => itemData != null ? itemData.weight * count : 0f;

        public static GridSlot CloneWithPosition(GridSlot src, int newX, int newY, bool newRotated)
        {
            return new GridSlot
            {
                slotId = src.slotId,
                itemData = src.itemData,
                count = src.count,
                x = newX, y = newY,
                w = newRotated ? src.OriginalHeight : src.OriginalWidth,
                h = newRotated ? src.OriginalWidth : src.OriginalHeight,
                rotated = newRotated,
                instanceId = src.instanceId,
                itemDurability = src.itemDurability,
                repairCount = src.repairCount,
                isGhost = src.isGhost,
                ghostSourceSlot = src.ghostSourceSlot,
            };
        }
    }
}
