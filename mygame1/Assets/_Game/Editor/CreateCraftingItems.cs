using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键生成合成系统所需的所有物品 .asset 文件。
/// 用法：菜单栏 → Game Tools → Create Crafting Items
///
/// 生成 ~130 ItemData，覆盖全部 8 种 ItemCategory。
/// 配合已有的 ~30 个物品（CreateDefaultItems），总计 ~160。
/// 遵循"前期简单、后期丰富"设计铁律。
/// </summary>
public static class CreateCraftingItems
{
    const string Base = "Assets/_Game/Config/Items";

    [MenuItem("Game Tools/Create Crafting Items")]
    public static void CreateAll()
    {
        EnsureDirs();

        CreateRawMaterials();     // 原材料 ~30
        CreateSemiFinished();     // 半成品 ~30
        CreateConsumables();      // 消耗品 ~25
        CreateEquipment();        // 装备/工具/武器 ~25
        CreateAmmo();             // 弹药 ~8
        CreateBuildables();       // 建筑套件 ~5
        CreateWorkstations();     // 工作台套件 ~7
        CreateFunctional();       // 功能道具 ~8

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("合成物品资产生成完毕！路径: " + Base);
    }

    static void EnsureDirs()
    {
        string[] dirs = { "RawMaterials", "SemiFinished", "Consumables", "Equipment",
                          "Ammo", "Buildables", "Workstations", "Functional" };
        foreach (var d in dirs)
        {
            string path = $"{Base}/{d}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(Base, d);
        }
    }

