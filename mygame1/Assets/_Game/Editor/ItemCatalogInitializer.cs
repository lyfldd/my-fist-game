using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 编辑器初始化：首次打开项目时自动创建 ItemCatalog.asset 到 Resources 目录。
    /// 用户无需手动操作。
    /// </summary>
    [InitializeOnLoad]
    public static class ItemCatalogInitializer
    {
        static ItemCatalogInitializer()
        {
            EditorApplication.delayCall += EnsureItemCatalog;
        }

        [MenuItem("Tools/重建 ItemCatalog")]
        public static void RebuildItemCatalog()
        {
            // 删除旧的
            var old = Resources.Load<ItemCatalog>("ItemCatalog");
            if (old != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(old));

            EnsureItemCatalog();
        }

        private static void EnsureItemCatalog()
        {
            // 检查是否已存在
            var existing = Resources.Load<ItemCatalog>("ItemCatalog");
            if (existing != null) return;

            // 确保 Resources 目录存在
            var resourcesPath = "Assets/_Game/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");

            // 扫描所有 ItemData
            var guids = AssetDatabase.FindAssets("t:ItemData");
            var items = new List<Config.ItemData>();
            var seenNames = new HashSet<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<Config.ItemData>(path);
                if (item == null || string.IsNullOrEmpty(item.itemName)) continue;

                if (!seenNames.Add(item.itemName))
                {
                    Debug.LogError($"[ItemCatalog] ⚠️ itemName 重复: '{item.itemName}' → {path}");
                    continue;
                }
                items.Add(item);
            }

            // 创建 ScriptableObject
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            catalog.AllItems = items.ToArray();

            var assetPath = $"{resourcesPath}/ItemCatalog.asset";
            AssetDatabase.CreateAsset(catalog, assetPath);
            AssetDatabase.SaveAssets();

            catalog.Build();
            Debug.Log($"[ItemCatalog] ✅ 自动创建完成: {assetPath} ({catalog.Count} 个物品)");
        }
    }
}
