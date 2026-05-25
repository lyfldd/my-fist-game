using System.Collections.Generic;
using UnityEngine;
using _Game.Config;

namespace _Game.Core
{
    /// <summary>
    /// 背包数据快照（纯数据，供 UI 只读使用）
    /// 由 Inventory 在每次变化时构建并发布
    /// 用于解耦 UI 层与 Inventory 系统
    /// </summary>
    public struct InventoryViewData
    {
        public List<ContainerViewData> containers;
        public Dictionary<EquipSlot, string> equippedNames;
        public float totalArmor;
        public float totalWarmth;
        public float currentWeight;
        public float maxWeight;
        public bool isHardOverloaded;
        public float overloadRatio;
    }

    public struct ContainerViewData
    {
        public string containerName;
        public EquipSlot equipSlot;
        public int gridWidth;
        public int gridHeight;
        public List<ItemOnGrid> items;
    }

    public struct ItemOnGrid
    {
        public string itemName;
        public int count;
        public int gridX;
        public int gridY;
        public int gridWidth;
        public int gridHeight;
        public Sprite icon;
        public ItemData itemData; // 仍需要 ItemData 用于 useTime/effects
    }
}
