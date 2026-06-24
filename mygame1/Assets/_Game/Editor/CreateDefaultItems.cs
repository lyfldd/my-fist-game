using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 编辑器工具：一键创建所有默认装备物品
/// 用法：Unity 菜单 → Tools → Create Default Items
/// </summary>
public class CreateDefaultItems
{
    [MenuItem("Tools/Create Default Items")]
    public static void CreateAll()
    {
        CreateEquipment();
        CreateConsumables();
        CreateHelmets();
        CreateWeapons();
        CreateMaterials();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Update Military Gear Sizes")]
    public static void UpdateMilitaryGear()
    {
        // 强制更新军用装备的容器尺寸
        UpdateStorage("TacticalJacket", 2, 4);
        UpdateStorage("TacticalPants", 2, 4);
        UpdateStorage("TacticalBelt", 2, 2);
        UpdateStorage("TacticalVest", 4, 4);
        UpdateStorage("MilitaryBag", 6, 5);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UpdateGridSizes();
    }

    static void UpdateGridSizes()
    {
        SetGridSize("TShirt", 2, 2);
        SetGridSize("TacticalJacket", 2, 2);
        SetGridSize("Jeans", 2, 2);
        SetGridSize("TacticalPants", 2, 2);
        SetGridSize("TacticalBelt", 2, 2);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Update Item Sizes (2x2)")]
    public static void UpdateItemSizesMenu()
    {
        UpdateGridSizes();
    }

    [MenuItem("Tools/Update Consumable Data")]
    public static void UpdateConsumableData()
    {
        // 强制更新消耗品的使用数据（useTime + itemEffects）
        SetConsumable("CanFood", 3f, new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 25, isInstant = true } });
        SetConsumable("Water", 2f, new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreThirst, value = 30, isInstant = true } });
        SetConsumable("Bandage", 4f, new List<ItemEffect> {
            new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 15, isInstant = true },
            new ItemEffect { effectType = ItemEffectType.CureBleeding, value = 0, isInstant = true } });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Update Armor Values")]
    public static void UpdateArmorValues()
    {
        // 设置装备护甲值
        SetArmor("TShirt", 1f);
        SetArmor("TacticalJacket", 8f);
        SetArmor("Jeans", 1f);
        SetArmor("TacticalPants", 6f);
        SetArmor("LeatherBelt", 0.5f);
        SetArmor("TacticalBelt", 3f);
        SetArmor("LightVest", 12f);
        SetArmor("TacticalVest", 25f);
        SetArmor("SmallBag", 2f);
        SetArmor("MilitaryBag", 5f);
        SetArmor("BaseballCap", 3f);
        SetArmor("CombatHelmet", 15f);
        SetArmor("MotorbikeHelmet", 10f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Update Clothing Warmth")]
    public static void UpdateClothingWarmth()
    {
        // 设置衣物的保暖值（越大越保暖）
        SetWarmth("TShirt", 0.5f);
        SetWarmth("TacticalJacket", 2f);
        SetWarmth("Jeans", 0.5f);
        SetWarmth("TacticalPants", 1.5f);
        SetWarmth("LeatherBelt", 0.2f);
        SetWarmth("TacticalBelt", 0.5f);
        SetWarmth("LightVest", 1f);
        SetWarmth("TacticalVest", 2.5f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void SetWarmth(string fileName, float warmth)
    {
        string[] folders = { "Tops", "Pants", "Belts", "Vests", "Backpacks", "Helmets" };
        foreach (var f in folders)
        {
            string path = $"Assets/_Game/Config/Equipment/{f}/{fileName}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
            {
                item.warmthValue = warmth;
                EditorUtility.SetDirty(item);
                return;
            }
        }
    }

    static void SetArmor(string fileName, float armor)
    {
        string[] folders = { "Tops", "Pants", "Belts", "Vests", "Backpacks", "Helmets" };
        foreach (var f in folders)
        {
            string path = $"Assets/_Game/Config/Equipment/{f}/{fileName}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
            {
                item.armorValue = armor;
                EditorUtility.SetDirty(item);
                return;
            }
        }
    }

    static void UpdateStorage(string fileName, int newW, int newH)
    {
        string[] paths = new string[]
        {
            $"Assets/_Game/Config/Equipment/Tops/{fileName}.asset",
            $"Assets/_Game/Config/Equipment/Pants/{fileName}.asset",
            $"Assets/_Game/Config/Equipment/Belts/{fileName}.asset",
            $"Assets/_Game/Config/Equipment/Vests/{fileName}.asset",
            $"Assets/_Game/Config/Equipment/Backpacks/{fileName}.asset",
        };

        foreach (var path in paths)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
            {
                item.storageWidth = newW;
                item.storageHeight = newH;
                EditorUtility.SetDirty(item);
            }
        }
    }

    static void SetGridSize(string fileName, int w, int h)
    {
        string[] folders = { "Equipment/Belts", "Equipment/Pants", "Equipment/Tops", "Equipment/Vests", "Equipment/Backpacks", "Equipment/Headwear" };
        foreach (var f in folders)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/_Game/Config/{f}/{fileName}.asset");
            if (item != null)
            {
                item.gridWidth = w;
                item.gridHeight = h;
                EditorUtility.SetDirty(item);
                return;
            }
        }
    }

    static void SetConsumable(string fileName, float useTime, List<ItemEffect> effects)
    {
        foreach (var folder in new[] { "Consumables" })
        {
            string path = $"Assets/_Game/Config/{folder}/{fileName}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
            {
                item.useTime = useTime;
                item.itemEffects = effects;
                EditorUtility.SetDirty(item);
            }
        }
    }

    static void CreateEquipment()
    {
        // 上衣 (民用 / 军用)
        CreateItem("T恤", "TShirt", 1, 1, 0.3f, ItemCategory.Equipment, EquipSlot.Tops, 2, 1);
        CreateItem("战术夹克", "TacticalJacket", 1, 1, 0.8f, ItemCategory.Equipment, EquipSlot.Tops, 2, 4);

        // 裤子 (民用 / 军用)
        CreateItem("牛仔裤", "Jeans", 1, 1, 0.5f, ItemCategory.Equipment, EquipSlot.Pants, 2, 2);
        CreateItem("战术裤", "TacticalPants", 1, 1, 0.8f, ItemCategory.Equipment, EquipSlot.Pants, 2, 4);

        // 腰带 (民用 / 军用)
        CreateItem("皮带", "LeatherBelt", 1, 1, 0.3f, ItemCategory.Equipment, EquipSlot.Belt, 1, 1);
        CreateItem("战术腰封", "TacticalBelt", 1, 1, 0.8f, ItemCategory.Equipment, EquipSlot.Belt, 2, 2);

        // 胸挂 (民用 / 军用)
        CreateItem("轻量背心", "LightVest", 2, 2, 1.5f, ItemCategory.Equipment, EquipSlot.Vest, 2, 2);
        CreateItem("战术背心", "TacticalVest", 2, 2, 2.5f, ItemCategory.Equipment, EquipSlot.Vest, 4, 4);

        // 背包 (民用 / 军用)
        CreateItem("小背包", "SmallBag", 2, 2, 1.0f, ItemCategory.Equipment, EquipSlot.Backpack, 4, 3);
        CreateItem("军用背包", "MilitaryBag", 2, 2, 1.8f, ItemCategory.Equipment, EquipSlot.Backpack, 6, 5);
    }

    static void CreateHelmets()
    {
        // 头盔（无容器，仅护甲）
        CreateItem("棒球帽", "BaseballCap", 1, 1, 0.2f, ItemCategory.Equipment, EquipSlot.Head, 0, 0);
        CreateItem("防弹头盔", "CombatHelmet", 1, 1, 1.2f, ItemCategory.Equipment, EquipSlot.Head, 0, 0);
        CreateItem("摩托车头盔", "MotorbikeHelmet", 1, 1, 1.5f, ItemCategory.Equipment, EquipSlot.Head, 0, 0);
    }

    static void CreateConsumables()
    {
        CreateItem("绷带", "Bandage", 1, 1, 0.2f, ItemCategory.Consumable, EquipSlot.None, 0, 0, 5,
            useTime: 4f, effects: new List<ItemEffect> {
                new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 15, isInstant = true },
                new ItemEffect { effectType = ItemEffectType.CureBleeding, value = 0, isInstant = true } });
        CreateItem("罐头", "CanFood", 1, 1, 0.5f, ItemCategory.Consumable, EquipSlot.None, 0, 0, 10,
            useTime: 3f, effects: new List<ItemEffect> {
                new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 25, isInstant = true } });
        CreateItem("饮用水", "Water", 1, 1, 0.6f, ItemCategory.Consumable, EquipSlot.None, 0, 0, 5,
            useTime: 2f, effects: new List<ItemEffect> {
                new ItemEffect { effectType = ItemEffectType.RestoreThirst, value = 30, isInstant = true } });
        CreateItem("止痛药", "Painkiller", 1, 1, 0.1f, ItemCategory.Consumable, EquipSlot.None, 0, 0, 10,
            useTime: 2f, effects: new List<ItemEffect> {
                new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 8, isInstant = true } });
        CreateItem("抗生素", "Antibiotics", 1, 1, 0.1f, ItemCategory.Consumable, EquipSlot.None, 0, 0, 5,
            useTime: 3f, effects: new List<ItemEffect> {
                new ItemEffect { effectType = ItemEffectType.CureInfected, value = 0, isInstant = true } });
        CreateItem("夹板", "Splint", 1, 1, 0.3f, ItemCategory.Consumable, EquipSlot.None, 0, 0, 3,
            useTime: 5f, effects: new List<ItemEffect> {
                new ItemEffect { effectType = ItemEffectType.FixFracture, value = 0, isInstant = true } });
        CreateItem("能量棒", "EnergyBar", 1, 1, 0.2f, ItemCategory.Consumable, EquipSlot.None, 0, 0, 8,
            useTime: 2f, effects: new List<ItemEffect> {
                new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 15, isInstant = true },
                new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 3, isInstant = true } });
        CreateItem("咖啡", "Coffee", 1, 1, 0.3f, ItemCategory.Consumable, EquipSlot.None, 0, 0, 6,
            useTime: 3f, effects: new List<ItemEffect> {
                new ItemEffect { effectType = ItemEffectType.TemporaryWarmth, value = 0, duration = 60, isInstant = false },
                new ItemEffect { effectType = ItemEffectType.RestoreThirst, value = 5, isInstant = true } });
        CreateItem("威士忌", "Whiskey", 1, 1, 0.5f, ItemCategory.Consumable, EquipSlot.None, 0, 0, 3,
            useTime: 2f, effects: new List<ItemEffect> {
                new ItemEffect { effectType = ItemEffectType.RestoreHealth, value = 5, isInstant = true },
                new ItemEffect { effectType = ItemEffectType.TemporaryWarmth, value = 0, duration = 30, isInstant = false } });
    }

    static void CreateWeapons()
    {
        CreateItem("木棍", "WoodenStick", 1, 2, 2f, ItemCategory.Equipment, EquipSlot.RightHand, 0, 0);
        CreateItem("长刀", "LongSword", 1, 3, 3.5f, ItemCategory.Equipment, EquipSlot.RightHand, 0, 0);
        // 沙漠之鹰 → M1911 / 改装AK47 → AK-47（见 CreateWeaponsAndAmmo）
    }

    [MenuItem("Tools/Update Weapon Data")]
    public static void UpdateWeaponData()
    {
        // 设置武器的基础属性
        SetWeaponProps("WoodenStick",  isFirearm: false, damage: 8,  range: 2,  fireRate: 0.8f);
        SetWeaponProps("LongSword",    isFirearm: false, damage: 20, range: 2.5f, fireRate: 0.6f);
        SetWeaponProps("DesertEagle",  isFirearm: true,  damage: 25, range: 50,  fireRate: 0.4f,
            baseSpread: 2, maxSpread: 15, spreadRecovery: 8, spreadPerShot: 1.5f);
        SetWeaponProps("AK47",         isFirearm: true,  damage: 20, range: 60,  fireRate: 0.12f,
            baseSpread: 3, maxSpread: 20, spreadRecovery: 6, spreadPerShot: 2f);

        // 更新 equipSlot（已存在的物品可能还是 None）
        SetEquipSlot("WoodenStick", EquipSlot.RightHand);
        SetEquipSlot("LongSword", EquipSlot.RightHand);
        SetEquipSlot("DesertEagle", EquipSlot.SidearmBelt);
        SetEquipSlot("AK47", EquipSlot.RightHand);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void SetEquipSlot(string fileName, EquipSlot slot)
    {
        string path = $"Assets/_Game/Config/Weapons/{fileName}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item != null)
        {
            item.equipSlot = slot;
            EditorUtility.SetDirty(item);
        }
    }

    static void SetWeaponProps(string fileName, bool isFirearm, float damage, float range,
        float fireRate, float baseSpread = 2f, float maxSpread = 15f, float spreadRecovery = 8f,
        float spreadPerShot = 1.5f, int magazineSize = 0)
    {
        string path = $"Assets/_Game/Config/Weapons/{fileName}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null) { return; }

        item.isFirearm = isFirearm;
        item.weaponDamage = damage;
        item.weaponRange = range;
        item.range = range;              // 别名同步
        item.fireRate = fireRate;
        item.baseSpread = baseSpread;
        item.maxSpread = maxSpread;
        item.spreadRecovery = spreadRecovery;
        item.spreadPerShot = spreadPerShot;
        item.magazineSize = magazineSize;

        EditorUtility.SetDirty(item);
    }

    static void CreateMaterials()
    {
        CreateItem("木头", "Wood", 1, 1, 0.5f, ItemCategory.RawMaterial, EquipSlot.None, 0, 0, 20);
        CreateItem("破布", "Cloth", 1, 1, 0.2f, ItemCategory.RawMaterial, EquipSlot.None, 0, 0, 10);
        CreateItem("铁钉", "Nails", 1, 1, 0.1f, ItemCategory.RawMaterial, EquipSlot.None, 0, 0, 30);
    }

    static void CreateItem(string displayName, string fileName, int gridW, int gridH,
        float weight, ItemCategory category, EquipSlot slot, int storageW, int storageH,
        int maxStack = 1, float useTime = 0f, List<ItemEffect> effects = null)
    {
        string folder = GetCategoryFolder(category, slot);

        // 装备类物品放进对应子文件夹
        string subFolder = GetEquipSubfolder(slot);
        if (!string.IsNullOrEmpty(subFolder))
            folder = $"Equipment/{subFolder}";

        string path = $"Assets/_Game/Config/{folder}/{fileName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (existing != null)
        {
            return;
        }

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemName = displayName;
        item.gridWidth = gridW;
        item.gridHeight = gridH;
        item.weight = weight;
        item.category = category;
        item.maxStack = maxStack;
        item.equipSlot = slot;
        item.storageWidth = storageW;
        item.storageHeight = storageH;
        item.useTime = useTime;
        item.itemEffects = effects ?? new List<ItemEffect>();

        AssetDatabase.CreateAsset(item, path);
    }

    static string GetCategoryFolder(ItemCategory category, EquipSlot slot = EquipSlot.None)
    {
        if (category == ItemCategory.Equipment)
        {
            bool isWeapon = slot is EquipSlot.RightHand or EquipSlot.LeftHand
                or EquipSlot.KnifeBelt or EquipSlot.SidearmBelt;
            return isWeapon ? "Weapons" : "Equipment";
        }
        return category switch
        {
            ItemCategory.RawMaterial => "Materials",
            ItemCategory.Consumable => "Consumables",
            _ => ""
        };
    }

    static string GetEquipSubfolder(EquipSlot slot)
    {
        return slot switch
        {
            EquipSlot.Tops => "Tops",
            EquipSlot.Pants => "Pants",
            EquipSlot.Belt => "Belts",
            EquipSlot.Vest => "Vests",
            EquipSlot.Backpack => "Backpacks",
            EquipSlot.Head => "Helmets",
            _ => ""
        };
    }
}
