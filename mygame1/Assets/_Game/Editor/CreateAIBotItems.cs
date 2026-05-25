using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键生成 AI 机器人驾驶系统所需的物品和合成配方。
/// 菜单栏 → Game Tools → Create AIBot Items & Recipes
/// </summary>
public static class CreateAIBotItems
{
    const string ItemBase = "Assets/_Game/Config/Items";
    const string RecipeBase = "Assets/_Game/Config/Recipes";

    static Dictionary<string, ItemData> _itemLookup;

    [MenuItem("Game Tools/Create AIBot Items & Recipes")]
    public static void CreateAll()
    {
        BuildItemLookup();
        EnsureDirs();

        CreateAmmoItems();
        CreateWeaponItems();
        CreateAmmoRecipes();
        CreateWeaponRecipes();

        // 创建武器预制体
        CreateWeaponPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("AIBot 武器/弹药物品、合成配方、预制体已全部创建！");
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
        if (_itemLookup.TryGetValue(name, out var item)) return item;
        Debug.LogWarning($"[CreateAIBotItems] 物品不存在: {name}");
        return null;
    }

    static void EnsureDirs()
    {
        string[] dirs = { "Ammo", "Equipment" };
        foreach (var d in dirs)
        {
            string path = $"{ItemBase}/{d}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(ItemBase, d);
        }

        string[] recipeDirs = { "简易工作台", "机械加工台", "电子装配台" };
        foreach (var d in recipeDirs)
        {
            string path = $"{RecipeBase}/{d}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(RecipeBase, d);
        }
    }

    // ================================================================
    // 物品创建
    // ================================================================

    static ItemData CreateItem(string folder, string assetName, string displayName,
        ItemCategory cat, int w = 1, int h = 1, float weight = 0.1f, int stack = 30,
        ItemQuality quality = ItemQuality.Scavenged)
    {
        string path = $"{ItemBase}/{folder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (existing != null)
        {
            existing.itemName = displayName;
            existing.category = cat;
            existing.gridWidth = w;
            existing.gridHeight = h;
            existing.weight = weight;
            existing.maxStack = stack;
            existing.quality = quality;
            EditorUtility.SetDirty(existing);
            _itemLookup[displayName] = existing;
            return existing;
        }

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemName = displayName;
        item.category = cat;
        item.gridWidth = w;
        item.gridHeight = h;
        item.weight = weight;
        item.maxStack = stack;
        item.quality = quality;
        AssetDatabase.CreateAsset(item, path);
        _itemLookup[displayName] = item;
        return item;
    }

    static void CreateAmmoItems()
    {
        const string F = "Ammo";
        const ItemCategory C = ItemCategory.Ammo;

        CreateItem(F, "Ammo_Pistol", "手枪子弹", C, 1, 1, 0.02f, 50, ItemQuality.Professional);
        CreateItem(F, "Ammo_Rifle", "步枪子弹", C, 1, 1, 0.04f, 40, ItemQuality.Professional);
        CreateItem(F, "Ammo_ShotgunShell", "霰弹", C, 1, 1, 0.05f, 20, ItemQuality.Professional);
    }

    static void CreateWeaponItems()
    {
        const string F = "Equipment";
        const ItemCategory C = ItemCategory.Equipment;

        // 盾牌
        var shield = CreateItem(F, "Shield", "盾牌", C, 1, 2, 3f, 1, ItemQuality.Professional);
        shield.equipSlot = EquipSlot.LeftHand;
        shield.armorValue = 12f;
        shield.hasDurability = true;
        shield.maxDurability = 150f;
        EditorUtility.SetDirty(shield);

        // 电锯
        var chainsaw = CreateItem(F, "Chainsaw", "电锯", C, 1, 3, 4f, 1, ItemQuality.Professional);
        chainsaw.equipSlot = EquipSlot.RightHand;
        chainsaw.isFirearm = false;
        chainsaw.weaponDamage = 15f;
        chainsaw.weaponRange = 3f;
        chainsaw.range = 3f;
        chainsaw.fireRate = 0.1f;
        chainsaw.hasDurability = true;
        chainsaw.maxDurability = 120f;
        EditorUtility.SetDirty(chainsaw);
    }

