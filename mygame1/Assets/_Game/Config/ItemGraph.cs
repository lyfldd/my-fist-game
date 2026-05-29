using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 物品依赖图谱 — 所有物品的上下游关系总表
    /// 由 ItemGraphBuilder 编辑器工具构建，运行时由 ItemGraphManager 加载查询
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Item Graph")]
    public class ItemGraph : ScriptableObject
    {
        [Header("构建信息")]
        [Tooltip("上次构建时间")]
        public string buildTimestamp;

        [Tooltip("构建时的配方总数")]
        public int recipeCountAtBuild;

        [Header("图谱数据")]
        [Tooltip("所有物品节点")]
        public ItemGraphNode[] nodes;

        [Header("统计")]
        public int rawMaterialCount;
        public int deadEndCount;
        public int coreMaterialCount; // 消费热度 >= 10

#if UNITY_EDITOR
        /// <summary>
        /// 按名称查找节点（编辑器用，运行时用 Dictionary）
        /// </summary>
        public ItemGraphNode FindNode(string itemName)
        {
            if (nodes == null) return null;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].itemName == itemName)
                    return nodes[i];
            }
            return null;
        }
#endif
    }
}
