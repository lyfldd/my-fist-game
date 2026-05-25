using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 建造物目录 — 集中管理所有可建造物 ScriptableObject
    ///
    /// 用法：
    /// 1. 在编辑器中：右键 → Create → Game/Buildable Catalog 创建
    /// 2. 把 BuildableData 资产拖入 buildables 数组
    /// 3. BuildMenuUI 引用此目录来显示建造物列表
    ///
    /// 后续 Phase 2+ 可扩展：
    /// - 按分类/等级解锁（依赖技能系统）
    /// - 动态注册（通过 Recipe 系统解锁）
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Buildable Catalog")]
    public class BuildableCatalog : ScriptableObject
    {
        [Header("建造物列表")]
        [Tooltip("所有玩家当前可建造的建造物。BuildMenuUI 读取此列表。")]
        public BuildableData[] buildables;

        /// <summary>
        /// 按索引获取建造物数据，越界返回 null
        /// </summary>
        public BuildableData GetBuildable(int index)
        {
            if (buildables == null || index < 0 || index >= buildables.Length)
                return null;
            return buildables[index];
        }

        /// <summary>
        /// 按显示名称查找建造物
        /// </summary>
        public BuildableData FindByName(string displayName)
        {
            if (buildables == null) return null;
            foreach (var b in buildables)
            {
                if (b != null && b.displayName == displayName)
                    return b;
            }
            return null;
        }

        /// <summary>
        /// 获取建造物数量
        /// </summary>
        public int Count => buildables?.Length ?? 0;

        /// <summary>
        /// 按分类筛选建造物
        /// </summary>
        public BuildableData[] GetByCategory(BuildableCategory category)
        {
            if (buildables == null) return System.Array.Empty<BuildableData>();
            int count = 0;
            foreach (var b in buildables)
                if (b != null && b.category == category)
                    count++;
            var result = new BuildableData[count];
            int i = 0;
            foreach (var b in buildables)
            {
                if (b != null && b.category == category)
                { result[i] = b; i++; }
            }
            return result;
        }

        /// <summary>
        /// 获取有内容的分类列表（用于构建标签页）
        /// </summary>
        public System.Collections.Generic.List<BuildableCategory> GetUsedCategories()
        {
            var set = new System.Collections.Generic.HashSet<BuildableCategory>();
            if (buildables != null)
            {
                foreach (var b in buildables)
                    if (b != null) set.Add(b.category);
            }
            var list = new System.Collections.Generic.List<BuildableCategory>(set);
            list.Sort((a, b) => a.CompareTo(b));
            return list;
        }
    }
}