    // ================================================================
    // 配方创建
    // ================================================================

    static RecipeData CreateRecipe(WorkstationTier station, string recipeName,
        RecipeCategory category, ItemData result, int resultCount = 1,
        float craftTime = 2f, float xp = 15f,
        (string itemName, int count)[] materials = null)
    {
        string dir = $"{RecipeBase}/{StationName(station)}";
        string path = $"{dir}/{recipeName}.asset";

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

    static string StationName(WorkstationTier tier)
    {
        switch (tier)
        {
            case WorkstationTier.SimpleBench: return "简易工作台";
            case WorkstationTier.Machining: return "机械加工台";
            case WorkstationTier.ElectronicsAssembly: return "电子装配台";
            default: return "简易工作台";
        }
    }

    // ================================================================
    // 弹药配方
    // ================================================================

    static void CreateAmmoRecipes()
    {
        // 手枪子弹 — 简易工作台
        CreateRecipe(WorkstationTier.SimpleBench, "手枪子弹",
            RecipeCategory.Ammo, GetItem("手枪子弹"), 10, 3f, 20f,
            new[] { ("弹壳", 10), ("底火", 10), ("弹头", 10), ("黑火药", 1) });

        // 步枪子弹 — 简易工作台
        CreateRecipe(WorkstationTier.SimpleBench, "步枪子弹",
            RecipeCategory.Ammo, GetItem("步枪子弹"), 10, 4f, 25f,
            new[] { ("弹壳", 10), ("底火", 10), ("弹头", 10), ("无烟火药", 1) });

        // 霰弹 — 简易工作台
        CreateRecipe(WorkstationTier.SimpleBench, "霰弹",
            RecipeCategory.Ammo, GetItem("霰弹"), 8, 4f, 25f,
            new[] { ("弹壳", 8), ("底火", 8), ("弹头", 8), ("黑火药", 1) });
    }

    // ================================================================
    // 武器配方
    // ================================================================

    static void CreateWeaponRecipes()
    {
        // 盾牌 — 机械加工台
        CreateRecipe(WorkstationTier.Machining, "盾牌",
            RecipeCategory.Weapon, GetItem("盾牌"), 1, 8f, 80f,
            new[] { ("钢锭", 3), ("铁锭", 2), ("高级零件", 1), ("皮革", 1) });

        // 电锯 — 机械加工台
        CreateRecipe(WorkstationTier.Machining, "电锯",
            RecipeCategory.Weapon, GetItem("电锯"), 1, 12f, 120f,
            new[] { ("钢锭", 4), ("电路板", 2), ("电池组", 2), ("伺服电机", 1), ("齿轮", 2) });
    }

    // ================================================================
    // 武器预制体（占位模型）
    // ================================================================

    static void CreateWeaponPrefabs()
    {
        const string prefabDir = "Assets/_Game/Config/Models/Weapons";
        if (!AssetDatabase.IsValidFolder(prefabDir))
            AssetDatabase.CreateFolder("Assets/_Game/Config/Models", "Weapons");

        CreatePrimitivePrefab($"{prefabDir}/Chainsaw.prefab", PrimitiveType.Cylinder,
            new Vector3(0.15f, 0.8f, 0.15f), "电锯");
        CreatePrimitivePrefab($"{prefabDir}/Shield.prefab", PrimitiveType.Cube,
            new Vector3(0.6f, 0.8f, 0.1f), "盾牌");

        // 将预制体引用写入 ItemData
        AssignPrefabToItem("电锯", $"{prefabDir}/Chainsaw.prefab");
        AssignPrefabToItem("盾牌", $"{prefabDir}/Shield.prefab");
    }

    static void CreatePrimitivePrefab(string path, PrimitiveType type, Vector3 scale, string name)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.localScale = scale;
        // 移除碰撞体（武器由武器系统管理）
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void AssignPrefabToItem(string itemName, string prefabPath)
    {
        var item = GetItem(itemName);
        if (item == null) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        item.worldPrefab = prefab;
        EditorUtility.SetDirty(item);
    }
}
