using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 生成研究中心研究数据。每个项目消耗材料→解锁对应工业设备配方。
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
            // ===== 金属链 =====
            Project("Metal_Basic", "基础金属加工", "解锁冲压机、粉碎机、回收站",
                new[] { ("SulfuricAcid", 2), ("IronIngot", 5), ("CopperIngot", 3) },
                new[] { "冲压机", "粉碎机", "回收站" }, 1),

            Project("Metal_Precision", "精密金属制造", "解锁车床、装配台、工业炉",
                new[] { ("SulfuricAcid", 3), ("ChemicalAgent", 2), ("SteelIngot", 5), ("AdvancedParts", 3) },
                new[] { "车床", "装配台", "工业炉" }, 3),

            Project("Metal_Weapon", "武器弹药工艺", "解锁弹药装填机、武器组装台",
                new[] { ("ChemicalAgent", 3), ("Gunpowder", 2), ("SteelIngot", 6), ("AdvancedParts", 4) },
                new[] { "弹药装填机", "武器组装台" }, 5),

            // ===== 电子链 =====
            Project("Elec_Basic", "基础电工学", "解锁拉线机、电池生产线",
                new[] { ("SulfuricAcid", 2), ("CopperIngot", 6), ("CircuitBoard", 1) },
                new[] { "拉线机", "电池生产线" }, 4),

            Project("Elec_Advanced", "电子工程", "解锁电子装配机、电路印刷机",
                new[] { ("ChemicalAgent", 3), ("CopperIngot", 4), ("CircuitBoard", 3), ("AdvancedParts", 2) },
                new[] { "电子装配机", "电路印刷机" }, 6),

            Project("Elec_Precision", "精密电子技术", "解锁精密装配台、电解槽",
                new[] { ("ChemicalAgent", 4), ("CircuitBoard", 4), ("ChipSet", 1), ("AdvancedParts", 4) },
                new[] { "精密装配台", "电解槽" }, 8),

            // ===== 化学链 =====
            Project("Chem_Organic", "有机化学", "解锁发酵罐、蒸馏器、制药台",
                new[] { ("SulfuricAcid", 3), ("Alcohol", 2), ("GlassPane", 4), ("CopperIngot", 3) },
                new[] { "发酵罐", "蒸馏器", "制药台" }, 2),

            Project("Chem_Explosive", "爆炸物化学", "解锁火药厂、离心机",
                new[] { ("SulfuricAcid", 4), ("ChemicalAgent", 3), ("BlackPowder", 3), ("SteelPipe", 4) },
                new[] { "火药厂", "离心机" }, 5),

            Project("Chem_Bio", "生物化学技术", "解锁基因分析台、净水厂",
                new[] { ("ChemicalAgent", 5), ("Alcohol", 3), ("GlassPane", 6), ("AdvancedParts", 4) },
                new[] { "基因分析台", "净水厂" }, 8),

            // ===== 能源链 =====
            Project("Energy_Core", "核心能源技术", "解锁发电机、太阳能板",
                new[] { ("SulfuricAcid", 3), ("CopperIngot", 5), ("CircuitBoard", 2), ("AdvancedParts", 3) },
                new[] { "发电机", "太阳能板" }, 5),

            Project("Energy_Nuclear", "核物理应用", "解锁核电站",
                new[] { ("ChemicalAgent", 6), ("LeadIngot", 8), ("CapacitorBank", 2), ("AdvancedParts", 6) },
                new[] { "核电站" }, 10),
        };

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreateChemicalResearchData] 已生成 {data.projects.Length} 个研究项目 → {SavePath}");
        EditorUtility.DisplayDialog("化学研究数据生成完成",
            $"已生成 {data.projects.Length} 个研究项目\n\n保存至: {SavePath}", "确定");
    }

    static ChemicalResearchProject Project(string id, string name, string desc,
        (string itemName, int count)[] cost, string[] devices, int intLevel)
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
            requiredIntellectLevel = intLevel,
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