    static ItemData Create(string folder, string assetName)
    {
        string path = $"{Base}/{folder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (existing != null)
        {
            // 已存在则刷新 displayName（可能之前创建时名字不对）
            return existing;
        }
        var item = ScriptableObject.CreateInstance<ItemData>();
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    static ItemData Make(string folder, string assetName, string displayName,
        ItemCategory cat, int w = 1, int h = 1, float weight = 0.1f, int stack = 30,
        ItemQuality quality = ItemQuality.Scavenged, ItemUsageTag[] tags = null)
    {
        var item = Create(folder, assetName);
        item.itemName = displayName;
        item.category = cat;
        item.gridWidth = w;
        item.gridHeight = h;
        item.weight = weight;
        item.maxStack = stack;
        item.quality = quality;
        item.usageTags = tags ?? new ItemUsageTag[0];
        EditorUtility.SetDirty(item);
        return item;
    }

    // ================================================================
    // 原材料 RawMaterial (~30)
    // ================================================================

    static void CreateRawMaterials()
    {
        const string F = "RawMaterials";
        const ItemCategory C = ItemCategory.RawMaterial;

        // 木材类（前期）
        Make(F, "WoodLog",    "原木",   C, 1, 3, 2f,    10,  ItemQuality.Scavenged);
        Make(F, "Branch",     "树枝",   C, 1, 2, 0.5f,  20,  ItemQuality.Scavenged);
        Make(F, "Bark",       "树皮",   C, 1, 1, 0.2f,  30,  ItemQuality.Scavenged);
        Make(F, "PlantFiber", "植物纤维", C, 1, 1, 0.1f, 50,  ItemQuality.Scavenged);

        // 石矿类（前期）
        Make(F, "Stone",        "石头",    C, 1, 1, 1f,   20, ItemQuality.Scavenged);
        Make(F, "Flint",        "燧石",    C, 1, 1, 0.3f, 30, ItemQuality.Scavenged);
        Make(F, "IronOre",      "铁矿石",  C, 1, 1, 1.5f, 15, ItemQuality.Scavenged);
        Make(F, "CopperOre",    "铜矿石",  C, 1, 1, 1.5f, 15, ItemQuality.Scavenged);
        Make(F, "Coal",         "煤矿",    C, 1, 1, 1f,   20, ItemQuality.Scavenged);
        Make(F, "Limestone",    "石灰石",  C, 1, 1, 1.2f, 20, ItemQuality.Scavenged);
        Make(F, "Sand",         "沙子",    C, 1, 1, 0.8f, 30, ItemQuality.Scavenged);
        Make(F, "Clay",         "黏土",    C, 1, 1, 0.8f, 30, ItemQuality.Scavenged);

        // 稀有矿物（后期）
        Make(F, "LeadOre",     "铅矿石",  C, 1, 1, 2f,   10, ItemQuality.Scavenged);
        Make(F, "AluminumOre", "铝矿石",  C, 1, 1, 1.5f, 10, ItemQuality.Scavenged);
        Make(F, "Sulfur",      "硫磺",    C, 1, 1, 0.8f, 20, ItemQuality.Scavenged);
        Make(F, "Niter",       "硝石",    C, 1, 1, 0.8f, 20, ItemQuality.Scavenged);
        Make(F, "UraniumOre",  "铀矿石",  C, 1, 1, 3f,   5,  ItemQuality.Professional);

        // 搜刮类（前期）
        Make(F, "ScrapMetal",  "废金属",   C, 1, 1, 0.8f, 20, ItemQuality.Scavenged);
        Make(F, "PlasticScrap","塑料碎片", C, 1, 1, 0.2f, 30, ItemQuality.Scavenged);
        Make(F, "GlassShard",  "玻璃碎片", C, 1, 1, 0.3f, 20, ItemQuality.Scavenged);
        Make(F, "Wire",        "电线",     C, 1, 1, 0.3f, 20, ItemQuality.Scavenged);
        Make(F, "Electronic",  "电子元件", C, 1, 1, 0.2f, 15, ItemQuality.Scavenged);
        Make(F, "Spring",      "弹簧",     C, 1, 1, 0.2f, 15, ItemQuality.Scavenged);
        Make(F, "Screw",       "螺丝",     C, 1, 1, 0.05f,50, ItemQuality.Scavenged);
        Make(F, "Rubber",      "橡胶",     C, 1, 1, 0.3f, 20, ItemQuality.Scavenged);

        // 生物类（狩猎/采集）
        Make(F, "AnimalHide",  "兽皮",     C, 1, 2, 1.5f, 10, ItemQuality.Scavenged);
        Make(F, "AnimalBone",  "兽骨",     C, 1, 1, 0.8f, 15, ItemQuality.Scavenged);
        Make(F, "RawMeat",     "生肉",     C, 1, 1, 0.8f, 10, ItemQuality.Scavenged);
        Make(F, "Feather",     "羽毛",     C, 1, 1, 0.1f, 30, ItemQuality.Scavenged);
        Make(F, "Herb",        "草药",     C, 1, 1, 0.1f, 30, ItemQuality.Scavenged);
        Make(F, "Mushroom",    "蘑菇",     C, 1, 1, 0.2f, 20, ItemQuality.Scavenged);
        Make(F, "Berry",       "浆果",     C, 1, 1, 0.1f, 25, ItemQuality.Scavenged);
    }

    // ================================================================
    // 半成品 SemiFinished (~30)
    // ================================================================

    static void CreateSemiFinished()
    {
        const string F = "SemiFinished";
        const ItemCategory C = ItemCategory.SemiFinished;

        // 基础加工品（前期-中期）
        Make(F, "WoodPlank",     "木板",      C, 1, 2, 0.6f, 20, ItemQuality.Handmade);
        Make(F, "StoneBrick",    "石砖",      C, 1, 1, 1.2f, 20, ItemQuality.Handmade);
        Make(F, "IronIngot",     "铁锭",      C, 1, 1, 1.5f, 15, ItemQuality.Handmade);
        Make(F, "CopperIngot",   "铜锭",      C, 1, 1, 1.5f, 15, ItemQuality.Handmade);
        Make(F, "SteelIngot",    "钢锭",      C, 1, 1, 2f,   10, ItemQuality.Professional);
        Make(F, "LeadIngot",     "铅锭",      C, 1, 1, 2.5f, 10, ItemQuality.Professional);
        Make(F, "AluminumIngot", "铝锭",      C, 1, 1, 1.2f, 10, ItemQuality.Professional);
        Make(F, "Nails",         "钉子",      C, 1, 1, 0.05f,50, ItemQuality.Handmade);
        Make(F, "CommonParts",   "通用零件",  C, 1, 1, 0.3f, 20, ItemQuality.Handmade);

        // 进阶零件（后期）
        Make(F, "AdvancedParts", "高级零件",  C, 1, 1, 0.5f, 15, ItemQuality.Professional);
        Make(F, "Gear",          "齿轮",      C, 1, 1, 0.4f, 15, ItemQuality.Professional);
        Make(F, "Bearing",       "轴承",      C, 1, 1, 0.3f, 15, ItemQuality.Professional);
        Make(F, "SpringAssembly","弹簧组件",  C, 1, 1, 0.3f, 15, ItemQuality.Professional);

        // 纺织类
        Make(F, "Cloth",        "碎布料",     C, 1, 1, 0.2f, 30, ItemQuality.Scavenged);
        Make(F, "ClothRoll",    "布匹",       C, 1, 2, 0.5f, 15, ItemQuality.Handmade);
        Make(F, "Leather",      "皮革",       C, 1, 2, 0.6f, 15, ItemQuality.Handmade);
        Make(F, "Thread",       "线",         C, 1, 1, 0.05f,50, ItemQuality.Handmade);
        Make(F, "Rope",         "绳索",       C, 1, 2, 0.3f, 10, ItemQuality.Handmade);

        // 化学类
        Make(F, "BlackPowder",  "黑火药",     C, 1, 1, 0.3f, 20, ItemQuality.Handmade);
        Make(F, "Gunpowder",    "无烟火药",   C, 1, 1, 0.3f, 20, ItemQuality.Professional);
        Make(F, "SulfuricAcid", "硫酸",       C, 1, 1, 0.8f, 10, ItemQuality.Professional);
        Make(F, "Alcohol",      "酒精",       C, 1, 1, 0.5f, 10, ItemQuality.Handmade);
        Make(F, "ChemicalAgent","化学试剂",   C, 1, 1, 0.3f, 10, ItemQuality.Professional);

        // 燃料
        Make(F, "CoalTar",      "煤焦油",     C, 1, 1, 0.8f, 10, ItemQuality.Handmade);
        Make(F, "RefinedCoal",  "精炼煤炭",   C, 1, 1, 1f,   15, ItemQuality.Professional);
        Make(F, "Gasoline",     "汽油",       C, 1, 1, 0.6f, 10, ItemQuality.Professional);

        // 弹药组件
        Make(F, "BulletCasing", "弹壳",       C, 1, 1, 0.05f,50, ItemQuality.Handmade);
        Make(F, "Primer",       "底火",       C, 1, 1, 0.02f,50, ItemQuality.Professional);
        Make(F, "BulletHead",   "弹头",       C, 1, 1, 0.1f, 30, ItemQuality.Handmade);

        // 电子/工业
        Make(F, "CircuitBoard", "电路板",     C, 1, 1, 0.2f, 10, ItemQuality.Professional);
        Make(F, "Coil",         "线圈",       C, 1, 1, 0.3f, 10, ItemQuality.Professional);
        Make(F, "BatteryPack",  "电池组",     C, 1, 1, 0.5f, 10, ItemQuality.Professional);
        Make(F, "Cable",        "电缆",       C, 1, 2, 0.5f, 20, ItemQuality.Professional);

        // 建材
        Make(F, "Cement",       "水泥",       C, 1, 1, 1.5f, 15, ItemQuality.Handmade);
        Make(F, "Rebar",        "钢筋",       C, 1, 3, 2f,   10, ItemQuality.Professional);
        Make(F, "GlassPane",    "玻璃板",     C, 1, 2, 0.8f, 10, ItemQuality.Handmade);
        Make(F, "SteelPipe",    "钢管",       C, 1, 3, 2f,   10, ItemQuality.Professional);

        // 精密电子（终局）
        Make(F, "CapacitorBank", "电容组",    C, 2, 1, 0.5f, 10, ItemQuality.Professional);
        Make(F, "ChipSet",       "芯片组",    C, 1, 1, 0.3f, 10, ItemQuality.Professional);
        Make(F, "ServoMotor",    "伺服电机",  C, 1, 1, 0.5f, 10, ItemQuality.Professional);
        Make(F, "OpticalLens",   "光学透镜",  C, 1, 1, 0.2f, 10, ItemQuality.Professional);

        // 高级材料（终局）
        Make(F, "TitaniumAlloy", "钛合金",    C, 1, 1, 2.5f, 10, ItemQuality.Professional);
        Make(F, "CarbonFiber",   "碳纤维",    C, 1, 1, 0.3f, 10, ItemQuality.Professional);
        Make(F, "VaccineSerum",  "疫苗原液",  C, 1, 1, 0.3f, 5,  ItemQuality.Professional);
        Make(F, "GeneSample",      "基因样本",  C, 1, 1, 0.1f, 5,  ItemQuality.Professional);
        Make(F, "EnrichedUranium","浓缩铀",    C, 1, 1, 3f,   3,  ItemQuality.Professional);

        // 无人机组件（终局）
        Make(F, "DroneBody",      "无人机机体",       C, 2, 1, 1.5f, 5, ItemQuality.Professional);
        Make(F, "DroneChip",      "无人机操控芯片",   C, 1, 1, 0.3f, 5, ItemQuality.Professional);
    }

    // ================================================================
    // 消耗品 Consumable (~25)
    // ================================================================

    static void CreateConsumables()
    {
        const string F = "Consumables";
        const ItemCategory C = ItemCategory.Consumable;

        // 烹饪（前期-中期）
        MakeConsumable(F, "CookedMeat",  "烤肉",     C, 2f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 30, isInstant = true } });
        MakeConsumable(F, "Stew",        "炖菜",     C, 3f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 40, isInstant = true },
            new ItemEffect { effectType = ItemEffectType.RestoreThirst, value = 10, isInstant = true } });
        MakeConsumable(F, "Bread",       "面包",     C, 2f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 20, isInstant = true } });
        MakeConsumable(F, "DriedMeat",   "肉干",     C, 1f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 15, isInstant = true } });
        MakeConsumable(F, "SmokedMeat",  "熏肉",     C, 1f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 25, isInstant = true } });
        MakeConsumable(F, "PickleVeg",   "腌菜",     C, 1f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 10, isInstant = true } });

        // 饮水
        MakeConsumable(F, "PurifiedWater","纯净水",  C, 2f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreThirst, value = 40, isInstant = true } });
        MakeConsumable(F, "Juice",       "果汁",     C, 2f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreThirst, value = 25, isInstant = true },
            new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 3, isInstant = true } });
        MakeConsumable(F, "Soup",        "汤",       C, 3f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 20, isInstant = true },
            new ItemEffect { effectType = ItemEffectType.RestoreThirst, value = 25, isInstant = true } });

        // 药品（前期-中期-后期）
        MakeConsumable(F, "FirstAidKit",  "急救包",  C, 4f, quality: ItemQuality.Professional,
            effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 30, isInstant = true },
            new ItemEffect { effectType = ItemEffectType.CureBleeding, value = 0, isInstant = true } });
        MakeConsumable(F, "Antiseptic",   "消毒水",  C, 2f, quality: ItemQuality.Handmade,
            effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.CureInfected, value = 0, isInstant = true } });
        MakeConsumable(F, "Vitamin",      "维生素",  C, 1f, effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 5, isInstant = true } });
        MakeConsumable(F, "Morphine",     "吗啡",    C, 2f, quality: ItemQuality.Professional,
            effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 20, isInstant = true } });
        MakeConsumable(F, "StypticPowder","止血粉",  C, 2f, quality: ItemQuality.Handmade,
            effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.CureBleeding, value = 0, isInstant = true } });
        MakeConsumable(F, "Antidote",     "解毒剂",  C, 3f, quality: ItemQuality.Professional,
            effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 10, isInstant = true } });

        // 投掷物（中期-后期）
        MakeConsumable(F, "Molotov",     "燃烧瓶",   C, 1f, w:1, h:1, weight:0.8f, stack:5,  quality: ItemQuality.Handmade);
        MakeConsumable(F, "Grenade",     "手榴弹",   C, 1f, w:1, h:1, weight:0.6f, stack:3,  quality: ItemQuality.Professional);
        MakeConsumable(F, "SmokeGrenade","烟雾弹",   C, 1f, w:1, h:1, weight:0.5f, stack:5,  quality: ItemQuality.Professional);
        MakeConsumable(F, "Flashbang",   "闪光弹",   C, 1f, w:1, h:1, weight:0.5f, stack:5,  quality: ItemQuality.Professional);

        // 特殊
        MakeConsumable(F, "Seeds",       "种子",     C, 1f, w:1, h:1, weight:0.05f, stack:50, quality: ItemQuality.Scavenged);
        MakeConsumable(F, "Fertilizer",  "化肥",     C, 1f, w:1, h:1, weight:0.8f,  stack:15, quality: ItemQuality.Professional);
        MakeConsumable(F, "Adrenaline",  "肾上腺素", C, 1f, w:1, h:1, weight:0.1f,  stack:3,  quality: ItemQuality.Professional,
            effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 15, isInstant = true } });

        // 终局医疗品
        MakeConsumable(F, "SurgeryKit",    "手术包",     C, 3f, w:2, h:2, weight:1f,    stack:3,  quality: ItemQuality.Professional,
            effects: new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 50, isInstant = true },
            new ItemEffect { effectType = ItemEffectType.CureBleeding, value = 0, isInstant = true },
            new ItemEffect { effectType = ItemEffectType.CureInfected, value = 0, isInstant = true } });
        MakeConsumable(F, "ImmuneBooster", "免疫增强剂", C, 1f, w:1, h:1, weight:0.3f,  stack:3,  quality: ItemQuality.Professional);
        MakeConsumable(F, "OrganRepair",   "器官修复针", C, 1f, w:1, h:1, weight:0.2f,  stack:1,  quality: ItemQuality.Professional);
        MakeConsumable(F, "NerveRegen",    "神经再生剂", C, 1f, w:1, h:1, weight:0.2f,  stack:1,  quality: ItemQuality.Professional);
        MakeConsumable(F, "ZombieVaccine", "丧尸逆转疫苗", C, 1f, w:1, h:1, weight:0.3f, stack:1, quality: ItemQuality.Professional);
    }

    // ================================================================
    // 装备/工具/武器 Equipment (~25)
    // ================================================================

    static void CreateEquipment()
    {
        const string F = "Equipment";
        const ItemCategory C = ItemCategory.Equipment;

        // 工具类（前期）
        MakeTool(F, "StoneAxe",    "石斧",     1, 2, 2f,    ItemQuality.Handmade, EquipSlot.RightHand);
        MakeTool(F, "StonePick",   "石镐",     1, 2, 2f,    ItemQuality.Handmade, EquipSlot.RightHand);
        MakeTool(F, "IronAxe",     "铁斧",     1, 2, 3f,    ItemQuality.Handmade, EquipSlot.RightHand);
        MakeTool(F, "IronPick",    "铁镐",     1, 2, 3f,    ItemQuality.Handmade, EquipSlot.RightHand);
        MakeTool(F, "Hammer",      "锤子",     1, 1, 1.5f,  ItemQuality.Handmade, EquipSlot.RightHand);
        MakeTool(F, "Screwdriver", "螺丝刀",   1, 1, 0.3f,  ItemQuality.Scavenged, EquipSlot.RightHand);
        MakeTool(F, "Wrench",      "扳手",     1, 1, 0.5f,  ItemQuality.Scavenged, EquipSlot.RightHand);
        MakeTool(F, "Saw",         "锯子",     1, 2, 1.2f,  ItemQuality.Handmade,  EquipSlot.RightHand);
        MakeTool(F, "Shovel",      "铁锹",     1, 3, 2.5f,  ItemQuality.Handmade,  EquipSlot.RightHand);

        // 武器（近战）
        MakeWeapon(F, "ShortKnife",   "短刀",    1, 2, 1.2f,  ItemQuality.Handmade,
            EquipSlot.KnifeBelt, isFirearm: false, damage: 12, range: 1.5f, fireRate: 0.5f);
        MakeWeapon(F, "Spear",        "长矛",    1, 4, 3f,    ItemQuality.Handmade,
            EquipSlot.RightHand, isFirearm: false, damage: 15, range: 2.5f, fireRate: 0.7f);
        MakeWeapon(F, "Machete",      "砍刀",    1, 3, 2.5f,  ItemQuality.Handmade,
            EquipSlot.RightHand, isFirearm: false, damage: 18, range: 2f, fireRate: 0.6f);
        MakeWeapon(F, "Bat",          "棒球棍",  1, 3, 2f,    ItemQuality.Scavenged,
            EquipSlot.RightHand, isFirearm: false, damage: 10, range: 1.8f, fireRate: 0.5f);

        // 武器（远程）— 旧版已删除，新版见 CreateWeaponsAndAmmo.cs
        MakeWeapon(F, "Crossbow",     "十字弓",  1, 3, 3f,    ItemQuality.Handmade,
            EquipSlot.RightHand, isFirearm: true, damage: 30, range: 40, fireRate: 1.5f);
        // 霰弹枪/手枪/步枪 → 已迁移到 CreateWeaponsAndAmmo (雷明顿870/M1911+M9/AK-47+M16A1)
        // 盾牌 → 已删除（用防弹衣/插板背心替代）
        // 皮甲/铁甲/防弹衣(旧)/猎枪/重狙 → 已删除（新装备见 CreateEquipment）
        MakeWeapon(F, "GrenadeLauncher","榴弹发射器", 1, 5, 8f, ItemQuality.Professional,
            EquipSlot.RightHand, isFirearm: true, damage: 100, range: 50, fireRate: 2.5f);

        // 终局武器
        MakeWeapon(F, "Railgun",      "电磁步枪", 1, 5, 6f,    ItemQuality.Professional,
            EquipSlot.RightHand, isFirearm: true, damage: 80, range: 100, fireRate: 2f);

        // 武器配件
        MakeTool(F, "Suppressor",   "消音器",   1, 1, 0.3f,  ItemQuality.Professional, EquipSlot.RightHand);

        // 功能性装备
        MakeArmor(F, "ToolBelt",     "工具腰带", 2, 1, 0.5f, ItemQuality.Handmade, EquipSlot.Belt, 1f, 3, 2);
        MakeArmor(F, "GasMask",      "防毒面具", 1, 1, 0.8f, ItemQuality.Professional, EquipSlot.Head, 3f);
        MakeArmor(F, "NightGoggles", "夜视仪",   1, 1, 0.5f, ItemQuality.Professional, EquipSlot.Head, 0f);
    }

    // ================================================================
    // 弹药 Ammo (~8)
    // ================================================================

    static void CreateAmmo()
    {
        const string F = "Ammo";
        const ItemCategory C = ItemCategory.Ammo;

        Make(F, "Ammo_9mm",     "9mm手枪弹",   C, 1, 1, 0.02f, 50, ItemQuality.Professional);
        Make(F, "Ammo_45ACP",   ".45沙鹰弹",   C, 1, 1, 0.03f, 30, ItemQuality.Professional);
        Make(F, "Ammo_762mm",   "7.62mm步枪弹", C, 1, 1, 0.04f, 40, ItemQuality.Professional);
        Make(F, "Ammo_12Gauge", "12号霰弹",    C, 1, 1, 0.05f, 20, ItemQuality.Professional);
        Make(F, "Ammo_556mm",   "5.56mm步枪弹", C, 1, 1, 0.03f, 40, ItemQuality.Professional);
        Make(F, "Ammo_22LR",    ".22小口径",    C, 1, 1, 0.01f, 60, ItemQuality.Professional);
        Make(F, "Arrow",        "箭矢",        C, 1, 2, 0.1f,  20, ItemQuality.Handmade);
        Make(F, "Bolt",         "弩箭",        C, 1, 1, 0.08f, 20, ItemQuality.Handmade);

        // 终局弹药
        Make(F, "Ammo_AP",        "穿甲弹",      C, 1, 1, 0.05f, 20, ItemQuality.Professional);
        Make(F, "Ammo_Incendiary","燃烧弹",      C, 1, 1, 0.06f, 15, ItemQuality.Professional);
        Make(F, "Ammo_Explosive", "爆炸箭头",    C, 1, 2, 0.15f, 10, ItemQuality.Professional);
    }

    // ================================================================
    // 建筑套件 Buildable (~5)
    // ================================================================

    static void CreateBuildables()
    {
        const string F = "Buildables";
        const ItemCategory C = ItemCategory.Buildable;

        Make(F, "WoodWallKit",   "木墙套件",   C, 2, 2, 5f,  5, ItemQuality.Handmade);
        Make(F, "StoneWallKit",  "石墙套件",   C, 2, 2, 8f,  3, ItemQuality.Handmade);
        Make(F, "WoodFloorKit",  "木地板套件", C, 2, 2, 4f,  5, ItemQuality.Handmade);
        Make(F, "StoneFloorKit", "石地板套件", C, 2, 2, 6f,  3, ItemQuality.Handmade);
        Make(F, "DoorKit",       "门套件",     C, 2, 2, 3f,  3, ItemQuality.Handmade);
    }

    // ================================================================
    // 工作台套件 Workstation (~7)
    // ================================================================

    static void CreateWorkstations()
    {
        const string F = "Workstations";
        const ItemCategory C = ItemCategory.Workstation;

        MakeWS(F, "WS_Campfire",       "篝火套件",       WorkstationTier.Campfire,       3f, 10);
        MakeWS(F, "WS_SimpleBench",    "简易工作台套件", WorkstationTier.SimpleBench,     5f, 5);
        MakeWS(F, "WS_Furnace",        "熔炉套件",       WorkstationTier.Furnace,         8f, 3);
        MakeWS(F, "WS_MediumBench",    "中级工作台套件", WorkstationTier.MediumBench,     8f, 3);
        MakeWS(F, "WS_AdvancedBench",  "高级工作台套件", WorkstationTier.AdvancedBench,  12f, 3);
        MakeWS(F, "WS_Chemistry",      "研究中心套件", WorkstationTier.Chemistry,      15f, 2);
        MakeWS(F, "WS_Machining",             "机械加工台套件",  WorkstationTier.Machining,            20f, 2);
        MakeWS(F, "WS_ElectronicsAssembly", "电子装配台套件",  WorkstationTier.ElectronicsAssembly,  22f, 2);
    }

    // ================================================================
    // 功能道具 Functional (~8)
    // ================================================================

    static void CreateFunctional()
    {
        const string F = "Functional";
        const ItemCategory C = ItemCategory.Functional;

        Make(F, "Lighter",      "打火机",     C, 1, 1, 0.1f, 1);
        Make(F, "Flashlight",   "手电筒",     C, 1, 1, 0.3f, 1);
        Make(F, "Battery",      "电池",       C, 1, 1, 0.1f, 10);
        Make(F, "Key",          "钥匙",       C, 1, 1, 0.05f,5);
        Make(F, "SkillBook",    "技能书",     C, 1, 1, 0.3f, 1);
        Make(F, "Blueprint",    "蓝图",       C, 1, 1, 0.2f, 1);
        Make(F, "Compass",      "指南针",     C, 1, 1, 0.1f, 1);
        Make(F, "Watch",        "手表",       C, 1, 1, 0.1f, 1);
        Make(F, "Toolbox",      "工具箱",     C, 2, 2, 2f,   1);

        // 终局电子设备
        Make(F, "Meteorologist",      "气象预测仪",     C, 1, 1, 0.5f, 1, ItemQuality.Professional);
        Make(F, "Decryptor",          "密码破译器",     C, 1, 1, 0.4f, 1, ItemQuality.Professional);
        Make(F, "AutomationController","自动化编程台",   C, 2, 2, 3f,   1, ItemQuality.Professional);
        Make(F, "SatelliteDish",      "卫星接收站",     C, 3, 3, 8f,   1, ItemQuality.Professional);
        Make(F, "AICore",             "AI核心",         C, 1, 1, 0.5f, 1, ItemQuality.Professional);

        // 武器改造套件
        Make(F, "SemiAutoKit",        "半自动改造套件", C, 1, 1, 0.5f, 3, ItemQuality.Professional);
        Make(F, "FullAutoKit",        "全自动改造套件", C, 1, 1, 0.6f, 3, ItemQuality.Professional);
        Make(F, "SmallReactor",       "小型反应堆",     C, 2, 2, 6f,   1, ItemQuality.Professional);

        // 无人机成品
        Make(F, "Drone",              "无人机",           C, 2, 2, 3f,   3, ItemQuality.Professional);
        Make(F, "DroneTerminal",      "无人机操控终端",   C, 2, 2, 2f,   1, ItemQuality.Professional);
    }

    // ================================================================
    // Helper methods
    // ================================================================

    static void MakeConsumable(string folder, string assetName, string displayName,
        ItemCategory cat, float useTime, int w = 1, int h = 1, float weight = 0.3f,
        int stack = 10, ItemQuality quality = ItemQuality.Scavenged,
        List<ItemEffect> effects = null)
    {
        var item = Make(folder, assetName, displayName, cat, w, h, weight, stack, quality);
        item.useTime = useTime;
        item.itemEffects = effects ?? new List<ItemEffect>();
        EditorUtility.SetDirty(item);
    }

    static void MakeTool(string folder, string assetName, string displayName,
        int w, int h, float weight, ItemQuality quality, EquipSlot slot)
    {
        var item = Make(folder, assetName, displayName, ItemCategory.Equipment, w, h, weight, 1, quality);
        item.equipSlot = slot;
        item.hasDurability = true;
        item.maxDurability = 100f;
        EditorUtility.SetDirty(item);
    }

    static void MakeWeapon(string folder, string assetName, string displayName,
        int w, int h, float weight, ItemQuality quality, EquipSlot slot,
        bool isFirearm, float damage, float range, float fireRate)
    {
        var item = Make(folder, assetName, displayName, ItemCategory.Equipment, w, h, weight, 1, quality);
        item.equipSlot = slot;
        item.isFirearm = isFirearm;
        item.weaponDamage = damage;
        item.weaponRange = range;
        item.range = range;
        item.fireRate = fireRate;
        item.hasDurability = true;
        item.maxDurability = isFirearm ? 200f : 100f;
        EditorUtility.SetDirty(item);
    }

    static void MakeArmor(string folder, string assetName, string displayName,
        int w, int h, float weight, ItemQuality quality, EquipSlot slot,
        float armorValue, int storageW = 0, int storageH = 0)
    {
        var item = Make(folder, assetName, displayName, ItemCategory.Equipment, w, h, weight, 1, quality);
        item.equipSlot = slot;
        item.armorValue = armorValue;
        item.storageWidth = storageW;
        item.storageHeight = storageH;
        item.hasDurability = true;
        item.maxDurability = 100f;
        EditorUtility.SetDirty(item);
    }

    static void MakeWS(string folder, string assetName, string displayName,
        WorkstationTier tier, float weight, int stack)
    {
        var item = Make(folder, assetName, displayName, ItemCategory.Workstation, 2, 2, weight, stack, ItemQuality.Handmade);
        item.isWorkstation = true;
        item.workstationTier = tier;
        EditorUtility.SetDirty(item);
    }
}
