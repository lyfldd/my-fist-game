using System.Collections.Generic;
using UnityEngine;
using _Game.Config;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人独立背包。6×5格(30格)，200kg负重。
    /// 右臂武器弹药从此背包消耗。
    /// </summary>
    public class AIBotInventory : MonoBehaviour
    {
        public const int GRID_WIDTH = 6;
        public const int GRID_HEIGHT = 5;
        public const int TOTAL_SLOTS = 30;
        public const float MAX_WEIGHT = 200f;

        [SerializeField] private List<AIBotInventorySlot> slots = new List<AIBotInventorySlot>();

        // 属性
        public int SlotCount => slots.Count;
        public int UsedSlots => slots.FindAll(s => s.itemData != null).Count;
        public int FreeSlots => TOTAL_SLOTS - UsedSlots;
        public float CurrentWeight => CalculateWeight();
        public float WeightPercent => MAX_WEIGHT > 0f ? Mathf.Clamp01(CurrentWeight / MAX_WEIGHT) : 0f;
        public bool IsFull => FreeSlots <= 0;
        public bool IsOverweight => CurrentWeight > MAX_WEIGHT;

        public delegate void InventoryChangedDelegate();
        public event InventoryChangedDelegate OnInventoryChanged;

        void Awake()
        {
            if (slots == null || slots.Count == 0)
            {
                slots = new List<AIBotInventorySlot>(TOTAL_SLOTS);
                for (int i = 0; i < TOTAL_SLOTS; i++)
                    slots.Add(new AIBotInventorySlot());
            }
        }

        // ============================================================
        // 物品管理
        // ============================================================

        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            // 尝试堆叠到已有同类格
            foreach (var slot in slots)
            {
                if (slot.itemData == item && slot.count < item.maxStack)
                {
                    int canAdd = Mathf.Min(count, item.maxStack - slot.count);
                    slot.count += canAdd;
                    count -= canAdd;
                    OnInventoryChanged?.Invoke();
                    if (count <= 0) return true;
                }
            }

            // 新建格子
            while (count > 0 && !IsFull)
            {
                int toAdd = Mathf.Min(count, item.maxStack);
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].itemData == null)
                    {
                        slots[i].itemData = item;
                        slots[i].count = toAdd;
                        count -= toAdd;
                        OnInventoryChanged?.Invoke();
                        break;
                    }
                }
                if (IsFull) break;
            }

            OnInventoryChanged?.Invoke();
            return count <= 0;
        }

        public bool RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            int remaining = count;
            foreach (var slot in slots)
            {
                if (slot.itemData == item)
                {
                    int toRemove = Mathf.Min(slot.count, remaining);
                    slot.count -= toRemove;
                    remaining -= toRemove;
                    if (slot.count <= 0)
                        slot.itemData = null;

                    if (remaining <= 0) break;
                }
            }

            OnInventoryChanged?.Invoke();
            return remaining <= 0;
        }

        public bool HasItem(ItemData item, int count = 1)
        {
            return GetItemCount(item) >= count;
        }

        public int GetItemCount(ItemData item)
        {
            int total = 0;
            foreach (var slot in slots)
            {
                if (slot.itemData == item)
                    total += slot.count;
            }
            return total;
        }

        /// <summary>按物品名称查找数量（用于弹药消耗）</summary>
        public int GetItemCountByName(string itemName)
        {
            int total = 0;
            foreach (var slot in slots)
            {
                if (slot.itemData != null && slot.itemData.itemName == itemName)
                    total += slot.count;
            }
            return total;
        }

        /// <summary>消耗指定名称的物品（用于弹药消耗）</summary>
        public bool ConsumeItemByName(string itemName, int count = 1)
        {
            int remaining = count;
            foreach (var slot in slots)
            {
                if (slot.itemData != null && slot.itemData.itemName == itemName)
                {
                    int toRemove = Mathf.Min(slot.count, remaining);
                    slot.count -= toRemove;
                    remaining -= toRemove;
                    if (slot.count <= 0)
                        slot.itemData = null;
                    if (remaining <= 0) break;
                }
            }
            OnInventoryChanged?.Invoke();
            return remaining <= 0;
        }

        public bool CanAddItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return true;

            float addedWeight = item.weight * count;
            float spaceInExisting = 0f;

            foreach (var slot in slots)
            {
                if (slot.itemData == item)
                {
                    int space = item.maxStack - slot.count;
                    spaceInExisting += space * item.weight;
                }
                else if (slot.itemData == null)
                {
                    spaceInExisting += item.maxStack * item.weight;
                }
            }

            return (CurrentWeight - spaceInExisting + addedWeight) <= MAX_WEIGHT || true;
            // 负重软限制：允许超重但UI会提示
        }

        public List<AIBotInventorySlot> GetAllSlots() => new List<AIBotInventorySlot>(slots);

        public AIBotInventorySlot GetSlot(int index)
        {
            if (index >= 0 && index < slots.Count)
                return slots[index];
            return null;
        }

        // ============================================================
        // 弹药消耗回调（注册到 AIBotCombat）
        // ============================================================

        public bool ConsumeAmmo(string ammoItemName, int count)
        {
            return ConsumeItemByName(ammoItemName, count);
        }

        // ============================================================
        // 内部
        // ============================================================

        float CalculateWeight()
        {
            float w = 0f;
            foreach (var slot in slots)
            {
                if (slot.itemData != null)
                    w += slot.itemData.weight * slot.count;
            }
            return w;
        }
    }

    /// <summary>
    /// 机器人背包单格数据
    /// </summary>
    [System.Serializable]
    public class AIBotInventorySlot
    {
        public ItemData itemData;
        public int count;
    }
}
