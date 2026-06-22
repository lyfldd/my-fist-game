using System.Collections.Generic;
using _Game.Config;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 全物品索引 ScriptableObject。
    /// 编辑器自动扫描所有 ItemData，运行时提供 O(1) itemName → ItemData 字典查找。
    ///
    /// 初始化时检测重复 itemName → Debug.LogError + 跳过重复项。
    ///
    /// 使用：Resources.Load&lt;ItemCatalog&gt;("ItemCatalog") 或直接拖拽引用。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        public ItemData[] AllItems
        {
            get => _allItems;
            set => _allItems = value;
        }
        [SerializeField] private ItemData[] _allItems;

        private Dictionary<string, ItemData> _lookup;
        private bool _built;

        public int Count => _allItems?.Length ?? 0;

        /// <summary>
        /// 构建运行时查找表。运行时 Awake 或首次访问时调用。
        /// </summary>
        public void Build()
        {
            _built = false; // 重置，防止序列化残留导致跳过
            _lookup = new Dictionary<string, ItemData>();
            if (_allItems == null || _allItems.Length == 0)
            {
                Debug.LogWarning("[ItemCatalog] 物品列表为空，请在 Editor 中点击 'Build Item Catalog'");
                _built = true;
                return;
            }

            var seen = new Dictionary<string, ItemData>();
            foreach (var item in _allItems)
            {
                if (item == null) continue;
                if (string.IsNullOrEmpty(item.itemName))
                {
                    Debug.LogWarning($"[ItemCatalog] 跳过无 itemName 的 ItemData: {item.name}");
                    continue;
                }

                if (seen.TryGetValue(item.itemName, out var existing))
                {
                    Debug.LogError($"[ItemCatalog] itemName 重复！" +
                        $"'{item.name}' 和 '{existing.name}' 都用了 itemName='{item.itemName}'。" +
                        $"加载存档时会返回错误的物品。请修改其中一个的 itemName。跳过重复项。");
                    continue;
                }

                seen[item.itemName] = item;
            }

            _lookup = seen;
            _built = true;
            Debug.Log($"[ItemCatalog] 构建完成: {_lookup.Count} 个物品索引");
        }

        /// <summary> O(1) 查找。返回 null 表示未找到。 </summary>
        public ItemData Find(string itemName)
        {
            if (_lookup == null || !_built) Build();
            if (_lookup == null) return null;
            if (string.IsNullOrEmpty(itemName)) return null;
            _lookup.TryGetValue(itemName, out var result);
            return result;
        }

        /// <summary> 检查 itemName 是否存在 </summary>
        public bool Exists(string itemName)
        {
            if (_lookup == null || !_built) Build();
            if (_lookup == null) return false;
            return !string.IsNullOrEmpty(itemName) && _lookup.ContainsKey(itemName);
        }

        /// <summary> 获取所有已索引的物品名称 </summary>
        public IEnumerable<string> GetAllItemNames()
        {
            if (_lookup == null || !_built) Build();
            if (_lookup == null) yield break;
            foreach (var k in _lookup.Keys) yield return k;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器方法：扫描所有 ItemData 并填充 _allItems。
        /// </summary>
        [ContextMenu("Build Item Catalog")]
        public void BuildFromAssets()
        {
            var guids = AssetDatabase.FindAssets("t:ItemData");
            var items = new List<ItemData>();
            var seen = new HashSet<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item == null) continue;

                if (string.IsNullOrEmpty(item.itemName))
                {
                    Debug.LogWarning($"[ItemCatalog] 跳过无 itemName 的资产: {path}");
                    continue;
                }

                if (!seen.Add(item.itemName))
                {
                    Debug.LogError($"[ItemCatalog] DUPLICATE itemName='{item.itemName}' 在 {path}");
                    continue;
                }

                items.Add(item);
            }

            _allItems = items.ToArray();
            EditorUtility.SetDirty(this);
            Debug.Log($"[ItemCatalog] 扫描完成: {_allItems.Length} 个物品（去重 {guids.Length - _allItems.Length} 个重复）");
        }
#endif
    }
}
