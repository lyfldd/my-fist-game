using UnityEditor;
using UnityEngine;
using _Game.Config;
using _Game.Systems.AIBot;

namespace _Game.Editor
{
    /// <summary>
    /// 一键创建 AIBotWeaponMapping.asset，映射 AIBot 武器枚举到具体 ItemData。
    /// Tools → AIBot → 创建武器映射表
    /// </summary>
    public class CreateAIBotWeaponMapping : EditorWindow
    {
        [MenuItem("Tools/AIBot/创建武器映射表")]
        public static void Run()
        {
            const string path = "Assets/_Game/Config/AIBot/AIBotWeaponMapping.asset";
            EnsureDir("Assets/_Game/Config/AIBot");

            var existing = AssetDatabase.LoadAssetAtPath<AIBotWeaponMapping>(path);
            var mapping = existing != null ? existing : ScriptableObject.CreateInstance<AIBotWeaponMapping>();

            mapping.entries = new AIBotWeaponMapping.Entry[]
            {
                // 右臂武器
                new AIBotWeaponMapping.Entry
                {
                    rightWeapon = RightArmWeapon.Pistol,
                    weaponItem = FindItem("M1911 手枪"),
                    ammoItem = FindItem(".45 ACP"),
                },
                new AIBotWeaponMapping.Entry
                {
                    rightWeapon = RightArmWeapon.Rifle,
                    weaponItem = FindItem("AK-47 突击步枪"),
                    ammoItem = FindItem("7.62mm步枪弹"),
                },
                new AIBotWeaponMapping.Entry
                {
                    rightWeapon = RightArmWeapon.Shotgun,
                    weaponItem = FindItem("雷明顿 870"),
                    ammoItem = FindItem("12号霰弹"),
                },
                new AIBotWeaponMapping.Entry
                {
                    rightWeapon = RightArmWeapon.ElectromagneticRifle,
                    weaponItem = FindItem("电磁步枪"),
                    ammoItem = FindItem("电池组"),
                },
                // 左臂武器
                new AIBotWeaponMapping.Entry
                {
                    leftWeapon = LeftArmWeapon.Shield,
                    leftWeaponItem = FindItem("盾牌"),
                },
                new AIBotWeaponMapping.Entry
                {
                    leftWeapon = LeftArmWeapon.Chainsaw,
                    leftWeaponItem = FindItem("电锯"),
                },
                new AIBotWeaponMapping.Entry
                {
                    leftWeapon = LeftArmWeapon.Knife,
                    leftWeaponItem = FindItem("短刀"),
                },
            };

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mapping, path);
                Debug.Log("[AIBot] 武器映射表已创建: " + path);
            }
            else
            {
                EditorUtility.SetDirty(mapping);
                Debug.Log("[AIBot] 武器映射表已更新: " + path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static ItemData FindItem(string name)
        {
            var guids = AssetDatabase.FindAssets($"t:ItemData {name}");
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(p);
                if (item != null && item.itemName == name) return item;
            }
            Debug.LogWarning($"[AIBot] 未找到物品: {name}");
            return null;
        }

        static void EnsureDir(string dir)
        {
            var parts = dir.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string parent = current;
                current += "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(current))
                    AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }
    }
}
