using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键生成所有合成配方 .asset 文件。
/// 用法：先执行 Game Tools → Create Crafting Items, 再执行本菜单。
/// 菜单栏 → Game Tools → Create Crafting Recipes
///
/// 生成 ~120 RecipeData，覆盖 8 个工作台等级。
/// 遵循"前期简单、后期丰富"设计铁律。
/// </summary>
public static class CreateCraftingRecipes
{
    const string RecipeBase = "Assets/_Game/Config/Recipes";

    // 站台对应的子目录名
    static readonly Dictionary<WorkstationTier, string> StationDirs = new Dictionary<WorkstationTier, string>
    {
        { WorkstationTier.Hands,         "徒手" },
        { WorkstationTier.Campfire,      "篝火" },
        { WorkstationTier.SimpleBench,   "简易工作台" },
        { WorkstationTier.Furnace,       "熔炉" },
        { WorkstationTier.MediumBench,   "中级工作台" },
        { WorkstationTier.AdvancedBench, "高级工作台" },
        { WorkstationTier.Chemistry,     "研究中心" },
        { WorkstationTier.Machining,             "机械加工台" },
        { WorkstationTier.ElectronicsAssembly,  "电子装配台" },
        { WorkstationTier.ElementFurnace,       "元素合成炉" },
    };

    static Dictionary<string, ItemData> _itemLookup;

