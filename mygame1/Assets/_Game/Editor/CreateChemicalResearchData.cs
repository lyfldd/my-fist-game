using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 生成研究中心科技树数据 v2.0。
/// 基础大类(智1-4) 5项 + 进阶大类(智5+) 7项 + 子研究 ~32项。
/// 全部成本 ×3。
/// </summary>
public static class CreateChemicalResearchData
{
    const string SavePath = "Assets/_Game/Config/Resources/ChemicalResearchData.asset";
    static Dictionary<string, ItemData> _itemLookup;

    [MenuItem("Tools/工业/生成化学研究数据")]
    public static void Generate()
    {
        BuildItemLookup();
        var data = GetOrCreateData();

        data.projects = BuildAllProjects();

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int cats = 0, children = 0;
        foreach (var p in data.projects)
        {
            if (p.isCategory) cats++;
            if (!string.IsNullOrEmpty(p.parentResearchId)) children++;
        }
        Debug.Log($"[CreateChemicalResearchData] 已生成 {data.projects.Length} 项 (大类{cats} + 子项{children}) → {SavePath}");
        EditorUtility.DisplayDialog("研究数据生成完成",
            $"已生成 {data.projects.Length} 个研究项目\n\n大类 {cats} 项 + 子项 {children} 项\n\n保存至: {SavePath}", "确定");
    }

    static ChemicalResearchData GetOrCreateData()
    {
        string dir = "Assets/_Game/Config/Resources";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Game/Config", "Resources");
        var data = AssetDatabase.LoadAssetAtPath<ChemicalResearchData>(SavePath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<ChemicalResearchData>();
            AssetDatabase.CreateAsset(data, SavePath);
        }
        return data;
    }

