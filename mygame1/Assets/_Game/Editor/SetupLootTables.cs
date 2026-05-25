using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键填充 4 个 LootTable 的物品条目
/// 用法：Unity 菜单 → Tools → Setup Loot Tables
/// </summary>
public class SetupLootTables
{
    [MenuItem("Tools/Setup Loot Tables")]
    public static void Setup()
    {
        SetupFridge();
        SetupCabinet();
        SetupCorpse();
        SetupCrate();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("4 个 LootTable 物品分配完成！冰箱5种 / 柜子10种 / 尸体13种 / 板条箱10种");
    }

    static void SetupFridge()
    {
        var table = LoadTable("FridgeLoot");
        if (table == null) return;
        table.entries.Clear();

        AddEntry(table, "Consumables/CanFood",     8f, 1, 3);
        AddEntry(table, "Consumables/Water",        5f, 1, 2);
        AddEntry(table, "Consumables/EnergyBar",    4f, 1, 2);
        AddEntry(table, "Consumables/Coffee",       3f, 1, 1);
        AddEntry(table, "Consumables/Whiskey",      1f, 1, 1);

        EditorUtility.SetDirty(table);
        Debug.Log("冰箱 (Fridge): 5种食物/饮品");
    }

    static void SetupCabinet()
    {
        var table = LoadTable("CabinetLoot");
        if (table == null) return;
        table.entries.Clear();

        AddEntry(table, "Materials/Cloth",          6f, 1, 3);
        AddEntry(table, "Consumables/Bandage",      5f, 1, 2);
        AddEntry(table, "Consumables/Painkiller",   4f, 1, 1);
        AddEntry(table, "Materials/Wood",           4f, 1, 3);
        AddEntry(table, "Materials/Nails",          3f, 1, 2);
        AddEntry(table, "Equipment/Tops/TShirt",    3f, 1, 1);
        AddEntry(table, "Equipment/Pants/Jeans",    2f, 1, 1);
        AddEntry(table, "Equipment/Belts/LeatherBelt", 2f, 1, 1);
        AddEntry(table, "Equipment/Helmets/BaseballCap", 2f, 1, 1);
        AddEntry(table, "Consumables/Antibiotics",  2f, 1, 1);

        EditorUtility.SetDirty(table);
        Debug.Log("柜子 (Cabinet): 10种日常杂物/医疗/衣物");
    }

    static void SetupCorpse()
    {
        var table = LoadTable("CorpseLoot");
        if (table == null) return;
        table.entries.Clear();

        AddEntry(table, "Consumables/Bandage",      5f, 1, 2);
        AddEntry(table, "Consumables/CanFood",      3f, 1, 1);
        AddEntry(table, "Consumables/Water",        3f, 1, 1);
        AddEntry(table, "Consumables/Painkiller",   3f, 1, 1);
        AddEntry(table, "Materials/Cloth",          3f, 1, 2);
        AddEntry(table, "Weapons/WoodenStick",      3f, 1, 1);
        AddEntry(table, "Equipment/Helmets/BaseballCap", 3f, 1, 1);
        AddEntry(table, "Equipment/Tops/TShirt",    2f, 1, 1);
        AddEntry(table, "Equipment/Belts/LeatherBelt", 2f, 1, 1);
        AddEntry(table, "Weapons/LongSword",        2f, 1, 1);
        AddEntry(table, "Consumables/Splint",       2f, 1, 1);
        AddEntry(table, "Weapons/DesertEagle",      1f, 1, 1);
        AddEntry(table, "Equipment/Backpacks/SmallBag", 1f, 1, 1);

        EditorUtility.SetDirty(table);
        Debug.Log("尸体 (Corpse): 13种随身物品/武器/医疗");
    }

    static void SetupCrate()
    {
        var table = LoadTable("CrateLoot");
        if (table == null) return;
        table.entries.Clear();

        AddEntry(table, "Materials/Wood",           8f, 2, 5);
        AddEntry(table, "Materials/Nails",          7f, 1, 4);
        AddEntry(table, "Materials/Cloth",          5f, 1, 3);
        AddEntry(table, "Consumables/Bandage",      3f, 1, 2);
        AddEntry(table, "Consumables/Painkiller",   3f, 1, 2);
        AddEntry(table, "Consumables/Splint",       3f, 1, 2);
        AddEntry(table, "Consumables/Antibiotics",  2f, 1, 1);
        AddEntry(table, "Consumables/EnergyBar",    2f, 1, 1);
        AddEntry(table, "Consumables/Whiskey",      1f, 1, 1);
        AddEntry(table, "Equipment/Vests/LightVest", 1f, 1, 1);

        EditorUtility.SetDirty(table);
        Debug.Log("板条箱 (Crate): 10种工具/建材/物资储备");
    }

    // ===== 工具方法 =====

    static LootTable LoadTable(string name)
    {
        string path = $"Assets/_Game/Config/LootTables/{name}.asset";
        var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
        if (table == null)
            Debug.LogError($"未找到 LootTable: {path}");
        return table;
    }

    static void AddEntry(LootTable table, string itemPath, float weight, int minCount, int maxCount)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/_Game/Config/{itemPath}.asset");
        if (item == null)
        {
            Debug.LogWarning($"未找到物品: Assets/_Game/Config/{itemPath}.asset");
            return;
        }

        table.entries.Add(new LootEntry
        {
            itemData = item,
            weight = weight,
            minCount = minCount,
            maxCount = maxCount
        });
    }
}
