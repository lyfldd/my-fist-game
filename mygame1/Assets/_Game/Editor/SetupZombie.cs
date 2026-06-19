using UnityEditor;
using UnityEngine;
using _Game.Systems.Zombie;
using _Game.Systems.Combat;

/// <summary>
/// 编辑器工具：一键创建僵尸寻路地基（圆柱体 + Controller）
/// </summary>
public class SetupZombie
{
    [MenuItem("Tools/Zombie/Create Prefab")]
    public static void CreatePrefab()
    {
        string path = "Assets/_Game/Prefabs/Zombie.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            return;
        }

        var go = new GameObject("Zombie");

        // 圆柱体（视觉）
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        vis.name = "Body";
        vis.transform.SetParent(go.transform);
        vis.transform.localPosition = new Vector3(0, 0.8f, 0);
        vis.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
        Object.DestroyImmediate(vis.GetComponent<Collider>());

        // CapsuleCollider（碰撞体）
        var col = go.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.height = 1.6f;
        col.radius = 0.3f;
        col.center = new Vector3(0, 0.8f, 0);

        // 脚本
        go.AddComponent<ZombieController>();
        go.AddComponent<DamageableZombie>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Zombie/Add PlayerCombat To Player")]
    public static void AddPlayerCombat()
    {
        var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (player == null)
        {
            return;
        }

        if (player.GetComponent<PlayerCombat>() != null)
        {
            return;
        }

        player.AddComponent<PlayerCombat>();
    }

    [MenuItem("Tools/Zombie/Add DamageablePlayer To Player")]
    public static void AddDamageablePlayer()
    {
        var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (player == null)
        {
            return;
        }
        if (player.GetComponent<DamageablePlayer>() != null)
        {
            return;
        }
        player.AddComponent<DamageablePlayer>();
    }
}