    static ChemicalResearchProject[] BuildAllProjects()
    {
        var list = new List<ChemicalResearchProject>();

        // ═══════════════════════════════════════
        // 基础大类 (智1-4, 5项)
        // ═══════════════════════════════════════

        list.Add(Category("Metal_Basic", "基础金属加工", "解锁冲压机、粉碎机、回收站",
            ResearchTier.Early, 2,
            new[] { ("SulfuricAcid", 6), ("IronIngot", 15), ("CopperIngot", 9) },
            new[] { "冲压机", "粉碎机", "回收站" }));

        list.Add(Category("Chem_Basic", "基础化学", "解锁发酵罐、蒸馏器",
            ResearchTier.Early, 2,
            new[] { ("SulfuricAcid", 6), ("GlassPane", 6), ("CopperIngot", 6) },
            new[] { "发酵罐", "蒸馏器" }));

        list.Add(Category("Energy_Basic", "基础能源", "解锁脚踏发电机、太阳能板、风车",
            ResearchTier.Early, 3,
            new[] { ("CopperIngot", 12), ("IronIngot", 9), ("CircuitBoard", 3) },
            new[] { "脚踏发电机", "太阳能板", "风车" }));

        list.Add(Category("Textile_Food", "纺织食品", "解锁织布机、锯木机、熏制房、炭窑、罐头封装机",
            ResearchTier.Early, 3,
            new[] { ("IronIngot", 9), ("Stone", 15), ("WoodLog", 30) },
            new[] { "织布机", "锯木机", "熏制房", "炭窑", "罐头封装机" }));

        list.Add(Category("Water_Basic", "水源入门", "解锁水泵",
            ResearchTier.Early, 4,
            new[] { ("IronIngot", 12), ("CopperIngot", 6), ("Rubber", 6) },
            new[] { "水泵" }));

        // ═══════════════════════════════════════
        // 进阶大类 (智5+) + 子研究
        // ═══════════════════════════════════════

        // ── 精密金属制造 ──
        list.Add(Category("Metal_Precision", "精密金属制造", "解锁车床、装配台、工业炉。研究后展开子项",
            ResearchTier.Mid, 5,
            new[] { ("SulfuricAcid", 9), ("ChemicalAgent", 6), ("SteelIngot", 15), ("AdvancedParts", 9) },
            new[] { "车床", "装配台", "工业炉" }));

        list.Add(Child("Metal_Precision", "Recipe_Pistol", "手枪配方", "研究手枪制造",
            5, new[] { ("SteelIngot", 12), ("AdvancedParts", 6), ("SpringAssembly", 3) },
            null, new[] { "Pistol" }));
        list.Add(Child("Metal_Precision", "Recipe_Shotgun", "霰弹枪配方", "研究霰弹枪制造",
            5, new[] { ("SteelIngot", 15), ("AdvancedParts", 8), ("SteelPipe", 4) },
            null, new[] { "Shotgun" }));
        list.Add(Child("Metal_Precision", "Recipe_Rifle", "步枪配方", "研究步枪制造",
            6, new[] { ("SteelIngot", 18), ("AdvancedParts", 10), ("SpringAssembly", 6) },
            null, new[] { "Rifle" }));
        list.Add(Child("Metal_Precision", "Recipe_HuntingRifle", "猎枪配方", "研究猎枪制造",
            6, new[] { ("SteelIngot", 20), ("AdvancedParts", 12), ("TitaniumAlloy", 2) },
            null, new[] { "HuntingRifle" }));
        list.Add(Child("Metal_Precision", "Recipe_PistolAmmo", "手枪弹药批量", "研究手枪弹药量产",
            5, new[] { ("CopperIngot", 12), ("Gunpowder", 6), ("SteelIngot", 6) },
            null, new[] { "PistolAmmo_Bulk" }));
        list.Add(Child("Metal_Precision", "Recipe_ShotgunAmmo", "霰弹弹药批量", "研究霰弹弹药量产",
            5, new[] { ("CopperIngot", 12), ("Gunpowder", 8), ("SteelIngot", 6) },
            null, new[] { "ShotgunAmmo_Bulk" }));
        list.Add(Child("Metal_Precision", "Recipe_RifleAmmo", "步枪弹药批量", "研究步枪弹药量产",
            6, new[] { ("CopperIngot", 15), ("Gunpowder", 10), ("SteelIngot", 9) },
            null, new[] { "RifleAmmo_Bulk" }));
        list.Add(Child("Metal_Precision", "Recipe_Suppressor", "消音器配方", "研究消音器制造",
            6, new[] { ("SteelPipe", 9), ("AdvancedParts", 6), ("CarbonFiber", 3) },
            null, new[] { "Suppressor" }));
        list.Add(Child("Metal_Precision", "Recipe_SemiAutoKit", "半自动改造套件", "研究半自动改造",
            7, new[] { ("AdvancedParts", 9), ("SpringAssembly", 6), ("SteelIngot", 9) },
            null, new[] { "SemiAutoKit" }));
        list.Add(Child("Metal_Precision", "Recipe_FullAutoKit", "全自动改造套件", "研究全自动改造",
            8, new[] { ("AdvancedParts", 12), ("SpringAssembly", 9), ("ChipSet", 2) },
            null, new[] { "FullAutoKit" }));

        // ── 高级武器工艺 ──
        list.Add(Category("Weapon_Advanced", "高级武器工艺", "解锁弹药装填机、武器组装台",
            ResearchTier.Mid, 7,
            new[] { ("ChemicalAgent", 9), ("Gunpowder", 6), ("SteelIngot", 18), ("AdvancedParts", 12) },
            new[] { "弹药装填机", "武器组装台" }));

        list.Add(Child("Weapon_Advanced", "Recipe_HeavySniper", "重狙配方", "研究重型狙击步枪制造",
            8, new[] { ("SteelIngot", 20), ("AdvancedParts", 12), ("TitaniumAlloy", 3), ("ChipSet", 1) },
            null, new[] { "HeavySniper" }));
        list.Add(Child("Weapon_Advanced", "Recipe_GrenadeLauncher", "榴弹配方", "研究榴弹发射器制造",
            8, new[] { ("SteelIngot", 18), ("AdvancedParts", 12), ("SpringAssembly", 6), ("Gunpowder", 12) },
            null, new[] { "GrenadeLauncher" }));
        list.Add(Child("Weapon_Advanced", "Recipe_APRounds", "穿甲弹配方", "研究穿甲弹制造",
            7, new[] { ("SteelIngot", 12), ("TitaniumAlloy", 3), ("Gunpowder", 8) },
            null, new[] { "APRounds" }));
        list.Add(Child("Weapon_Advanced", "Recipe_Railgun", "电磁步枪配方", "研究电磁步枪制造",
            9, new[] { ("TitaniumAlloy", 9), ("ChipSet", 3), ("CapacitorBank", 6), ("AdvancedParts", 15) },
            null, new[] { "Railgun" }));

        // ── 电子工程 ──
        list.Add(Category("Elec_Engineering", "电子工程", "解锁电子装配机、电路印刷机、广播塔",
            ResearchTier.Mid, 6,
            new[] { ("ChemicalAgent", 9), ("CopperIngot", 12), ("CircuitBoard", 9), ("AdvancedParts", 6) },
            new[] { "电子装配机", "电路印刷机", "广播塔" }));

        list.Add(Child("Elec_Engineering", "Recipe_ChipSet", "芯片组配方", "研究芯片组批量生产",
            7, new[] { ("CircuitBoard", 12), ("ChemicalAgent", 9), ("CopperIngot", 9) },
            null, new[] { "ChipSet" }));
        list.Add(Child("Elec_Engineering", "Recipe_AICore", "AI核心配方", "研究AI核心制造",
            8, new[] { ("ChipSet", 4), ("CircuitBoard", 12), ("ChemicalAgent", 12), ("AdvancedParts", 9) },
            null, new[] { "AICore" }));
        list.Add(Child("Elec_Engineering", "Recipe_ServoMotor", "伺服电机配方", "研究伺服电机制造",
            8, new[] { ("CapacitorBank", 3), ("CopperIngot", 12), ("AdvancedParts", 9), ("SpringAssembly", 6) },
            null, new[] { "ServoMotor" }));
        list.Add(Child("Elec_Engineering", "Recipe_CapacitorBank", "电容组配方", "研究电容组制造",
            7, new[] { ("BatteryPack", 6), ("CopperIngot", 9), ("CircuitBoard", 6) },
            null, new[] { "CapacitorBank" }));
        list.Add(Child("Elec_Engineering", "Recipe_AutoTurret", "自动炮塔配方", "研究自动炮塔制造",
            9, new[] { ("AICore", 1), ("ServoMotor", 3), ("SteelIngot", 20), ("ChipSet", 4) },
            null, new[] { "AutoTurret" }));

        // ── 高等化学 ──
        list.Add(Category("Chem_Advanced", "高等化学", "解锁火药厂、离心机、制药台",
            ResearchTier.Mid, 6,
            new[] { ("SulfuricAcid", 12), ("ChemicalAgent", 9), ("BlackPowder", 9), ("SteelPipe", 12) },
            new[] { "火药厂", "离心机", "制药台" }));

        list.Add(Child("Chem_Advanced", "Recipe_GunpowderBulk", "火药批量配方", "研究火药量产",
            5, new[] { ("Sulfur", 12), ("Charcoal", 9), ("ChemicalAgent", 6) },
            null, new[] { "Gunpowder_Bulk" }));
        list.Add(Child("Chem_Advanced", "Recipe_FirstAidKit", "急救包配方", "研究急救包制造",
            5, new[] { ("ChemicalAgent", 6), ("Alcohol", 6), ("RagScrap", 9) },
            null, new[] { "FirstAidKit" }));
        list.Add(Child("Chem_Advanced", "Recipe_SurgeryKit", "手术包配方", "研究手术包制造",
            6, new[] { ("ChemicalAgent", 9), ("Alcohol", 9), ("GlassPane", 6), ("AdvancedParts", 3) },
            null, new[] { "SurgeryKit" }));
        list.Add(Child("Chem_Advanced", "Recipe_Plastic", "塑料配方", "研究塑料合成",
            7, new[] { ("ChemicalAgent", 9), ("CoalTar", 6), ("Sulfur", 6) },
            null, new[] { "Plastic" }));
        list.Add(Child("Chem_Advanced", "Recipe_VaccineSerum", "疫苗血清配方", "研究疫苗血清制造",
            8, new[] { ("ChemicalAgent", 12), ("Alcohol", 9), ("GlassPane", 12), ("Herb", 20) },
            null, new[] { "VaccineSerum" }));

        // ── 电力工程 ──
        list.Add(Category("Energy_Power", "电力工程", "解锁发电机、电解槽、拉线机、电池生产线",
            ResearchTier.Mid, 6,
            new[] { ("SulfuricAcid", 9), ("CopperIngot", 15), ("CircuitBoard", 6), ("AdvancedParts", 9) },
            new[] { "发电机", "电解槽", "拉线机", "电池生产线" }));

        list.Add(Child("Energy_Power", "Recipe_BatteryPackBulk", "电池组批量配方", "研究电池组量产",
            5, new[] { ("CopperIngot", 12), ("SulfuricAcid", 6), ("CircuitBoard", 3) },
            null, new[] { "BatteryPack_Bulk" }));
        list.Add(Child("Energy_Power", "Recipe_CircuitBoardBulk", "电路板批量配方", "研究电路板量产",
            6, new[] { ("CopperIngot", 15), ("ChemicalAgent", 9), ("GlassPane", 6) },
            null, new[] { "CircuitBoard_Bulk" }));
        list.Add(Child("Energy_Power", "Recipe_CapacitorBankBulk", "电容组批量配方", "研究电容组量产",
            7, new[] { ("BatteryPack", 9), ("CopperIngot", 12), ("CircuitBoard", 6) },
            null, new[] { "CapacitorBank_Bulk" }));

        // ── 材料科学 ──
        list.Add(Category("Material_Science", "材料科学", "解锁高级材料配方",
            ResearchTier.Late, 7,
            new[] { ("ChemicalAgent", 12), ("AluminumIngot", 15), ("AdvancedParts", 12) },
            null));

        list.Add(Child("Material_Science", "Recipe_TitaniumAlloy", "钛合金配方", "研究钛合金熔炼",
            7, new[] { ("ChemicalAgent", 12), ("AluminumIngot", 15), ("IronIngot", 15), ("AdvancedParts", 9) },
            null, new[] { "TitaniumAlloy" }));
        list.Add(Child("Material_Science", "Recipe_CarbonFiber", "碳纤维配方", "研究碳纤维合成",
            7, new[] { ("ChemicalAgent", 12), ("Rubber", 9), ("GlassPane", 9), ("AdvancedParts", 6) },
            null, new[] { "CarbonFiber" }));
        list.Add(Child("Material_Science", "Recipe_AdvAlloy", "高级合金复合材料", "研究高级合金",
            8, new[] { ("ChemicalAgent", 15), ("TitaniumAlloy", 6), ("CarbonFiber", 3), ("AdvancedParts", 12) },
            null, new[] { "AdvAlloyComposite" }));
        list.Add(Child("Material_Science", "Recipe_GeneSample", "基因样本配方", "研究基因样本提取",
            8, new[] { ("ChemicalAgent", 15), ("VaccineSerum", 3), ("GlassPane", 12), ("Alcohol", 9) },
            null, new[] { "GeneSample" }));

        // ── 核物理 ──
        list.Add(Category("Nuclear_Physics", "核物理", "解锁核电站",
            ResearchTier.Endgame, 9,
            new[] { ("EnrichedUranium", 9), ("TitaniumAlloy", 15), ("SuperConductingCoil", 6), ("ChipSet", 9) },
            new[] { "核电站" }));

        list.Add(Child("Nuclear_Physics", "Recipe_SmallReactor", "小型反应堆配方", "研究小型核反应堆制造",
            9, new[] { ("TitaniumAlloy", 12), ("ChipSet", 6), ("EnrichedUranium", 3), ("AdvancedParts", 15) },
            null, new[] { "SmallReactor" }));
        list.Add(Child("Nuclear_Physics", "Recipe_FusionCoreSmall", "聚变核心(小)配方", "研究小型聚变核心",
            9, new[] { ("SmallReactor", 1), ("EnrichedUranium", 6), ("SuperConductingCoil", 3), ("QuantumProcessor", 1) },
            null, new[] { "FusionCore_Small" }));
        list.Add(Child("Nuclear_Physics", "Recipe_FusionCoreLarge", "聚变核心(大)配方", "研究大型聚变核心",
            10, new[] { ("FusionCore_Small", 2), ("EnrichedUranium", 12), ("SuperConductingCoil", 6), ("QuantumProcessor", 2) },
            null, new[] { "FusionCore_Large" }));
        list.Add(Child("Nuclear_Physics", "Recipe_PlasmaBattery", "等离子电池配方", "研究等离子电池",
            9, new[] { ("CapacitorBank", 6), ("EnrichedUranium", 3), ("ChemicalAgent", 12), ("GlassPane", 9) },
            null, new[] { "PlasmaBattery" }));
        list.Add(Child("Nuclear_Physics", "Recipe_SuperConductingCoil", "超导线圈配方", "研究超导线圈",
            9, new[] { ("CopperIngot", 20), ("TitaniumAlloy", 6), ("ChemicalAgent", 9), ("CapacitorBank", 3) },
            null, new[] { "SuperConductingCoil" }));
        list.Add(Child("Nuclear_Physics", "Recipe_QuantumProcessor", "量子处理器配方", "研究量子处理器",
            10, new[] { ("AICore", 2), ("ChipSet", 12), ("SuperConductingCoil", 3), ("AdvancedParts", 15) },
            null, new[] { "QuantumProcessor" }));
        list.Add(Child("Nuclear_Physics", "Recipe_EMFence", "电磁围栏配方", "研究电磁围栏",
            9, new[] { ("CopperIngot", 20), ("CapacitorBank", 4), ("AdvancedParts", 12), ("TitaniumAlloy", 6) },
            null, new[] { "EMFence" }));
        list.Add(Child("Nuclear_Physics", "Recipe_MobileFortress", "移动要塞配方", "研究移动基地车制造",
            10, new[] { ("AICore", 2), ("ServoMotor", 6), ("TitaniumAlloy", 20), ("CapacitorBank", 9), ("ChipSet", 9) },
            null, new[] { "MobileFortress" }));

        return list.ToArray();
    }

