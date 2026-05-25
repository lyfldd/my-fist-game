using UnityEditor;
using UnityEngine;

public static class CreateDefaultZombieData
{
    const string Dir = "Assets/_Game/Config/ZombieData";
    const string ProfileDir = "Assets/_Game/Config/ZombieData";

    [MenuItem("Game Tools/Create Default ZombieData (3 types + 默认Profile)")]
    private static void Create()
    {
        EnsureDirectory();

        CreateNormal();
        CreateFast();
        CreateFat();
        CreateDefaultProfile();
    }

    static void EnsureDirectory()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
        {
            string parent = "Assets/_Game/Config";
            AssetDatabase.CreateFolder(parent, "ZombieData");
        }
    }

    static void CreateNormal()
    {
        string path = $"{Dir}/普通僵尸.asset";
        if (AssetDatabase.LoadAssetAtPath<_Game.Config.ZombieData>(path) != null)
        {
            Debug.Log($"已存在，跳过: {path}");
            return;
        }
        var data = ScriptableObject.CreateInstance<_Game.Config.ZombieData>();
        data.zombieName = "普通僵尸";
        data.maxHealth = 100f;
        data.moveSpeed = 3f;
        data.attackDamage = 10;
        data.attackRange = 1.5f;
        data.attackCooldown = 1.5f;
        data.detectRange = 10f;
        data.loseRange = 30f;
        data.visionAngle = 90f;
        data.bodyRadius = 0.35f;
        data.bodyHeight = 1.8f;
        data.lootGroup = "common";
        AssetDatabase.CreateAsset(data, path);
        Debug.Log($"已创建: {path}");
    }

    static void CreateFast()
    {
        string path = $"{Dir}/快速僵尸.asset";
        if (AssetDatabase.LoadAssetAtPath<_Game.Config.ZombieData>(path) != null)
        {
            Debug.Log($"已存在，跳过: {path}");
            return;
        }
        var data = ScriptableObject.CreateInstance<_Game.Config.ZombieData>();
        data.zombieName = "快速僵尸";
        data.maxHealth = 50f;
        data.moveSpeed = 6f;
        data.attackDamage = 8;
        data.attackRange = 1.5f;
        data.attackCooldown = 1.0f;
        data.detectRange = 15f;
        data.loseRange = 40f;
        data.visionAngle = 120f;
        data.bodyRadius = 0.25f;
        data.bodyHeight = 1.6f;
        data.lootGroup = "common";
        AssetDatabase.CreateAsset(data, path);
        Debug.Log($"已创建: {path}");
    }

    static void CreateFat()
    {
        string path = $"{Dir}/胖僵尸.asset";
        if (AssetDatabase.LoadAssetAtPath<_Game.Config.ZombieData>(path) != null)
        {
            Debug.Log($"已存在，跳过: {path}");
            return;
        }
        var data = ScriptableObject.CreateInstance<_Game.Config.ZombieData>();
        data.zombieName = "胖僵尸";
        data.maxHealth = 300f;
        data.moveSpeed = 1.5f;
        data.attackDamage = 20;
        data.attackRange = 2f;
        data.attackCooldown = 2.5f;
        data.detectRange = 8f;
        data.loseRange = 25f;
        data.visionAngle = 60f;
        data.bodyRadius = 0.5f;
        data.bodyHeight = 2.0f;
        data.lootGroup = "rare";
        AssetDatabase.CreateAsset(data, path);
        Debug.Log($"已创建: {path}");
    }

    static void CreateDefaultProfile()
    {
        string path = $"{ProfileDir}/默认地段.asset";
        if (AssetDatabase.LoadAssetAtPath<_Game.Config.ZoneSpawnProfile>(path) != null)
        {
            Debug.Log($"已存在，跳过: {path}");
            return;
        }
        var profile = ScriptableObject.CreateInstance<_Game.Config.ZoneSpawnProfile>();
        profile.zoneName = "默认地段";
        profile.initialMin = 2;
        profile.initialMax = 5;
        profile.maxPerChunk = 10;
        profile.respawnInterval = 120f;
        profile.minSpawnDistFromPlayer = 25f;
        profile.maxPerRespawnBatch = 2;

        // 加载刚创建的 3 个 ZombieData 作为默认权重
        var normal = AssetDatabase.LoadAssetAtPath<_Game.Config.ZombieData>($"{Dir}/普通僵尸.asset");
        var fast = AssetDatabase.LoadAssetAtPath<_Game.Config.ZombieData>($"{Dir}/快速僵尸.asset");
        var fat = AssetDatabase.LoadAssetAtPath<_Game.Config.ZombieData>($"{Dir}/胖僵尸.asset");

        var weights = new System.Collections.Generic.List<_Game.Config.ZoneSpawnProfile.ZombieTypeWeight>();
        if (normal != null) weights.Add(new _Game.Config.ZoneSpawnProfile.ZombieTypeWeight { data = normal, weight = 60 });
        if (fast != null) weights.Add(new _Game.Config.ZoneSpawnProfile.ZombieTypeWeight { data = fast, weight = 25 });
        if (fat != null) weights.Add(new _Game.Config.ZoneSpawnProfile.ZombieTypeWeight { data = fat, weight = 15 });
        profile.typeWeights = weights.ToArray();

        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"已创建: {path}");
    }

    [MenuItem("Game Tools/Create ZombieSpawner in Scene")]
    private static void CreateSpawner()
    {
        var existing = Object.FindObjectOfType<_Game.Systems.Zombie.ZombieSpawner>();
        if (existing != null)
        {
            Debug.Log("ZombieSpawner 已存在");
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var go = new GameObject("ZombieSpawner", typeof(_Game.Systems.Zombie.ZombieSpawner));
        Undo.RegisterCreatedObjectUndo(go, "Create ZombieSpawner");

        // 尝试自动绑定默认 Profile
        var defaultProfile = AssetDatabase.LoadAssetAtPath<_Game.Config.ZoneSpawnProfile>($"{ProfileDir}/默认地段.asset");
        if (defaultProfile != null)
        {
            var spawner = go.GetComponent<_Game.Systems.Zombie.ZombieSpawner>();
            spawner.zoneProfiles = new[] { defaultProfile };
        }

        Debug.Log("已创建 ZombieSpawner");
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }
}
