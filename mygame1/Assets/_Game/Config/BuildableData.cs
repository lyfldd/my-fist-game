using System;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 建造物类别枚举
    /// </summary>
    public enum BuildableCategory
    {
        Wall,        // 墙壁（垂直面阻挡）
        Floor,       // 地板（水平面）
        Furniture,   // 家具（装饰+功能）
        Barricade,   // 路障（门/窗加固）
        Workstation, // 工作台（交互打开合成面板）
        MetalIndustry,        // 金属工业（冶炼/冲压/车床/装配/弹药/武器）
        ElectronicsIndustry,  // 电子工业（拉线/插件/电路/精密装配/AI）
        ChemicalIndustry,     // 化学工业（发酵/蒸馏/制药/火药/离心/基因）
        BioIndustry,          // 生物食品工业（织布/锯木/熏制/罐头/水泵）
        EnergyIndustry,       // 能源工业（发电机/太阳能/核电站）
        Power           // 电力设备（发电端+终端）
    }

    /// <summary>
    /// 建造模式状态枚举
    /// Inactive=未激活, Preview=虚影预览中, Building=建造读条中
    /// </summary>
    public enum BuildModeState
    {
        Inactive,
        MenuOnly,   // 建造模式激活，菜单可见，但未选中物品（无虚影）
        Preview,
        Building
    }

    /// <summary>
    /// 建造物数据定义（ScriptableObject）
    /// 在编辑器中：右键 → Create → Game/Buildable 创建
    /// 
    /// 与 ItemData 独立分离：
    /// - ItemData 管背包里的物品（武器/食物/材料等）
    /// - BuildableData 管建造规则（占地/材料清单/技能要求等）
    /// - 材料通过 ItemRequirement[] 引用 ItemData
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Buildable")]
    public class BuildableData : ScriptableObject
    {
        [Header("基本信息")]
        public string displayName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("分类")]
        public BuildableCategory category;

        [Header("模型")]
        [Tooltip("半透明预览模型（材质需支持透明度）")]
        public GameObject previewPrefab;
        [Tooltip("放置后的实体模型（带碰撞体）")]
        public GameObject builtPrefab;

        [Header("占地体积")]
        [Tooltip("用于 OverlapBox 碰撞检测，决定能否放置")]
        public Vector3 placementSize = Vector3.one;

        [Header("网格吸附")]
        [Tooltip("0=自由放置, >0=吸附到该步长")]
        public float snapSize = 1f;

        [Header("建造需求")]
        [Tooltip("建造耗时（秒）")]
        public float buildDuration = 3f;
        [Tooltip("需要的工具（null=不需要工具），工具需装备在 RightHand")]
        public ItemData requiredTool;
        [Tooltip("材料清单")]
        public ItemRequirement[] materials;
        [Tooltip("建造技能要求（空=无要求），支持多技能组合")]
        public SkillRequirement[] skillRequirements;

        [Header("生存消耗（建造中每秒消耗，Phase 1 预留）")]
        public float hungerDrainPerSec = 0f;
        public float thirstDrainPerSec = 0f;
        public float staminaDrainPerSec = 0f;

        [Header("结构属性")]
        [Tooltip("建造物的血量（0表示无敌/不可破坏）")]
        public float maxHealth = 100f;
        [Tooltip("是否阻挡 NavMesh（僵尸 AI 路径规划，Phase 3 生效）")]
        public bool blocksNavMesh = true;

        [Header("拆除返还")]
        [Range(0f, 1f)]
        [Tooltip("拆除时返还材料的比例")]
        public float deconstructReturnRate = 0.5f;

        [Header("电力")]
        [Tooltip("供电半径（>0=用电终端，建造预览显示黄色圆圈）")]
        public float powerSupplyRadius;
        [Tooltip("发电功率W（>0=发电端）")]
        public float powerOutput;
        [Tooltip("电源类型（仅 powerOutput>0 有效）")]
        public _Game.Systems.Power.PowerSourceType powerSourceType;
        [Tooltip("是否需要燃料")]
        public bool powerRequiresFuel;
        [Tooltip("每小时燃料消耗量")]
        public float powerFuelPerHour = 1f;
        [Tooltip("燃料物品名称（UI显示）")]
        public string powerFuelItemName;
        [Tooltip("燃料物品（引用 ItemData，用于从背包消耗燃料）")]
        public ItemData powerFuelItemData;
        [Tooltip("是否仅白天")]
        public bool powerDaytimeOnly;
        [Tooltip("是否必须露天")]
        public bool powerRequiresOpenAir;
        [Tooltip("是否必须水边")]
        public bool powerRequiresWater;
        [Tooltip("噪音半径(米)")]
        public float powerNoiseRadius = 10f;
        [Header("设备用电（仅生产设备有效）")]
        [Tooltip("需求功率W")]
        public float powerRequired;
        [Tooltip("无电时允许烧煤兜底")]
        public bool powerAllowCoal;
        [Tooltip("烧煤功率")]
        public float powerCoalPower;
        [Tooltip("通电后生产速度倍率")]
        public float powerElectricSpeedMul = 2f;

        [Header("工作台/生产设备")]
        [Tooltip("是否为工作台（放置后玩家可交互打开合成面板）")]
        public bool isWorkstation;
        [Tooltip("工作台等级（仅 isWorkstation=true 时生效）")]
        public WorkstationTier workstationTier;
        [Tooltip("关联的生产设备数据（null=纯工作台，非null=放置后附加自运转生产逻辑）")]
        public ProductionDeviceData productionDeviceRef;

        [Header("工业设备联动")]
        [Tooltip("所属产业链（工业设备自动推算）")]
        public ChainType industrialChain;
        [Tooltip("该设备产出的物品名列表（建造菜单预览用）")]
        public string[] producedItems;
    }

    /// <summary>
    /// 建造材料需求（可序列化）
    /// </summary>
    [Serializable]
    public class ItemRequirement
    {
        public ItemData itemData;
        public int count = 1;
    }
}
