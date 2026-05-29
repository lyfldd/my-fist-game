using System.Linq;
using _Game.Config;
using UnityEditor;
using UnityEngine;

namespace _Game.Editor
{
    [CustomEditor(typeof(ItemGraph))]
    public class ItemGraphInspector : UnityEditor.Editor
    {
        private string _search = "";
        private int _chainFilter = -1;
        private int _depthMin, _depthMax = 10;
        private bool _deadEndOnly, _coreOnly, _rawMaterialOnly, _notReadyOnly;
        private int _selected = -1;
        private Vector2 _listScroll, _detailScroll;

        private int _editChain, _editProd, _editStation;
        private bool _editOverride;

        // 统计
        private int _notReadyCount;

        public override void OnInspectorGUI()
        {
            var graph = (ItemGraph)target;
            if (graph.nodes == null || graph.nodes.Length == 0)
            {
                EditorGUILayout.HelpBox("图谱为空。请运行 Tools/物品图谱/构建 ItemGraph", MessageType.Warning);
                if (GUILayout.Button("立即构建"))
                    ItemGraphBuilder.Build();
                return;
            }

            // 重新计算未就绪数量
            _notReadyCount = graph.nodes.Count(n => n != null && !n.allSystemsReady);

            // 统计条
            EditorGUILayout.LabelField(
                $"物品 {graph.nodes.Length} | 原材 {graph.rawMaterialCount} | 断头 {graph.deadEndCount} | 核心 {graph.coreMaterialCount} | 缺脚本 {_notReadyCount} | {graph.buildTimestamp}");

            EditorGUILayout.Space(4);

            // 过滤器第一行
            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField("搜索", _search, GUILayout.Width(160));
            _chainFilter = EditorGUILayout.IntPopup(_chainFilter,
                new[] { "全部链", "金属", "电子", "化学", "生物", "食品", "能源" },
                new[] { -1, 0, 1, 2, 3, 4, 5 }, GUILayout.Width(140));
            _deadEndOnly = EditorGUILayout.Toggle("断头", _deadEndOnly, GUILayout.Width(50));
            _coreOnly = EditorGUILayout.Toggle("核心", _coreOnly, GUILayout.Width(50));
            _rawMaterialOnly = EditorGUILayout.Toggle("原材", _rawMaterialOnly, GUILayout.Width(50));
            _notReadyOnly = EditorGUILayout.Toggle("缺脚本", _notReadyOnly, GUILayout.Width(55));
            EditorGUILayout.EndHorizontal();

            // 过滤器第二行
            EditorGUILayout.BeginHorizontal();
            _depthMin = EditorGUILayout.IntField("深度", _depthMin, GUILayout.Width(60));
            _depthMax = EditorGUILayout.IntField("~", _depthMax, GUILayout.Width(60));
            if (GUILayout.Button("重建图谱", GUILayout.Width(80)))
                ItemGraphBuilder.Build();
            EditorGUILayout.EndHorizontal();

            // 列表
            var filtered = GetFiltered(graph);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(200));

            for (int i = 0; i < filtered.Length; i++)
            {
                var n = filtered[i];
                var origIdx = System.Array.IndexOf(graph.nodes, n);

                var oldBg = GUI.backgroundColor;
                if (!n.allSystemsReady)
                    GUI.backgroundColor = origIdx == _selected ? new Color(0.7f, 0.5f, 0.1f) : new Color(0.5f, 0.35f, 0.05f);
                else if (origIdx == _selected)
                    GUI.backgroundColor = new Color(0.4f, 0.6f, 0.9f);
                else if (n.isDeadEnd && !n.isRawMaterial)
                    GUI.backgroundColor = new Color(0.55f, 0.3f, 0.3f);
                else if (n.consumerCount >= 10)
                    GUI.backgroundColor = new Color(0.15f, 0.5f, 0.15f);

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"[{ChainLabel(n.primaryChain)}]", GUILayout.Width(48));
                if (GUILayout.Button(n.itemName, EditorStyles.label, GUILayout.MinWidth(90)))
                {
                    _selected = origIdx;
                    LoadEdit(n);
                }
                EditorGUILayout.LabelField($"d={n.MinDepth}", GUILayout.Width(28));
                var tag = n.isRawMaterial ? "R" : (n.isDeadEnd ? "X" : $"↓{n.consumerCount}");
                EditorGUILayout.LabelField(tag, GUILayout.Width(26));
                EditorGUILayout.LabelField(n.EffectiveStation.ToString(), GUILayout.Width(74));
                if (!n.allSystemsReady) EditorGUILayout.LabelField("⚠", GUILayout.Width(16));
                if (n.overrideStation) EditorGUILayout.LabelField("[覆]", GUILayout.Width(24));
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = oldBg;
            }

