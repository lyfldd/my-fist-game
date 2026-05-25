using UnityEditor;
using UnityEngine;
using _Game.Config;
using _Game.Systems.WorldContainer;

/// <summary>
/// 编辑器工具：创建世界容器（柜子）测试预制体
/// </summary>
public class SetupWorldContainer
{
    private const string PROFILE_PATH = "Assets/_Game/Config/ContainerProfiles/";
    private const string LOOT_PATH = "Assets/_Game/Config/LootTables/";

    [MenuItem("Tools/WorldContainer/Setup UI")]
    public static void SetupUI()
    {
        var canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("场景中没有 Canvas，请先打开总面板生成 Canvas");
            return;
        }

        var existing = canvas.GetComponentInChildren<ContainerWindowUI>();
        if (existing != null)
        {
            Debug.Log("ContainerWindowUI 已存在");
            return;
        }

        var go = new GameObject("ContainerWindowUI", typeof(ContainerWindowUI));
        go.transform.SetParent(canvas.transform, false);
        Debug.Log("已创建 ContainerWindowUI（容器子窗口）");
    }

    [MenuItem("Tools/WorldContainer/Create Cabinet Prefab")]
    public static void CreateCabinet()
    {
        string prefabPath = "Assets/_Game/Prefabs/Cabinet.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.Log("Cabinet.prefab 已存在，如需重建请手动删除");
            return;
        }

        // 确保目录存在
        EnsureDir(LOOT_PATH);
        EnsureDir(PROFILE_PATH);

        // 创建 LootTable（军火箱掉落表）
        string lootPath = LOOT_PATH + "ArmoryLoot.asset";
        var lootTable = AssetDatabase.LoadAssetAtPath<LootTable>(lootPath);
        if (lootTable == null)
        {
            lootTable = ScriptableObject.CreateInstance<LootTable>();
            AssetDatabase.CreateAsset(lootTable, lootPath);
        }

        // 创建 ContainerLootProfile（军火箱配置）
        string profilePath = PROFILE_PATH + "ArmoryProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<ContainerLootProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<ContainerLootProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        // 配置 Profile
        profile.displayName = "军火箱";
        profile.lootTable = lootTable;
        profile.gridWidth = 4;
        profile.gridHeight = 4;
        profile.searchTime = 2f;
        profile.emptyChance = 0.1f;
        profile.minLootTypes = 1;
        profile.maxLootTypes = 3;
        profile.containerTag = "CABINET";
        EditorUtility.SetDirty(profile);

        // 填充 LootTable（军装掉落）
        lootTable.entries = new System.Collections.Generic.List<LootEntry> {
            NewEntry("Assets/_Game/Config/Equipment/Tops/TacticalJacket.asset", 1f, 1, 1),
            NewEntry("Assets/_Game/Config/Equipment/Pants/TacticalPants.asset", 1f, 1, 1),
            NewEntry("Assets/_Game/Config/Equipment/Belt/TacticalBelt.asset", 1f, 1, 1),
            NewEntry("Assets/_Game/Config/Equipment/Vest/TacticalVest.asset", 1f, 1, 1),
        };
        EditorUtility.SetDirty(lootTable);

        AssetDatabase.SaveAssets();

        // 创建预制体
        var go = new GameObject("Cabinet");
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Body";
        box.transform.SetParent(go.transform);
        box.transform.localPosition = new Vector3(0, 0.5f, 0);
        box.transform.localScale = new Vector3(1f, 1f, 1f);

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 1f, 1f);
        col.center = new Vector3(0, 0.5f, 0);

        var wc = go.AddComponent<WorldContainer>();
        wc.profile = profile;

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
        Debug.Log("柜子预制体已创建: " + prefabPath);
    }

    private static LootEntry NewEntry(string itemPath, float weight, int minCount, int maxCount)
    {
        return new LootEntry
        {
            itemData = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath),
            weight = weight,
            minCount = minCount,
            maxCount = maxCount,
        };
    }

    private static void EnsureDir(string path)
    {
        string dir = path.TrimEnd('/');
        string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
        string name = System.IO.Path.GetFileName(dir);
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(parent, name);
    }
}
