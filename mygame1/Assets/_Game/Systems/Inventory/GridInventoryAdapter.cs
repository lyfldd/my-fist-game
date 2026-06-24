using System.Collections.Generic;

namespace _Game.Systems.Inventory
{
    /// <summary>
    /// GridInventory2D 适配器 — 从旧 InventoryContainer 转换/同步。
    /// 过渡期用，等全量迁移完成后删除。
    /// </summary>
    public static class GridInventoryAdapter
    {
        static Dictionary<InventoryContainer, GridInventory2D> _cache = new();

        /// <summary> 获取或创建对应的 GridInventory2D（与旧容器同步） </summary>
        public static GridInventory2D ToGrid(this InventoryContainer container)
        {
            if (container == null) return null;
            if (_cache.TryGetValue(container, out var g)) return g;

            g = new GridInventory2D(container.gridWidth, container.gridHeight);
            // 迁移现有物品
            for (int i = container.placedItems.Count - 1; i >= 0; i--)
            {
                var p = container.placedItems[i];
                if (p.itemData == null) continue;
                g.Place(p.itemData, p.count, p.gridX, p.gridY, p.rotated,
                    p.instanceId, p.itemDurability, p.repairCount);
            }
            _cache[container] = g;
            return g;
        }

        /// <summary> 清除缓存（容器被销毁后调用） </summary>
        public static void InvalidateCache(InventoryContainer container)
        {
            _cache.Remove(container);
        }

        /// <summary> 测试用：清除所有缓存 </summary>
        public static void ClearCache() => _cache.Clear();
    }
}
