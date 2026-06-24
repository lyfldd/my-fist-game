using UnityEngine;
using _Game.Config;

namespace _Game.Systems.Inventory
{
    /// <summary>
    /// 容器视图快照 — UI 层渲染用的只读数据。事件驱动，按需生成。
    /// </summary>
    public readonly struct ContainerView
    {
        public readonly string id;
        public readonly EquipSlot equipSlot;
        public readonly int width, height;
        public readonly SlotData[] slots;
        public readonly int usedSlots, totalSlots;
        public readonly float totalWeight, maxWeight;

        public ContainerView(string id, EquipSlot equipSlot, int width, int height,
            SlotData[] slots, int usedSlots, int totalSlots, float totalWeight, float maxWeight)
        {
            this.id = id;
            this.equipSlot = equipSlot;
            this.width = width;
            this.height = height;
            this.slots = slots;
            this.usedSlots = usedSlots;
            this.totalSlots = totalSlots;
            this.totalWeight = totalWeight;
            this.maxWeight = maxWeight;
        }

        public static ContainerView FromGrid(string id, EquipSlot equipSlot, GridInventory2D grid, float maxWeight)
        {
            var list = new System.Collections.Generic.List<SlotData>();
            foreach (var s in grid.AllSlots())
            {
                float ratio = s.itemData != null && s.itemData.hasDurability && s.itemData.maxDurability > 0f
                    ? Mathf.Clamp01(s.itemDurability / s.itemData.maxDurability) : 1f;
                list.Add(new SlotData(s, ratio));
            }
            return new ContainerView(id, equipSlot, grid.Width, grid.Height,
                list.ToArray(), grid.UsedSlots, grid.TotalSlots, grid.TotalWeight, maxWeight);
        }
    }

    /// <summary>
    /// 单个物品槽的 UI 快照 — 仅渲染所需字段。
    /// </summary>
    public readonly struct SlotData
    {
        public readonly int slotId, x, y, w, h;
        public readonly string itemName;
        public readonly Sprite icon;
        public readonly int count;
        public readonly bool rotated;
        public readonly int instanceId;
        public readonly float durabilityRatio;
        public readonly bool isGhost;
        public readonly EquipSlot ghostSource;
        public readonly int repairCount;

        public SlotData(GridSlot s, float durabilityRatio)
        {
            slotId = s.slotId;
            x = s.x; y = s.y; w = s.w; h = s.h;
            itemName = s.itemData?.itemName ?? "";
            icon = s.itemData?.icon;
            count = s.count;
            rotated = s.rotated;
            instanceId = s.instanceId;
            this.durabilityRatio = durabilityRatio;
            isGhost = s.isGhost;
            ghostSource = s.ghostSourceSlot;
            repairCount = s.repairCount;
        }
    }
}
