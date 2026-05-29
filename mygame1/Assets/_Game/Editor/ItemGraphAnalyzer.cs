using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using _Game.Config;

namespace _Game.Editor
{
    /// <summary>
    /// 物品图谱断头路分析工具 — 诊断产业链漏洞
    /// </summary>
    public static class ItemGraphAnalyzer
    {
        [MenuItem("Tools/物品图谱/断头路分析报告")]
        public static void Analyze()
        {
            var graph = AssetDatabase.LoadAssetAtPath<ItemGraph>(
                "Assets/_Game/Config/ItemGraph.asset");
            if (graph == null || graph.nodes == null)
            {
                Debug.LogError("[ItemGraphAnalyzer] 未找到 ItemGraph.asset，请先构建图谱");
                return;
            }

            var nodes = graph.nodes;
            var lookup = nodes.ToDictionary(n => n.itemName, n => n);

            // =============================================
            // 1. 总览
            // =============================================
            int rawCount = nodes.Count(n => n.isRawMaterial);
            int deadCount = nodes.Count(n => n.isDeadEnd);
            int nonRawDead = nodes.Count(n => n.isDeadEnd && !n.isRawMaterial);
            int orphanCount = nodes.Count(n => n.isDeadEnd && n.isRawMaterial);

            Debug.Log("========== 物品图谱断头路分析报告 ==========");
            Debug.Log($"总物品: {nodes.Length}  原材料: {rawCount}  断头路: {deadCount}  非原材料断头: {nonRawDead}  孤儿: {orphanCount}");
            Debug.Log("");

            // =============================================
            // 2. 断头路分类分析
            // =============================================
            Debug.Log("===== 一、非原材料断头路（合成产出后无任何消费） =====");
            var deadEnds = nodes
                .Where(n => n.isDeadEnd && !n.isRawMaterial)
                .OrderBy(n => n.primaryChain)
                .ThenBy(n => n.itemName)
                .ToList();

            // 分组
            var ammo = deadEnds.Where(n => n.itemName.Contains("弹") || n.itemName.Contains("箭")).ToList();
            var weapons = deadEnds.Where(n => n.itemName.Contains("枪") || n.itemName.Contains("刀") ||
                n.itemName.Contains("弓") || n.itemName.Contains("矛") || n.itemName.Contains("斧") ||
                n.itemName.Contains("锯") || n.itemName.Contains("锤") || n.itemName.Contains("镐") ||
                n.itemName.Contains("棍") || n.itemName.Contains("棒")).ToList();
            var kits = deadEnds.Where(n => n.itemName.Contains("套件")).ToList();
            var consumable = deadEnds.Where(n => n.itemName.Contains("食物") || n.itemName.Contains("药") ||
                n.itemName.Contains("剂") || n.itemName.Contains("水") || n.itemName.Contains("汤") ||
                n.itemName.Contains("肉") || n.itemName.Contains("菜") || n.itemName.Contains("啡") ||
                n.itemName.Contains("包") || n.itemName.Contains("啡") || n.itemName.Contains("啡") ||
                n.itemName.Contains("啡")).ToList();
            var equipment = deadEnds.Where(n => n.itemName.Contains("头盔") || n.itemName.Contains("甲") ||
                n.itemName.Contains("盾") || n.itemName.Contains("裤") || n.itemName.Contains("鞋") ||
                n.itemName.Contains("帽") || n.itemName.Contains("T恤") || n.itemName.Contains("腰带") ||
                n.itemName.Contains("包")).ToList();
            var endgame = deadEnds.Where(n => n.itemName.Contains("AI核心") || n.itemName.Contains("卫星") ||
                n.itemName.Contains("编程台") || n.itemName.Contains("破译器") || n.itemName.Contains("预测仪") ||
                n.itemName.Contains("电磁")).ToList();
            var materials_dead = deadEnds.Where(n =>
                n.primaryChain == ChainType.Metal ||
                n.primaryChain == ChainType.Chemical ||
                n.primaryChain == ChainType.Electronics).ToList();
            var other = deadEnds.Except(ammo).Except(weapons).Except(kits).Except(consumable)
                .Except(equipment).Except(endgame).Except(materials_dead).ToList();

            PrintGroup("弹药/箭矢（消耗品，合理断头）", ammo, true);
            PrintGroup("武器（装备，合理断头）", weapons, true);
            PrintGroup("工业套件（建造消耗品，合理断头）", kits, true);
            PrintGroup("食物/药品/饮水（消耗品，合理断头）", consumable, true);
            PrintGroup("装备/护甲（装备，合理断头）", equipment, true);
            PrintGroup("终局科技（终局产物，合理断头）", endgame, true);
            PrintGroup("半成品/中间材料（⚠ 不应断头！需补下游配方）", materials_dead.Where(n =>
                !ammo.Contains(n) && !weapons.Contains(n) && !kits.Contains(n) &&
                !consumable.Contains(n) && !equipment.Contains(n) && !endgame.Contains(n)).ToList(), false);
            PrintGroup("其他未分类", other, false);

            // =============================================
            // 3. 半成品断头路 — 核心问题
            // =============================================
            Debug.Log("===== 二、⚠ 半成品断头路详细分析（应补下游配方） =====");
            var problemDeadEnds = deadEnds.Where(n =>
                (n.primaryChain == ChainType.Metal || n.primaryChain == ChainType.Chemical ||
                 n.primaryChain == ChainType.Electronics || n.primaryChain == ChainType.Energy) &&
                !n.itemName.Contains("套件") && !n.itemName.Contains("弹") && !n.itemName.Contains("箭") &&
                !n.itemName.Contains("枪") && !n.itemName.Contains("刀") && !n.itemName.Contains("弓") &&
                !n.itemName.Contains("矛") && !n.itemName.Contains("盾") && !n.itemName.Contains("甲") &&
                !n.itemName.Contains("AI核心") && !n.itemName.Contains("卫星") && !n.itemName.Contains("编程台") &&
                !n.itemName.Contains("破译器") && !n.itemName.Contains("预测仪") && !n.itemName.Contains("电磁")
            ).ToList();

            foreach (var n in problemDeadEnds.OrderBy(n => n.primaryChain).ThenBy(n => n.MinDepth))
            {
                string upstreamStr = n.upstreamItemNames != null && n.upstreamItemNames.Length > 0
                    ? string.Join(", ", n.upstreamItemNames) : "(无)";
                Debug.Log($"  [{n.primaryChain}] {n.itemName}  depth={n.MinDepth}  station={n.EffectiveStation}  材料: {upstreamStr}");
            }

            if (problemDeadEnds.Count == 0)
                Debug.Log("  ✅ 无问题断头路！");

            // =============================================
            // 4. 孤儿物品
            // =============================================
            Debug.Log("===== 三、孤儿物品（无产出+无消费） =====");
            var orphans = nodes.Where(n => n.isDeadEnd && n.isRawMaterial).ToList();
            foreach (var n in orphans.OrderBy(n => n.primaryChain).ThenBy(n => n.itemName))
                Debug.Log($"  [{n.primaryChain}] {n.itemName} — 只能从世界获取，也没配方消费");
            Debug.Log($"  共 {orphans.Count} 个（正常：打火机/手表等世界观物品）");

            // =============================================
            // 5. 低热度中间材料
            // =============================================
            Debug.Log("===== 四、低热度中间材料（仅被1个配方消费） =====");
            var lowHeat = nodes.Where(n => !n.isRawMaterial && n.consumerCount == 1 && !n.isDeadEnd)
                .OrderBy(n => n.primaryChain).ThenBy(n => n.itemName).ToList();
            PrintGroup("热度=1（可扩展为多用途）", lowHeat, false);

            // =============================================
            // 6. 原材料未充分利用
            // =============================================
            Debug.Log("===== 五、原材料消费热度 =====");
            var rawMats = nodes.Where(n => n.isRawMaterial).OrderBy(n => n.primaryChain).ThenBy(n => n.itemName).ToList();
            foreach (var n in rawMats)
            {
                var usedBy = n.downstreamItemNames != null && n.downstreamItemNames.Length > 0
                    ? string.Join(", ", n.downstreamItemNames) : "(无)";
                Debug.Log($"  [{n.primaryChain}] {n.itemName}  热度={n.consumerCount}  消费方: {usedBy}");
            }

            // =============================================
            // 7. 按链类型统计断头路
            // =============================================
            Debug.Log("===== 六、断头路链类型分布 =====");
            foreach (var g in deadEnds.GroupBy(n => n.primaryChain).OrderByDescending(g => g.Count()))
            {
                var names = string.Join(", ", g.Select(n => n.itemName));
                Debug.Log($"  {g.Key}: {g.Count()} 个 — {names}");
            }

            // =============================================
            // 8. 建议
            // =============================================
            Debug.Log("===== 七、修复建议 =====");
            Debug.Log("  以上半成品断头路为实际诊断结果，非模板建议。");
            Debug.Log("  修复方向：加钢筋到重型工业套件、空电池灌酸产电池、聚变核心补产出配方。");
            Debug.Log("========== 报告结束 ==========");
        }

        static void PrintGroup(string title, List<ItemGraphNode> items, bool isReasonable)
        {
            if (items.Count == 0) return;
            string icon = isReasonable ? "  " : "⚠ ";
            Debug.Log($"\n--- {icon}{title} ({items.Count} 个) ---");
            foreach (var n in items)
            {
                string upstreamStr = n.upstreamItemNames != null && n.upstreamItemNames.Length > 0
                    ? string.Join("+", n.upstreamItemNames) : "原材料";
                Debug.Log($"  [{n.primaryChain}] {n.itemName} ← {upstreamStr}  depth={n.MinDepth}");
            }
        }
    }
}
