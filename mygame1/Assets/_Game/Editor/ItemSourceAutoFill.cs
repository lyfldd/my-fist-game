using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

namespace _Game.Editor
{
    /// <summary>
    /// 自动填充 ItemGraph 中所有节点的来源和动作。
    /// 用法：Tools → 自动填充物品来源
    /// </summary>
    public class ItemSourceAutoFill : EditorWindow
    {
        [MenuItem("Tools/自动填充物品来源")]
        public static void Run()
        {
            var graph = AssetDatabase.LoadAssetAtPath<ItemGraph>("Assets/_Game/Config/ItemGraph.asset");
            if (graph == null)
            {
                var guids = AssetDatabase.FindAssets("t:ItemGraph");
                if (guids.Length == 0) { Debug.LogError("找不到 ItemGraph.asset"); return; }
                graph = AssetDatabase.LoadAssetAtPath<ItemGraph>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (graph == null || graph.nodes == null) return;

            int filled = 0;
            foreach (var node in graph.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.itemName)) continue;
                if (node.sources != null && node.sources.Length > 0) continue; // 已填过，跳过

                var result = InferSource(node);
                if (result.sources.Length > 0)
                {
                    node.sources = result.sources;
                    node.obtainActions = result.actions;
                    node.sourceDescriptions = result.descriptions;
                    filled++;
                }
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemSourceAutoFill] 已填充 {filled} 个节点的来源/动作，共 {graph.nodes.Length} 个节点");
        }

        static (ItemSourceType[] sources, ItemObtainAction[] actions, string[] descriptions) InferSource(ItemGraphNode node)
        {
            string name = node.itemName;
            bool hasRecipe = node.producerRecipes != null && node.producerRecipes.Length > 0;
            var sources = new List<ItemSourceType>();
            var actions = new List<ItemObtainAction>();
            var descs = new List<string>();

            // 规则1: 按名字推断自然资源
            if (name.Contains("矿") || name.Contains("石头") || name.Contains("石灰") || name.Contains("燧石") || name.Contains("黏土") || name.Contains("沙子") || name.Contains("硝石") || name.Contains("硫磺"))
            { sources.Add(ItemSourceType.Mine); actions.Add(ItemObtainAction.Mine); descs.Add("矿山(挖掘)"); }
            else if (name.Contains("木") || name.Contains("树枝") || name.Contains("纤维") || name.Contains("原木") || name.Contains("树"))
            { sources.Add(ItemSourceType.Tree); actions.Add(ItemObtainAction.Chop); descs.Add("树木(砍伐)"); }
            else if (name.Contains("浆果") || name.Contains("蘑菇") || name.Contains("草药") || name.Contains("生肉") || name.Contains("饮用水") || name.Contains("蛋"))
            { sources.Add(ItemSourceType.Container_Kitchen); actions.Add(ItemObtainAction.Search); descs.Add("厨房容器(搜索)"); }
            else if (name.Contains("绷带") || name.Contains("抗生素") || name.Contains("止血") || name.Contains("疫苗") || name.Contains("维生素") || name.Contains("药品") || name.Contains("医疗"))
            { sources.Add(ItemSourceType.Container_Medical); actions.Add(ItemObtainAction.Search); descs.Add("医疗容器(搜索)"); }
            else if (name.Contains("弹药") || name.Contains("弹壳") || name.Contains("弹头") || name.Contains("底火") || name.Contains("子弹"))
            { sources.Add(ItemSourceType.Container_Ammo); actions.Add(ItemObtainAction.Search); descs.Add("弹药容器(搜索)"); }
            else if (name.Contains("废金属") || name.Contains("螺丝") || name.Contains("弹簧") || name.Contains("轴承") || name.Contains("齿轮") || name.Contains("工具") || name.Contains("零件"))
            { sources.Add(ItemSourceType.Container_Garage); actions.Add(ItemObtainAction.Search); descs.Add("车库容器(搜索)"); }
            else if (name.Contains("电池") || name.Contains("打火机") || name.Contains("手电筒") || name.Contains("指南针") || name.Contains("手表") || name.Contains("绳子") || name.Contains("布") || name.Contains("木板") || name.Contains("塑料"))
            { sources.Add(ItemSourceType.Container_General); actions.Add(ItemObtainAction.Search); descs.Add("通用容器(搜索)"); }
            // 没有匹配的自然资源 → 通用搜刮
            else if (node.isRawMaterial && !hasRecipe)
            { sources.Add(ItemSourceType.Scavenge); actions.Add(ItemObtainAction.Loot); descs.Add("世界搜刮"); }

            // 规则2: 有配方 → 加制作来源（描述用工作台名）
            if (hasRecipe && !node.isRawMaterial)
            {
                string stationName = node.EffectiveStation.ToString();
                // 有 producerRecipes 时看看是不是工业设备
                if (node.producerRecipes.Length > 0 && node.producerRecipes[0] != null && !string.IsNullOrEmpty(node.producerRecipes[0].requiredStationName))
                    stationName = node.producerRecipes[0].requiredStationName;
                sources.Add(ItemSourceType.Craft);
                actions.Add(ItemObtainAction.Craft);
                descs.Add($"{stationName}(制作)");
            }
            // 规则3: 既无自然资源也无配方 → 兜底搜刮
            else if (!node.isRawMaterial && !hasRecipe)
            {
                // 可能是半成品但配方不在图谱中，兜底搜刮
                if (sources.Count == 0)
                { sources.Add(ItemSourceType.Scavenge); actions.Add(ItemObtainAction.Loot); descs.Add("世界搜刮"); }
            }

            // 规则4: 既是原材料又有配方 → 两种方式都有
            if (node.isRawMaterial && hasRecipe && sources.Count < 2)
            {
                string stationName = node.EffectiveStation.ToString();
                sources.Add(ItemSourceType.Craft);
                actions.Add(ItemObtainAction.Craft);
                descs.Add($"{stationName}(制作)");
            }

            return (sources.ToArray(), actions.ToArray(), descs.ToArray());
        }
    }
}
