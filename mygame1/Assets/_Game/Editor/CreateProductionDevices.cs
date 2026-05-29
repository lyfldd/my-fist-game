using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键生成所有生产设备数据 .asset 文件。
/// 用法：先执行 Create Crafting Items, 再执行本菜单。
/// 菜单栏 → Game Tools → Create Production Devices
///
/// 生成 ~30 ProductionDeviceData，覆盖全部6条工业链的生产设备。
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
        // CreateRadioTower(); // 广播塔已移出工业设备，改为通讯设施
        CreateCentrifuge();
        CreateGeneAnalyzer();
        CreatePrecisionAssembly();
        CreateWaterPurifier();
        CreateAIBot();
        CreateNuclearPlant();

        // 新增7个工业设备
        CreateWireDrawer();
        CreateBatteryLine();
        CreateElectronicsAssembler();
        CreateCircuitPrinter();
        CreateGunpowderFactory();
        CreateAmmoLoader();
        CreateWeaponAssembly();

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

    // 多材料配方便捷方法
    static ProductionRecipe RM(string outputName, int outputCount, float baseTime, params (string name, int count)[] inputs)
    {
        var reqs = new ItemRequirement[inputs.Length];
        for (int i = 0; i < inputs.Length; i++)
            reqs[i] = new ItemRequirement { itemData = GetItem(inputs[i].name), count = inputs[i].count };
        return new ProductionRecipe
        {
            inputs = reqs,
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
            WorkstationTier.Machining, interval: 4f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("ScrapMetal", 3, "CommonParts", 5, 4f),
                R("IronIngot", 2, "CommonParts", 8, 3f),
                R("IronIngot", 1, "Nails", 15, 2f),
                R("SteelIngot", 2, "SteelPipe", 3, 5f),
                R("SteelIngot", 2, "Rebar", 4, 5f),
                R("IronIngot", 1, "Screw", 10, 3f),
                R("CopperIngot", 1, "BulletCasing", 8, 4f),
                R("LeadIngot", 1, "BulletHead", 10, 4f),
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
                R("ScrapMetal", 4, "IronIngot", 1, 6f),
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
                R("WoodLog", 3, "WoodPlank", 6, 3f),       // 批量锯木板（效率提升）
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
                R("Alcohol", 5, "Antiseptic", 1, 6f),    // 蒸馏浓缩酒精→消毒水
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
                RM("BatteryPack", 2, 8f, ("EmptyBattery", 1), ("Coal", 2)),
                RM("BatteryPack", 1, 10f, ("EmptyBattery", 1), ("WoodLog", 3)),
                RM("BatteryPack", 3, 6f, ("EmptyBattery", 2), ("Alcohol", 5)),
            });
    }

    // ================================================================
    // 车床 — 金属精密加工
    // ================================================================

    static void CreateLathe()
    {
        Create("Lathe", "车床",
            WorkstationTier.Machining, interval: 5f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("IronIngot", 2, "Gear", 1, 5f),
                R("IronIngot", 2, "Bearing", 1, 4f),
                R("SteelIngot", 2, "SpringAssembly", 1, 6f),
                R("IronIngot", 2, "Spring", 4, 4f),
                // RM("TitaniumAlloy", ...) // 钛合金暂缺ItemData
            });
    }

    // ================================================================
    // 装配台 — 零件组装
    // ================================================================

    static void CreateAssemblyTable()
    {
        Create("AssemblyTable", "装配台",
            WorkstationTier.Machining, interval: 5f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("CommonParts", 4, "AdvancedParts", 1, 6f),
                // RM("MechanicalAssembly", ...) // 机械传动组件暂缺ItemData
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
                // 回收拆解：电路板→电子元件
                R("CircuitBoard", 1, "Electronic", 3, 8f),
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
                R("EmptyBattery", 1, "BatteryPack", 1, 15f),
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
                // 广播塔扫描信号→发现电子元件/电池组（无线电侦测回收）
                R("BatteryPack", 1, "Electronic", 5, 12f),
                R("BatteryPack", 2, "BatteryPack", 3, 10f),
            });
    }

    // ================================================================
    // 拉线机 — 电子链阶段1：铜锭→电线/电缆
    // ================================================================

    static void CreateWireDrawer()
    {
        Create("WireDrawer", "拉线机",
            WorkstationTier.ElectronicsAssembly, interval: 4f, batchSize: 1,
            requiresFuel: true, fuel: GetItem("Coal"), fuelPerCycle: 1f,
            recipes: new[]
            {
                R("CopperIngot", 1, "Wire", 6, 4f),
                RM("Cable", 3, 5f, ("CopperIngot", 1), ("Rubber", 1)),
            });
    }

    // ================================================================
    // 电池生产线 — 电子链阶段2：电池制造
    // ================================================================

    static void CreateBatteryLine()
    {
        Create("BatteryLine", "电池生产线",
            WorkstationTier.Machining, interval: 6f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                RM("Battery", 5, 6f, ("LeadOre", 2), ("Sulfur", 1), ("CopperIngot", 2)),
                RM("BatteryPack", 3, 8f, ("Battery", 2), ("SulfuricAcid", 1), ("CopperIngot", 1)),
            });
    }

    // ================================================================
    // 电子装配机 — 电子链阶段2：元件批量制造
    // ================================================================

    static void CreateElectronicsAssembler()
    {
        Create("ElectronicsAssembler", "电子装配机",
            WorkstationTier.ElectronicsAssembly, interval: 5f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                RM("Electronic", 4, 5f, ("Wire", 2), ("PlasticScrap", 1), ("CopperIngot", 1)),
                RM("Coil", 3, 5f, ("Wire", 2), ("Rubber", 1), ("CopperIngot", 1)),
                RM("CapacitorBank", 2, 6f, ("BatteryPack", 1), ("CopperIngot", 2)),
            });
    }

    // ================================================================
    // 电路印刷机 — 电子链阶段3：电路板批量生产
    // ================================================================

    static void CreateCircuitPrinter()
    {
        Create("CircuitPrinter", "电路印刷机",
            WorkstationTier.ElectronicsAssembly, interval: 6f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                RM("CircuitBoard", 2, 6f, ("Electronic", 2), ("Wire", 1), ("PlasticScrap", 1), ("CopperIngot", 1)),
            });
    }

    // ================================================================
    // 火药厂 — 化学链阶段3：火药+塑料量产
    // ================================================================

    static void CreateGunpowderFactory()
    {
        Create("GunpowderFactory", "火药厂",
            WorkstationTier.Chemistry, interval: 6f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                RM("BlackPowder", 5, 6f, ("Niter", 1), ("Sulfur", 1), ("Coal", 2)),
                RM("Gunpowder", 3, 8f, ("SulfuricAcid", 1), ("Alcohol", 1), ("BlackPowder", 2)),
                RM("PlasticScrap", 4, 5f, ("ChemicalAgent", 1), ("PlantFiber", 3)),
            });
    }

    // ================================================================
    // 弹药装填机 — 金属链阶段5：子弹批量装填
    // ================================================================

    static void CreateAmmoLoader()
    {
        Create("AmmoLoader", "弹药装填机",
            WorkstationTier.Machining, interval: 8f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                RM("Ammo_9mm", 20, 8f, ("BulletCasing", 2), ("BulletHead", 2), ("Primer", 2), ("BlackPowder", 1)),
                RM("Ammo_12Gauge", 10, 8f, ("BulletCasing", 2), ("BulletHead", 2), ("Primer", 2), ("BlackPowder", 1)),
                RM("Ammo_762mm", 15, 8f, ("BulletCasing", 2), ("BulletHead", 2), ("Primer", 2), ("Gunpowder", 1)),
                RM("Ammo_556mm", 15, 8f, ("BulletCasing", 2), ("BulletHead", 2), ("Primer", 2), ("Gunpowder", 1)),
                RM("Ammo_45ACP", 10, 8f, ("BulletCasing", 2), ("BulletHead", 2), ("Primer", 2), ("Gunpowder", 1)),
                RM("Ammo_22LR", 20, 6f, ("BulletCasing", 2), ("BulletHead", 2), ("Primer", 2), ("BlackPowder", 1)),
                RM("Ammo_AP", 8, 10f, ("SteelIngot", 1), ("LeadIngot", 1), ("Gunpowder", 1)),
            });
    }

    // ================================================================
    // 武器组装台 — 金属链阶段5：枪械/防具组装
    // ================================================================

    static void CreateWeaponAssembly()
    {
        Create("WeaponAssembly", "武器组装台",
            WorkstationTier.Machining, interval: 12f, batchSize: 1,
            requiresFuel: false,
            recipes: new[]
            {
                RM("Pistol", 1, 12f, ("SteelIngot", 2), ("WoodPlank", 1), ("AdvancedParts", 1)),
                RM("Rifle", 1, 12f, ("SteelIngot", 3), ("WoodPlank", 1), ("AdvancedParts", 1)),
                RM("Shotgun", 1, 15f, ("SteelIngot", 2), ("WoodPlank", 1), ("AdvancedParts", 1), ("Gear", 1)),
                RM("HeavySniper", 1, 20f, ("TitaniumAlloy", 2), ("SteelPipe", 2), ("AdvancedParts", 2)),
                RM("BulletproofVest", 1, 12f, ("ClothRoll", 2), ("SteelIngot", 2), ("AdvancedParts", 1)),
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