            EditorGUILayout.EndScrollView();

            // 详情
            if (_selected >= 0 && _selected < graph.nodes.Length)
            {
                var node = graph.nodes[_selected];
                EditorGUILayout.Space(8);

                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.Height(320));
                DrawDetail(graph, node);
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawDetail(ItemGraph graph, ItemGraphNode node)
        {
            EditorGUILayout.LabelField(node.itemName, EditorStyles.largeLabel);

            // 图谱信息
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"链: {node.primaryChain}  深度: [{string.Join(",", node.depths)}]  热度: {node.consumerCount}");
            if (node.isRawMaterial) EditorGUILayout.LabelField("→ 原材料");
            if (node.isDeadEnd) EditorGUILayout.LabelField("→ 断头路");
            EditorGUILayout.LabelField($"自动工作站: {node.autoAssignedStation}  生效: {node.EffectiveStation}");
            EditorGUILayout.EndVertical();

            // 系统就绪状态
            DrawSystemStatus(node);

            // 产出配方
            if (node.producerRecipes != null && node.producerRecipes.Length > 0)
            {
                if (EditorGUILayout.Foldout(true, $"产出配方 ({node.producerRecipes.Length})", true))
                {
                    EditorGUI.indentLevel++;
                    foreach (var r in node.producerRecipes)
                    {
                        if (r == null) continue;
                        var tag = r.isIndustrial ? "[工业]" : "[手工]";
                        var mats = r.materials != null && r.materials.Length > 0
                            ? string.Join(", ", System.Array.ConvertAll(r.materials, m => m.itemData != null ? m.itemData.itemName : "?"))
                            : "无材料";
                        EditorGUILayout.LabelField($"{tag} {r.recipeName}  ← {mats}");
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // 消费配方（本物品作为材料被哪些配方使用）
            if (node.consumerRecipes != null && node.consumerRecipes.Length > 0)
            {
                if (EditorGUILayout.Foldout(true, $"消费配方 ({node.consumerRecipes.Length})", true))
                {
                    EditorGUI.indentLevel++;
                    foreach (var r in node.consumerRecipes)
                    {
                        if (r == null) continue;
                        var tag = r.isIndustrial ? "[工业]" : "[手工]";
                        var output = r.resultItem != null ? r.resultItem.itemName : "?";
                        EditorGUILayout.LabelField($"{tag} {r.recipeName}  → {output}");
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // 上游
            if (node.upstreamItemNames.Length > 0)
            {
                if (EditorGUILayout.Foldout(true, $"上游 ({node.upstreamItemNames.Length})", true))
                {
                    EditorGUI.indentLevel++;
                    foreach (var u in node.upstreamItemNames)
                    {
                        var un = graph.FindNode(u);
                        var s = un != null ? $"{u}  d={un.MinDepth}  [{un.primaryChain}]" : u;
                        EditorGUILayout.LabelField(s);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // 下游
            if (node.downstreamItemNames.Length > 0)
            {
                if (EditorGUILayout.Foldout(true, $"下游 ({node.downstreamItemNames.Length})", true))
                {
                    EditorGUI.indentLevel++;
                    foreach (var d in node.downstreamItemNames)
                    {
                        var dn = graph.FindNode(d);
                        var s = dn != null ? $"{d}  d={dn.MinDepth}  [{dn.primaryChain}]" : d;
                        EditorGUILayout.LabelField(s);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(6);

            // 覆写编辑
            EditorGUILayout.LabelField("手动覆写", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _editOverride = EditorGUILayout.Toggle("覆写工作站", _editOverride);
            if (_editOverride)
            {
                _editStation = EditorGUILayout.IntPopup("指定",
                    _editStation,
                    new[] { "Hands", "Campfire", "SimpleBench", "Furnace", "MediumBench", "AdvancedBench", "Chemistry", "Machining", "ElectronicsAssembly", "ElementFurnace" },
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            }

            _editChain = EditorGUILayout.IntPopup("链类型",
                _editChain,
                new[] { "Metal", "Electronics", "Chemical", "Biological", "Food", "Energy" },
                new[] { 0, 1, 2, 3, 4, 5 });

            _editProd = EditorGUILayout.IntPopup("生产模式",
                _editProd,
                new[] { "Manual", "Industrial", "Both" },
                new[] { 0, 1, 2 });

            EditorGUILayout.EndVertical();

            if (GUILayout.Button("保存修改", GUILayout.Height(22)))
                SaveEdit(graph, node);
        }

        void DrawSystemStatus(ItemGraphNode node)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var systems = node.requiredSystems ?? new string[0];

            if (node.allSystemsReady)
            {
                EditorGUILayout.LabelField("✅ 系统就绪", EditorStyles.boldLabel);
                if (systems.Length == 0)
                    EditorGUILayout.LabelField("  (无特殊脚本需求，纯数据驱动)");
                else
                    EditorGUILayout.LabelField($"  依赖: {string.Join(", ", systems)}");
            }
            else
            {
                EditorGUILayout.LabelField("⚠ 缺脚本", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  依赖系统: {string.Join(", ", systems)}");
            }

            EditorGUILayout.EndVertical();
        }

        ItemGraphNode[] GetFiltered(ItemGraph graph)
        {
            return graph.nodes.Where(n =>
            {
                if (n == null) return false;
                if (!string.IsNullOrEmpty(_search) && !n.itemName.Contains(_search)) return false;
                if (_chainFilter >= 0 && (int)n.primaryChain != _chainFilter) return false;
                if (n.MinDepth < _depthMin || n.MinDepth > _depthMax) return false;
                if (_deadEndOnly && !n.isDeadEnd) return false;
                if (_coreOnly && n.consumerCount < 10) return false;
                if (_rawMaterialOnly && !n.isRawMaterial) return false;
                if (_notReadyOnly && n.allSystemsReady) return false;
                return true;
            }).OrderBy(n => n.allSystemsReady ? 1 : 0)
              .ThenBy(n => n.primaryChain)
              .ThenBy(n => n.MinDepth).ToArray();
        }

        void LoadEdit(ItemGraphNode n)
        {
            _editChain = (int)n.primaryChain;
            _editProd = (int)n.productionMode;
            _editStation = (int)n.manualStation;
            _editOverride = n.overrideStation;
        }

        void SaveEdit(ItemGraph graph, ItemGraphNode node)
        {
            Undo.RecordObject(graph, "编辑节点");
            node.primaryChain = (ChainType)_editChain;
            node.productionMode = (ProductionMode)_editProd;
            node.overrideStation = _editOverride;
            node.manualStation = (WorkstationTier)_editStation;

            if ((int)node.primaryChain != _editChain)
                node.autoAssignedStation = ItemGraphBuilder.GetAutoStationStatic(node.primaryChain, node.MinDepth);

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemGraph] 已保存 {node.itemName}: 链={node.primaryChain} 模式={node.productionMode} 站={node.EffectiveStation}{(node.overrideStation ? " [手动]" : "")}");
        }

        static string ChainLabel(ChainType c) => c switch
        {
            ChainType.Metal => "金",
            ChainType.Electronics => "电",
            ChainType.Chemical => "化",
            ChainType.Biological => "生",
            ChainType.Food => "食",
            ChainType.Energy => "能",
            _ => "?"
        };
    }
}