    // ═══════════════════════════════════════
    // 工厂方法
    // ═══════════════════════════════════════

    static ChemicalResearchProject Category(string id, string name, string desc,
        ResearchTier tier, int intLevel,
        (string itemName, int count)[] cost, string[] devices)
    {
        return new ChemicalResearchProject
        {
            researchId = id,
            displayName = name,
            description = desc,
            cost = ToReqs(cost),
            unlockedDeviceNames = devices,
            requiredIntellectLevel = intLevel,
            tier = tier,
            isCategory = true,
            parentResearchId = null,
        };
    }

    static ChemicalResearchProject Child(string parentId, string id, string name, string desc,
        int intLevel, (string itemName, int count)[] cost,
        string[] devices, string[] recipeIds)
    {
        return new ChemicalResearchProject
        {
            researchId = id,
            displayName = name,
            description = desc,
            cost = ToReqs(cost),
            unlockedDeviceNames = devices,
            unlockedRecipeIds = recipeIds,
            requiredIntellectLevel = intLevel,
            tier = ResearchTier.Late, // 子项统一后期风格
            isCategory = false,
            parentResearchId = parentId,
        };
    }

    static ItemRequirement[] ToReqs((string itemName, int count)[] cost)
    {
        var reqs = new ItemRequirement[cost.Length];
        for (int i = 0; i < cost.Length; i++)
            reqs[i] = new ItemRequirement { itemData = GetItem(cost[i].itemName), count = cost[i].count };
        return reqs;
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
    }

    static ItemData GetItem(string assetName)
    {
        if (_itemLookup.TryGetValue(assetName, out var item)) return item;
        Debug.LogWarning($"[CreateChemicalResearchData] 未找到 ItemData: {assetName}");
        return null;
    }
}
