using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键生成所有生产设备数据 .asset 文件。
/// 用法：先执行 Create Crafting Items, 再执行本菜单。
/// 菜单栏 → Game Tools → Create Production Devices
///
/// 生成 ~19 ProductionDeviceData，覆盖从简易工作台到机械加工台的生产设备。
/// 生产设备 = 放置后自运转：消耗原料 → 产出 → 放入输出槽。
/// </summary>
public static class CreateProductionDevices
{
    const string Dir = "Assets/_Game/Config/ProductionDevices";

    static Dictionary<string, ItemData> _itemLookup;

    [MenuItem("Game Tools/Create Production Devices")]
    public static void CreateAll()
    {
        BuildItemLookup();
        EnsureDir();

        CreatePressMachine();
        CreateIndustrialFurnace();
        CreateCrusher();
        CreateLoom();
        CreateSawmill();
        CreateWaterPump();
        CreateFermenter();
        CreateDistiller();
        CreateElectrolysisTank();
        CreateKiln();
        CreateElectricGenerator();
        CreateLathe();
        CreateAssemblyTable();
        CreateRecyclingStation();
        CreateSolarPanel();
        CreateSmokehouse();
        CreateCanningMachine();
        CreatePharmaBench();
        CreateRadioTower();
        CreateCentrifuge();
        CreateGeneAnalyzer();
        CreatePrecisionAssembly();
        CreateWaterPurifier();
        CreateAIBot();
        CreateNuclearPlant();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"生产设备资产生成完毕！路径: {Dir}");
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
        Debug.Log($"[CreateProductionDevices] 已索引 {_itemLookup.Count} 个 ItemData");
    }

    static void EnsureDir()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Game/Config", "ProductionDevices");
    }

    static ItemData GetItem(string assetName)
    {
        if (_itemLookup.TryGetValue(assetName, out var item))
            return item;
        Debug.LogWarning($"[CreateProductionDevices] 未找到 ItemData: {assetName}");
        return null;
    }

    static ProductionDeviceData Create(string assetName, string displayName,
        WorkstationTier tier, float interval, int batchSize = 1,
        bool requiresFuel = false, ItemData fuel = null, float fuelPerCycle = 1f,
        bool acceptsAutomation = true, float automationMultiplier = 2f,
        ProductionRecipe[] recipes = null)
    {
        string path = $"{Dir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ProductionDeviceData>(path);
        if (existing == null)
        {
            existing = ScriptableObject.CreateInstance<ProductionDeviceData>();
            AssetDatabase.CreateAsset(existing, path);
        }

        existing.deviceName = displayName;
        existing.tier = tier;
        existing.productionInterval = interval;
        existing.batchSize = batchSize;
        existing.requiresFuel = requiresFuel;
        existing.fuelItem = fuel;
        existing.fuelPerCycle = fuelPerCycle;
        existing.acceptsAutomation = acceptsAutomation;
        existing.automationMultiplier = automationMultiplier;
        existing.recipes = recipes ?? new ProductionRecipe[0];
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static ProductionRecipe R(string inputName, int inputCount, string outputName, int outputCount, float baseTime = 5f)
    {
        return new ProductionRecipe
        {
            input = GetItem(inputName),
            inputCount = inputCount,
            output = GetItem(outputName),
            outputCount = outputCount,
            baseTime = baseTime
        };
    }

    // ================================================================
    // 冲压机 — 通用零件批量生产
    // ================================================================

    static void CreatePressMachine()
    {
        Create("PressMachine", "冲压机",
            WorkstationTier.MediumBench, interval: 4f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("ScrapMetal", 3, "CommonParts", 5, 4f),
                R("IronIngot", 2, "CommonParts", 8, 3f),
                R("IronIngot", 1, "Nails", 15, 2f),
            });
    }

    // ================================================================
    // 工业炉 — 自动冶炼
    // ================================================================

    static void CreateIndustrialFurnace()
    {
        Create("IndustrialFurnace", "工业炉",
            WorkstationTier.AdvancedBench, interval: 5f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 2f,
            recipes: new[]
            {
                R("IronOre", 3, "IronIngot", 2, 4f),
                R("CopperOre", 3, "CopperIngot", 2, 4f),
                R("IronIngot", 2, "SteelIngot", 1, 6f),
                R("ScrapMetal", 4, "IronIngot", 1, 5f),
                R("Limestone", 2, "Cement", 3, 4f),
                R("Sand", 3, "GlassPane", 2, 3f),
            });
    }

    // ================================================================
    // 粉碎机 — 原料粉碎与回收
    // ================================================================

    static void CreateCrusher()
    {
        Create("Crusher", "粉碎机",
            WorkstationTier.MediumBench, interval: 3f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("Stone", 2, "Sand", 3, 3f),
                R("Limestone", 2, "Cement", 2, 3f),
                R("WoodLog", 1, "WoodPlank", 3, 2f),
                R("ScrapMetal", 3, "IronOre", 1, 5f),
                R("AnimalBone", 2, "Fertilizer", 1, 4f),
            });
    }

    // ================================================================
    // 织布机 — 纺织自动化
    // ================================================================

    static void CreateLoom()
    {
        Create("Loom", "织布机",
            WorkstationTier.SimpleBench, interval: 3f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                R("PlantFiber", 3, "Thread", 5, 3f),
                R("Thread", 5, "ClothRoll", 1, 4f),
                R("PlantFiber", 8, "ClothRoll", 1, 6f),
            });
    }

    // ================================================================
    // 锯木机 — 木材加工
    // ================================================================

    static void CreateSawmill()
    {
        Create("Sawmill", "锯木机",
            WorkstationTier.SimpleBench, interval: 2f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("WoodLog", 1, "WoodPlank", 3, 2f),
                R("WoodLog", 2, "Branch", 5, 2f),
            });
    }

    // ================================================================
    // 水泵 — 自动取水
    // ================================================================

    static void CreateWaterPump()
    {
        Create("WaterPump", "水泵",
            WorkstationTier.SimpleBench, interval: 8f, batchSize: 1,
            requiresFuel: false, acceptsAutomation: true, automationMultiplier: 3f,
            recipes: new[]
            {
                R("Water", 0, "Water", 3, 10f),
            });
    }

    // ================================================================
    // 发酵罐 — 生物发酵
    // ================================================================

    static void CreateFermenter()
    {
        Create("Fermenter", "发酵罐",
            WorkstationTier.MediumBench, interval: 6f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                R("Berry", 5, "Alcohol", 2, 6f),
                R("Mushroom", 4, "ChemicalAgent", 1, 8f),
                R("Herb", 6, "ChemicalAgent", 1, 8f),
            });
    }

    // ================================================================
    // 蒸馏器 — 液体分离纯化
    // ================================================================

    static void CreateDistiller()
    {
        Create("Distiller", "蒸馏器",
            WorkstationTier.AdvancedBench, interval: 5f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("Alcohol", 2, "ChemicalAgent", 1, 5f),
                R("Water", 5, "PurifiedWater", 3, 4f),
                R("Alcohol", 4, "StypticPowder", 1, 6f),
            });
    }

    // ================================================================
    // 电解槽 — 电化学精炼
    // ================================================================

    static void CreateElectrolysisTank()
    {
        Create("ElectrolysisTank", "电解槽",
            WorkstationTier.Machining, interval: 8f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("BatteryPack"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("AluminumOre", 3, "AluminumIngot", 2, 6f),
                R("CopperOre", 3, "CopperIngot", 3, 5f),
                R("Water", 4, "PurifiedWater", 5, 4f),
                R("ChemicalAgent", 1, "SulfuricAcid", 1, 6f),
                R("ScrapMetal", 5, "IronIngot", 2, 5f),
            });
    }

    // ================================================================
    // 炭窑 — 烧炭/烧砖
    // ================================================================

    static void CreateKiln()
    {
        Create("Kiln", "炭窑",
            WorkstationTier.Furnace, interval: 5f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                R("WoodLog", 3, "Coal", 5, 5f),
                R("Clay", 3, "StoneBrick", 3, 4f),
                R("Branch", 5, "Coal", 2, 4f),
            });
    }

    // ================================================================
    // 发电机 — 燃料→电力
    // ================================================================

    static void CreateElectricGenerator()
    {
        Create("ElectricGenerator", "发电机",
            WorkstationTier.Machining, interval: 10f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 2f,
            acceptsAutomation: false,
            recipes: new[]
            {
                R("Coal", 2, "BatteryPack", 1, 8f),
                R("WoodLog", 3, "BatteryPack", 1, 10f),
                R("Alcohol", 5, "BatteryPack", 2, 6f),
            });
    }

    // ================================================================
    // 车床 — 金属精密加工
    // ================================================================

    static void CreateLathe()
    {
        Create("Lathe", "车床",
            WorkstationTier.AdvancedBench, interval: 5f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("IronIngot", 2, "Gear", 1, 5f),
                R("IronIngot", 2, "Bearing", 1, 4f),
                R("SteelIngot", 2, "SpringAssembly", 1, 6f),
            });
    }

    // ================================================================
    // 装配台 — 零件组装
    // ================================================================

    static void CreateAssemblyTable()
    {
        Create("AssemblyTable", "装配台",
            WorkstationTier.MediumBench, interval: 5f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("CommonParts", 4, "AdvancedParts", 1, 6f),
            });
    }

    // ================================================================
    // 回收站 — 废料回收
    // ================================================================

    static void CreateRecyclingStation()
    {
        Create("RecyclingStation", "回收站",
            WorkstationTier.Furnace, interval: 6f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("ScrapMetal", 4, "IronIngot", 1, 5f),
                R("ScrapMetal", 6, "CopperIngot", 1, 6f),
                R("Electronic", 2, "CircuitBoard", 1, 8f),
                R("PlasticScrap", 3, "Rubber", 1, 5f),
            });
    }

    // ================================================================
    // 太阳能板 — 清洁能源（缓慢免费）
    // ================================================================

    static void CreateSolarPanel()
    {
        Create("SolarPanel", "太阳能板",
            WorkstationTier.Machining, interval: 15f, batchSize: 1,
            requiresFuel: false, acceptsAutomation: false,
            recipes: new[]
            {
                R("Water", 0, "BatteryPack", 1, 15f),
            });
    }

    // ================================================================
    // 熏制房 — 食物保存
    // ================================================================

    static void CreateSmokehouse()
    {
        Create("Smokehouse", "熏制房",
            WorkstationTier.SimpleBench, interval: 8f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("WoodLog"), fuelPerCycle: 2f,
            recipes: new[]
            {
                R("RawMeat", 2, "SmokedMeat", 1, 8f),
            });
    }

    // ================================================================
    // 罐头封装机 — 食品罐装
    // ================================================================

    static void CreateCanningMachine()
    {
        Create("CanningMachine", "罐头封装机",
            WorkstationTier.MediumBench, interval: 5f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("Stew", 1, "CanFood", 1, 5f),
            });
    }

    // ================================================================
    // 制药台 — 药品自动化
    // ================================================================

    static void CreatePharmaBench()
    {
        Create("PharmaBench", "制药台",
            WorkstationTier.Chemistry, interval: 8f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                R("Herb", 4, "Antibiotics", 1, 8f),
                R("Herb", 5, "Antiseptic", 1, 6f),
                R("ChemicalAgent", 1, "Vitamin", 2, 7f),
                R("Herb", 3, "StypticPowder", 1, 6f),
            });
    }

    // ================================================================
    // 广播塔 — 无线电扫描
    // ================================================================

    static void CreateRadioTower()
    {
        Create("RadioTower", "广播塔",
            WorkstationTier.Machining, interval: 12f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("BatteryPack"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("Electronic", 2, "CircuitBoard", 1, 12f),
                R("BatteryPack", 1, "AdvancedParts", 1, 10f),
            });
    }

    // ================================================================
    // 终局设备
    // ================================================================

    static void CreateCentrifuge()
    {
        Create("Centrifuge", "离心机",
            WorkstationTier.Machining, interval: 10f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                R("UraniumOre", 5, "EnrichedUranium", 1, 15f),
                R("Herb", 4, "VaccineSerum", 1, 10f),
            });
    }

    static void CreateGeneAnalyzer()
    {
        Create("GeneAnalyzer", "基因分析台",
            WorkstationTier.ElectronicsAssembly, interval: 15f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                R("VaccineSerum", 3, "ZombieVaccine", 1, 20f),
                R("GeneSample", 1, "GeneSample", 2, 20f),
            });
    }

    static void CreatePrecisionAssembly()
    {
        Create("PrecisionAssembly", "精密装配台",
            WorkstationTier.ElectronicsAssembly, interval: 10f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                R("CircuitBoard", 3, "ChipSet", 1, 10f),
                R("Coil", 2, "ServoMotor", 1, 12f),
            });
    }

    static void CreateWaterPurifier()
    {
        Create("WaterPurifier", "净水厂",
            WorkstationTier.Machining, interval: 6f, batchSize: 5,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("Water", 10, "PurifiedWater", 10, 5f),
            });
    }

    static void CreateAIBot()
    {
        Create("AIBot", "AI机器人",
            WorkstationTier.ElectronicsAssembly, interval: 0f, batchSize: 0,
            requiresFuel: false, acceptsAutomation: false,
            recipes: new ProductionRecipe[0]);
    }

    static void CreateNuclearPlant()
    {
        Create("NuclearPlant", "核电站",
            WorkstationTier.Machining, interval: 0f, batchSize: 0,
            requiresFuel: false, acceptsAutomation: false,
            recipes: new ProductionRecipe[0]);
    }
}
