using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

namespace _Game.Editor
{
    /// <summary>
    /// 物品来源编辑器 — 逐个为 ItemGraph 节点设置来源和动作。
    /// 用法：Tools → 物品来源编辑器
    /// </summary>
    public class ItemSourceEditor : EditorWindow
    {
        Vector2 _scroll;
        ItemGraph _graph;
        Dictionary<string, ItemGraphNode> _nodes;
        string _search = "";
        string _selectedChain = "全部";
        bool _showOnlyRaw = true;

        [MenuItem("Tools/物品来源编辑器")]
        public static void ShowWindow() => GetWindow<ItemSourceEditor>("物品来源编辑器");

        void OnEnable()
        {
            _graph = AssetDatabase.LoadAssetAtPath<ItemGraph>("Assets/_Game/Config/ItemGraph.asset");
            if (_graph == null)
            {
                var guids = AssetDatabase.FindAssets("t:ItemGraph");
                if (guids.Length > 0)
                    _graph = AssetDatabase.LoadAssetAtPath<ItemGraph>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (_graph != null && _graph.nodes != null)
            {
                _nodes = new Dictionary<string, ItemGraphNode>();
                foreach (var n in _graph.nodes)
                    if (n != null && !string.IsNullOrEmpty(n.itemName))
                        _nodes[n.itemName] = n;
            }
        }

        void OnGUI()
        {
            if (_graph == null || _nodes == null)
            {
                EditorGUILayout.HelpBox("未找到 ItemGraph.asset", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField("搜索", _search);
            _showOnlyRaw = EditorGUILayout.Toggle("仅显示原材料", _showOnlyRaw);
            if (GUILayout.Button("全部标记为已处理", GUILayout.Width(120)))
            {
                foreach (var kv in _nodes)
                {
                    if (kv.Value.sources == null || kv.Value.sources.Length == 0)
                        continue;
                }
            }
            EditorGUILayout.EndHorizontal();

            string[] chains = { "全部", "金属", "电子", "化工", "生物", "食品", "能源" };
            int sel = 0;
            for (int i = 0; i < chains.Length; i++)
                if (chains[i] == _selectedChain) { sel = i; break; }
            sel = EditorGUILayout.Popup("产业链", sel, chains);
            _selectedChain = chains[sel];

            EditorGUILayout.LabelField($"共 {_nodes.Count} 个节点");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var kv in _nodes)
            {
                var node = kv.Value;
                if (!string.IsNullOrEmpty(_search) && !node.itemName.Contains(_search)) continue;
                if (_selectedChain != "全部")
                {
                    string cn = node.primaryChain.ToString();
                    if (cn != _selectedChain) continue;
                }
                if (_showOnlyRaw && !node.isRawMaterial) continue;

                DrawNode(node);
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("保存", GUILayout.Height(30)))
            {
                EditorUtility.SetDirty(_graph);
                AssetDatabase.SaveAssets();
                Debug.Log("[ItemSourceEditor] 已保存");
            }
        }

        void DrawNode(ItemGraphNode node)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(node.itemName, EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField($"[{node.primaryChain}] d={node.MinDepth} raw={node.isRawMaterial}", GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();

            // 来源
            int srcCount = node.sources != null ? node.sources.Length : 0;
            int newSrcCount = EditorGUILayout.IntField("来源数量", srcCount);
            if (newSrcCount != srcCount)
            {
                var oldSrc = node.sources;
                var oldAct = node.obtainActions;
                var oldDesc = node.sourceDescriptions;
                node.sources = new ItemSourceType[newSrcCount];
                node.obtainActions = new ItemObtainAction[newSrcCount];
                node.sourceDescriptions = new string[newSrcCount];
                for (int i = 0; i < Mathf.Min(newSrcCount, srcCount); i++)
                {
                    if (oldSrc != null && i < oldSrc.Length) node.sources[i] = oldSrc[i];
                    if (oldAct != null && i < oldAct.Length) node.obtainActions[i] = oldAct[i];
                    if (oldDesc != null && i < oldDesc.Length) node.sourceDescriptions[i] = oldDesc[i];
                }
            }

            for (int i = 0; i < newSrcCount; i++)
            {
                EditorGUILayout.BeginHorizontal();
                node.sources[i] = (ItemSourceType)EditorGUILayout.EnumPopup("来源", node.sources[i], GUILayout.Width(200));
                node.obtainActions[i] = (ItemObtainAction)EditorGUILayout.EnumPopup("动作", node.obtainActions[i], GUILayout.Width(150));
                if (string.IsNullOrEmpty(node.sourceDescriptions[i]) && node.sources[i] != ItemSourceType.None)
                    node.sourceDescriptions[i] = $"{node.sources[i]}({node.obtainActions[i]})";
                node.sourceDescriptions[i] = EditorGUILayout.TextField(node.sourceDescriptions[i]);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
