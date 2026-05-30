using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 生成研究中心研究数据。四层递进：前期大类→中期大类→后期配方→终局配方。
/// 全部成本 ×3 起步。
/// </summary>
public static class CreateChemicalResearchData
{
    const string SavePath = "Assets/_Game/Config/Resources/ChemicalResearchData.asset";

    static Dictionary<string, ItemData> _itemLookup;

    [MenuItem("Tools/工业/生成化学研究数据")]
    public static void Generate()
    {
        BuildItemLookup();

        string dir = "Assets/_Game/Config/Resources";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Game/Config", "Resources");

        var data = AssetDatabase.LoadAssetAtPath<ChemicalResearchData>(SavePath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<ChemicalResearchData>();
            AssetDatabase.CreateAsset(data, SavePath);
        }

        data.projects = new ChemicalResearchProject[]
        {
            // ═══════════════════════════════════════════════
            // 前期 (智力1-3, 大类解锁设备, 6项)
            // ═══════════════════════════════════════════════

            DeviceProject("Metal_Basic", "基础金属加工", "解锁冲压机、粉碎机、回收站",
                ResearchTier.Early, 1,
                new[] { ("SulfuricAcid", 6), ("IronIngot", 15), ("CopperIngot", 9) },
                new[] { "冲压机", "粉碎机", "回收站" }),

            DeviceProject("Chem_Basic", "基础化学", "解锁发酵罐、蒸馏器",
                ResearchTier.Early, 1,
                new[] { ("SulfuricAcid", 6), ("GlassPane", 6), ("CopperIngot", 6) },
                new[] { "发酵罐", "蒸馏器" }),

            DeviceProject("Energy_Basic", "基础能源", "解锁脚踏发电机、太阳能板、风车",
                ResearchTier.Early, 2,
                new[] { ("CopperIngot", 12), ("IronIngot", 9), ("CircuitBoard", 3) },
                new[] { "脚踏发电机", "太阳能板", "风车" }),

            DeviceProject("Textile_Basic", "纺织入门", "解锁织布机、锯木机",
                ResearchTier.Early, 2,
                new[] { ("IronIngot", 9), ("CopperIngot", 3), ("RagScrap", 12) },
                new[] { "织布机", "锯木机" }),

            DeviceProject("Food_Basic", "食品加工", "解锁熏制房、炭窑、罐头封装机",
                ResearchTier.Early, 2,
                new[] { ("IronIngot", 9), ("Stone", 15), ("WoodLog", 30) },
                new[] { "熏制房", "炭窑", "罐头封装机" }),

            DeviceProject("Water_Basic", "水源基础", "解锁水泵、净水厂",
                ResearchTier.Early, 3,
                new[] { ("IronIngot", 12), ("CopperIngot", 6), ("Rubber", 6) },
                new[] { "水泵", "净水厂" }),

            // ═══════════════════════════════════════════════
            // 中期 (智力4-6, 大类解锁设备, 7项)
            // ═══════════════════════════════════════════════

            DeviceProject("Metal_Precision", "精密金属制造", "解锁车床、装配台、工业炉",
                ResearchTier.Mid, 4,
                new[] { ("SulfuricAcid", 9), ("ChemicalAgent", 6), ("SteelIngot", 15), ("AdvancedParts", 9) },
                new[] { "车床", "装配台", "工业炉" }),

            DeviceProject("Elec_Engineering", "电工学", "解锁拉线机、电池生产线",
                ResearchTier.Mid, 4,
                new[] { ("SulfuricAcid", 6), ("CopperIngot", 18), ("CircuitBoard", 3) },
                new[] { "拉线机", "电池生产线" }),

            DeviceProject("Metal_Weapon", "武器弹药工艺", "解锁弹药装填机、武器组装台",
                ResearchTier.Mid, 5,
                new[] { ("ChemicalAgent", 9), ("Gunpowder", 6), ("SteelIngot", 18), ("AdvancedParts", 12) },
                new[] { "弹药装填机", "武器组装台" }),

            DeviceProject("Chem_Organic", "有机化学", "解锁制药台",
                ResearchTier.Mid, 5,
                new[] { ("SulfuricAcid", 9), ("Alcohol", 6), ("GlassPane", 12), ("CopperIngot", 9) },
                new[] { "制药台" }),

            DeviceProject("Chem_Explosive", "爆炸物化学", "解锁火药厂、离心机",
                ResearchTier.Mid, 6,
                new[] { ("SulfuricAcid", 12), ("ChemicalAgent", 9), ("BlackPowder", 9), ("SteelPipe", 12) },
                new[] { "火药厂", "离心机" }),

            DeviceProject("Energy_Power", "电力工程", "解锁发电机、太阳能板(标准)、电解槽",
                ResearchTier.Mid, 6,
                new[] { ("SulfuricAcid", 9), ("CopperIngot", 15), ("CircuitBoard", 6), ("AdvancedParts", 9) },
                new[] { "发电机", "电解槽" }),

            DeviceProject("Elec_Advanced", "电子工程", "解锁电子装配机、电路印刷机、广播塔",
                ResearchTier.Mid, 6,
                new[] { ("ChemicalAgent", 9), ("CopperIngot", 12), ("CircuitBoard", 9), ("AdvancedParts", 6) },
                new[] { "电子装配机", "电路印刷机", "广播塔" }),

            // ═══════════════════════════════════════════════
            // 后期 (智力7-8, 逐个配方解锁, 15项)
            // ═══════════════════════════════════════════════

            RecipeProject("Recipe_TitaniumAlloy", "钛合金配方", "研究钛合金熔炼技术",
                ResearchTier.Late, 7,
                new[] { ("ChemicalAgent", 12), ("AluminumIngot", 15), ("IronIngot", 15), ("AdvancedParts", 9) },
                new[] { "TitaniumAlloy" }),

            RecipeProject("Recipe_CarbonFiber", "碳纤维配方", "研究碳纤维合成技术",
                ResearchTier.Late, 7,
                new[] { ("ChemicalAgent", 12), ("Rubber", 9), ("GlassPane", 9), ("AdvancedParts", 6) },
                new[] { "CarbonFiber" }),

            RecipeProject("Recipe_HeavySniper", "重狙配方", "研究重型狙击步枪制造",
                ResearchTier.Late, 7,
                new[] { ("SteelIngot", 20), ("AdvancedParts", 12), ("TitaniumAlloy", 3), ("ChipSet", 1) },
                new[] { "HeavySniper" }),

            RecipeProject("Recipe_GrenadeLauncher", "榴弹配方", "研究榴弹发射器制造",
                ResearchTier.Late, 7,
                new[] { ("SteelIngot", 18), ("AdvancedParts", 12), ("SpringAssembly", 6), ("Gunpowder", 12) },
                new[] { "GrenadeLauncher" }),

            RecipeProject("Recipe_SemiAutoKit", "半自动改造套件", "研究半自动改造技术",
                ResearchTier.Late, 7,
                new[] { ("AdvancedParts", 9), ("SpringAssembly", 6), ("SteelIngot", 9) },
                new[] { "SemiAutoKit" }),

            RecipeProject("Recipe_Suppressor", "消音器配方", "研究消音器制造技术",
                ResearchTier.Late, 7,
                new[] { ("SteelPipe", 9), ("AdvancedParts", 6), ("CarbonFiber", 3) },
                new[] { "Suppressor" }),

            RecipeProject("Recipe_CapacitorBank", "电容组配方", "研究电容组制造技术",
                ResearchTier.Late, 7,
                new[] { ("BatteryPack", 6), ("CopperIngot", 9), ("CircuitBoard", 6) },
                new[] { "CapacitorBank" }),

            RecipeProject("Recipe_Plastic", "塑料配方", "研究塑料合成技术",
                ResearchTier.Late, 7,
                new[] { ("ChemicalAgent", 9), ("CoalTar", 6), ("Sulfur", 6) },
                new[] { "Plastic" }),

            RecipeProject("Recipe_FullAutoKit", "全自动改造套件", "研究全自动改造技术",
                ResearchTier.Late, 8,
                new[] { ("AdvancedParts", 12), ("SpringAssembly", 9), ("ChipSet", 2) },
                new[] { "FullAutoKit" }),

            RecipeProject("Recipe_ChipSet", "芯片组配方", "研究芯片组批量生产技术",
                ResearchTier.Late, 8,
                new[] { ("CircuitBoard", 12), ("ChemicalAgent", 9), ("CopperIngot", 9) },
                new[] { "ChipSet" }),

            RecipeProject("Recipe_AICore", "AI核心配方", "研究AI核心制造技术",
                ResearchTier.Late, 8,
                new[] { ("ChipSet", 4), ("CircuitBoard", 12), ("ChemicalAgent", 12), ("AdvancedParts", 9) },
                new[] { "AICore" }),

            RecipeProject("Recipe_ServoMotor", "伺服电机配方", "研究伺服电机制造技术",
                ResearchTier.Late, 8,
                new[] { ("CapacitorBank", 3), ("CopperIngot", 12), ("AdvancedParts", 9), ("SpringAssembly", 6) },
                new[] { "ServoMotor" }),

            RecipeProject("Recipe_VaccineSerum", "疫苗血清配方", "研究疫苗血清制造技术",
                ResearchTier.Late, 7,
                new[] { ("ChemicalAgent", 12), ("Alcohol", 9), ("GlassPane", 12), ("Herb", 20) },
                new[] { "VaccineSerum" }),

            RecipeProject("Recipe_GeneSample", "基因样本配方", "研究基因样本提取技术",
                ResearchTier.Late, 8,
                new[] { ("ChemicalAgent", 15), ("VaccineSerum", 3), ("GlassPane", 12), ("Alcohol", 9) },
                new[] { "GeneSample" }),

            RecipeProject("Recipe_AdvAlloy", "高级合金复合材料", "研究高级合金复合材料",
                ResearchTier.Late, 8,
                new[] { ("ChemicalAgent", 15), ("TitaniumAlloy", 6), ("CarbonFiber", 3), ("AdvancedParts", 12) },
                new[] { "AdvAlloyComposite" }),

            // ═══════════════════════════════════════════════
            // 终局 (智力9-10, 逐个配方解锁, 12项)
            // ═══════════════════════════════════════════════

            RecipeProject("Recipe_SmallReactor", "小型反应堆配方", "研究小型核反应堆制造",
                ResearchTier.Endgame, 9,
                new[] { ("TitaniumAlloy", 12), ("ChipSet", 6), ("EnrichedUranium", 3), ("AdvancedParts", 15) },
                new[] { "SmallReactor" }),

            RecipeProject("Recipe_FusionCoreSmall", "聚变核心(小)配方", "研究小型聚变核心制造",
                ResearchTier.Endgame, 9,
                new[] { ("SmallReactor", 1), ("EnrichedUranium", 6), ("SuperConductingCoil", 3), ("QuantumProcessor", 1) },
                new[] { "FusionCore_Small" }),

            RecipeProject("Recipe_FusionCoreLarge", "聚变核心(大)配方", "研究大型聚变核心制造",
                ResearchTier.Endgame, 10,
                new[] { ("FusionCore_Small", 2), ("EnrichedUranium", 12), ("SuperConductingCoil", 6), ("QuantumProcessor", 2) },
                new[] { "FusionCore_Large" }),

            RecipeProject("Recipe_PlasmaBattery", "等离子电池配方", "研究等离子电池制造",
                ResearchTier.Endgame, 9,
                new[] { ("CapacitorBank", 6), ("EnrichedUranium", 3), ("ChemicalAgent", 12), ("GlassPane", 9) },
                new[] { "PlasmaBattery" }),

            RecipeProject("Recipe_SuperConductingCoil", "超导线圈配方", "研究超导线圈制造",
                ResearchTier.Endgame, 9,
                new[] { ("CopperIngot", 20), ("TitaniumAlloy", 6), ("ChemicalAgent", 9), ("CapacitorBank", 3) },
                new[] { "SuperConductingCoil" }),

            RecipeProject("Recipe_QuantumProcessor", "量子处理器配方", "研究量子处理器制造",
                ResearchTier.Endgame, 10,
                new[] { ("AICore", 2), ("ChipSet", 12), ("SuperConductingCoil", 3), ("AdvancedParts", 15) },
                new[] { "QuantumProcessor" }),

            RecipeProject("Recipe_Railgun", "电磁步枪配方", "研究电磁步枪制造",
                ResearchTier.Endgame, 9,
                new[] { ("TitaniumAlloy", 9), ("ChipSet", 3), ("CapacitorBank", 6), ("AdvancedParts", 15) },
                new[] { "Railgun" }),

            RecipeProject("Recipe_AutoTurret", "自动炮塔配方", "研究自动炮塔制造",
                ResearchTier.Endgame, 9,
                new[] { ("AICore", 1), ("ServoMotor", 3), ("SteelIngot", 20), ("ChipSet", 4) },
                new[] { "AutoTurret" }),

            RecipeProject("Recipe_EMFence", "电磁围栏配方", "研究电磁围栏制造",
                ResearchTier.Endgame, 9,
                new[] { ("CopperIngot", 20), ("CapacitorBank", 4), ("AdvancedParts", 12), ("TitaniumAlloy", 6) },
                new[] { "EMFence" }),

            RecipeProject("Recipe_ReverseVaccine", "逆转疫苗配方", "研究丧尸逆转疫苗制造",
                ResearchTier.Endgame, 10,
                new[] { ("GeneSample", 3), ("VaccineSerum", 6), ("ChemicalAgent", 20), ("Alcohol", 15) },
                new[] { "ReverseVaccine" }),

            RecipeProject("Recipe_NuclearPlant", "核电站配方", "研究核电站制造",
                ResearchTier.Endgame, 10,
                new[] { ("EnrichedUranium", 9), ("TitaniumAlloy", 15), ("SuperConductingCoil", 6), ("ChipSet", 9), ("AdvancedParts", 20) },
                new[] { "NuclearPlant" }),

            RecipeProject("Recipe_MobileFortress", "移动要塞配方", "研究移动基地车/末日要塞制造",
                ResearchTier.Endgame, 10,
                new[] { ("AICore", 2), ("ServoMotor", 6), ("TitaniumAlloy", 20), ("CapacitorBank", 9), ("ChipSet", 9) },
                new[] { "MobileFortress" }),
        };

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int early = 0, mid = 0, late = 0, end = 0;
        foreach (var p in data.projects)
        {
            switch (p.tier)
            {
                case ResearchTier.Early: early++; break;
                case ResearchTier.Mid: mid++; break;
                case ResearchTier.Late: late++; break;
                case ResearchTier.Endgame: end++; break;
            }
        }

        Debug.Log($"[CreateChemicalResearchData] 已生成 {data.projects.Length} 个研究项目 → {SavePath}");
        Debug.Log($"  前期 {early}  |  中期 {mid}  |  后期 {late}  |  终局 {end}");
        EditorUtility.DisplayDialog("研究数据生成完成",
            $"已生成 {data.projects.Length} 个研究项目\n\n"
            + $"前期 {early} 项  (智1-3, 大类解锁设备)\n"
            + $"中期 {mid} 项  (智4-6, 大类解锁设备)\n"
            + $"后期 {late} 项  (智7-8, 逐个配方)\n"
            + $"终局 {end} 项  (智9-10, 逐个配方)\n\n"
            + $"保存至: {SavePath}", "确定");
    }

