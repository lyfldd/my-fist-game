using System;
using _Game.Config;

namespace _Game.Systems.Inventory
{
    /// <summary>
    /// 放在矩形背包中的物品 + 位置信息
    /// </summary>
    [Serializable]
    public struct PlacedItem
    {
        /// <summary> 唯一实例 ID（存档系统用，跨存读定位物品） </summary>
        public int instanceId;
        public ItemData itemData;
        public int count;
        public int gridX;
        public int gridY;

        /// <summary> 是否已旋转90°（CW）</summary>
        public bool rotated;
        /// <summary> 是否为腰带武器槽占位幽灵格（isGhost=true 时 itemData 为 null）</summary>
        public bool isGhost;
        /// <summary> 幽灵格来源武器槽（KnifeBelt / SidearmBelt）</summary>
        public EquipSlot ghostSourceSlot;

        /// <summary> 前置A2：当前耐久（0=满耐久/无耐久系统）</summary>
        public float itemDurability;
        /// <summary> 前置A2：已修理次数（0=未修理过）</summary>
        public int repairCount;

        public PlacedItem(ItemData itemData, int count, int gridX, int gridY)
        {
            this.instanceId = 0; // 由 Inventory 分配
            this.itemData = itemData;
            this.count = count;
            this.gridX = gridX;
            this.gridY = gridY;
            this.rotated = false;
            this.isGhost = false;
            this.ghostSourceSlot = EquipSlot.None;
            this.itemDurability = 0f;
            this.repairCount = 0;
        }

        /// <summary> 移动/旋转时用：从原物品拷贝所有字段，仅覆盖位置和旋转 </summary>
        public static PlacedItem CloneWithPosition(PlacedItem src, int newGridX, int newGridY, bool newRotated)
        {
            return new PlacedItem
            {
                instanceId = src.instanceId,
                itemData = src.itemData,
                count = src.count,
                gridX = newGridX,
                gridY = newGridY,
                rotated = newRotated,
                isGhost = src.isGhost,
                ghostSourceSlot = src.ghostSourceSlot,
                itemDurability = src.itemDurability,
                repairCount = src.repairCount,
            };
        }

        /// <summary> 创建幽灵占位格（腰带武器槽容量占用）</summary>
        public static PlacedItem Ghost(int gridX, int gridY, EquipSlot sourceSlot)
        {
            return new PlacedItem
            {
                itemData = null,
                count = 0,
                gridX = gridX,
                gridY = gridY,
                isGhost = true,
                ghostSourceSlot = sourceSlot
            };
        }

        /// <summary> 当前重量 = 单件重量 × 数量 </summary>
        public float TotalWeight => itemData != null ? itemData.weight * count : 0f;

        /// <summary> 在网格中占几行几列（考虑旋转） </summary>
        public int GridWidth => isGhost ? 1 : (itemData != null ? (rotated ? itemData.gridHeight : itemData.gridWidth) : 1);
        public int GridHeight => isGhost ? 1 : (itemData != null ? (rotated ? itemData.gridWidth : itemData.gridHeight) : 1);
        public int OriginalWidth => itemData != null ? itemData.gridWidth : 1;
        public int OriginalHeight => itemData != null ? itemData.gridHeight : 1;
    }
}
