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
        public bool hasDurability;          // 武器/护甲/工具消耗耐久
        public float maxDurability = 100f;
        public int maxRepairs = 8;          // 最大修理次数（0=不可修），见耐久系统.md §6.2
        public ItemMaterial itemMaterial = ItemMaterial.Default;  // 前置B：材质标签

        [Header("用途标签")]
        public ItemUsageTag[] usageTags;

        [Header("工作台")]
        public bool isWorkstation;
        public WorkstationTier workstationTier;

        [Header("电子设备（前置C）")]
        public ElectronicDeviceType electronicDeviceType = ElectronicDeviceType.Flashlight;

        [Header("武器附件（前置D）")]
        public AttachmentSlot attachmentSlot = AttachmentSlot.None;  // 此物品是哪种附件
        public AttachmentSlot[] hostSlots;                            // 此物品接受哪些槽位的附件

        [Header("装备属性")]
        public EquipSlot equipSlot = EquipSlot.None;  // 装备位
        public int storageWidth;                       // 装备后展开网格列数
        public int storageHeight;                      // 装备后展开网格行数
        public float armorValue;                       // 护甲值（减伤）

        [Header("世界表现")]
        public GameObject worldPrefab;

        [Header("行为列表（替代无限加字段，每个条目对应一个功能脚本）")]
        public List<ItemBehaviourEntry> behaviours;

        [Header("使用")]
        public float useTime = GameConstants.DEFAULT_USE_TIME;  // 使用耗时（秒）

        [Header("生存系统")]
        public List<ItemEffect> itemEffects;
        public float warmthValue;
        public bool isWaterproof;
        [Tooltip("食物类型：留空=非食物  Meat=荤  Vegetable=素  Drink=饮品  Medicine=药品")]
        public string foodType;
        [Tooltip("作为燃料时的能量值（0=非燃料）")]
        public float fuelValue;
        [Tooltip("药品治疗量（>0 药品生效，医疗技能可加成）")]
        public float healAmount;
        [Tooltip("能量值（电池/铀/核心 → AIBot 充能）")]
        public float energyValue;
        [Tooltip("修理值（高级零件/铁锭 → AIBot 修理）")]
        public float repairValue;
        [Tooltip("聚变核心大小（None/Small/Large → AIBot 识别）")]
        public FusionCoreSize fusionCoreSize = FusionCoreSize.None;

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

        [Header("武器→弹药/声音/VFX 连接")]
        [Tooltip("消耗的弹药物品名（向后兼容，ammoItemData 优先）")]
        public string ammoItemName;
        [Tooltip("弹药直接引用（优先于 ammoItemName，改名不中断）")]
        public ItemData ammoItemData;
        [Tooltip("枪声音色：留空=静音  Pistol/Rifle/Shotgun/Heavy")]
        public string gunshotSoundType;
        [Tooltip("枪口火焰：留空=无火焰  Small/Medium/Large")]
        public string muzzleFlashType;
        [Tooltip("换弹耗时（秒）")]
        public float reloadTime = 1.5f;
    }
}
