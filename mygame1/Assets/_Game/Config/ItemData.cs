using System.Collections.Generic;
using UnityEngine;
using _Game.Core;

namespace _Game.Config
{
    /// <summary>
    /// 物品数据定义（ScriptableObject）
    /// 在 Unity 编辑器中：右键 → Create → Game/Item 创建具体的物品配置
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Item")]
    public class ItemData : ScriptableObject
    {
        [Header("基本信息")]
        public string itemName;
        public Sprite icon;
        [TextArea(2, 4)]
        public string description;

        [Header("背包空间")]
        public int gridWidth = GameConstants.DEFAULT_ITEM_GRID_WIDTH;   // 占几列
        public int gridHeight = GameConstants.DEFAULT_ITEM_GRID_HEIGHT; // 占几行

        [Header("游戏属性")]
        public ItemCategory category;
        public int maxStack = GameConstants.DEFAULT_MAX_STACK;
        public float weight = GameConstants.DEFAULT_ITEM_WEIGHT;

        [Header("品质 & 耐久")]
        public ItemQuality quality = ItemQuality.Scavenged;
        public bool hasDurability;
        public float maxDurability = 100f;

        [Header("用途标签")]
        public ItemUsageTag[] usageTags;

        [Header("工作台")]
        public bool isWorkstation;
        public WorkstationTier workstationTier;

        [Header("装备属性")]
        public EquipSlot equipSlot = EquipSlot.None;  // 装备位
        public int storageWidth;                       // 装备后展开网格列数
        public int storageHeight;                      // 装备后展开网格行数
        public float armorValue;                       // 护甲值（减伤）

        [Header("世界表现")]
        public GameObject worldPrefab;

        [Header("使用")]
        public float useTime = GameConstants.DEFAULT_USE_TIME;  // 使用耗时（秒）

        [Header("生存系统")]
        public List<ItemEffect> itemEffects;
        public float warmthValue;
        public bool isWaterproof;

        [Header("武器属性（category==Weapon 生效）")]
        public bool isFirearm = false;        // true=枪械(远程)，false=近战
        public float weaponDamage = 10f;      // 单发伤害
        public float weaponRange = GameConstants.DEFAULT_WEAPON_RANGE;  // 最大射程(米)
        public float fireRate = 0.5f;         // 射击间隔(秒/发)
        public float baseSpread = 2f;         // 基础散射角(度, 锥形半角)
        public float maxSpread = 15f;         // 最大散射角(度)
        public float spreadRecovery = 8f;     // 散射恢复速度(度/秒)
        public float spreadPerShot = 1.5f;    // 每发散射增量(度)
        public int magazineSize = GameConstants.DEFAULT_MAGAZINE_SIZE;  // 弹匣容量(0=无弹药限制/近战)
        public float range = GameConstants.DEFAULT_WEAPON_RANGE;  // 别名(同weaponRange, 向上兼容)
    }
}
