using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键生成所有建造物 .asset 文件（含材料需求）。
/// 用法：先执行 Create Crafting Items，再执行本菜单。
/// 菜单栏 → Game Tools → Create Default Buildables
///
/// 生成 46 BuildableData，覆盖全部 6 种 BuildableCategory。
/// </summary>
public static class CreateDefaultBuildables
{
    const string Dir = "Assets/_Game/Config/BuildableData";
    const string CatalogPath = "Assets/_Game/Config/BuildableCatalog.asset";

    static SkillRequirement[] BuildReq(int level) => level <= 0 ? null :
        new SkillRequirement[] { new SkillRequirement { skill = SkillType.建造拆解, level = level } };
    static SkillRequirement[] Skills(params (SkillType skill, int level)[] list)
    {
        var r = new SkillRequirement[list.Length];
        for (int i = 0; i < list.Length; i++)
            r[i] = new SkillRequirement { skill = list[i].skill, level = list[i].level };
        return r;
    }

    static Dictionary<string, ItemData> _itemLookup;
    static List<(BuildableData asset, string name)> _pendingAssets;

    [MenuItem("Game Tools/Create Default Buildables")]
    public static void CreateAll()
    {
        BuildItemLookup();
        EnsureDir();

        // 先清空旧文件
        var oldGuids = AssetDatabase.FindAssets("t:BuildableData", new[] { Dir });
        foreach (var guid in oldGuids)
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        AssetDatabase.Refresh();

        // 阶段1: 在内存中创建所有 ScriptableObject，设完所有字段
        _pendingAssets = new List<(BuildableData, string)>();
        CreateWorkstations();
        CreateIndustrialDevices();
        CreateAdditionalStructures();

        // 阶段2: 字段全部设好后，一次性写入磁盘
        foreach (var (asset, name) in _pendingAssets)
            AssetDatabase.CreateAsset(asset, $"{Dir}/{name}.asset");

        UpdateCatalog();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"BuildableData 资产生成完毕（含材料）！路径: {Dir}");
    }

    static void BuildItemLookup()
    {
        _itemLookup = new Dictionary<string, ItemData>();
        var guids = AssetDatabase.FindAssets("t:ItemData");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
            {
                string key = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!_itemLookup.ContainsKey(key))
                    _itemLookup[key] = item;
            }
        }
        Debug.Log($"[CreateDefaultBuildables] 已索引 {_itemLookup.Count} 个 ItemData");
    }

    static ItemData GetItem(string assetName)
    {
        if (_itemLookup.TryGetValue(assetName, out var item))
            return item;
        Debug.LogWarning($"[CreateDefaultBuildables] 未找到 ItemData: {assetName}");
        return null;
    }

    static ItemRequirement M(string itemName, int count)
    {
        return new ItemRequirement { itemData = GetItem(itemName), count = count };
    }

    static void EnsureDir()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Game/Config", "BuildableData");
    }

    static BuildableData Create(string assetName)
    {
        var b = ScriptableObject.CreateInstance<BuildableData>();
        // 延迟到所有字段设完后再 CreateAsset，避免空数据写盘
        _pendingAssets.Add((b, assetName));
        return b;
    }

    static ProductionDeviceData LoadProdRef(string deviceName)
    {
        var path = $"Assets/_Game/Config/ProductionDevices/{deviceName}.asset";
        var pd = AssetDatabase.LoadAssetAtPath<ProductionDeviceData>(path);
        if (pd == null)
            Debug.LogWarning($"[CreateDefaultBuildables] 未找到 ProductionDeviceData: {deviceName}");
        return pd;
    }

    // ================================================================
    // 工作台 (8)
    // ================================================================

    static void CreateWorkstations()
    {
        // 篝火
        {
            var b = Create("Buildable_Campfire");
            b.displayName = "篝火";
            b.description = "基础烹饪与取暖。放置后可进行徒手烹饪。";
            b.category = BuildableCategory.Workstation;
            b.buildDuration = 3f;
            b.maxHealth = 40f;
            b.isWorkstation = true;
            b.workstationTier = WorkstationTier.Campfire;
            b.skillRequirements = BuildReq(0);
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1f, 0.6f, 1f);
            b.blocksNavMesh = false;
            b.materials = new ItemRequirement[] { M("WoodLog", 3), M("Stone", 5), M("Flint", 1) };
        }

        // 简易工作台
        {
            var b = Create("Buildable_SimpleBench");
            b.displayName = "简易工作台";
            b.description = "基础手工制作台。可制作简易工具、武器、建筑材料和基础医疗用品。";
            b.category = BuildableCategory.Workstation;
            b.buildDuration = 5f;
            b.maxHealth = 60f;
            b.isWorkstation = true;
            b.workstationTier = WorkstationTier.SimpleBench;
            b.skillRequirements = BuildReq(0);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 1f, 1f);
            b.materials = new ItemRequirement[] { M("WoodLog", 5), M("WoodPlank", 3), M("Nails", 4) };
        }

        // 熔炉
        {
            var b = Create("Buildable_Furnace");
            b.displayName = "熔炉";
            b.description = "高温冶炼设备。可熔炼矿石、铸造金属锭、回收金属制品。";
            b.category = BuildableCategory.Workstation;
            b.buildDuration = 10f;
            b.maxHealth = 120f;
            b.isWorkstation = true;
            b.workstationTier = WorkstationTier.Furnace;
            b.skillRequirements = BuildReq(1);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 1.5f, 1.5f);
            b.staminaDrainPerSec = 2f;
            b.materials = new ItemRequirement[] { M("Stone", 20), M("Clay", 10), M("IronIngot", 2) };
        }

        // 中级工作台
        {
            var b = Create("Buildable_MediumBench");
            b.displayName = "中级工作台";
            b.description = "精密手工制造。可制作中级工具、武器、护甲、弹药和家具。";
            b.category = BuildableCategory.Workstation;
            b.buildDuration = 8f;
            b.maxHealth = 100f;
            b.isWorkstation = true;
            b.workstationTier = WorkstationTier.MediumBench;
            b.skillRequirements = BuildReq(2);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.2f, 1.5f);
            b.materials = new ItemRequirement[] { M("WoodPlank", 8), M("ScrapMetal", 6), M("CommonParts", 4), M("Nails", 5) };
        }

        // 高级工作台
        {
            var b = Create("Buildable_AdvancedBench");
            b.displayName = "高级工作台";
            b.description = "精密制造与组装。可制作高级零件、精密工具、高级武器和高级护甲。";
            b.category = BuildableCategory.Workstation;
            b.buildDuration = 12f;
            b.maxHealth = 150f;
            b.isWorkstation = true;
            b.workstationTier = WorkstationTier.AdvancedBench;
            b.skillRequirements = BuildReq(3);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2.5f, 1.2f, 2f);
            b.staminaDrainPerSec = 1f;
            b.materials = new ItemRequirement[] { M("SteelIngot", 3), M("AdvancedParts", 4), M("CommonParts", 8), M("Gear", 2) };
        }

        // 研究中心
        {
            var b = Create("Buildable_Chemistry");
            b.displayName = "研究中心";
            b.description = "工业技术研发设施。消耗材料研究解锁工业设备配方。";
            b.category = BuildableCategory.Workstation;
            b.buildDuration = 15f;
            b.maxHealth = 120f;
            b.isWorkstation = true;
            b.workstationTier = WorkstationTier.Chemistry;
            b.skillRequirements = BuildReq(3);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 1.5f);
            b.materials = new ItemRequirement[] { M("GlassPane", 4), M("SteelPipe", 3), M("ChemicalAgent", 2), M("PlasticScrap", 5) };
        }

        // 机械加工台
        {
            var b = Create("Buildable_Machining");
            b.displayName = "机械加工台";
            b.description = "工业级机械加工。可制作车辆配件、电力设备、高级机械和动力装置。";
            b.category = BuildableCategory.Workstation;
            b.buildDuration = 18f;
            b.maxHealth = 180f;
            b.isWorkstation = true;
            b.workstationTier = WorkstationTier.Machining;
            b.skillRequirements = BuildReq(5);
            b.snapSize = 1f;
            b.placementSize = new Vector3(3f, 1.5f, 2.5f);
            b.staminaDrainPerSec = 1f;
            b.materials = new ItemRequirement[] { M("WS_Machining", 1), M("SteelIngot", 4), M("IronIngot", 6), M("SteelPipe", 3) };
        }

        // 电子装配台
        {
            var b = Create("Buildable_ElectronicsAssembly");
            b.displayName = "电子装配台";
            b.description = "精密电子制造。可制作芯片、传感器、电磁武器和智能设备。需要供电。";
            b.category = BuildableCategory.Workstation;
            b.buildDuration = 20f;
            b.maxHealth = 150f;
            b.isWorkstation = true;
            b.workstationTier = WorkstationTier.ElectronicsAssembly;
            b.skillRequirements = Skills((SkillType.建造拆解, 8), (SkillType.智力, 8));
            b.snapSize = 1f;
            b.placementSize = new Vector3(2.5f, 1.5f, 2f);
            b.staminaDrainPerSec = 1f;
            b.powerRequired = 100f;
            b.materials = new ItemRequirement[] { M("WS_ElectronicsAssembly", 1), M("SteelIngot", 5), M("CircuitBoard", 3), M("Coil", 2) };
        }
    }

    // ================================================================
    // 工业设备 (19)
    // ================================================================

    static void CreateIndustrialDevices()
    {
        // 冲压机
        {
            var b = Create("Buildable_PressMachine");
            b.displayName = "冲压机";
            b.description = "批量生产通用零件和标准件。初级自动化设备。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 12f;
            b.maxHealth = 150f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("PressMachine");
            b.skillRequirements = BuildReq(2);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 2f, 2f);
            b.staminaDrainPerSec = 5f;
            b.materials = new ItemRequirement[] { M("WS_PressMachine", 1), M("SteelIngot", 4), M("IronIngot", 6) };
        }

        // 工业炉
        {
            var b = Create("Buildable_IndustrialFurnace");
            b.displayName = "工业炉";
            b.description = "高温批量冶炼。可自动化熔炼矿石和生产金属锭。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 15f;
            b.maxHealth = 200f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("IndustrialFurnace");
            b.skillRequirements = BuildReq(6);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2.5f, 2.5f, 2.5f);
            b.staminaDrainPerSec = 3f;
            b.materials = new ItemRequirement[] { M("WS_IndustrialFurnace", 1), M("StoneBrick", 15), M("SteelIngot", 5), M("IronIngot", 3) };
        }

        // 电解槽
        {
            var b = Create("Buildable_ElectrolysisTank");
            b.displayName = "电解槽";
            b.description = "电解水制氢气和氧气。需要电力驱动。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 15f;
            b.maxHealth = 120f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("ElectrolysisTank");
            b.skillRequirements = BuildReq(6);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 2f);
            b.materials = new ItemRequirement[] { M("WS_ElectrolysisTank", 1), M("SteelPipe", 5), M("CopperIngot", 4), M("GlassPane", 3) };
        }

        // 发酵罐
        {
            var b = Create("Buildable_Fermenter");
            b.displayName = "发酵罐";
            b.description = "生物发酵。可生产酒精、有机酸和生物燃料。";
            b.category = BuildableCategory.ChemicalIndustry;
            b.buildDuration = 10f;
            b.maxHealth = 80f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("Fermenter");
            b.skillRequirements = BuildReq(2);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 2f, 1.5f);
            b.materials = new ItemRequirement[] { M("WS_Fermenter", 1), M("GlassPane", 4), M("SteelPipe", 3), M("Rubber", 2) };
        }

        // 蒸馏器
        {
            var b = Create("Buildable_Distiller");
            b.displayName = "蒸馏器";
            b.description = "液体分离纯化。可生产蒸馏水、精炼酒精和化工溶剂。";
            b.category = BuildableCategory.ChemicalIndustry;
            b.buildDuration = 12f;
            b.maxHealth = 100f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("Distiller");
            b.skillRequirements = BuildReq(2);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 2.5f, 1.5f);
            b.materials = new ItemRequirement[] { M("WS_Distiller", 1), M("GlassPane", 3), M("SteelPipe", 4), M("CopperIngot", 3) };
        }

        // 粉碎机
        {
            var b = Create("Buildable_Crusher");
            b.displayName = "粉碎机";
            b.description = "原料粉碎与研磨。可将矿石粉碎为矿粉、回收建筑废料。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 10f;
            b.maxHealth = 150f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("Crusher");
            b.skillRequirements = BuildReq(2);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 2f, 2f);
            b.staminaDrainPerSec = 2f;
            b.materials = new ItemRequirement[] { M("WS_Crusher", 1), M("SteelIngot", 3), M("IronIngot", 5), M("StoneBrick", 4) };
        }

        // 织布机
        {
            var b = Create("Buildable_Loom");
            b.displayName = "织布机";
            b.description = "自动纺织设备。可将植物纤维织成线和布。";
            b.category = BuildableCategory.BioIndustry;
            b.buildDuration = 8f;
            b.maxHealth = 80f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("Loom");
            b.skillRequirements = BuildReq(1);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 1.5f, 1.5f);
            b.materials = new ItemRequirement[] { M("WS_Loom", 1), M("WoodPlank", 6), M("Nails", 4), M("IronIngot", 2) };
        }

        // 锯木机
        {
            var b = Create("Buildable_Sawmill");
            b.displayName = "锯木机";
            b.description = "自动木材加工。将原木锯成木板和木棍。";
            b.category = BuildableCategory.BioIndustry;
            b.buildDuration = 8f;
            b.maxHealth = 100f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("Sawmill");
            b.skillRequirements = BuildReq(1);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 2f);
            b.staminaDrainPerSec = 2f;
            b.materials = new ItemRequirement[] { M("WS_Sawmill", 1), M("WoodPlank", 4), M("IronIngot", 3), M("Nails", 4) };
        }

        // 水泵
        {
            var b = Create("Buildable_WaterPump");
            b.displayName = "水泵";
            b.description = "自动取水设备。从地下抽取清水。";
            b.category = BuildableCategory.BioIndustry;
            b.buildDuration = 6f;
            b.maxHealth = 100f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("WaterPump");
            b.skillRequirements = BuildReq(1);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1f, 1.5f, 1f);
            b.materials = new ItemRequirement[] { M("WS_WaterPump", 1), M("IronIngot", 4), M("SteelPipe", 2), M("Rubber", 2) };
        }

        // 炭窑
        {
            var b = Create("Buildable_Kiln");
            b.displayName = "炭窑";
            b.description = "高温窑炉。烧制木炭和砖块。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 10f;
            b.maxHealth = 120f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("Kiln");
            b.skillRequirements = BuildReq(1);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 1.5f, 1.5f);
            b.materials = new ItemRequirement[] { M("WS_Kiln", 1), M("StoneBrick", 6), M("Clay", 4), M("IronIngot", 2) };
        }

        // 车床
        {
            var b = Create("Buildable_Lathe");
            b.displayName = "车床";
            b.description = "金属精密加工。可制造齿轮、轴承和弹簧组件。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 14f;
            b.maxHealth = 150f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("Lathe");
            b.skillRequirements = BuildReq(6);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 2f);
            b.staminaDrainPerSec = 2f;
            b.materials = new ItemRequirement[] { M("WS_Lathe", 1), M("SteelIngot", 6), M("IronIngot", 4), M("SteelPipe", 3) };
        }

        // 装配台
        {
            var b = Create("Buildable_AssemblyTable");
            b.displayName = "装配台";
            b.description = "零件组装设备。可将通用零件组装为高级零件。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 10f;
            b.maxHealth = 120f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("AssemblyTable");
            b.skillRequirements = BuildReq(2);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 1.5f);
            b.materials = new ItemRequirement[] { M("WS_AssemblyTable", 1), M("SteelIngot", 4), M("IronIngot", 3), M("WoodPlank", 4) };
        }

        // 回收站
        {
            var b = Create("Buildable_RecyclingStation");
            b.displayName = "回收站";
            b.description = "废料回收处理。可将废金属和电子垃圾回收为有用材料。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 10f;
            b.maxHealth = 150f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("RecyclingStation");
            b.skillRequirements = BuildReq(2);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 2f, 2f);
            b.staminaDrainPerSec = 1f;
            b.materials = new ItemRequirement[] { M("WS_RecyclingStation", 1), M("SteelIngot", 4), M("IronIngot", 4), M("StoneBrick", 4) };
        }

        // 熏制房
        {
            var b = Create("Buildable_Smokehouse");
            b.displayName = "熏制房";
            b.description = "食物保存设施。可将生肉熏制成可长期保存的熏肉。";
            b.category = BuildableCategory.BioIndustry;
            b.buildDuration = 6f;
            b.maxHealth = 80f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("Smokehouse");
            b.skillRequirements = BuildReq(1);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 2f, 1.5f);
            b.materials = new ItemRequirement[] { M("WS_Smokehouse", 1), M("WoodLog", 8), M("StoneBrick", 4), M("IronIngot", 2) };
        }

        // 罐头封装机
        {
            var b = Create("Buildable_CanningMachine");
            b.displayName = "罐头封装机";
            b.description = "食品罐装设备。可将炖菜封装为可长期保存的罐头。";
            b.category = BuildableCategory.BioIndustry;
            b.buildDuration = 8f;
            b.maxHealth = 100f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("CanningMachine");
            b.skillRequirements = BuildReq(2);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 1.5f, 1.5f);
            b.materials = new ItemRequirement[] { M("WS_CanningMachine", 1), M("IronIngot", 6), M("SteelIngot", 3), M("Rubber", 2) };
        }

        // 制药台
        {
            var b = Create("Buildable_PharmaBench");
            b.displayName = "制药台";
            b.description = "自动制药设备。可批量生产抗生素、消毒水和维生素。";
            b.category = BuildableCategory.ChemicalIndustry;
            b.buildDuration = 12f;
            b.maxHealth = 100f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("PharmaBench");
            b.skillRequirements = Skills((SkillType.建造拆解, 4), (SkillType.医疗生存, 4));
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 1.5f);
            b.materials = new ItemRequirement[] { M("WS_PharmaBench", 1), M("GlassPane", 6), M("SteelIngot", 3), M("CopperIngot", 2) };
        }

        // 广播塔
        {
            var b = Create("Buildable_RadioTower");
            b.displayName = "广播塔";
            b.description = "无线电扫描塔。可分析电子元件获取电路板和高级零件。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 18f;
            b.maxHealth = 200f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("RadioTower");
            b.skillRequirements = Skills((SkillType.建造拆解, 8), (SkillType.智力, 5));
            b.snapSize = 1f;
            b.placementSize = new Vector3(1f, 4f, 1f);
            b.staminaDrainPerSec = 2f;
            b.materials = new ItemRequirement[] { M("WS_RadioTower", 1), M("SteelIngot", 10), M("CopperIngot", 8), M("SteelPipe", 6) };
        }

        // 离心机
        {
            var b = Create("Buildable_Centrifuge");
            b.displayName = "离心机";
            b.description = "高速离心分离设备。加速研究中心产出，可分离血液提取疫苗原液。";
            b.category = BuildableCategory.ChemicalIndustry;
            b.buildDuration = 15f;
            b.maxHealth = 120f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("Centrifuge");
            b.skillRequirements = BuildReq(7);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 2f);
            b.staminaDrainPerSec = 2f;
            b.powerRequired = 80f;
            b.materials = new ItemRequirement[] { M("WS_Centrifuge", 1), M("SteelIngot", 6), M("Coil", 3), M("CircuitBoard", 2) };
        }

        // 精密装配台
        {
            var b = Create("Buildable_PrecisionAssembly");
            b.displayName = "精密装配台";
            b.description = "芯片焊接与电路印刷。可制造芯片组、伺服电机，加速电子装配台产出。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 18f;
            b.maxHealth = 140f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("PrecisionAssembly");
            b.skillRequirements = Skills((SkillType.建造拆解, 8), (SkillType.智力, 8));
            b.snapSize = 1f;
            b.placementSize = new Vector3(2.5f, 1.5f, 2f);
            b.staminaDrainPerSec = 1f;
            b.powerRequired = 120f;
            b.materials = new ItemRequirement[] { M("WS_PrecisionAssembly", 1), M("SteelIngot", 8), M("ChipSet", 1), M("ServoMotor", 2) };
        }

        // 基因分析台
        {
            var b = Create("Buildable_GeneAnalyzer");
            b.displayName = "基因分析台";
            b.description = "基因测序与疫苗合成设备。可制造逆转疫苗和高级医疗品。";
            b.category = BuildableCategory.ChemicalIndustry;
            b.buildDuration = 22f;
            b.maxHealth = 150f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("GeneAnalyzer");
            b.skillRequirements = Skills((SkillType.建造拆解, 9), (SkillType.医疗生存, 9));
            b.snapSize = 1f;
            b.placementSize = new Vector3(2.5f, 2f, 2f);
            b.staminaDrainPerSec = 1f;
            b.powerRequired = 150f;
            b.materials = new ItemRequirement[] { M("WS_GeneAnalyzer", 1), M("GlassPane", 6), M("SteelIngot", 4), M("CircuitBoard", 3) };
        }

        // 净水厂
        {
            var b = Create("Buildable_WaterPurifier");
            b.displayName = "净水厂";
            b.description = "大规模净水设施。自动生产纯净水，满足基地用水需求。";
            b.category = BuildableCategory.ChemicalIndustry;
            b.buildDuration = 20f;
            b.maxHealth = 250f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("WaterPurifier");
            b.skillRequirements = Skills((SkillType.建造拆解, 9), (SkillType.智力, 7));
            b.snapSize = 1f;
            b.placementSize = new Vector3(3f, 2f, 3f);
            b.staminaDrainPerSec = 3f;
            b.powerRequired = 200f;
            b.powerRequiresWater = true;
            b.materials = new ItemRequirement[] { M("WS_WaterPurifier", 1), M("SteelPipe", 8), M("StoneBrick", 15), M("CapacitorBank", 2) };
        }

        // 移动要塞基地车
        {
            var b = Create("Buildable_MobileFortress");
            b.displayName = "移动要塞基地车";
            b.description = "大巴/卡车改装移动基地。内置工作台+8格储物+床位(睡觉存档)+车顶瞭望台。油耗4倍，噪音50m。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 30f;
            b.maxHealth = 800f;
            b.isWorkstation = false;
            b.skillRequirements = BuildReq(9);
            b.snapSize = 1f;
            b.placementSize = new Vector3(6f, 3f, 3f);
            b.staminaDrainPerSec = 5f;
            b.materials = new ItemRequirement[] { M("WS_MobileFortress", 1), M("SteelIngot", 20), M("SteelPipe", 10), M("CapacitorBank", 4), M("ServoMotor", 2) };
        }

        // 自动炮塔
        {
            var b = Create("Buildable_AutoTurret");
            b.displayName = "自动炮塔";
            b.description = "全自动防御炮塔。自动锁定并射击范围内僵尸，需供电。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 25f;
            b.maxHealth = 400f;
            b.isWorkstation = false;
            b.skillRequirements = BuildReq(10);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 2f, 1.5f);
            b.staminaDrainPerSec = 3f;
            b.powerRequired = 300f;
            b.materials = new ItemRequirement[] { M("WS_AutoTurret", 1), M("TitaniumAlloy", 8), M("ServoMotor", 3), M("ChipSet", 2) };
        }

        // 电磁围栏
        {
            var b = Create("Buildable_EMFence");
            b.displayName = "电磁围栏";
            b.description = "高压电磁屏障。僵尸触碰即被电击麻痹，需大量电力。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 20f;
            b.maxHealth = 500f;
            b.isWorkstation = false;
            b.skillRequirements = BuildReq(10);
            b.snapSize = 1f;
            b.placementSize = new Vector3(1f, 2.5f, 0.3f);
            b.staminaDrainPerSec = 2f;
            b.powerRequired = 500f;
            b.materials = new ItemRequirement[] { M("WS_EMFence", 1), M("TitaniumAlloy", 6), M("CapacitorBank", 5), M("Coil", 10) };
        }

        // 无人机平台
        {
            var b = Create("Buildable_DronePlatform");
            b.displayName = "无人机平台";
            b.description = "侦察无人机起降平台。自动巡逻侦察，标记范围内僵尸位置。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 25f;
            b.maxHealth = 300f;
            b.isWorkstation = false;
            b.skillRequirements = Skills((SkillType.建造拆解, 10), (SkillType.智力, 8));
            b.snapSize = 1f;
            b.placementSize = new Vector3(3f, 0.5f, 3f);
            b.staminaDrainPerSec = 1f;
            b.powerRequired = 250f;
            b.materials = new ItemRequirement[] { M("WS_DronePlatform", 1), M("CarbonFiber", 6), M("ServoMotor", 4), M("OpticalLens", 2), M("ChipSet", 1) };
        }

        // 核电站
        {
            var b = Create("Buildable_NuclearPlant");
            b.displayName = "核电站";
            b.description = "终极发电设施。消耗浓缩铀产生巨额电力(5000W)，需水冷。噪音极大(100m)，爆炸半径20m。";
            b.category = BuildableCategory.EnergyIndustry;
            b.buildDuration = 40f;
            b.maxHealth = 2000f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("NuclearPlant");
            b.skillRequirements = Skills((SkillType.建造拆解, 10), (SkillType.智力, 9));
            b.snapSize = 1f;
            b.placementSize = new Vector3(5f, 3f, 5f);
            b.staminaDrainPerSec = 5f;
            b.powerOutput = 5000f;
            b.powerSourceType = _Game.Systems.Power.PowerSourceType.Nuclear;
            b.powerRequiresFuel = true;
            b.powerFuelPerHour = 0.1f;
            b.powerFuelItemName = "浓缩铀";
            b.powerFuelItemData = GetItem("EnrichedUranium");
            b.powerNoiseRadius = 100f;
            b.powerRequiresWater = true;
            b.materials = new ItemRequirement[] { M("WS_NuclearPlant", 1), M("TitaniumAlloy", 10), M("SteelIngot", 20), M("SteelPipe", 12), M("CapacitorBank", 5) };
        }

        // AI机器人
        {
            var b = Create("Buildable_AIBot");
            b.displayName = "AI机器人";
            b.description = "自主AI战斗伙伴。跟随玩家，自动拾取战利品(200kg负重)，电击防御。限1台/人。消耗电池组驱动，可额外加入浓缩铀启动增强模式(电击伤害+50%、移速+30%)。双进度条：电量+核燃料。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 30f;
            b.maxHealth = 500f;
            b.isWorkstation = false;
            b.skillRequirements = Skills((SkillType.建造拆解, 10), (SkillType.智力, 10));
            b.snapSize = 1f;
            b.placementSize = new Vector3(1.5f, 2f, 1.5f);
            b.staminaDrainPerSec = 3f;
            b.materials = new ItemRequirement[] { M("AICore", 1), M("AutomationController", 1), M("SatelliteDish", 1), M("SmallReactor", 1), M("WS_SolarPanel", 1), M("TitaniumAlloy", 5), M("AluminumIngot", 8), M("ServoMotor", 3), M("CapacitorBank", 3), M("ChipSet", 2), M("BatteryPack", 5) };
        }

        // ================================================================
        // 新增7个工业设备 (v0.22)
        // ================================================================

        // 拉线机 — 电子链
        {
            var b = Create("Buildable_WireDrawer");
            b.displayName = "拉线机";
            b.description = "金属拉丝设备。将铜锭拉成电线和电缆。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 12f;
            b.maxHealth = 120f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("WireDrawer");
            b.skillRequirements = BuildReq(5);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 2f);
            b.materials = new ItemRequirement[] { M("WS_WireDrawer", 1), M("SteelIngot", 4), M("CopperIngot", 4), M("IronIngot", 3) };
        }

        // 电池生产线 — 电子链
        {
            var b = Create("Buildable_BatteryLine");
            b.displayName = "电池生产线";
            b.description = "电池自动装配线。批量生产电池和电池组。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 15f;
            b.maxHealth = 140f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("BatteryLine");
            b.skillRequirements = BuildReq(5);
            b.snapSize = 1f;
            b.placementSize = new Vector3(2.5f, 2f, 2f);
            b.materials = new ItemRequirement[] { M("WS_BatteryLine", 1), M("SteelIngot", 5), M("CopperIngot", 3), M("CircuitBoard", 2) };
        }

        // 电子装配机 — 电子链
        {
            var b = Create("Buildable_ElectronicsAssembler");
            b.displayName = "电子装配机";
            b.description = "电子元件自动装配。批量生产电子元件、线圈和电容组。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 18f;
            b.maxHealth = 140f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("ElectronicsAssembler");
            b.skillRequirements = Skills((SkillType.建造拆解, 6), (SkillType.智力, 4));
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 2f);
            b.materials = new ItemRequirement[] { M("WS_ElectronicsAssembler", 1), M("SteelIngot", 5), M("CopperIngot", 4), M("CircuitBoard", 3) };
        }

        // 电路印刷机 — 电子链
        {
            var b = Create("Buildable_CircuitPrinter");
            b.displayName = "电路印刷机";
            b.description = "PCB电路板印刷设备。批量生产电路板。";
            b.category = BuildableCategory.ElectronicsIndustry;
            b.buildDuration = 20f;
            b.maxHealth = 130f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("CircuitPrinter");
            b.skillRequirements = Skills((SkillType.建造拆解, 7), (SkillType.智力, 5));
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 1.5f, 2f);
            b.materials = new ItemRequirement[] { M("WS_CircuitPrinter", 1), M("SteelIngot", 4), M("CopperIngot", 3), M("ChipSet", 1), M("CircuitBoard", 2) };
        }

        // 火药厂 — 化学链
        {
            var b = Create("Buildable_GunpowderFactory");
            b.displayName = "火药厂";
            b.description = "火药制造设备。批量生产黑火药、无烟火药和塑料碎片。";
            b.category = BuildableCategory.ChemicalIndustry;
            b.buildDuration = 18f;
            b.maxHealth = 180f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("GunpowderFactory");
            b.skillRequirements = Skills((SkillType.建造拆解, 5), (SkillType.医疗生存, 4));
            b.snapSize = 1f;
            b.placementSize = new Vector3(2.5f, 2f, 2.5f);
            b.staminaDrainPerSec = 2f;
            b.materials = new ItemRequirement[] { M("WS_GunpowderFactory", 1), M("SteelIngot", 6), M("StoneBrick", 8), M("SteelPipe", 3) };
        }

        // 弹药装填机 — 金属链
        {
            var b = Create("Buildable_AmmoLoader");
            b.displayName = "弹药装填机";
            b.description = "弹药自动装填设备。批量生产各类子弹和穿甲弹。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 20f;
            b.maxHealth = 200f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("AmmoLoader");
            b.skillRequirements = Skills((SkillType.建造拆解, 6), (SkillType.枪械专精, 3));
            b.snapSize = 1f;
            b.placementSize = new Vector3(2.5f, 2f, 2f);
            b.staminaDrainPerSec = 2f;
            b.materials = new ItemRequirement[] { M("WS_AmmoLoader", 1), M("SteelIngot", 8), M("SteelPipe", 4), M("Gear", 3), M("AdvancedParts", 2) };
        }

        // 武器组装台 — 金属链
        {
            var b = Create("Buildable_WeaponAssembly");
            b.displayName = "武器组装台";
            b.description = "武器精密组装设备。可制造手枪、步枪、霰弹枪、重狙和防弹衣。";
            b.category = BuildableCategory.MetalIndustry;
            b.buildDuration = 25f;
            b.maxHealth = 250f;
            b.isWorkstation = false;
            b.productionDeviceRef = LoadProdRef("WeaponAssembly");
            b.skillRequirements = Skills((SkillType.建造拆解, 7), (SkillType.枪械专精, 4));
            b.snapSize = 1f;
            b.placementSize = new Vector3(3f, 2f, 2.5f);
            b.staminaDrainPerSec = 3f;
            b.materials = new ItemRequirement[] { M("WS_WeaponAssembly", 1), M("SteelIngot", 10), M("SteelPipe", 5), M("AdvancedParts", 4), M("Gear", 4) };
        }
    }

    // ================================================================
    // 基础建筑/家具/路障 (15)
    // ================================================================

    static void CreateAdditionalStructures()
    {
        // 木墙
        {
            var b = Create("Buildable_WoodWall");
            b.displayName = "木墙";
            b.description = "基础木制墙壁。用于围住安全区。";
            b.category = BuildableCategory.Wall;
            b.buildDuration = 3f;
            b.maxHealth = 80f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1f, 2f, 0.15f);
            b.materials = new ItemRequirement[] { M("WoodPlank", 4), M("Nails", 4) };
        }

        // 木门
        {
            var b = Create("Buildable_WoodDoor");
            b.displayName = "木门";
            b.description = "可开关的木门。有门才有家。";
            b.category = BuildableCategory.Wall;
            b.buildDuration = 5f;
            b.maxHealth = 100f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1f, 2f, 0.15f);
            b.skillRequirements = BuildReq(1);
            b.materials = new ItemRequirement[] { M("WoodPlank", 3), M("Nails", 2) };
        }

        // 木窗框
        {
            var b = Create("Buildable_WoodWindow");
            b.displayName = "木窗框";
            b.description = "带窗口的木墙段。可以通过窗户观察外面。";
            b.category = BuildableCategory.Wall;
            b.buildDuration = 4f;
            b.maxHealth = 60f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1f, 2f, 0.15f);
            b.skillRequirements = BuildReq(1);
            b.materials = new ItemRequirement[] { M("WoodPlank", 2), M("Nails", 2) };
        }

        // 木地板
        {
            var b = Create("Buildable_WoodFloor");
            b.displayName = "木地板";
            b.description = "木制地板。铺在泥地上走路不沾泥。";
            b.category = BuildableCategory.Floor;
            b.buildDuration = 2f;
            b.maxHealth = 50f;
            b.snapSize = 1f;
            b.placementSize = new Vector3(2f, 0.1f, 2f);
            b.materials = new ItemRequirement[] { M("WoodPlank", 3), M("Nails", 2) };
        }

        // 木桌
        {
            var b = Create("Buildable_WoodTable");
            b.displayName = "木桌";
            b.description = "木制桌子。可以放东西。";
            b.category = BuildableCategory.Furniture;
            b.buildDuration = 4f;
            b.maxHealth = 40f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1.5f, 0.8f, 1f);
            b.skillRequirements = BuildReq(1);
            b.deconstructReturnRate = 0.7f;
            b.materials = new ItemRequirement[] { M("WoodPlank", 4), M("Nails", 4) };
        }

        // 门板路障
        {
            var b = Create("Buildable_BarricadeDoor");
            b.displayName = "门板路障";
            b.description = "加固门用的木板。多钉几层更安全。";
            b.category = BuildableCategory.Barricade;
            b.buildDuration = 2f;
            b.maxHealth = 60f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1f, 2f, 0.1f);
            b.deconstructReturnRate = 0.3f;
            b.materials = new ItemRequirement[] { M("WoodPlank", 4), M("Nails", 6) };
        }

        // 窗板路障
        {
            var b = Create("Buildable_BarricadeWindow");
            b.displayName = "窗板路障";
            b.description = "钉在窗户上的木板。僵尸敲开前能挡一阵。";
            b.category = BuildableCategory.Barricade;
            b.buildDuration = 1.5f;
            b.maxHealth = 40f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1f, 1.5f, 0.1f);
            b.deconstructReturnRate = 0.3f;
            b.materials = new ItemRequirement[] { M("WoodPlank", 3), M("Nails", 4) };
        }

        // 石墙
        {
            var b = Create("Buildable_StoneWall");
            b.displayName = "石墙";
            b.description = "坚固的石制墙壁，比木墙更耐久。";
            b.category = BuildableCategory.Wall;
            b.buildDuration = 8f;
            b.maxHealth = 200f;
            b.snapSize = 1f;
            b.placementSize = new Vector3(1f, 2f, 0.3f);
            b.skillRequirements = BuildReq(1);
            b.staminaDrainPerSec = 3f;
            b.materials = new ItemRequirement[] { M("Stone", 15), M("Cement", 3) };
        }

        // 金属墙
        {
            var b = Create("Buildable_MetalWall");
            b.displayName = "金属墙";
            b.description = "金属板焊接的墙壁，极高耐久。";
            b.category = BuildableCategory.Wall;
            b.buildDuration = 12f;
            b.maxHealth = 400f;
            b.snapSize = 1f;
            b.placementSize = new Vector3(1f, 2f, 0.2f);
            b.skillRequirements = BuildReq(2);
            b.staminaDrainPerSec = 2f;
            b.materials = new ItemRequirement[] { M("ScrapMetal", 12), M("SteelIngot", 3), M("Screw", 8) };
        }

        // 石地板
        {
            var b = Create("Buildable_StoneFloor");
            b.displayName = "石地板";
            b.description = "石制地板，耐用且美观。";
            b.category = BuildableCategory.Floor;
            b.buildDuration = 5f;
            b.maxHealth = 150f;
            b.snapSize = 1f;
            b.placementSize = new Vector3(1f, 0.1f, 1f);
            b.staminaDrainPerSec = 2f;
            b.materials = new ItemRequirement[] { M("Stone", 8), M("Cement", 2) };
        }

        // 床
        {
            var b = Create("Buildable_Bed");
            b.displayName = "床";
            b.description = "用于睡觉恢复体力和保存游戏。";
            b.category = BuildableCategory.Furniture;
            b.buildDuration = 8f;
            b.maxHealth = 50f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(2f, 1f, 1.5f);
            b.blocksNavMesh = false;
            b.materials = new ItemRequirement[] { M("WoodLog", 6), M("ClothRoll", 4), M("Rope", 3), M("Nails", 2) };
        }

        // 大型木箱
        {
            var b = Create("Buildable_LargeCrate");
            b.displayName = "大型木箱";
            b.description = "大型存储容器，6×4 格。";
            b.category = BuildableCategory.Furniture;
            b.buildDuration = 6f;
            b.maxHealth = 60f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1.5f, 1.5f, 1.5f);
            b.blocksNavMesh = false;
            b.materials = new ItemRequirement[] { M("WoodPlank", 8), M("Nails", 6), M("IronIngot", 2) };
        }

        // 晾肉架
        {
            var b = Create("Buildable_DryingRack");
            b.displayName = "晾肉架";
            b.description = "风干肉类，延长保存时间。";
            b.category = BuildableCategory.Furniture;
            b.buildDuration = 4f;
            b.maxHealth = 30f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1.5f, 2f, 0.5f);
            b.blocksNavMesh = false;
            b.materials = new ItemRequirement[] { M("Branch", 4), M("Rope", 3) };
        }

        // 木栅栏
        {
            var b = Create("Buildable_WoodFence");
            b.displayName = "木栅栏";
            b.description = "简易木制障碍物，可阻挡僵尸行进。";
            b.category = BuildableCategory.Barricade;
            b.buildDuration = 4f;
            b.maxHealth = 100f;
            b.snapSize = 1f;
            b.placementSize = new Vector3(1f, 1.5f, 0.1f);
            b.materials = new ItemRequirement[] { M("WoodLog", 4), M("Branch", 6) };
        }

        // 陷阱
        {
            var b = Create("Buildable_Trap");
            b.displayName = "陷阱";
            b.description = "地面陷阱，可伤害经过的僵尸。";
            b.category = BuildableCategory.Barricade;
            b.buildDuration = 5f;
            b.maxHealth = 50f;
            b.snapSize = 0.5f;
            b.placementSize = new Vector3(1f, 0.2f, 1f);
            b.blocksNavMesh = false;
            b.materials = new ItemRequirement[] { M("ScrapMetal", 4), M("Spring", 3), M("Rope", 2) };
        }
    }

    // ================================================================
    // 更新目录
    // ================================================================

    static void UpdateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<BuildableCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("[CreateDefaultBuildables] 未找到 BuildableCatalog.asset，创建新目录");
            catalog = ScriptableObject.CreateInstance<BuildableCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        // 收集所有 BuildableData
        var guids = AssetDatabase.FindAssets("t:BuildableData", new[] { Dir });
        var all = new BuildableData[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            all[i] = AssetDatabase.LoadAssetAtPath<BuildableData>(path);
        }

        // 按 category 排序
        System.Array.Sort(all, (a, b) =>
        {
            int cmp = a.category.CompareTo(b.category);
            if (cmp != 0) return cmp;
            return string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal);
        });

        catalog.buildables = all;
        EditorUtility.SetDirty(catalog);
        Debug.Log($"[CreateDefaultBuildables] 目录已更新，共 {all.Length} 个建造物");
    }
}
