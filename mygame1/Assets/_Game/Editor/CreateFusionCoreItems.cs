using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键生成聚变核心物品 + 元素合成炉建筑 + 合成配方。
/// 菜单栏 → Game Tools → Create FusionCore Items
/// </summary>
public static class CreateFusionCoreItems
{
    const string ItemBase = "Assets/_Game/Config/Items";
    const string RecipeBase = "Assets/_Game/Config/Recipes/元素合成炉";
    const string BuildableBase = "Assets/_Game/Config/Buildables";

    static Dictionary<string, ItemData> _itemLookup;

    [MenuItem("Game Tools/Create FusionCore Items")]
    public static void CreateAll()
    {
        BuildItemLookup();
        EnsureDirs();

        CreateFusionCoreSmall();
        CreateFusionCoreLarge();
        CreateFusionCoreRecipes();
        CreateElementFurnaceBuildable();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("聚变核心物品、配方、元素合成炉已全部创建！");
    }

    static void BuildItemLookup()
    {
        _itemLookup = new Dictionary<string, ItemData>();
        var guids = AssetDatabase.FindAssets("t:ItemData", new[] { ItemBase });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null && !string.IsNullOrEmpty(item.itemName))
                _itemLookup[item.itemName] = item;
        }
    }

    static ItemData GetItem(string name)
    {
        return _itemLookup.TryGetValue(name, out var item) ? item : null;
    }

    static void EnsureDirs()
    {
        foreach (var d in new[] { ItemBase, RecipeBase, BuildableBase })
        {
            if (AssetDatabase.IsValidFolder(d)) continue;
            var parts = d.Split('/');
            var parent = string.Join("/", parts, 0, parts.Length - 1);
            AssetDatabase.CreateFolder(parent, parts[parts.Length - 1]);
        }
    }

    // ── 小型聚变核心 ────────────────────────────────

    static void CreateFusionCoreSmall()
    {
        var path = $"{ItemBase}/FusionCore_Small.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (existing != null)
        {
            Debug.Log("聚变核心(小) 已存在，跳过");
            return;
        }

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemName = "聚变核心(小)";
        item.description = "微型核反应堆燃料棒。燃耗4小时，产出铀30/h。";
        item.category = ItemCategory.SemiFinished;
        item.quality = ItemQuality.Scavenged;
        item.maxStack = 10;
        item.weight = 2f;
        item.gridWidth = 1;
        item.gridHeight = 1;

        AssetDatabase.CreateAsset(item, path);
        Debug.Log("创建: 聚变核心(小)");
    }

    // ── 大型聚变核心 ────────────────────────────────

    static void CreateFusionCoreLarge()
    {
        var path = $"{ItemBase}/FusionCore_Large.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (existing != null)
        {
            Debug.Log("聚变核心(大) 已存在，跳过");
            return;
        }

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemName = "聚变核心(大)";
        item.description = "大型微型核反应堆燃料棒。3个小核心合成。燃耗12小时，产出铀45/h。";
        item.category = ItemCategory.SemiFinished;
        item.quality = ItemQuality.Scavenged;
        item.maxStack = 5;
        item.weight = 5f;
        item.gridWidth = 1;
        item.gridHeight = 1;

        AssetDatabase.CreateAsset(item, path);
        Debug.Log("创建: 聚变核心(大)");
    }

    // ── 合成配方 ─────────────────────────────────────

    static void CreateFusionCoreRecipes()
    {
        // 小型聚变核心 = 浓缩铀×3 + 精炼煤炭×9 + 汽油×3 + 煤焦油×6
        CreateRecipe(WorkstationTier.ElementFurnace, "Recipe_FusionCore_Small",
            RecipeCategory.Industry, GetItem("聚变核心(小)"), 1, 30f, 50f,
            new (string, int)[]
            {
                ("浓缩铀", 3),
                ("精炼煤炭", 9),
                ("汽油", 3),
                ("煤焦油", 6),
            });

        // 大型聚变核心 = 聚变核心(小)×3
        CreateRecipe(WorkstationTier.ElementFurnace, "Recipe_FusionCore_Large",
            RecipeCategory.Industry, GetItem("聚变核心(大)"), 1, 60f, 80f,
            new (string, int)[]
            {
                ("聚变核心(小)", 3),
            });
    }

    static RecipeData CreateRecipe(WorkstationTier station, string recipeName,
        RecipeCategory category, ItemData result, int resultCount,
        float craftTime, float xp, (string itemName, int count)[] materials)
    {
        string path = $"{RecipeBase}/{recipeName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
        if (existing != null)
        {
            existing.recipeName = recipeName;
            existing.category = category;
            existing.requiredStation = station;
            existing.resultItem = result;
            existing.resultCount = resultCount;
            existing.craftTime = craftTime;
            existing.xpReward = xp;
            existing.materials = ToItemReq(materials);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var recipe = ScriptableObject.CreateInstance<RecipeData>();
        recipe.recipeName = recipeName;
        recipe.category = category;
        recipe.requiredStation = station;
        recipe.resultItem = result;
        recipe.resultCount = resultCount;
        recipe.craftTime = craftTime;
        recipe.xpReward = xp;
        recipe.materials = ToItemReq(materials);
        AssetDatabase.CreateAsset(recipe, path);
        return recipe;
    }

    static ItemRequirement[] ToItemReq((string itemName, int count)[] mats)
    {
        if (mats == null || mats.Length == 0) return new ItemRequirement[0];
        var arr = new ItemRequirement[mats.Length];
        for (int i = 0; i < mats.Length; i++)
            arr[i] = new ItemRequirement { itemData = GetItem(mats[i].itemName), count = mats[i].count };
        return arr;
    }

    // ── 元素合成炉 建筑 ─────────────────────────────

    static void CreateElementFurnaceBuildable()
    {
        var path = $"{BuildableBase}/Buildable_ElementFurnace.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BuildableData>(path);
        if (existing != null)
        {
            Debug.Log("元素合成炉建筑 已存在，跳过");
            return;
        }

        var b = ScriptableObject.CreateInstance<BuildableData>();
        b.displayName = "元素合成炉";
        b.description = "终局工业设备。在极高温度下将浓缩铀与多种燃料聚合成聚变核心。需2000W电力供应。";
        b.category = BuildableCategory.LateIndustrial;
        b.buildDuration = 45f;
        b.maxHealth = 1500f;
        b.isWorkstation = true;
        b.workstationTier = WorkstationTier.ElementFurnace;
        b.snapSize = 3f;
        b.placementSize = new Vector3(3f, 2.5f, 3f);
        b.powerRequired = 2000f;
        b.staminaDrainPerSec = 3f;
        b.skillRequirements = new SkillRequirement[]
        {
            new SkillRequirement { skill = SkillType.智力, level = 8 },
            new SkillRequirement { skill = SkillType.建造拆解, level = 7 },
        };
        b.materials = new ItemRequirement[]
        {
            new ItemRequirement { itemData = GetItem("钛合金"), count = 5 },
            new ItemRequirement { itemData = GetItem("高级零件"), count = 8 },
        };

        AssetDatabase.CreateAsset(b, path);
        Debug.Log("创建建筑: 元素合成炉");
    }

    static string StationName(WorkstationTier tier)
    {
        switch (tier)
        {
            case WorkstationTier.ElementFurnace: return "元素合成炉";
            case WorkstationTier.SimpleBench: return "简易工作台";
            case WorkstationTier.Machining: return "机械加工台";
            case WorkstationTier.ElectronicsAssembly: return "电子装配台";
            case WorkstationTier.Chemistry: return "化学台";
            default: return "元素合成炉";
        }
    }
}
