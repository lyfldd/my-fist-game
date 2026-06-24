using System;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 物品图谱节点 — 记录每个物品在产业链中的位置
    /// 由 ItemGraphBuilder 自动计算填充，支持手动覆写
    /// </summary>
    [Serializable]
    public class ItemGraphNode
    {
        [Header("基础信息")]
        [Tooltip("物品名称（对应 ItemData.itemName）")]
        public string itemName;

        [Tooltip("主链类型")]
        public ChainType primaryChain;

        [Tooltip("生产模式")]
        public ProductionMode productionMode = ProductionMode.Manual;

        [Header("手动覆写（勾选后覆盖自动分配）")]
        public bool overrideStation;
        public WorkstationTier manualStation;

        [Header("自动计算（由 ItemGraphBuilder 填充）")]
        [Tooltip("所有生产路径的深度（距原材料步数），取最小值做自动分配")]
        public int[] depths;

        [Tooltip("上游物品名称（哪些物品是它的材料）")]
        public string[] upstreamItemNames;

        [Tooltip("下游物品名称（哪些配方消费它）")]
        public string[] downstreamItemNames;

        [Tooltip("产出该物品的配方引用")]
        public RecipeData[] producerRecipes;

        [Tooltip("消费该物品的配方引用")]
        public RecipeData[] consumerRecipes;

        [Tooltip("下游配方数量（消费热度）")]
        public int consumerCount;

        [Tooltip("规则自动分配的工作台等级")]
        public WorkstationTier autoAssignedStation;

        [Tooltip("是否为原材料（无配方产出它，只能从世界获取）")]
        public bool isRawMaterial;

        [Tooltip("是否断头路（无配方消费它）")]
        public bool isDeadEnd;

        [Header("获取来源与动作")]
        [Tooltip("获取来源列表（可多个）")]
        public ItemSourceType[] sources;

        [Tooltip("对应获取动作")]
        public ItemObtainAction[] obtainActions;

        [Tooltip("来源描述（自动生成）")]
        public string[] sourceDescriptions;

        [Header("功能实现追踪")]
        [Tooltip("依赖哪些系统（从 ItemData.behaviours 汇总）")]
        public string[] requiredSystems;

        [Tooltip("所有依赖系统的脚本是否都已实现")]
        public bool allSystemsReady;

        /// <summary>
        /// 生效的工作台等级：手动覆写优先，否则用自动分配
        /// </summary>
        public WorkstationTier EffectiveStation =>
            overrideStation ? manualStation : autoAssignedStation;

        /// <summary>
        /// 最浅生产深度（用于自动分配工作台）
        /// </summary>
        public int MinDepth
        {
            get
            {
                if (depths == null || depths.Length == 0) return 0;
                int min = int.MaxValue;
                for (int i = 0; i < depths.Length; i++)
                    if (depths[i] < min) min = depths[i];
                return min;
            }
        }

        /// <summary>
        /// 最深生产深度
        /// </summary>
        public int MaxDepth
        {
            get
            {
                if (depths == null || depths.Length == 0) return 0;
                int max = 0;
                for (int i = 0; i < depths.Length; i++)
                    if (depths[i] > max) max = depths[i];
                return max;
            }
        }
    }
}
