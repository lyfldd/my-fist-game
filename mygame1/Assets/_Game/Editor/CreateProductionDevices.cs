using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键生成所有生产设备数据 .asset 文件。
/// 菜单栏 → Game Tools → Create Production Devices
/// v3.0 — 全部材料成本 ×3
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
        CreateCentrifuge();
        CreateGeneAnalyzer();
        CreatePrecisionAssembly();
        CreateWaterPurifier();
        CreateAIBot();
        CreateNuclearPlant();
        CreateDronePlatform();
        CreateWireDrawer();
        CreateBatteryLine();
        CreateElectronicsAssembler();
        CreateCircuitPrinter();
        CreateGunpowderFactory();
        CreateAmmoLoader();
        CreateWeaponAssembly();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"生产设备资产 v3.0 (×3成本) 生成完毕！{Dir}");
    }

    static void BuildItemLookup()
    {
        _itemLookup = new Dictionary<string, ItemData>();
        foreach (var guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null) _itemLookup[System.IO.Path.GetFileNameWithoutExtension(path)] = item;
        }
        Debug.Log($"[CreateProductionDevices] 索引 {_itemLookup.Count} 个 ItemData");
    }
    static void EnsureDir() { if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Game/Config", "ProductionDevices"); }
    static ItemData GetItem(string n) { _itemLookup.TryGetValue(n, out var i); if (i == null) Debug.LogWarning($"[CPD] 未找到: {n}"); return i; }

    static ProductionDeviceData Create(string name, string display, WorkstationTier tier, float interval, int batch = 1,
        bool fuel = false, ItemData fuelItem = null, float fuelPer = 1f, bool auto = true, float autoMul = 2f, ProductionRecipe[] recipes = null)
    {
        string p = $"{Dir}/{name}.asset";
        var d = AssetDatabase.LoadAssetAtPath<ProductionDeviceData>(p);
        if (d == null) { d = ScriptableObject.CreateInstance<ProductionDeviceData>(); AssetDatabase.CreateAsset(d, p); }
        d.deviceName = display; d.tier = tier; d.productionInterval = interval; d.batchSize = batch;
        d.requiresFuel = fuel; d.fuelItem = fuelItem; d.fuelPerCycle = fuelPer;
        d.acceptsAutomation = auto; d.automationMultiplier = autoMul;
        d.recipes = recipes ?? new ProductionRecipe[0]; EditorUtility.SetDirty(d); return d;
    }
    static ProductionRecipe R(string i, int ic, string o, int oc, float t = 5f) => new ProductionRecipe { input = GetItem(i), inputCount = ic, output = GetItem(o), outputCount = oc, baseTime = t };
    static ProductionRecipe RM(string o, int oc, float t, params (string n, int c)[] inputs)
    {
        var reqs = new ItemRequirement[inputs.Length];
        for (int j = 0; j < inputs.Length; j++) reqs[j] = new ItemRequirement { itemData = GetItem(inputs[j].n), count = inputs[j].c };
        return new ProductionRecipe { inputs = reqs, output = GetItem(o), outputCount = oc, baseTime = t };
    }

    // ── 冲压机: ×3 ──
    static void CreatePressMachine() => Create("PressMachine", "冲压机", WorkstationTier.Machining, 5f, 1, true, GetItem("Coal"), 2f,
        recipes: new[] { R("ScrapMetal", 9, "CommonParts", 4), R("IronIngot", 6, "CommonParts", 6), R("IronIngot", 3, "Nails", 12),
            R("SteelIngot", 6, "SteelPipe", 3), R("SteelIngot", 6, "Rebar", 4), R("IronIngot", 3, "Screw", 8),
            R("CopperIngot", 3, "BulletCasing", 6), R("LeadIngot", 3, "BulletHead", 8) });

    // ── 工业炉: ×3 ──
    static void CreateIndustrialFurnace() => Create("IndustrialFurnace", "工业炉", WorkstationTier.AdvancedBench, 6f, 1, true, GetItem("Coal"), 3f,
        recipes: new[] { R("IronOre", 9, "IronIngot", 3), R("CopperOre", 9, "CopperIngot", 3), R("IronIngot", 6, "SteelIngot", 2),
            R("ScrapMetal", 12, "IronIngot", 2), R("Limestone", 6, "Cement", 3), R("Sand", 9, "GlassPane", 2) });

    // ── 粉碎机: ×3 ──
    static void CreateCrusher() => Create("Crusher", "粉碎机", WorkstationTier.MediumBench, 4f, 1, true, GetItem("Coal"), 2f,
        recipes: new[] { R("Stone", 6, "Sand", 3), R("Limestone", 6, "Cement", 2), R("WoodLog", 3, "WoodPlank", 3),
            R("ScrapMetal", 12, "IronIngot", 1), R("AnimalBone", 6, "Fertilizer", 2) });

    // ── 织布机: ×3 ──
    static void CreateLoom() => Create("Loom", "织布机", WorkstationTier.SimpleBench, 4f, 1, false,
        recipes: new[] { R("PlantFiber", 9, "Thread", 4), R("Thread", 15, "ClothRoll", 1), R("PlantFiber", 24, "ClothRoll", 1) });

    // ── 锯木机: ×3 ──
    static void CreateSawmill() => Create("Sawmill", "锯木机", WorkstationTier.SimpleBench, 3f, 1, true, GetItem("Coal"), 2f,
        recipes: new[] { R("WoodLog", 3, "WoodPlank", 3), R("WoodLog", 9, "WoodPlank", 6) });

    // ── 水泵: 0不能×3，改为需空桶 ──
    static void CreateWaterPump() => Create("WaterPump", "水泵", WorkstationTier.SimpleBench, 10f, 1, false, autoMul: 3f,
        recipes: new[] { R("EmptyBattery", 1, "Water", 3, 12f) });

    // ── 发酵罐: ×3 ──
    static void CreateFermenter() => Create("Fermenter", "发酵罐", WorkstationTier.MediumBench, 8f, 1, false,
        recipes: new[] { R("Berry", 15, "Alcohol", 2), R("Mushroom", 12, "ChemicalAgent", 1), R("Herb", 18, "ChemicalAgent", 1) });

    // ── 蒸馏器: ×3 ──
    static void CreateDistiller() => Create("Distiller", "蒸馏器", WorkstationTier.AdvancedBench, 6f, 1, true, GetItem("Coal"), 2f,
        recipes: new[] { R("Alcohol", 6, "ChemicalAgent", 1), R("Water", 15, "PurifiedWater", 3), R("Alcohol", 15, "Antiseptic", 1) });

    // ── 电解槽: ×3 ──
    static void CreateElectrolysisTank() => Create("ElectrolysisTank", "电解槽", WorkstationTier.Machining, 10f, 1, true, GetItem("BatteryPack"), 2f,
        recipes: new[] { R("AluminumOre", 9, "AluminumIngot", 3), R("CopperOre", 9, "CopperIngot", 3),
            R("Water", 12, "PurifiedWater", 4), R("ChemicalAgent", 3, "SulfuricAcid", 1), R("ScrapMetal", 15, "IronIngot", 2) });

    // ── 炭窑: ×3 ──
    static void CreateKiln() => Create("Kiln", "炭窑", WorkstationTier.Furnace, 6f, 1, false,
        recipes: new[] { R("WoodLog", 9, "Coal", 5), R("Clay", 9, "StoneBrick", 3), R("Branch", 15, "Coal", 3) });

    // ── 发电机: ×3 ──
    static void CreateElectricGenerator() => Create("ElectricGenerator", "发电机", WorkstationTier.Machining, 12f, 1, true, GetItem("Coal"), 3f, false,
        recipes: new[] { RM("BatteryPack", 2, 10f, ("EmptyBattery", 2), ("Coal", 6)),
            RM("BatteryPack", 1, 12f, ("EmptyBattery", 2), ("WoodLog", 9)),
            RM("BatteryPack", 2, 8f, ("EmptyBattery", 3), ("Alcohol", 15)) });

    // ── 车床: ×3 ──
    static void CreateLathe() => Create("Lathe", "车床", WorkstationTier.Machining, 6f, 1, true, GetItem("Coal"), 2f,
        recipes: new[] { R("IronIngot", 6, "Gear", 1), R("IronIngot", 6, "Bearing", 1),
            R("SteelIngot", 6, "SpringAssembly", 1), R("IronIngot", 6, "Spring", 3),
            RM("TitaniumAlloy", 2, 8f, ("IronIngot", 6), ("AluminumIngot", 3)) });

    // ── 装配台: ×3 + 机械传动组件/弹簧组件 ──
    static void CreateAssemblyTable() => Create("AssemblyTable", "装配台", WorkstationTier.Machining, 6f, 1, true, GetItem("Coal"), 2f,
        recipes: new[] { R("CommonParts", 12, "AdvancedParts", 1),
            RM("MechanicalAssembly", 2, 8f, ("Gear", 2), ("Bearing", 2), ("SteelPipe", 2)),
            RM("SpringAssembly", 2, 6f, ("Spring", 6), ("SteelIngot", 3)) });

    // ── 回收站: ×3 ──
    static void CreateRecyclingStation() => Create("RecyclingStation", "回收站", WorkstationTier.Furnace, 8f, 1, true, GetItem("Coal"), 2f,
        recipes: new[] { R("ScrapMetal", 12, "IronIngot", 1), R("ScrapMetal", 18, "CopperIngot", 1),
            R("CircuitBoard", 1, "Electronic", 2, 9f), R("PlasticScrap", 9, "Rubber", 1) });

    // ── 太阳能板: 免费能源不变 ──
    static void CreateSolarPanel() => Create("SolarPanel", "太阳能板", WorkstationTier.Machining, 20f, 1, auto: false,
        recipes: new[] { R("EmptyBattery", 1, "BatteryPack", 1, 20f) });

    // ── 熏制房: ×3 ──
    static void CreateSmokehouse() => Create("Smokehouse", "熏制房", WorkstationTier.SimpleBench, 10f, 1, true, GetItem("WoodLog"), 3f,
        recipes: new[] { R("RawMeat", 6, "SmokedMeat", 1) });

    // ── 罐头封装机: ×3 ──
    static void CreateCanningMachine() => Create("CanningMachine", "罐头封装机", WorkstationTier.MediumBench, 6f, 1, true, GetItem("Coal"), 2f,
        recipes: new[] { R("Stew", 3, "CanFood", 1) });

    // ── 制药台: ×3 ──
    static void CreatePharmaBench() => Create("PharmaBench", "制药台", WorkstationTier.Chemistry, 10f, 1, false,
        recipes: new[] { R("Herb", 12, "Antibiotics", 1), R("Herb", 15, "Antiseptic", 1),
            R("ChemicalAgent", 3, "Vitamin", 1, 8f), R("Herb", 9, "StypticPowder", 1) });

    // ── 拉线机: ×3 ──
    static void CreateWireDrawer() => Create("WireDrawer", "拉线机", WorkstationTier.ElectronicsAssembly, 5f, 1, true, GetItem("Coal"), 2f,
        recipes: new[] { R("CopperIngot", 3, "Wire", 5), RM("Cable", 3, 6f, ("CopperIngot", 3), ("Rubber", 3)) });

    // ── 电池生产线: ×3 ──
    static void CreateBatteryLine() => Create("BatteryLine", "电池生产线", WorkstationTier.Machining, 8f, 1, false,
        recipes: new[] { RM("Battery", 4, 8f, ("LeadOre", 6), ("Sulfur", 3), ("CopperIngot", 6)),
            RM("BatteryPack", 2, 10f, ("Battery", 6), ("SulfuricAcid", 3), ("CopperIngot", 3)) });

    // ── 电子装配机: ×3 ──
    static void CreateElectronicsAssembler() => Create("ElectronicsAssembler", "电子装配机", WorkstationTier.ElectronicsAssembly, 6f, 1, false,
        recipes: new[] { RM("Electronic", 3, 6f, ("Wire", 6), ("PlasticScrap", 3), ("CopperIngot", 3)),
            RM("Coil", 2, 6f, ("Wire", 6), ("Rubber", 3), ("CopperIngot", 3)),
            RM("CapacitorBank", 2, 8f, ("BatteryPack", 3), ("CopperIngot", 6)) });

    // ── 电路印刷机: ×3 ──
    static void CreateCircuitPrinter() => Create("CircuitPrinter", "电路印刷机", WorkstationTier.ElectronicsAssembly, 8f, 1, false,
        recipes: new[] { RM("CircuitBoard", 2, 8f, ("Electronic", 6), ("Wire", 3), ("PlasticScrap", 3), ("CopperIngot", 3)) });

    // ── 火药厂: ×3 ──
    static void CreateGunpowderFactory() => Create("GunpowderFactory", "火药厂", WorkstationTier.Chemistry, 8f, 1, false,
        recipes: new[] { RM("BlackPowder", 4, 8f, ("Niter", 3), ("Sulfur", 3), ("Coal", 6)),
            RM("Gunpowder", 2, 10f, ("SulfuricAcid", 3), ("Alcohol", 3), ("BlackPowder", 6)),
            RM("PlasticScrap", 3, 6f, ("ChemicalAgent", 3), ("PlantFiber", 9)) });

    // ── 弹药装填机: ×3 ──
    static void CreateAmmoLoader() => Create("AmmoLoader", "弹药装填机", WorkstationTier.Machining, 10f, 1, false,
        recipes: new[] {
            RM("Ammo_9mm", 15, 10f, ("BulletCasing", 6), ("BulletHead", 6), ("Primer", 6), ("BlackPowder", 3)),
            RM("Ammo_12Gauge", 8, 10f, ("BulletCasing", 6), ("BulletHead", 6), ("Primer", 6), ("BlackPowder", 3)),
            RM("Ammo_762mm", 10, 10f, ("BulletCasing", 6), ("BulletHead", 6), ("Primer", 6), ("Gunpowder", 3)),
            RM("Ammo_556mm", 10, 10f, ("BulletCasing", 6), ("BulletHead", 6), ("Primer", 6), ("Gunpowder", 3)),
            RM("Ammo_45ACP", 8, 10f, ("BulletCasing", 6), ("BulletHead", 6), ("Primer", 6), ("Gunpowder", 3)),
            RM("Ammo_22LR", 15, 8f, ("BulletCasing", 6), ("BulletHead", 6), ("Primer", 6), ("BlackPowder", 2)),
            RM("Ammo_AP", 5, 14f, ("SteelIngot", 3), ("LeadIngot", 3), ("Gunpowder", 3)),
        });

    // ── 武器组装台: ×3 ──
    static void CreateWeaponAssembly() => Create("WeaponAssembly", "武器组装台", WorkstationTier.Machining, 15f, 1, false,
        recipes: new[] {
            RM("M1911", 1, 15f, ("SteelIngot", 6), ("WoodPlank", 3), ("AdvancedParts", 3)),
            RM("AK47", 1, 18f, ("SteelIngot", 9), ("WoodPlank", 3), ("AdvancedParts", 3)),
            RM("Remington870", 1, 20f, ("SteelIngot", 6), ("WoodPlank", 3), ("AdvancedParts", 3), ("Gear", 2)),
            RM("SVD", 1, 25f, ("TitaniumAlloy", 6), ("SteelPipe", 6), ("AdvancedParts", 6)),
            RM("BulletproofVest", 1, 15f, ("ClothRoll", 6), ("SteelIngot", 6), ("AdvancedParts", 3)),
        });

    // ── 终局设备: ×2（稀有材料不宜3倍）──
    static void CreateCentrifuge() => Create("Centrifuge", "离心机", WorkstationTier.Machining, 12f, 1, false,
        recipes: new[] { R("UraniumOre", 10, "EnrichedUranium", 1, 18f), R("Herb", 12, "VaccineSerum", 1, 12f) });

    static void CreateGeneAnalyzer() => Create("GeneAnalyzer", "基因分析台", WorkstationTier.ElectronicsAssembly, 18f, 1, false,
        recipes: new[] { R("VaccineSerum", 6, "ZombieVaccine", 1, 24f), R("GeneSample", 1, "GeneSample", 2, 24f) });

    static void CreatePrecisionAssembly() => Create("PrecisionAssembly", "精密装配台", WorkstationTier.ElectronicsAssembly, 12f, 1, false,
        recipes: new[] {
            R("CircuitBoard", 9, "ChipSet", 1, 12f),
            R("Coil", 6, "ServoMotor", 1, 14f),
            // 无人机组装 — 用无人机平台产出的机体+芯片
            RM("Drone", 1, 20f, ("DroneBody", 1), ("DroneChip", 1), ("BatteryPack", 6), ("OpticalLens", 1)),
            RM("DroneTerminal", 1, 15f, ("DroneChip", 1), ("CircuitBoard", 3), ("BatteryPack", 4), ("Electronic", 8)),
        });

    static void CreateWaterPurifier() => Create("WaterPurifier", "净水厂", WorkstationTier.Machining, 8f, 5, true, GetItem("Coal"), 2f,
        recipes: new[] { R("Water", 30, "PurifiedWater", 10) });

    static void CreateAIBot() => Create("AIBot", "AI机器人", WorkstationTier.ElectronicsAssembly, 0, 0, auto: false, recipes: new ProductionRecipe[0]);
    static void CreateNuclearPlant() => Create("NuclearPlant", "核电站", WorkstationTier.Machining, 0, 0, auto: false, recipes: new ProductionRecipe[0]);

    // ── 无人机平台：生产机体+芯片，供精密装配台组装 ──
    static void CreateDronePlatform() => Create("DronePlatform", "无人机平台", WorkstationTier.ElectronicsAssembly, 12f, 1, false,
        recipes: new[] {
            RM("DroneBody", 1, 12f, ("CarbonFiber", 8), ("TitaniumAlloy", 4), ("ServoMotor", 3)),
            RM("DroneChip", 1, 14f, ("ChipSet", 3), ("CircuitBoard", 5), ("CapacitorBank", 3)),
        });
}
