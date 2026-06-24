using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 为 L3+ 配方自动添加智力技能要求。
/// 智力=认知门槛，建造/医疗/枪械=动手门槛，两条都要过。
/// 菜单栏 → Tools/工业/配方添加智力门槛
/// </summary>
public static class AddIntelligenceRequirements
{
    const string RecipeBase = "Assets/_Game/Config/Recipes";

    // 按配方类型决定智力要求
    static readonly Dictionary<RecipeCategory, int> CategoryIntel = new Dictionary<RecipeCategory, int>
    {
        { RecipeCategory.Material, 2 },     // 通用材料
        { RecipeCategory.Building, 1 },      // 建筑材料不需智力
        { RecipeCategory.Smelting, 1 },      // 冶炼纯体力
        { RecipeCategory.Industry, 3 },      // 工业套件
        { RecipeCategory.Ammo, 2 },          // 弹药
        { RecipeCategory.Consumable, 2 },    // 消耗品
        { RecipeCategory.Cooking, 0 },       // 烹饪不需智力
        { RecipeCategory.Weapon, 2 },        // 武器
        { RecipeCategory.Armor, 1 },         // 护甲纯手工
        { RecipeCategory.Tool, 2 },          // 工具
        { RecipeCategory.Farming, 1 },       // 农业
    };

    // 特定配方名称→智力覆盖（精确控制）
    static readonly Dictionary<string, int> NameOverrides = new Dictionary<string, int>
    {
        // AdvancedBench 高级工作台
        { "高级零件", 2 },
        { "轴承", 2 },
        { "电路板", 3 },
        { "电子元件", 4 },
        { "钢管", 1 },
        { "钢筋", 1 },
        { "底火", 2 },
        { "冲压机套件", 3 },
        { "车床套件", 3 },
        { "弹药装填机套件", 3 },
        { "武器组装台套件", 3 },
        { "拉线机套件", 4 },
        { "电池生产线套件", 4 },
        { "火药厂套件", 3 },

        // Chemistry 研究中心
        { "硫酸", 2 },
        { "化学试剂", 3 },
        { "无烟火药", 3 },
        { "合成汽油", 4 },
        { "橡胶", 2 },
        { "抗生素", 3 },
        { "解毒剂", 3 },
        { "肾上腺素", 4 },
        { "手榴弹", 2 },
        { "烟雾弹", 2 },
        { "闪光弹", 2 },
        { "燃烧瓶", 1 },
        { "固体酒精", 2 },
        { "电池组", 4 },
        { "发酵罐套件", 3 },
        { "蒸馏器套件", 3 },
        { "制药台套件", 3 },
        { "手术包", 5 },
        { "免疫增强剂", 6 },
        { "器官修复针", 7 },
        { "神经再生剂", 8 },
        { "燃烧弹", 5 },

        // Machining 机械加工台
        { "工业炉套件", 4 },
        { "电解槽套件", 5 },
        { "发电机套件", 4 },
        { "太阳能板套件", 4 },
        { "广播塔套件", 6 },
        { "高级零件批量", 2 },
        { "齿轮批量", 2 },
        { "轴承批量", 2 },
        { "线圈", 4 },
        { "弹簧组件批量", 2 },
        { "钢管批量", 1 },
        { "钢筋批量", 1 },
        { "夜视仪", 5 },
        { "防毒面具", 2 },
        { "碳纤维", 5 },
        { "钛合金", 5 },
        // 猎枪 → SKS 半自动（已删除）
        { "消音器", 4 },
        { "半自动改造套件", 4 },
        { "全自动改造套件", 5 },
        { "榴弹发射器", 7 },
        { "离心机套件", 6 },
        { "移动要塞基地车套件", 7 },
        { "电子装配台套件", 8 },  // already has 智8, keep via override
        { "核电站套件", 9 },     // already has 智9, set explicitly

        // ElectronicsAssembly 电子装配台
        { "基因分析台套件", 7 },
        { "精密装配台套件", 8 },
        { "净水厂套件", 7 },
        { "自动炮塔套件", 8 },
        { "电磁围栏套件", 8 },
        { "无人机平台套件", 8 },

        // ElementFurnace 元素合成炉
        { "浓缩铀批量", 7 },
    };

    // 不处理的类别
    static readonly HashSet<string> SkipCategories = new HashSet<string> { "Cooking", "Farming" };

    // 只处理的工作站
    static readonly HashSet<WorkstationTier> TargetStations = new HashSet<WorkstationTier>
    {
        WorkstationTier.AdvancedBench,
        WorkstationTier.Chemistry,
        WorkstationTier.Machining,
        WorkstationTier.ElectronicsAssembly,
        WorkstationTier.ElementFurnace,
    };

    [MenuItem("Tools/工业/配方添加智力门槛")]
    public static void AddIntel()
    {
        var guids = AssetDatabase.FindAssets("t:RecipeData", new[] { RecipeBase });
        int changed = 0;
        int skipped = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
            if (recipe == null) continue;
            if (!TargetStations.Contains(recipe.requiredStation)) continue;
            if (SkipCategories.Contains(recipe.category.ToString())) continue;

            // 检查是否已有智力要求
            bool hasIntel = false;
            if (recipe.skillRequirements != null)
            {
                foreach (var sr in recipe.skillRequirements)
                    if (sr.skill == SkillType.智力) { hasIntel = true; break; }
            }

            // 确定智力值
            int intelLevel;
            if (NameOverrides.TryGetValue(recipe.recipeName, out int level))
                intelLevel = level;
            else if (CategoryIntel.TryGetValue(recipe.category, out int catLevel))
                intelLevel = catLevel;
            else
                intelLevel = 1;

            if (intelLevel <= 0) { skipped++; continue; }

            // 如果已有智力要求且值足够，跳过
            if (hasIntel)
            {
                foreach (var sr in recipe.skillRequirements)
                {
                    if (sr.skill == SkillType.智力 && sr.level >= intelLevel)
                    {
                        skipped++;
                        goto next;
                    }
                }
                // 值不够，不移除旧的，但添加新的会重复。跳过已处理过的。
                skipped++;
                continue;
            }

            // 添加智力要求
            var oldReqs = recipe.skillRequirements ?? new SkillRequirement[0];
            var newReqs = new SkillRequirement[oldReqs.Length + 1];
            for (int i = 0; i < oldReqs.Length; i++)
                newReqs[i] = oldReqs[i];
            newReqs[oldReqs.Length] = new SkillRequirement { skill = SkillType.智力, level = intelLevel };

            recipe.skillRequirements = newReqs;
            EditorUtility.SetDirty(recipe);
            changed++;

        next: ;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AddIntel] 已为 {changed} 个配方添加智力门槛，跳过 {skipped} 个（已有/不适用）");
        EditorUtility.DisplayDialog("完成",
            $"已为 {changed} 个配方添加智力门槛。\n跳过 {skipped} 个（已有智力要求或不适用）。\n\n规则：\n- 建筑材料 智1\n- 通用材料/工具 智2\n- 电路/化工/弹药 智3-4\n- 精密电子 智5-7\n- 终局装备 智7-9",
            "确定");
    }
}
