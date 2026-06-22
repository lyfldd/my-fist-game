using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Inventory
{
    /// <summary>
    /// 单个存储容器（口袋、腰带、胸挂、背包等）
    /// 独立管理自己的网格和物品列表
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

        // ===== 属性 =====

        public int TotalCells => gridWidth * gridHeight;

        /// <summary> 获取 (x,y) 位置的物品（没有则返回 null）</summary>
        public PlacedItem? GetItemAt(int x, int y)
        {
            foreach (var p in placedItems)
            {
                if (x >= p.gridX && x < p.gridX + p.GridWidth &&
                    y >= p.gridY && y < p.gridY + p.GridHeight)
                    return p;
            }
            return null;
        }

        public int UsedCells
        {
            get
            {
                int cells = 0;
                foreach (var item in placedItems)
                    cells += item.GridWidth * item.GridHeight;
                return cells;
            }
        }

        public float CurrentWeight
        {
            get
            {
                float total = 0f;
                foreach (var item in placedItems)
                    total += item.TotalWeight;
                return total;
            }
        }

        // ===== 网格检测 =====

        public bool IsSpaceFree(int x, int y, int w, int h)
        {
            if (x < 0 || y < 0) return false;
            if (x + w > gridWidth || y + h > gridHeight) return false;

            foreach (var item in placedItems)
            {
                if (x < item.gridX + item.GridWidth &&
                    x + w > item.gridX &&
                    y < item.gridY + item.GridHeight &&
                    y + h > item.gridY)
                    return false;
            }
            return true;
        }

        /// <summary> 检测空间是否空闲，排除指定原位置的物品（用于原地旋转）</summary>
        public bool IsSpaceFreeFor(int x, int y, int w, int h, int excludeX, int excludeY, int excludeW, int excludeH)
        {
            if (x < 0 || y < 0) return false;
            if (x + w > gridWidth || y + h > gridHeight) return false;

            foreach (var item in placedItems)
            {
                if (item.gridX == excludeX && item.gridY == excludeY) continue;

                if (x < item.gridX + item.GridWidth &&
                    x + w > item.gridX &&
                    y < item.gridY + item.GridHeight &&
                    y + h > item.gridY)
                    return false;
            }
            return true;
        }

        public bool FindSpace(int w, int h, out int outX, out int outY)
        {
            for (int y = 0; y <= gridHeight - h; y++)
            {
                for (int x = 0; x <= gridWidth - w; x++)
                {
                    if (IsSpaceFree(x, y, w, h))
                    {
                        outX = x;
                        outY = y;
                        return true;
                    }
                }
            }
            outX = outY = 0;
            return false;
        }

        // ===== 增删操作 =====

        /// <summary> 添加物品，返回实际添加数量 </summary>
        public int AddItem(ItemData item, int count, float overloadWeight)
        {
            if (item == null || count <= 0) return 0;

            // 超载检查（由外部 Inventory 统一检查，这里留一个安全兜底）
            if (CurrentWeight + count * item.weight > overloadWeight)
                return 0;

            int remaining = count;

            // 堆叠已有的同类型 1x1 物品
            if (item.maxStack > 1)
            {
                for (int i = 0; i < placedItems.Count && remaining > 0; i++)
                {
                    var p = placedItems[i];
                    if (p.itemData == item && p.count < item.maxStack)
                    {
                        int canAdd = item.maxStack - p.count;
                        int toAdd = Mathf.Min(canAdd, remaining);

                        float newWeight = CurrentWeight + toAdd * item.weight;
                        if (newWeight > overloadWeight)
                        {
                            toAdd = Mathf.FloorToInt((overloadWeight - (CurrentWeight - p.TotalWeight + p.count * item.weight)) / item.weight);
                            if (toAdd <= 0) break;
                        }

                        p.count += toAdd;
                        placedItems[i] = p;
                        remaining -= toAdd;
                    }
                }
            }

            // 找空位放新物品
            while (remaining > 0)
            {
                int perStack = Mathf.Min(remaining, item.maxStack);

                if (!FindSpace(item.gridWidth, item.gridHeight, out int fx, out int fy))
                    break;

                float weightAdd = perStack * item.weight;
                if (CurrentWeight + weightAdd > overloadWeight)
                {
                    perStack = Mathf.FloorToInt((overloadWeight - CurrentWeight) / item.weight);
                    if (perStack <= 0) break;
                }

                var placed = new PlacedItem(item, perStack, fx, fy);
                placedItems.Add(placed);
                remaining -= perStack;
            }

            return count - remaining;
        }

        /// <summary> 移除物品，返回是否成功 </summary>
        public bool RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            int remaining = count;
            for (int i = placedItems.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var p = placedItems[i];
                if (p.itemData == item)
                {
                    if (p.count > remaining)
                    {
                        p.count -= remaining;
                        placedItems[i] = p;
                        remaining = 0;
                    }
                    else
                    {
                        remaining -= p.count;
                        placedItems.RemoveAt(i);
                    }
                }
            }

            return count - remaining > 0;
        }

        /// <summary> 按格子坐标精确移除指定数量的物品（用于拖拽，防止同类型误扣）</summary>
        public bool RemoveItemAt(int gridX, int gridY, int count)
        {
            for (int i = 0; i < placedItems.Count; i++)
            {
                var p = placedItems[i];
                if (p.gridX == gridX && p.gridY == gridY && p.itemData != null)
                {
                    if (p.count > count)
                    {
                        p.count -= count;
                        placedItems[i] = p;
                    }
                    else
                    {
                        placedItems.RemoveAt(i);
                    }
                    return true;
                }
            }
            return false;
        }

        public int GetItemCount(ItemData item)
        {
            int total = 0;
            foreach (var p in placedItems)
            {
                if (p.itemData == item)
                    total += p.count;
            }
            return total;
        }
    }
}
