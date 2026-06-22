using System;
using System.Collections.Generic;
using System.Linq;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 背包/装备存档数据。7 个容器 + 装备字典 + 武器弹药。
    /// 物品用 instanceId 定位，itemName 做键（通过 ItemCatalog 查找 ItemData）。
    /// </summary>
    [Serializable]
    public class InventorySaveData : ICloneable
    {
        public List<ContainerSaveData> containers;
        public Dictionary<string, EquippedItemSaveData> equippedItems; // EquipSlot名 → 装备来源
        public Dictionary<string, int> ammoReserves;                   // EquipSlot名 → 弹夹子弹数

        public object Clone()
        {
            return new InventorySaveData
            {
                containers = this.containers?.Select(c => c.Clone() as ContainerSaveData).ToList(),
                equippedItems = this.equippedItems?.ToDictionary(kv => kv.Key, kv => kv.Value.Clone() as EquippedItemSaveData),
                ammoReserves = this.ammoReserves != null ? new Dictionary<string, int>(this.ammoReserves) : null,
            };
        }
    }

    /// <summary>
    /// 装备物品来源。用 PlacedItem.instanceId 定位（不依赖列表顺序）。
    /// </summary>
    [Serializable]
    public class EquippedItemSaveData : ICloneable
    {
        public string equipSlotName;   // EquipSlot 枚举名
        public int itemInstanceId;     // PlacedItem.instanceId

        public object Clone()
        {
            return new EquippedItemSaveData { equipSlotName = this.equipSlotName, itemInstanceId = this.itemInstanceId };
        }
    }

    [Serializable]
    public class ContainerSaveData : ICloneable
    {
        public string containerName;     // 中文名
        public string equipSlotName;     // EquipSlot 枚举名（双保险）
        public int gridWidth, gridHeight;
        public List<SlotSaveData> slots;

        public object Clone()
        {
            return new ContainerSaveData
            {
                containerName = this.containerName,
                equipSlotName = this.equipSlotName,
                gridWidth = this.gridWidth, gridHeight = this.gridHeight,
                slots = this.slots?.Select(s => s.Clone() as SlotSaveData).ToList(),
            };
        }
    }

    [Serializable]
    public class SlotSaveData : ICloneable
    {
        public int instanceId;           // PlacedItem.instanceId（0 = 空格子）
        public string itemName;          // ItemData.itemName（null/空 = 空格子）
        public int count;
        public int gridX, gridY;
        public bool rotated;
        public bool isGhost;
        public string ghostSourceSlot;   // EquipSlot 名

        public object Clone()
        {
            return new SlotSaveData
            {
                instanceId = this.instanceId,
                itemName = this.itemName,
                count = this.count,
                gridX = this.gridX, gridY = this.gridY,
                rotated = this.rotated,
                isGhost = this.isGhost,
                ghostSourceSlot = this.ghostSourceSlot,
            };
        }
    }
}
