using UnityEngine;
using _Game.Config;
using _Game.Core;
using Inv = _Game.Systems.Inventory.Inventory;

namespace _Game.Systems.Durability
{
    /// <summary>
    /// 耐久系统 — 单例。追踪物品实例的耐久消耗、查询、护甲磨损分担。
    /// </summary>
    public class DurabilitySystem : MonoBehaviour
    {
        public static DurabilitySystem Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // ============================================================
        // 耐久 v1.0：核心查询与消耗
        // ============================================================

        /// <summary> 获取物品耐久比（0~1），无耐久物品返回 1 </summary>
        public float GetRatio(int instanceId)
        {
            var inv = ServiceLocator.Get<Inv>();
            if (inv == null) return 1f;
            var p = inv.FindPlacedItem(instanceId);
            if (p == null || p.Value.itemData == null || !p.Value.itemData.hasDurability)
                return 1f;
            // itemDurability==0 且 hasDurability==true → 视为满耐久（首次初始化）
            float cur = p.Value.itemDurability;
            float max = p.Value.itemData.maxDurability;
            if (cur <= 0f && max > 0f) return 1f;
            return max > 0f ? Mathf.Clamp01(cur / max) : 1f;
        }

        /// <summary> 消耗物品耐久。负值=磨损，正值=修复。自动发布 DurabilityChangedEvent </summary>
        public void ConsumeDurability(int instanceId, float amount)
        {
            if (instanceId <= 0 || amount <= 0f) return;
            var inv = ServiceLocator.Get<Inv>();
            if (inv == null) return;

            var p = inv.FindPlacedItem(instanceId);
            if (p == null || p.Value.itemData == null || !p.Value.itemData.hasDurability)
                return;

            float max = p.Value.itemData.maxDurability;
            float cur = p.Value.itemDurability;

            // 首次消耗：初始化为满耐久
            if (cur <= 0f) cur = max;

            inv.ModifyDurability(instanceId, -amount);

            // 查新值
            float newRatio = GetRatio(instanceId);
            EventBus.Publish(new DurabilityChangedEvent(instanceId, p.Value.itemData, newRatio));

            // 损坏事件 — 反向查槽位，让订阅者知道哪个装备槽的物品坏了
            if (newRatio <= 0f)
            {
                EquipSlot slot = inv.GetSlotByInstanceId(instanceId);
                EventBus.Publish(new ItemBrokenEvent(instanceId, p.Value.itemData, slot));
            }
        }

        /// <summary> 将伤害分担到多个护甲实例的耐久上 </summary>
        public void DistributeArmorWear(int[] equippedArmorIds, float totalDamage)
        {
            if (equippedArmorIds == null || equippedArmorIds.Length == 0 || totalDamage <= 0f)
                return;
            float perArmor = totalDamage * GameConstants.DURABILITY_ARMOR_PER_DAMAGE / equippedArmorIds.Length;
            foreach (var id in equippedArmorIds)
            {
                if (id > 0) ConsumeDurability(id, perArmor);
            }
        }

        // ============================================================
        // 前置 B：材质系数
        // ============================================================

        public static float GetMaterialFactor(ItemMaterial mat) => mat switch
        {
            ItemMaterial.Cloth  => 1.5f,
            ItemMaterial.Wood   => 1.3f,
            ItemMaterial.Iron   => 0.9f,
            ItemMaterial.Steel  => 0.6f,
            ItemMaterial.Carbon => 0.4f,
            _                   => 1.0f
        };

        // ============================================================
        // 前置 I：工具效率曲线
        // ============================================================

        public float GetToolEfficiency(int instanceId)
        {
            float ratio = GetRatio(instanceId);
            return GameConstants.DURABILITY_TOOL_EFFICIENCY_MIN +
                   (1f - GameConstants.DURABILITY_TOOL_EFFICIENCY_MIN) * ratio;
        }
    }
}
