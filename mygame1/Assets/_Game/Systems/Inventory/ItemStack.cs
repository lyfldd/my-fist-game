using System;
using _Game.Config;

namespace _Game.Systems.Inventory
{
    /// <summary>
    /// 物品堆叠数据
    /// 记录背包中某一种物品和它的数量
    /// </summary>
    [Serializable]
    public struct ItemStack
    {
        public ItemData itemData;
        public int count;

        public ItemStack(ItemData itemData, int count)
        {
            this.itemData = itemData;
            this.count = count;
        }

        /// <summary>
        /// 当前重量 = 单件重量 × 数量
        /// </summary>
        public float TotalWeight => itemData != null ? itemData.weight * count : 0f;
    }
}
