using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 后期/终局配方代价上调工具。
/// L3+(智5+) 材料翻倍，L4+(智8+) 翻三倍。
/// 菜单栏 → Tools/工业/上调后期配方代价
/// </summary>
public static class ScaleRecipeCosts
{
    const string RecipeBase = "Assets/_Game/Config/Recipes";

    // 需要上调的工作站（L3+）
    static readonly HashSet<WorkstationTier> LateGameStations = new HashSet<WorkstationTier>
    {
        WorkstationTier.AdvancedBench,       // L3 高级工作台
        WorkstationTier.Chemistry,           // L3 研究中心
        WorkstationTier.Machining,           // L4 机械加工台
        WorkstationTier.ElectronicsAssembly, // L5 电子装配台
        WorkstationTier.ElementFurnace,      // L5 元素合成炉
    };

    // L4+ 翻三倍的工作站
    static readonly HashSet<WorkstationTier> EndGameStations = new HashSet<WorkstationTier>
    {
        WorkstationTier.Machining,           // L4
        WorkstationTier.ElectronicsAssembly, // L5
        WorkstationTier.ElementFurnace,      // L5
    };

    // 终局稀有材料（这些翻倍但不过度）
    static readonly HashSet<string> RareMaterials = new HashSet<string>
    {
        "TitaniumAlloy", "ChipSet", "CapacitorBank", "ServoMotor",
        "OpticalLens", "CarbonFiber", "VaccineSerum", "GeneSample",
        "EnrichedUranium", "SmallReactor", "FusionCore_Small", "FusionCore_Large",
    };

    // 不翻倍的消耗品/工具类
    static readonly HashSet<string> SkipCategories = new HashSet<string>
    {
        "Consumable", "Cooking", "Weapon", "Armor", "Ammo", "Tool",
    };

    [MenuItem("Tools/工业/上调后期配方代价")]
    public static void ScaleUp()
    {
        var guids = AssetDatabase.FindAssets("t:RecipeData", new[] { RecipeBase });
        int changed = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
            if (recipe == null || recipe.materials == null || recipe.materials.Length == 0)
                continue;
            if (!LateGameStations.Contains(recipe.requiredStation))
                continue;
            if (SkipCategories.Contains(recipe.category.ToString()))
                continue;

            bool isEndGame = EndGameStations.Contains(recipe.requiredStation);
            float multiplier = isEndGame ? 3f : 2f;

            bool anyChange = false;
            foreach (var mat in recipe.materials)
            {
                if (mat.itemData == null) continue;
                string assetName = GetAssetName(mat.itemData);

                // 稀有材料只翻1.5倍，不跟大部队
                float m = RareMaterials.Contains(assetName) ? 1.5f : multiplier;
                int newCount = Mathf.Max(1, Mathf.RoundToInt(mat.count * m));

                if (newCount != mat.count)
                {
                    mat.count = newCount;
                    anyChange = true;
                }
            }

            if (anyChange)
            {
                EditorUtility.SetDirty(recipe);
                changed++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ScaleRecipeCosts] 已上调 {changed} 个后期/终局配方代价 (L3×2, L4+×3, 稀有×1.5)");
        EditorUtility.DisplayDialog("完成", $"已上调 {changed} 个配方材料代价。\n\n规则：\n- L3(高级台/研究中心) → ×2\n- L4+(机械台/电子台/元素炉) → ×3\n- 稀有材料(钛合金/芯片组等) → ×1.5\n- 消耗品/武器/弹药 → 不变", "确定");
    }

    static string GetAssetName(ItemData item)
    {
        if (item == null) return "";
        var path = AssetDatabase.GetAssetPath(item);
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }
}