    static ChemicalResearchProject DeviceProject(string id, string name, string desc,
        ResearchTier tier, int intLevel,
        (string itemName, int count)[] cost, string[] devices)
    {
        var reqs = new ItemRequirement[cost.Length];
        for (int i = 0; i < cost.Length; i++)
            reqs[i] = new ItemRequirement { itemData = GetItem(cost[i].itemName), count = cost[i].count };
        return new ChemicalResearchProject
        {
            researchId = id,
            displayName = name,
            description = desc,
            cost = reqs,
            unlockedDeviceNames = devices,
            unlockedRecipeIds = null,
            requiredIntellectLevel = intLevel,
            tier = tier,
        };
    }

    static ChemicalResearchProject RecipeProject(string id, string name, string desc,
        ResearchTier tier, int intLevel,
        (string itemName, int count)[] cost, string[] recipeIds)
    {
        var reqs = new ItemRequirement[cost.Length];
        for (int i = 0; i < cost.Length; i++)
            reqs[i] = new ItemRequirement { itemData = GetItem(cost[i].itemName), count = cost[i].count };
        return new ChemicalResearchProject
        {
            researchId = id,
            displayName = name,
            description = desc,
            cost = reqs,
            unlockedDeviceNames = null,
            unlockedRecipeIds = recipeIds,
            requiredIntellectLevel = intLevel,
            tier = tier,
        };
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
