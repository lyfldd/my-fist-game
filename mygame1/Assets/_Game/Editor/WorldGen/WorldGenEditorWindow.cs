using UnityEditor;
using UnityEngine;
using _Game.Systems.WorldGen;
using _Game.Systems.WorldGen.Core;
using _Game.Systems.WorldGen.Stages;
using _Game.Systems.WorldGen.Data;
using _Game.Core;

namespace _Game.Editor.WorldGen
{
    /// <summary>
    /// Phase 1 一级模块城市生成编辑器预览窗口。
    /// MenuItem: Tools → WorldGen → Open Preview
    /// Pipeline: Seed(10) → CityLayout(20) → ModuleGrid(30) → ModuleAssignment(35) → Mesh(80)
    /// </summary>
    public class WorldGenEditorWindow : EditorWindow
    {
        private const string CityRootName = "WorldGen_City";
        private const string TerrainRootName = "WorldGen_Terrain";

        [MenuItem("Tools/WorldGen/Open Preview")]
        public static void ShowWindow()
        {
            GetWindow<WorldGenEditorWindow>("一级模块城市生成");
        }

        // ── 参数 ──
        private int _seed = 42;

        // ── 状态 ──
        private WorldData _lastData;
        private bool _hasGenerated;
        private Vector2 _scrollPos;

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            GUILayout.Label("程序化城市生成 — 一级模块", EditorStyles.boldLabel);
            GUILayout.Space(8);

            // 种子
            EditorGUILayout.BeginHorizontal();
            _seed = EditorGUILayout.IntField("种子 (Seed)", _seed);
            if (GUILayout.Button("随机", GUILayout.Width(60)))
            {
                _seed = UnityEngine.Random.Range(0, int.MaxValue);
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Pipeline: Seed(10) → CityLayout(20) → ModuleGrid(30) → ModuleAssignment(35) → Mesh(80)\n" +
                "一级模块（120~240m）：商业区 / 住宅密集 / 住宅稀疏 / 工业区 / 郊区 / 道路 / 水域 / 森林\n" +
                "风格: 中心辐射 / 沿路延伸 / 沿河两岸 / 临森林边缘",
                MessageType.Info);

            GUILayout.Space(8);

            // ── 状态栏 ──
            DrawStatusBar();

            GUILayout.Space(8);

            // ── 生成城市 ──
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("★ 生成城市", GUILayout.Height(40)))
            {
                GenerateCity();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(4);

            // ── 清除 ──
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("清除全部", GUILayout.Height(28)))
            {
                ClearAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
        }

        // ── 状态栏 ──

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("状态:", GUILayout.Width(36));
            GUI.color = _hasGenerated ? Color.green : Color.gray;
            GUILayout.Label(_hasGenerated ? "已生成" : "就绪", GUILayout.Width(60));
            GUI.color = Color.white;

            if (_lastData != null && _lastData.moduleGrid != null)
            {
                var counts = ModuleAssignmentStage.GetTypeCounts(
                    _lastData.moduleGrid, _lastData.gridSize);

                GUILayout.Label($"风格={_lastData.cityStyle} | " +
                    $"网格={_lastData.gridSize}x{_lastData.gridSize} | " +
                    $"等级={_lastData.wealthLevel}");

                // 在下一行显示类型统计
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndHorizontal();

            // 类型数量统计
            if (_lastData != null && _lastData.moduleGrid != null)
            {
                var counts = ModuleAssignmentStage.GetTypeCounts(
                    _lastData.moduleGrid, _lastData.gridSize);

                if (counts.Count > 0)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    foreach (var kv in counts)
                    {
                        GUILayout.Label($"{ModuleColors.GetLabel(kv.Key)}:{kv.Value}",
                            GUILayout.Width(72));
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        // ── 参数打包 ──

        private WorldData CreateWorldData()
        {
            // 清除旧对象
            ClearAll();

            GameObject root = new GameObject(CityRootName);
            root.transform.position = Vector3.zero;

            return new WorldData
            {
                seed = _seed,
                worldSize = new Vector2Int(1, 1), // 一级模块不需要 Chunk 系统
                chunkSize = GameConstants.CHUNK_SIZE,
                parentTransform = root.transform,
                spawnPoint = new Vector2(GameConstants.WORLD_SPAWN_X, GameConstants.WORLD_SPAWN_Z)
            };
        }

        // ── 生成 ──

        private void GenerateCity()
        {
            ClearAll();
            WorldData data = CreateWorldData();

            var gen = new WorldGenerator();
            gen.AddStage(new SeedStage());
            gen.AddStage(new CityLayoutStage());
            gen.AddStage(new ModuleGridStage());
            gen.AddStage(new ModuleAssignmentStage());
            gen.AddStage(new MeshStage());

            Debug.Log($"[CityGen] Phase 1 一级模块生成: seed={_seed}");

            gen.Generate(data);

            _lastData = data;
            _hasGenerated = true;

            if (data.parentTransform != null)
                Selection.activeGameObject = data.parentTransform.gameObject;

            // 输出统计
            var counts = ModuleAssignmentStage.GetTypeCounts(data.moduleGrid, data.gridSize);
            Debug.Log($"[CityGen] 完成！风格={data.cityStyle}, 网格={data.gridSize}x{data.gridSize}, " +
                      $"等级={data.wealthLevel}, 类型数={counts.Count}");
        }

        private void ClearAll()
        {
            // 清除新管线根节点
            var cityRoot = GameObject.Find(CityRootName);
            if (cityRoot != null)
            {
                DestroyImmediate(cityRoot);
                Debug.Log("[CityGen] 已清除城市可视化。");
            }

            // 清除旧管线根节点
            var terrainRoot = GameObject.Find(TerrainRootName);
            if (terrainRoot != null)
            {
                DestroyImmediate(terrainRoot);
            }

            // 额外清除可能的遗留子对象
            string[] childNames = { "WorldGen_Roads", "WorldGen_Buildings", "WorldGen_Blocks" };
            foreach (var name in childNames)
            {
                var go = GameObject.Find(name);
                if (go != null) DestroyImmediate(go);
            }

            _lastData = null;
            _hasGenerated = false;
        }
    }
}