    [MenuItem("Game Tools/Create Industrial Kit Items")]
    public static void CreateIndustrialKitItems()
    {
        const string dir = "Assets/_Game/Config/Items/Workstations";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Game/Config/Items", "Workstations");

        CreateKitItem(dir, "WS_PressMachine", "冲压机套件", 20f);
        CreateKitItem(dir, "WS_IndustrialFurnace", "工业熔炉套件", 25f);
        CreateKitItem(dir, "WS_ElectrolysisTank", "电解槽套件", 25f);
        CreateKitItem(dir, "WS_Fermenter", "发酵罐套件", 15f);
        CreateKitItem(dir, "WS_Distiller", "蒸馏器套件", 15f);
        CreateKitItem(dir, "WS_Crusher", "粉碎机套件", 18f);
        CreateKitItem(dir, "WS_Loom", "织布机套件", 12f);
        CreateKitItem(dir, "WS_Sawmill", "锯木机套件", 14f);
        CreateKitItem(dir, "WS_WaterPump", "水泵套件", 14f);
        CreateKitItem(dir, "WS_Kiln", "炭窑套件", 15f);
        CreateKitItem(dir, "WS_ElectricGenerator", "发电机套件", 22f);
        CreateKitItem(dir, "WS_Lathe", "车床套件", 18f);
        CreateKitItem(dir, "WS_AssemblyTable", "装配台套件", 16f);
        CreateKitItem(dir, "WS_RecyclingStation", "回收站套件", 15f);
        CreateKitItem(dir, "WS_SolarPanel", "太阳能板套件", 25f);
        CreateKitItem(dir, "WS_Smokehouse", "熏制房套件", 12f);
        CreateKitItem(dir, "WS_CanningMachine", "罐头封装机套件", 18f);
        CreateKitItem(dir, "WS_PharmaBench", "制药台套件", 20f);
        CreateKitItem(dir, "WS_RadioTower", "广播塔套件", 30f);

        // 新增7个工业设备套件（v0.22 工业链扩展）
        CreateKitItem(dir, "WS_WireDrawer", "拉线机套件", 15f);
        CreateKitItem(dir, "WS_BatteryLine", "电池生产线套件", 18f);
        CreateKitItem(dir, "WS_ElectronicsAssembler", "电子装配机套件", 20f);
        CreateKitItem(dir, "WS_CircuitPrinter", "电路印刷机套件", 22f);
        CreateKitItem(dir, "WS_GunpowderFactory", "火药厂套件", 20f);
        CreateKitItem(dir, "WS_AmmoLoader", "弹药装填机套件", 22f);
        CreateKitItem(dir, "WS_WeaponAssembly", "武器组装台套件", 25f);

        // 终局设备套件（电子装配台 / 机械加工台）
        CreateKitItem(dir, "WS_Centrifuge", "离心机套件", 22f);
        CreateKitItem(dir, "WS_GeneAnalyzer", "基因分析台套件", 28f);
        CreateKitItem(dir, "WS_PrecisionAssembly", "精密装配台套件", 25f);
        CreateKitItem(dir, "WS_WaterPurifier", "净水厂套件", 30f);
        CreateKitItem(dir, "WS_MobileFortress", "移动要塞基地车套件", 50f);
        CreateKitItem(dir, "WS_AutoTurret", "自动炮塔套件", 35f);
        CreateKitItem(dir, "WS_EMFence", "电磁围栏套件", 30f);
        CreateKitItem(dir, "WS_DronePlatform", "无人机平台套件", 35f);
        CreateKitItem(dir, "WS_NuclearPlant", "核电站套件", 50f);

        // 空电池
        CreateKitItem(dir, "EmptyBattery", "空电池", 0.3f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("28 个工业套件物品已创建（含空电池）！");
    }

    static void CreateKitItem(string dir, string assetName, string displayName, float weight)
    {
        string path = $"{dir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (existing != null)
        {
            // 更新已存在的
            existing.itemName = displayName;
            existing.category = ItemCategory.Buildable;
            existing.gridWidth = 2;
            existing.gridHeight = 2;
            existing.weight = weight;
            existing.maxStack = 2;
            EditorUtility.SetDirty(existing);
            Debug.Log($"已更新 {displayName}");
            return;
        }

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemName = displayName;
        item.category = ItemCategory.Buildable;
        item.gridWidth = 2;
        item.gridHeight = 2;
        item.weight = weight;
        item.maxStack = 2;

        AssetDatabase.CreateAsset(item, path);
        Debug.Log($"创建 {displayName}");
    }

    [MenuItem("Game Tools/Create Crafting Recipes")]
    public static void CreateAll()
    {
        BuildItemLookup();
        EnsureDirs();

        CreateHandsRecipes();
        CreateCampfireRecipes();
        CreateSimpleBenchRecipes();
        CreateFurnaceRecipes();
        CreateMediumBenchRecipes();
        CreateAdvancedBenchRecipes();
        CreateChemistryRecipes();
        CreateMachiningRecipes();
        CreateElectronicsAssemblyRecipes();
        CreateElementFurnaceRecipes();

        UpdateCatalog();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"配方资产生成完毕！路径: {RecipeBase}");
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
                // key = 文件名（不含扩展名）
                string key = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!_itemLookup.ContainsKey(key))
                    _itemLookup[key] = item;
            }
        }
        Debug.Log($"[CreateCraftingRecipes] 已索引 {_itemLookup.Count} 个 ItemData");
    }

    static void EnsureDirs()
    {
        if (!AssetDatabase.IsValidFolder(RecipeBase))
            AssetDatabase.CreateFolder("Assets/_Game/Config", "Recipes");
        foreach (var kv in StationDirs)
        {
            string dir = $"{RecipeBase}/{kv.Value}";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(RecipeBase, kv.Value);
        }
    }

    static ItemData GetItem(string assetName)
    {
        if (_itemLookup.TryGetValue(assetName, out var item))
            return item;
        Debug.LogWarning($"[CreateCraftingRecipes] 未找到 ItemData: {assetName}");
        return null;
    }

    static RecipeData CreateRecipe(WorkstationTier station, string recipeName,
        RecipeCategory category, ItemData result, int resultCount = 1,
        float craftTime = 2f, float xp = 15f,
        (ItemData item, int count)[] materials = null,
        (SkillType skill, int level)[] skills = null,
        string recipeId = null)
    {
        string dir = $"{RecipeBase}/{StationDirs[station]}";
        string path = $"{dir}/{recipeName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
        if (existing != null)
        {
            // 更新已有配方
            existing.recipeName = recipeName;
            existing.recipeId = string.IsNullOrEmpty(recipeId) ? recipeName : recipeId;
            existing.category = category;
            existing.requiredStation = station;
            existing.resultItem = result;
            existing.resultCount = resultCount;
            existing.craftTime = craftTime;
            existing.xpReward = xp;
            existing.materials = ToItemReq(materials);
            existing.skillRequirements = ToSkillReq(skills);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var recipe = ScriptableObject.CreateInstance<RecipeData>();
        recipe.recipeName = recipeName;
        recipe.recipeId = string.IsNullOrEmpty(recipeId) ? recipeName : recipeId;
        recipe.category = category;
        recipe.requiredStation = station;
        recipe.resultItem = result;
        recipe.resultCount = resultCount;
        recipe.craftTime = craftTime;
        recipe.xpReward = xp;
        recipe.materials = ToItemReq(materials);
        recipe.skillRequirements = ToSkillReq(skills);

        AssetDatabase.CreateAsset(recipe, path);
        return recipe;
    }

    static ItemRequirement[] ToItemReq((ItemData item, int count)[] mats)
    {
        if (mats == null || mats.Length == 0) return new ItemRequirement[0];
        var arr = new ItemRequirement[mats.Length];
        for (int i = 0; i < mats.Length; i++)
            arr[i] = new ItemRequirement { itemData = mats[i].item, count = mats[i].count };
        return arr;
    }

    static SkillRequirement[] ToSkillReq((SkillType skill, int level)[] skills)
    {
        if (skills == null || skills.Length == 0) return new SkillRequirement[0];
        var arr = new SkillRequirement[skills.Length];
        for (int i = 0; i < skills.Length; i++)
            arr[i] = new SkillRequirement { skill = skills[i].skill, level = skills[i].level };
        return arr;
    }

    static (ItemData, int) M(string name, int count = 1)
    {
        var item = GetItem(name);
        if (item == null) Debug.LogError($"[CreateCraftingRecipes] 关键物品缺失: {name}");
        return (item, count);
    }

    // ================================================================
    // 徒手 Hands — 最基础的生存制作 (8 recipes)
    // ================================================================

    static void CreateHandsRecipes()
    {
        var S = WorkstationTier.Hands;

        CreateRecipe(S, "木棍", RecipeCategory.Weapon,
            GetItem("WoodenStick"), 1, 2f, 8f,
            new[] { M("Branch", 2) });

        CreateRecipe(S, "简易绳索", RecipeCategory.Material,
            GetItem("Rope"), 1, 3f, 10f,
            new[] { M("PlantFiber", 4) });

        CreateRecipe(S, "简易绷带", RecipeCategory.Consumable,
            GetItem("Bandage"), 2, 2f, 8f,
            new[] { M("Cloth", 2), M("PlantFiber", 1) });

        CreateRecipe(S, "燧石刀", RecipeCategory.Tool,
            GetItem("ShortKnife"), 1, 3f, 12f,
            new[] { M("Flint", 2), M("Branch", 1) });

        CreateRecipe(S, "简易火把", RecipeCategory.Tool,
            GetItem("WoodenStick"), 2, 1f, 5f,
            new[] { M("Branch", 2), M("Cloth", 1) });

        CreateRecipe(S, "皮革片", RecipeCategory.Material,
            GetItem("Leather"), 1, 5f, 10f,
            new[] { M("AnimalHide", 1), M("Flint", 1) });

        CreateRecipe(S, "碎布料", RecipeCategory.Material,
            GetItem("Cloth"), 3, 3f, 8f,
            new[] { M("PlantFiber", 6) });

        CreateRecipe(S, "石斧", RecipeCategory.Tool,
            GetItem("StoneAxe"), 1, 5f, 15f,
            new[] { M("Stone", 2), M("Branch", 2), M("PlantFiber", 2) });

        CreateRecipe(S, "石镐", RecipeCategory.Tool,
            GetItem("StonePick"), 1, 5f, 15f,
            new[] { M("Stone", 2), M("Branch", 2), M("PlantFiber", 2) });

        CreateRecipe(S, "树皮绳", RecipeCategory.Material,
            GetItem("Rope"), 1, 4f, 10f,
            new[] { M("Bark", 3) });
    }

    // ================================================================
    // 篝火 Campfire — 烹饪 (10 recipes)
    // ================================================================

    static void CreateCampfireRecipes()
    {
        var S = WorkstationTier.Campfire;

        CreateRecipe(S, "烤肉", RecipeCategory.Cooking,
            GetItem("CookedMeat"), 1, 3f, 12f,
            new[] { M("RawMeat", 1), M("Branch", 1) });

        CreateRecipe(S, "烤肉串", RecipeCategory.Cooking,
            GetItem("CookedMeat"), 2, 4f, 15f,
            new[] { M("RawMeat", 2), M("Branch", 2) });

        CreateRecipe(S, "炖菜", RecipeCategory.Cooking,
            GetItem("Stew"), 1, 5f, 18f,
            new[] { M("RawMeat", 1), M("Mushroom", 2), M("Berry", 1), M("PurifiedWater", 1) });

        CreateRecipe(S, "汤", RecipeCategory.Cooking,
            GetItem("Soup"), 1, 4f, 15f,
            new[] { M("Mushroom", 2), M("Berry", 1), M("PurifiedWater", 1) });

        CreateRecipe(S, "烤肉干", RecipeCategory.Cooking,
            GetItem("DriedMeat"), 2, 6f, 15f,
            new[] { M("RawMeat", 3), M("Branch", 3) });

        CreateRecipe(S, "熏肉", RecipeCategory.Cooking,
            GetItem("SmokedMeat"), 1, 8f, 20f,
            new[] { M("RawMeat", 2), M("WoodPlank", 1), M("Coal", 1) });

        CreateRecipe(S, "煮鸡蛋", RecipeCategory.Cooking,
            GetItem("CookedMeat"), 1, 2f, 8f,
            new[] { M("PurifiedWater", 1) });

        CreateRecipe(S, "烤蘑菇", RecipeCategory.Cooking,
            GetItem("CookedMeat"), 1, 2f, 8f,
            new[] { M("Mushroom", 3) });

        CreateRecipe(S, "烤鱼", RecipeCategory.Cooking,
            GetItem("CookedMeat"), 1, 3f, 12f,
            new[] { M("RawMeat", 1), M("Branch", 1) });

        CreateRecipe(S, "果酱", RecipeCategory.Cooking,
            GetItem("Juice"), 1, 3f, 10f,
            new[] { M("Berry", 4) });
    }

    // ================================================================
    // 简易工作台 SimpleBench — 基础工具/医疗/建材/民生 (14 recipes)
    // ================================================================

    static void CreateSimpleBenchRecipes()
    {
        var S = WorkstationTier.SimpleBench;

        // 工具
        CreateRecipe(S, "铁斧", RecipeCategory.Tool,
            GetItem("IronAxe"), 1, 4f, 20f,
            new[] { M("IronIngot", 2), M("Branch", 2) });
        CreateRecipe(S, "铁镐", RecipeCategory.Tool,
            GetItem("IronPick"), 1, 4f, 20f,
            new[] { M("IronIngot", 2), M("Branch", 2) });
        CreateRecipe(S, "锤子", RecipeCategory.Tool,
            GetItem("Hammer"), 1, 3f, 15f,
            new[] { M("IronIngot", 1), M("WoodPlank", 1) });
        CreateRecipe(S, "锯子", RecipeCategory.Tool,
            GetItem("Saw"), 1, 3f, 15f,
            new[] { M("IronIngot", 2), M("WoodPlank", 1) });
        CreateRecipe(S, "铁锹", RecipeCategory.Tool,
            GetItem("Shovel"), 1, 4f, 18f,
            new[] { M("IronIngot", 2), M("WoodPlank", 3) });

        // 基础医疗
        CreateRecipe(S, "急救包", RecipeCategory.Consumable,
            GetItem("FirstAidKit"), 1, 4f, 20f,
            new[] { M("Bandage", 2), M("Antiseptic", 1), M("Cloth", 1) });
        CreateRecipe(S, "消毒水", RecipeCategory.Consumable,
            GetItem("Antiseptic"), 1, 3f, 15f,
            new[] { M("Alcohol", 1), M("Herb", 2) });
        CreateRecipe(S, "止血粉", RecipeCategory.Consumable,
            GetItem("StypticPowder"), 1, 2f, 12f,
            new[] { M("Herb", 3), M("Cloth", 1) });

        // 建材（建筑/民生下放）
        CreateRecipe(S, "木板", RecipeCategory.Material,
            GetItem("WoodPlank"), 3, 2f, 10f,
            new[] { M("WoodLog", 1) });
        CreateRecipe(S, "石砖", RecipeCategory.Building,
            GetItem("StoneBrick"), 3, 3f, 12f,
            new[] { M("Stone", 2) });
        CreateRecipe(S, "钉子", RecipeCategory.Material,
            GetItem("Nails"), 10, 2f, 8f,
            new[] { M("ScrapMetal", 1) });
        CreateRecipe(S, "布匹", RecipeCategory.Material,
            GetItem("ClothRoll"), 1, 3f, 10f,
            new[] { M("Cloth", 3), M("Thread", 2) });

        // 生产设备
        CreateRecipe(S, "织布机套件", RecipeCategory.Industry,
            GetItem("WS_Loom"), 1, 6f, 22f,
            new[] { M("WoodPlank", 6), M("Nails", 4), M("IronIngot", 2) },
            new[] { (SkillType.建造拆解, 2) });
        CreateRecipe(S, "锯木机套件", RecipeCategory.Industry,
            GetItem("WS_Sawmill"), 1, 6f, 24f,
            new[] { M("WoodPlank", 4), M("IronIngot", 3), M("Nails", 4) },
            new[] { (SkillType.建造拆解, 2) });
        CreateRecipe(S, "熏制房套件", RecipeCategory.Industry,
            GetItem("WS_Smokehouse"), 1, 6f, 22f,
            new[] { M("WoodLog", 8), M("StoneBrick", 4), M("IronIngot", 2) },
            new[] { (SkillType.建造拆解, 1) });

        // 食物保存·前期
        CreateRecipe(S, "肉干", RecipeCategory.Cooking,
            GetItem("DriedMeat"), 3, 5f, 15f,
            new[] { M("RawMeat", 3) });
        CreateRecipe(S, "腌菜", RecipeCategory.Cooking,
            GetItem("PickleVeg"), 2, 4f, 12f,
            new[] { M("Mushroom", 4), M("Berry", 2) });

        // 基础零件
        CreateRecipe(S, "弹簧", RecipeCategory.Material,
            GetItem("Spring"), 2, 2f, 10f,
            new[] { M("IronIngot", 1) });
        CreateRecipe(S, "电线", RecipeCategory.Material,
            GetItem("Wire"), 3, 2f, 10f,
            new[] { M("CopperIngot", 1) });
        CreateRecipe(S, "螺丝", RecipeCategory.Material,
            GetItem("Screw"), 5, 2f, 8f,
            new[] { M("IronIngot", 1) });
    }

    // ================================================================
    // 熔炉 Furnace — 冶炼 (10 recipes)
    // ================================================================

    static void CreateFurnaceRecipes()
    {
        var S = WorkstationTier.Furnace;

        CreateRecipe(S, "冶炼铁锭", RecipeCategory.Smelting,
            GetItem("IronIngot"), 2, 4f, 20f,
            new[] { M("IronOre", 3), M("Coal", 1) });
        CreateRecipe(S, "冶炼铜锭", RecipeCategory.Smelting,
            GetItem("CopperIngot"), 2, 4f, 20f,
            new[] { M("CopperOre", 3), M("Coal", 1) });
        CreateRecipe(S, "冶炼钢锭", RecipeCategory.Smelting,
            GetItem("SteelIngot"), 1, 6f, 30f,
            new[] { M("IronIngot", 2), M("Coal", 2), M("Limestone", 1) });
        CreateRecipe(S, "冶炼铅锭", RecipeCategory.Smelting,
            GetItem("LeadIngot"), 2, 4f, 18f,
            new[] { M("LeadOre", 3), M("Coal", 1) });
        CreateRecipe(S, "冶炼铝锭", RecipeCategory.Smelting,
            GetItem("AluminumIngot"), 2, 4f, 20f,
            new[] { M("AluminumOre", 3), M("Coal", 2) });

        CreateRecipe(S, "回收废铁", RecipeCategory.Smelting,
            GetItem("IronIngot"), 1, 4f, 12f,
            new[] { M("ScrapMetal", 3), M("Coal", 1) });

        CreateRecipe(S, "回收废金属", RecipeCategory.Smelting,
            GetItem("ScrapMetal"), 3, 4f, 12f,
            new[] { M("IronOre", 2), M("Coal", 1) });

        CreateRecipe(S, "烧制玻璃碎片", RecipeCategory.Building,
            GetItem("GlassShard"), 3, 3f, 8f,
            new[] { M("Sand", 3) });

        CreateRecipe(S, "烧制玻璃板", RecipeCategory.Building,
            GetItem("GlassPane"), 2, 3f, 12f,
            new[] { M("Sand", 3), M("Coal", 1) });
        CreateRecipe(S, "烧制水泥", RecipeCategory.Building,
            GetItem("Cement"), 3, 4f, 15f,
            new[] { M("Limestone", 2), M("Clay", 1), M("Coal", 2) });

        CreateRecipe(S, "烧制木炭", RecipeCategory.Smelting,
            GetItem("Coal"), 3, 3f, 10f,
            new[] { M("WoodLog", 2) });
        CreateRecipe(S, "黑火药", RecipeCategory.Material,
            GetItem("BlackPowder"), 5, 5f, 20f,
            new[] { M("Sulfur", 1), M("Niter", 2), M("Coal", 3) });

        CreateRecipe(S, "精炼煤炭", RecipeCategory.Smelting,
            GetItem("RefinedCoal"), 1, 8f, 25f,
            new[] { M("Coal", 3) });
        CreateRecipe(S, "干馏煤焦油", RecipeCategory.Smelting,
            GetItem("CoalTar"), 1, 6f, 18f,
            new[] { M("Coal", 2) });

        // 生产设备
        CreateRecipe(S, "炭窑套件", RecipeCategory.Industry,
            GetItem("WS_Kiln"), 1, 8f, 30f,
            new[] { M("StoneBrick", 6), M("Clay", 4), M("IronIngot", 2) },
            new[] { (SkillType.建造拆解, 2) });
        CreateRecipe(S, "回收站套件", RecipeCategory.Industry,
            GetItem("WS_RecyclingStation"), 1, 8f, 28f,
            new[] { M("SteelIngot", 4), M("IronIngot", 4), M("CommonParts", 3), M("Bearing", 2) },
            new[] { (SkillType.建造拆解, 3) });
    }

    // ================================================================
    // 中级工作台 MediumBench — 中端武器/护甲/弹药/酿造 (19 recipes)
    // ================================================================

    static void CreateMediumBenchRecipes()
    {
        var S = WorkstationTier.MediumBench;

        // 武器
        CreateRecipe(S, "长矛", RecipeCategory.Weapon,
            GetItem("Spear"), 1, 4f, 20f,
            new[] { M("IronIngot", 2), M("WoodPlank", 3), M("Rope", 1) });
        CreateRecipe(S, "砍刀", RecipeCategory.Weapon,
            GetItem("Machete"), 1, 5f, 25f,
            new[] { M("IronIngot", 3), M("WoodPlank", 1) });
        CreateRecipe(S, "十字弓", RecipeCategory.Weapon,
            GetItem("Crossbow"), 1, 8f, 35f,
            new[] { M("IronIngot", 3), M("WoodPlank", 4), M("Rope", 2), M("Spring", 1) });

        // 护甲 — 旧皮甲/木盾已删除（新装备见 CreateEquipment）
        CreateRecipe(S, "工具腰带", RecipeCategory.Armor,
            GetItem("ToolBelt"), 1, 3f, 15f,
            new[] { M("Leather", 2), M("Thread", 1), M("Nails", 3) });

        // 弹药
        CreateRecipe(S, "箭矢", RecipeCategory.Ammo,
            GetItem("Arrow"), 5, 3f, 15f,
            new[] { M("Branch", 2), M("Flint", 1), M("Feather", 2) });
        CreateRecipe(S, "弩箭", RecipeCategory.Ammo,
            GetItem("Bolt"), 5, 3f, 15f,
            new[] { M("IronIngot", 1), M("Feather", 2) });
        CreateRecipe(S, "弹壳", RecipeCategory.Ammo,
            GetItem("BulletCasing"), 10, 2f, 10f,
            new[] { M("CopperIngot", 1) });
        CreateRecipe(S, "弹头", RecipeCategory.Ammo,
            GetItem("BulletHead"), 10, 2f, 12f,
            new[] { M("LeadIngot", 1) });

        // 中级医疗
        CreateRecipe(S, "吗啡", RecipeCategory.Consumable,
            GetItem("Morphine"), 1, 4f, 25f,
            new[] { M("Herb", 4), M("Alcohol", 2) },
            new[] { (SkillType.医疗生存, 2) });

        // 酿造（食品保存·中期）
        CreateRecipe(S, "酒精", RecipeCategory.Material,
            GetItem("Alcohol"), 2, 5f, 18f,
            new[] { M("Berry", 5), M("Mushroom", 2) });
        CreateRecipe(S, "熏肉架用", RecipeCategory.Cooking,
            GetItem("SmokedMeat"), 2, 6f, 20f,
            new[] { M("RawMeat", 2), M("WoodPlank", 2), M("Coal", 2) });

        // 工业设备
        CreateRecipe(S, "水泵套件", RecipeCategory.Industry,
            GetItem("WS_WaterPump"), 1, 8f, 28f,
            new[] { M("IronIngot", 4), M("SteelPipe", 2), M("CommonParts", 2) },
            new[] { (SkillType.建造拆解, 3) });
        CreateRecipe(S, "粉碎机套件", RecipeCategory.Industry,
            GetItem("WS_Crusher"), 1, 10f, 40f,
            new[] { M("SteelIngot", 4), M("Gear", 3), M("AdvancedParts", 2) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "装配台套件", RecipeCategory.Industry,
            GetItem("WS_AssemblyTable"), 1, 8f, 30f,
            new[] { M("SteelIngot", 4), M("CommonParts", 6), M("IronIngot", 3), M("Gear", 2) },
            new[] { (SkillType.建造拆解, 3) });
        CreateRecipe(S, "罐头封装机套件", RecipeCategory.Industry,
            GetItem("WS_CanningMachine"), 1, 8f, 32f,
            new[] { M("IronIngot", 6), M("SteelIngot", 3), M("CommonParts", 4) },
            new[] { (SkillType.建造拆解, 3) });

        // 杂项
        CreateRecipe(S, "通用零件", RecipeCategory.Material,
            GetItem("CommonParts"), 5, 3f, 15f,
            new[] { M("IronIngot", 1), M("ScrapMetal", 1) });
        CreateRecipe(S, "弹簧组件", RecipeCategory.Material,
            GetItem("SpringAssembly"), 2, 3f, 15f,
            new[] { M("Spring", 2), M("IronIngot", 1) });
        CreateRecipe(S, "齿轮", RecipeCategory.Material,
            GetItem("Gear"), 2, 3f, 18f,
            new[] { M("IronIngot", 2) });
        CreateRecipe(S, "绳索", RecipeCategory.Material,
            GetItem("Rope"), 2, 3f, 10f,
            new[] { M("PlantFiber", 5) });
        CreateRecipe(S, "线", RecipeCategory.Material,
            GetItem("Thread"), 3, 2f, 8f,
            new[] { M("PlantFiber", 3) });

        // 电力
        CreateRecipe(S, "电缆", RecipeCategory.Material,
            GetItem("Cable"), 3, 5f, 15f,
            new[] { M("CopperIngot", 2), M("Rubber", 1) });

        // 空电池（发电机/太阳能板充电用）
        CreateRecipe(S, "空电池", RecipeCategory.Material,
            GetItem("EmptyBattery"), 1, 5f, 18f,
            new[] { M("LeadIngot", 2), M("CopperIngot", 1), M("SulfuricAcid", 1) },
            new[] { (SkillType.建造拆解, 2) });

        // 终局弹药
        CreateRecipe(S, "爆炸箭头", RecipeCategory.Ammo,
            GetItem("Ammo_Explosive"), 3, 5f, 25f,
            new[] { M("Arrow", 3), M("Gunpowder", 2), M("ScrapMetal", 1), M("Cloth", 1) },
            new[] { (SkillType.枪械专精, 8) });
    }

    // ================================================================
    // 高级工作台 AdvancedBench — 精密制造 (21 recipes)
    // ================================================================

    static void CreateAdvancedBenchRecipes()
    {
        var S = WorkstationTier.AdvancedBench;

        // 高级零件链
        CreateRecipe(S, "高级零件", RecipeCategory.Material,
            GetItem("AdvancedParts"), 2, 4f, 25f,
            new[] { M("CommonParts", 3), M("SteelIngot", 1), M("SpringAssembly", 1) },
            new[] { (SkillType.建造拆解, 3) });
        CreateRecipe(S, "轴承", RecipeCategory.Material,
            GetItem("Bearing"), 2, 4f, 22f,
            new[] { M("SteelIngot", 2), M("CommonParts", 1) },
            new[] { (SkillType.建造拆解, 3) });

        // 高级武器 — 已移入武器组装台（工业设备），不再手搓
        // CreateRecipe(S, "霰弹枪", RecipeCategory.Weapon, ...);
        // CreateRecipe(S, "步枪", RecipeCategory.Weapon, ...);
        // CreateRecipe(S, "手枪", RecipeCategory.Weapon, ...);

        // 高级护甲 — 旧铁甲/铁盾已删除（新装备见 CreateEquipment）
        // CreateRecipe(S, "防弹衣", ...); // 已移入武器组装台（工业设备）

        // 高级工具
        CreateRecipe(S, "工具箱", RecipeCategory.Tool,
            GetItem("Toolbox"), 1, 5f, 25f,
            new[] { M("IronIngot", 2), M("WoodPlank", 2), M("CommonParts", 3) });

        // 电路
        CreateRecipe(S, "电路板", RecipeCategory.Material,
            GetItem("CircuitBoard"), 1, 5f, 25f,
            new[] { M("CopperIngot", 2), M("PlasticScrap", 3), M("Wire", 2) },
            new[] { (SkillType.建造拆解, 3) });

        CreateRecipe(S, "电子元件", RecipeCategory.Material,
            GetItem("Electronic"), 1, 6f, 28f,
            new[] { M("CopperIngot", 1), M("PlasticScrap", 2), M("Wire", 1) },
            new[] { (SkillType.建造拆解, 3) });

        // 建筑
        CreateRecipe(S, "钢管", RecipeCategory.Building,
            GetItem("SteelPipe"), 2, 4f, 20f,
            new[] { M("SteelIngot", 2) });
        CreateRecipe(S, "钢筋", RecipeCategory.Building,
            GetItem("Rebar"), 3, 4f, 20f,
            new[] { M("SteelIngot", 2) });

        // 高级弹药组件
        CreateRecipe(S, "底火", RecipeCategory.Ammo,
            GetItem("Primer"), 20, 3f, 18f,
            new[] { M("CopperIngot", 1), M("BlackPowder", 1) });

        // 工业设备
        CreateRecipe(S, "冲压机套件", RecipeCategory.Industry,
            GetItem("WS_PressMachine"), 1, 12f, 50f,
            new[] { M("SteelIngot", 6), M("AdvancedParts", 4), M("Gear", 3), M("Bearing", 2), M("Rebar", 2) },
            new[] { (SkillType.建造拆解, 5) });
        CreateRecipe(S, "车床套件", RecipeCategory.Industry,
            GetItem("WS_Lathe"), 1, 10f, 38f,
            new[] { M("SteelIngot", 8), M("AdvancedParts", 4), M("Bearing", 4), M("CircuitBoard", 2) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "弹药装填机套件", RecipeCategory.Industry,
            GetItem("WS_AmmoLoader"), 1, 12f, 45f,
            new[] { M("SteelIngot", 6), M("AdvancedParts", 3), M("Gear", 2), M("SteelPipe", 2) },
            new[] { (SkillType.建造拆解, 5) });
        CreateRecipe(S, "武器组装台套件", RecipeCategory.Industry,
            GetItem("WS_WeaponAssembly"), 1, 14f, 50f,
            new[] { M("SteelIngot", 8), M("AdvancedParts", 4), M("Gear", 3), M("Bearing", 2) },
            new[] { (SkillType.建造拆解, 5) });
        CreateRecipe(S, "拉线机套件", RecipeCategory.Industry,
            GetItem("WS_WireDrawer"), 1, 8f, 30f,
            new[] { M("SteelIngot", 4), M("CopperIngot", 3), M("CircuitBoard", 1), M("CommonParts", 2) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "电池生产线套件", RecipeCategory.Industry,
            GetItem("WS_BatteryLine"), 1, 10f, 35f,
            new[] { M("SteelIngot", 5), M("CopperIngot", 3), M("CircuitBoard", 2), M("AdvancedParts", 2) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "电子装配机套件", RecipeCategory.Industry,
            GetItem("WS_ElectronicsAssembler"), 1, 12f, 42f,
            new[] { M("SteelIngot", 5), M("CircuitBoard", 3), M("Coil", 2), M("AdvancedParts", 3) },
            new[] { (SkillType.建造拆解, 5), (SkillType.智力, 4) });
        CreateRecipe(S, "电路印刷机套件", RecipeCategory.Industry,
            GetItem("WS_CircuitPrinter"), 1, 10f, 38f,
            new[] { M("SteelIngot", 4), M("CircuitBoard", 2), M("ChipSet", 1), M("AdvancedParts", 2) },
            new[] { (SkillType.建造拆解, 5), (SkillType.智力, 4) });
        CreateRecipe(S, "火药厂套件", RecipeCategory.Industry,
            GetItem("WS_GunpowderFactory"), 1, 12f, 40f,
            new[] { M("SteelIngot", 6), M("AdvancedParts", 3), M("SulfuricAcid", 2), M("CircuitBoard", 1) },
            new[] { (SkillType.建造拆解, 4) });

        // 工作台制造
        CreateRecipe(S, "熔炉套件", RecipeCategory.Tool,
            GetItem("WS_Furnace"), 1, 10f, 35f,
            new[] { M("IronIngot", 5), M("StoneBrick", 10), M("Cement", 3) },
            new[] { (SkillType.建造拆解, 2) });
        CreateRecipe(S, "篝火套件", RecipeCategory.Tool,
            GetItem("WS_Campfire"), 1, 3f, 12f,
            new[] { M("Stone", 5), M("Branch", 3) });
        CreateRecipe(S, "简易工作台套件", RecipeCategory.Tool,
            GetItem("WS_SimpleBench"), 1, 5f, 20f,
            new[] { M("WoodPlank", 5), M("Nails", 10) });

        // 弹药 — 已移入弹药装填机（工业设备），不再手搓
        // CreateRecipe(S, "9mm手枪弹", ...);
        // CreateRecipe(S, "-45沙鹰弹", ...);
        // CreateRecipe(S, "7-62mm步枪弹", ...);
        // CreateRecipe(S, "12号霰弹", ...);
        // CreateRecipe(S, "穿甲弹", ...);
    }

    // ================================================================
    // 研究中心 Chemistry — 药剂/化工/投掷物/燃料 (18 recipes)
    // ================================================================

    static void CreateChemistryRecipes()
    {
        var S = WorkstationTier.Chemistry;

        // 化学基础
        CreateRecipe(S, "硫酸", RecipeCategory.Material,
            GetItem("SulfuricAcid"), 1, 6f, 30f,
            new[] { M("Sulfur", 3), M("PurifiedWater", 2) },
            new[] { (SkillType.医疗生存, 3) });
        CreateRecipe(S, "化学试剂", RecipeCategory.Material,
            GetItem("ChemicalAgent"), 2, 5f, 25f,
            new[] { M("SulfuricAcid", 1), M("Alcohol", 2), M("Herb", 3) },
            new[] { (SkillType.医疗生存, 3) });
        CreateRecipe(S, "无烟火药", RecipeCategory.Material,
            GetItem("Gunpowder"), 3, 6f, 30f,
            new[] { M("BlackPowder", 3), M("SulfuricAcid", 1), M("Alcohol", 1) },
            new[] { (SkillType.建造拆解, 3) });
        CreateRecipe(S, "合成汽油", RecipeCategory.Material,
            GetItem("Gasoline"), 1, 15f, 40f,
            new[] { M("CoalTar", 1), M("Alcohol", 2), M("ChemicalAgent", 1) },
            new[] { (SkillType.建造拆解, 4) });

        // 电池灌酸（化学活化：空电池+硫酸→电池）
        CreateRecipe(S, "电池灌酸", RecipeCategory.Material,
            GetItem("Battery"), 1, 6f, 18f,
            new[] { M("EmptyBattery", 1), M("SulfuricAcid", 1) },
            new[] { (SkillType.医疗生存, 2) });

        // 化工材料
        CreateRecipe(S, "橡胶", RecipeCategory.Material,
            GetItem("Rubber"), 2, 5f, 20f,
            new[] { M("PlantFiber", 4), M("ChemicalAgent", 1) },
            new[] { (SkillType.建造拆解, 2) });

        CreateRecipe(S, "塑料碎片", RecipeCategory.Material,
            GetItem("PlasticScrap"), 3, 5f, 18f,
            new[] { M("PlantFiber", 4), M("ChemicalAgent", 1) });

        // 高级药品（医疗三级链·高级）
        CreateRecipe(S, "抗生素", RecipeCategory.Consumable,
            GetItem("Antibiotics"), 2, 5f, 30f,
            new[] { M("ChemicalAgent", 1), M("Herb", 4), M("Alcohol", 1) },
            new[] { (SkillType.医疗生存, 4) });
        CreateRecipe(S, "维生素", RecipeCategory.Consumable,
            GetItem("Vitamin"), 3, 4f, 22f,
            new[] { M("ChemicalAgent", 1), M("Herb", 3) },
            new[] { (SkillType.医疗生存, 2) });
        CreateRecipe(S, "解毒剂", RecipeCategory.Consumable,
            GetItem("Antidote"), 1, 5f, 28f,
            new[] { M("ChemicalAgent", 1), M("Herb", 5), M("PurifiedWater", 2) },
            new[] { (SkillType.医疗生存, 4) });
        CreateRecipe(S, "肾上腺素", RecipeCategory.Consumable,
            GetItem("Adrenaline"), 1, 6f, 35f,
            new[] { M("ChemicalAgent", 2), M("Herb", 6), M("Alcohol", 2) },
            new[] { (SkillType.医疗生存, 5) });

        // 投掷物
        CreateRecipe(S, "燃烧瓶", RecipeCategory.Consumable,
            GetItem("Molotov"), 2, 4f, 22f,
            new[] { M("Alcohol", 2), M("Cloth", 2), M("GlassShard", 2) });
        CreateRecipe(S, "手榴弹", RecipeCategory.Consumable,
            GetItem("Grenade"), 1, 8f, 40f,
            new[] { M("IronIngot", 2), M("Gunpowder", 4), M("Primer", 2) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "烟雾弹", RecipeCategory.Consumable,
            GetItem("SmokeGrenade"), 2, 5f, 25f,
            new[] { M("Sulfur", 2), M("ChemicalAgent", 1), M("PlasticScrap", 3) });
        CreateRecipe(S, "闪光弹", RecipeCategory.Consumable,
            GetItem("Flashbang"), 2, 6f, 28f,
            new[] { M("AluminumIngot", 2), M("ChemicalAgent", 1), M("PlasticScrap", 3) });

        // 燃料
        CreateRecipe(S, "固体酒精", RecipeCategory.Material,
            GetItem("Alcohol"), 3, 4f, 20f,
            new[] { M("Alcohol", 1), M("ChemicalAgent", 1) });
        CreateRecipe(S, "电池组", RecipeCategory.Material,
            GetItem("BatteryPack"), 2, 5f, 25f,
            new[] { M("Battery", 4), M("CopperIngot", 1), M("SulfuricAcid", 1) });

        // 工业设备
        CreateRecipe(S, "发酵罐套件", RecipeCategory.Industry,
            GetItem("WS_Fermenter"), 1, 10f, 40f,
            new[] { M("SteelIngot", 3), M("SteelPipe", 3), M("CommonParts", 5) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "蒸馏器套件", RecipeCategory.Industry,
            GetItem("WS_Distiller"), 1, 10f, 42f,
            new[] { M("CopperIngot", 4), M("SteelPipe", 3), M("GlassPane", 2) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "制药台套件", RecipeCategory.Industry,
            GetItem("WS_PharmaBench"), 1, 10f, 36f,
            new[] { M("GlassPane", 6), M("SteelIngot", 3), M("ChemicalAgent", 4), M("CopperIngot", 2) },
            new[] { (SkillType.建造拆解, 4) });

        // 高级食物保存
        CreateRecipe(S, "罐头", RecipeCategory.Cooking,
            GetItem("CanFood"), 2, 5f, 22f,
            new[] { M("IronIngot", 1), M("CookedMeat", 3) });
        CreateRecipe(S, "净化水", RecipeCategory.Cooking,
            GetItem("PurifiedWater"), 5, 4f, 18f,
            new[] { M("Water", 5), M("Coal", 1) });
        CreateRecipe(S, "化肥", RecipeCategory.Farming,
            GetItem("Fertilizer"), 3, 4f, 18f,
            new[] { M("ChemicalAgent", 1), M("Mushroom", 3) });

        // 终局弹药·化工
        CreateRecipe(S, "燃烧弹", RecipeCategory.Ammo,
            GetItem("Ammo_Incendiary"), 5, 5f, 30f,
            new[] { M("Gasoline", 1), M("ChemicalAgent", 1), M("BulletCasing", 5), M("Cloth", 1) },
            new[] { (SkillType.枪械专精, 7) });

        // 终局医疗品
        CreateRecipe(S, "手术包", RecipeCategory.Consumable,
            GetItem("SurgeryKit"), 1, 8f, 35f,
            new[] { M("ClothRoll", 3), M("ChemicalAgent", 2), M("IronIngot", 1) },
            new[] { (SkillType.医疗生存, 6) });
        CreateRecipe(S, "免疫增强剂", RecipeCategory.Consumable,
            GetItem("ImmuneBooster"), 1, 12f, 50f,
            new[] { M("VaccineSerum", 1), M("ChemicalAgent", 1), M("Herb", 3) },
            new[] { (SkillType.医疗生存, 7) });
        CreateRecipe(S, "器官修复针", RecipeCategory.Consumable,
            GetItem("OrganRepair"), 1, 15f, 60f,
            new[] { M("VaccineSerum", 2), M("GeneSample", 1), M("ChemicalAgent", 3) },
            new[] { (SkillType.医疗生存, 8) });
        CreateRecipe(S, "神经再生剂", RecipeCategory.Consumable,
            GetItem("NerveRegen"), 1, 18f, 70f,
            new[] { M("VaccineSerum", 3), M("GeneSample", 2), M("Herb", 10) },
            new[] { (SkillType.医疗生存, 9) });
        CreateRecipe(S, "丧尸逆转疫苗", RecipeCategory.Consumable,
            GetItem("ZombieVaccine"), 1, 30f, 120f,
            new[] { M("VaccineSerum", 5), M("GeneSample", 3), M("ChemicalAgent", 5) },
            new[] { (SkillType.医疗生存, 10), (SkillType.智力, 9) });
    }

    // ================================================================
    // 机械加工台 Machining — 工业/电力/车辆 (18 recipes)
    // ================================================================

    static void CreateMachiningRecipes()
    {
        var S = WorkstationTier.Machining;

        // 工业设备套件（最高级重工业设备）
        CreateRecipe(S, "工业炉套件", RecipeCategory.Industry,
            GetItem("WS_IndustrialFurnace"), 1, 12f, 50f,
            new[] { M("SteelIngot", 8), M("StoneBrick", 15), M("AdvancedParts", 3), M("Coil", 2), M("Rebar", 3) },
            new[] { (SkillType.建造拆解, 5) });
        CreateRecipe(S, "电解槽套件", RecipeCategory.Industry,
            GetItem("WS_ElectrolysisTank"), 1, 15f, 55f,
            new[] { M("SteelIngot", 4), M("CopperIngot", 5), M("BatteryPack", 3), M("CircuitBoard", 2) },
            new[] { (SkillType.建造拆解, 5) });
        CreateRecipe(S, "发电机套件", RecipeCategory.Industry,
            GetItem("WS_ElectricGenerator"), 1, 15f, 55f,
            new[] { M("SteelIngot", 6), M("CopperIngot", 4), M("Coil", 3), M("AdvancedParts", 4), M("CircuitBoard", 1) },
            new[] { (SkillType.建造拆解, 5) });
        CreateRecipe(S, "太阳能板套件", RecipeCategory.Industry,
            GetItem("WS_SolarPanel"), 1, 18f, 60f,
            new[] { M("GlassPane", 8), M("CopperIngot", 6), M("CircuitBoard", 4), M("SteelIngot", 4) },
            new[] { (SkillType.建造拆解, 5) });
        CreateRecipe(S, "广播塔套件", RecipeCategory.Industry,
            GetItem("WS_RadioTower"), 1, 20f, 70f,
            new[] { M("SteelIngot", 10), M("CopperIngot", 8), M("CircuitBoard", 6), M("AdvancedParts", 4), M("Coil", 4) },
            new[] { (SkillType.建造拆解, 6) });

        // CreateRecipe(S, "5-56mm步枪弹", ...); // 已移入弹药装填机（工业设备）

        // 高级生产
        CreateRecipe(S, "高级零件批量", RecipeCategory.Material,
            GetItem("AdvancedParts"), 5, 4f, 30f,
            new[] { M("CommonParts", 5), M("SteelIngot", 2), M("SpringAssembly", 2) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "齿轮批量", RecipeCategory.Material,
            GetItem("Gear"), 5, 4f, 25f,
            new[] { M("SteelIngot", 3), M("CommonParts", 2) });
        CreateRecipe(S, "轴承批量", RecipeCategory.Material,
            GetItem("Bearing"), 5, 4f, 28f,
            new[] { M("SteelIngot", 3), M("AdvancedParts", 1) });

        // 车辆配件
        CreateRecipe(S, "线圈", RecipeCategory.Material,
            GetItem("Coil"), 2, 4f, 25f,
            new[] { M("CopperIngot", 2), M("Wire", 5), M("Rubber", 2) },
            new[] { (SkillType.建造拆解, 4) });

        // 高级设备
        CreateRecipe(S, "夜视仪", RecipeCategory.Tool,
            GetItem("NightGoggles"), 1, 10f, 50f,
            new[] { M("Electronic", 3), M("CircuitBoard", 2), M("BatteryPack", 1), M("GlassPane", 1) },
            new[] { (SkillType.建造拆解, 5) });
        CreateRecipe(S, "防毒面具", RecipeCategory.Armor,
            GetItem("GasMask"), 1, 6f, 30f,
            new[] { M("Rubber", 3), M("GlassPane", 1), M("ChemicalAgent", 1) },
            new[] { (SkillType.建造拆解, 3) });

        // 工业零件
        CreateRecipe(S, "弹簧组件批量", RecipeCategory.Material,
            GetItem("SpringAssembly"), 5, 4f, 22f,
            new[] { M("Spring", 8), M("SteelIngot", 2) });
        CreateRecipe(S, "钢管批量", RecipeCategory.Building,
            GetItem("SteelPipe"), 5, 4f, 22f,
            new[] { M("SteelIngot", 3) });

        CreateRecipe(S, "钢筋批量", RecipeCategory.Building,
            GetItem("Rebar"), 8, 4f, 22f,
            new[] { M("SteelIngot", 3) });

        // 高级工作台套件（自举）
        CreateRecipe(S, "高级工作台套件", RecipeCategory.Tool,
            GetItem("WS_AdvancedBench"), 1, 12f, 45f,
            new[] { M("SteelIngot", 5), M("WoodPlank", 6), M("AdvancedParts", 3), M("CircuitBoard", 1) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "研究中心套件", RecipeCategory.Tool,
            GetItem("WS_Chemistry"), 1, 12f, 45f,
            new[] { M("SteelIngot", 4), M("GlassPane", 4), M("CopperIngot", 3), M("AdvancedParts", 2) },
            new[] { (SkillType.建造拆解, 4) });
        CreateRecipe(S, "机械加工台套件", RecipeCategory.Tool,
            GetItem("WS_Machining"), 1, 15f, 55f,
            new[] { M("SteelIngot", 8), M("AdvancedParts", 5), M("Gear", 4), M("CircuitBoard", 3) },
            new[] { (SkillType.建造拆解, 5) });

        CreateRecipe(S, "中级工作台套件", RecipeCategory.Tool,
            GetItem("WS_MediumBench"), 1, 8f, 30f,
            new[] { M("IronIngot", 4), M("WoodPlank", 5), M("CommonParts", 3) },
            new[] { (SkillType.建造拆解, 3) });

        // 终局套件（机械加工台）
        CreateRecipe(S, "离心机套件", RecipeCategory.Industry,
            GetItem("WS_Centrifuge"), 1, 16f, 60f,
            new[] { M("SteelIngot", 6), M("Coil", 3), M("AdvancedParts", 2), M("CircuitBoard", 1) },
            new[] { (SkillType.建造拆解, 7) });
        CreateRecipe(S, "移动要塞基地车套件", RecipeCategory.Industry,
            GetItem("WS_MobileFortress"), 1, 30f, 100f,
            new[] { M("SteelIngot", 15), M("SteelPipe", 8), M("AdvancedParts", 6), M("Gear", 4), M("Bearing", 4), M("Rebar", 4) },
            new[] { (SkillType.建造拆解, 9) });
        CreateRecipe(S, "电子装配台套件", RecipeCategory.Tool,
            GetItem("WS_ElectronicsAssembly"), 1, 20f, 65f,
            new[] { M("SteelIngot", 5), M("CircuitBoard", 3), M("Coil", 2), M("AdvancedParts", 4) },
            new[] { (SkillType.建造拆解, 8), (SkillType.智力, 8) });

        // 终局材料（机械加工台）
        CreateRecipe(S, "碳纤维", RecipeCategory.Material,
            GetItem("CarbonFiber"), 2, 6f, 30f,
            new[] { M("Rubber", 3), M("ChemicalAgent", 1) },
            new[] { (SkillType.建造拆解, 6) });
        CreateRecipe(S, "钛合金", RecipeCategory.Material,
            GetItem("TitaniumAlloy"), 2, 8f, 35f,
            new[] { M("AluminumIngot", 3), M("IronIngot", 2) },
            new[] { (SkillType.建造拆解, 7) });

        // 中期-终局武器 — 旧猎枪已删除（→ SKS 半自动，见 CreateWeaponsAndAmmo）
        CreateRecipe(S, "消音器", RecipeCategory.Weapon,
            GetItem("Suppressor"), 1, 6f, 25f,
            new[] { M("SteelPipe", 1), M("CarbonFiber", 1), M("Rubber", 1) },
            new[] { (SkillType.枪械专精, 5) });
        CreateRecipe(S, "半自动改造套件", RecipeCategory.Weapon,
            GetItem("SemiAutoKit"), 1, 8f, 35f,
            new[] { M("AdvancedParts", 3), M("SpringAssembly", 2), M("Coil", 1) },
            new[] { (SkillType.枪械专精, 6) });
        CreateRecipe(S, "全自动改造套件", RecipeCategory.Weapon,
            GetItem("FullAutoKit"), 1, 10f, 45f,
            new[] { M("AdvancedParts", 4), M("ServoMotor", 1), M("SteelIngot", 3) },
            new[] { (SkillType.枪械专精, 7) });
        // CreateRecipe(S, "重狙(.50)", ...); // 已移入武器组装台（工业设备）
        CreateRecipe(S, "榴弹发射器", RecipeCategory.Weapon,
            GetItem("GrenadeLauncher"), 1, 20f, 75f,
            new[] { M("TitaniumAlloy", 6), M("ServoMotor", 2), M("SpringAssembly", 4), M("SteelPipe", 3) },
            new[] { (SkillType.枪械专精, 9) });

        // 核电站套件
        CreateRecipe(S, "核电站套件", RecipeCategory.Industry,
            GetItem("WS_NuclearPlant"), 1, 40f, 150f,
            new[] { M("SmallReactor", 1), M("TitaniumAlloy", 10), M("SteelIngot", 20), M("SteelPipe", 12), M("CapacitorBank", 5), M("ChipSet", 3), M("Rebar", 5) },
            new[] { (SkillType.建造拆解, 10), (SkillType.智力, 9) });
    }

    // ================================================================
    // 电子装配台 ElectronicsAssembly — 电子/智能/终局 (16 recipes)
    // ================================================================

    static void CreateElectronicsAssemblyRecipes()
    {
        var S = WorkstationTier.ElectronicsAssembly;

        // 电子材料
        CreateRecipe(S, "电容组", RecipeCategory.Material,
            GetItem("CapacitorBank"), 1, 5f, 25f,
            new[] { M("BatteryPack", 2), M("CopperIngot", 3) },
            new[] { (SkillType.智力, 5) });
        CreateRecipe(S, "伺服电机", RecipeCategory.Material,
            GetItem("ServoMotor"), 1, 6f, 30f,
            new[] { M("Coil", 2), M("SteelIngot", 1), M("CapacitorBank", 1) },
            new[] { (SkillType.智力, 6) });
        CreateRecipe(S, "光学透镜", RecipeCategory.Material,
            GetItem("OpticalLens"), 1, 4f, 20f,
            new[] { M("GlassPane", 3), M("AdvancedParts", 1) },
            new[] { (SkillType.智力, 5) });

        // 芯片组（需精密装配台）
        CreateRecipe(S, "芯片组", RecipeCategory.Material,
            GetItem("ChipSet"), 1, 10f, 40f,
            new[] { M("CircuitBoard", 3), M("AdvancedParts", 2) },
            new[] { (SkillType.智力, 7) });

        // 终局设备套件
        CreateRecipe(S, "基因分析台套件", RecipeCategory.Industry,
            GetItem("WS_GeneAnalyzer"), 1, 22f, 75f,
            new[] { M("SteelIngot", 4), M("GlassPane", 4), M("CircuitBoard", 3), M("VaccineSerum", 1) },
            new[] { (SkillType.建造拆解, 9), (SkillType.医疗生存, 9) });
        CreateRecipe(S, "精密装配台套件", RecipeCategory.Industry,
            GetItem("WS_PrecisionAssembly"), 1, 18f, 65f,
            new[] { M("SteelIngot", 6), M("ChipSet", 1), M("ServoMotor", 2), M("CircuitBoard", 3) },
            new[] { (SkillType.建造拆解, 8), (SkillType.智力, 8) });
        CreateRecipe(S, "净水厂套件", RecipeCategory.Industry,
            GetItem("WS_WaterPurifier"), 1, 20f, 70f,
            new[] { M("SteelPipe", 6), M("CapacitorBank", 2), M("SteelIngot", 4), M("CopperIngot", 4), M("Rebar", 3) },
            new[] { (SkillType.建造拆解, 9), (SkillType.智力, 7) });
        CreateRecipe(S, "自动炮塔套件", RecipeCategory.Industry,
            GetItem("WS_AutoTurret"), 1, 25f, 85f,
            new[] { M("TitaniumAlloy", 5), M("ServoMotor", 3), M("ChipSet", 1), M("OpticalLens", 1) },
            new[] { (SkillType.建造拆解, 10) });
        CreateRecipe(S, "电磁围栏套件", RecipeCategory.Industry,
            GetItem("WS_EMFence"), 1, 20f, 75f,
            new[] { M("TitaniumAlloy", 4), M("CapacitorBank", 5), M("Coil", 8) },
            new[] { (SkillType.建造拆解, 10) });
        CreateRecipe(S, "无人机平台套件", RecipeCategory.Industry,
            GetItem("WS_DronePlatform"), 1, 25f, 85f,
            new[] { M("CarbonFiber", 4), M("ServoMotor", 3), M("OpticalLens", 2), M("ChipSet", 1) },
            new[] { (SkillType.建造拆解, 10), (SkillType.智力, 8) });


        // 电子/智能设备
        CreateRecipe(S, "气象预测仪", RecipeCategory.Tool,
            GetItem("Meteorologist"), 1, 10f, 45f,
            new[] { M("ChipSet", 1), M("CapacitorBank", 1), M("Coil", 1), M("GlassPane", 2) },
            new[] { (SkillType.智力, 6) });
        CreateRecipe(S, "密码破译器", RecipeCategory.Tool,
            GetItem("Decryptor"), 1, 12f, 50f,
            new[] { M("ChipSet", 2), M("CircuitBoard", 4), M("BatteryPack", 2) },
            new[] { (SkillType.智力, 7) });
        CreateRecipe(S, "自动化编程台套件", RecipeCategory.Tool,
            GetItem("AutomationController"), 1, 15f, 60f,
            new[] { M("ChipSet", 3), M("ServoMotor", 2), M("CircuitBoard", 4) },
            new[] { (SkillType.智力, 8) });
        CreateRecipe(S, "卫星接收站套件", RecipeCategory.Tool,
            GetItem("SatelliteDish"), 1, 20f, 75f,
            new[] { M("ChipSet", 4), M("ServoMotor", 3), M("CapacitorBank", 4), M("SteelPipe", 6) },
            new[] { (SkillType.智力, 9), (SkillType.建造拆解, 8) });
        CreateRecipe(S, "AI核心", RecipeCategory.Tool,
            GetItem("AICore"), 1, 25f, 100f,
            new[] { M("ChipSet", 5), M("ServoMotor", 3), M("CapacitorBank", 4), M("OpticalLens", 2), M("TitaniumAlloy", 4) },
            new[] { (SkillType.智力, 10) });

        // 终局武器
        CreateRecipe(S, "电磁步枪", RecipeCategory.Weapon,
            GetItem("Railgun"), 1, 20f, 80f,
            new[] { M("TitaniumAlloy", 4), M("CapacitorBank", 3), M("Coil", 4), M("ChipSet", 1) },
            new[] { (SkillType.枪械专精, 10), (SkillType.智力, 7) });

        // 小型反应堆
        CreateRecipe(S, "小型反应堆", RecipeCategory.Tool,
            GetItem("SmallReactor"), 1, 30f, 100f,
            new[] { M("EnrichedUranium", 2), M("TitaniumAlloy", 4), M("CapacitorBank", 4), M("ChipSet", 2), M("LeadIngot", 5) },
            new[] { (SkillType.智力, 9), (SkillType.建造拆解, 8) });
    }

    // ================================================================
    // 元素合成炉 ElementFurnace — 终局能源材料 (1 recipe)
    // ================================================================

    static void CreateElementFurnaceRecipes()
    {
        var S = WorkstationTier.ElementFurnace;

        // 浓缩铀批量生产
        CreateRecipe(S, "浓缩铀批量", RecipeCategory.Material,
            GetItem("EnrichedUranium"), 3, 20f, 60f,
            new[] { M("UraniumOre", 6), M("SulfuricAcid", 2), M("RefinedCoal", 3) },
            new[] { (SkillType.智力, 7) });
    }

    // ================================================================
    // 更新 RecipeCatalog
    // ================================================================

    static void UpdateCatalog()
    {
        const string catalogPath = "Assets/_Game/Config/RecipeCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<RecipeCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<RecipeCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }

        var guids = AssetDatabase.FindAssets("t:RecipeData", new[] { RecipeBase });
        var all = new RecipeData[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            all[i] = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
        }

        System.Array.Sort(all, (a, b) =>
        {
            int cmp = a.requiredStation.CompareTo(b.requiredStation);
            if (cmp != 0) return cmp;
            return string.Compare(a.recipeName, b.recipeName, System.StringComparison.Ordinal);
        });

        catalog.recipes = all;
        EditorUtility.SetDirty(catalog);
        Debug.Log($"[CreateCraftingRecipes] RecipeCatalog 已更新，共 {all.Length} 个配方");
    }
}
