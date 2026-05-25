using System.Collections.Generic;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 掉落表 — 权重随机生成容器内容
    /// 由 ContainerLootProfile 引用，GenerateLoot 返回随机物品列表
    /// 不放回抽样：每次选中一个类型后从池中移除，保证不重复
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "Game/LootTable")]
    public class LootTable : ScriptableObject
    {
        public List<LootEntry> entries = new List<LootEntry>();

        /// <summary>
        /// 从掉落表按权重随机生成物品列表（不放回）
        /// </summary>
        /// <param name="minTypes">最少生成几种物品</param>
        /// <param name="maxTypes">最多生成几种物品</param>
        /// <returns>每项包含物品数据 + 随机数量</returns>
        public List<(ItemData item, int count)> GenerateLoot(int minTypes, int maxTypes)
        {
            var result = new List<(ItemData, int)>();

            if (entries == null || entries.Count == 0)
                return result;

            // 过滤：必须有 itemData 且 maxCount > 0
            var valid = new List<LootEntry>();
            foreach (var e in entries)
            {
                if (e.itemData != null && e.maxCount > 0)
                    valid.Add(e);
            }
            if (valid.Count == 0) return result;

            // 种类数
            int typeCount = Random.Range(minTypes, maxTypes + 1);
            typeCount = Mathf.Clamp(typeCount, 1, valid.Count);

            // 构建索引池 + 总权重
            var pool = new List<int>(valid.Count);
            float totalWeight = 0f;
            for (int i = 0; i < valid.Count; i++)
            {
                pool.Add(i);
                totalWeight += Mathf.Max(valid[i].weight, 0.01f); // 保底权重防除零
            }

            // 不放回权重随机抽取
            for (int t = 0; t < typeCount && pool.Count > 0; t++)
            {
                float roll = Random.Range(0f, totalWeight);
                float acc = 0f;
                int pickedIdx = -1;
                int removePos = -1;

                for (int i = 0; i < pool.Count; i++)
                {
                    acc += Mathf.Max(valid[pool[i]].weight, 0.01f);
                    if (roll <= acc)
                    {
                        pickedIdx = pool[i];
                        removePos = i;
                        break;
                    }
                }

                // 浮点精度兜底
                if (pickedIdx < 0)
                {
                    removePos = pool.Count - 1;
                    pickedIdx = pool[removePos];
                }

                var entry = valid[pickedIdx];
                int count = Random.Range(entry.minCount, entry.maxCount + 1);
                result.Add((entry.itemData, count));

                // 移除已抽中的索引
                totalWeight -= Mathf.Max(entry.weight, 0.01f);
                pool.RemoveAt(removePos);
            }

            return result;
        }
    }

    /// <summary>
    /// 掉落表条目：物品种类 + 权重 + 数量区间
    /// </summary>
    [System.Serializable]
    public struct LootEntry
    {
        [Tooltip("物品数据（ScriptableObject）")]
        public ItemData itemData;

        [Tooltip("权重（越大越容易出），0=极少概率")]
        [Range(0f, 10f)]
        public float weight;

        [Tooltip("最少数量")]
        [Min(0)]
        public int minCount;

        [Tooltip("最多数量")]
        [Min(0)]
        public int maxCount;
    }
}
