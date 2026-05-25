using UnityEngine;
using _Game.Systems.WorldGen.Data;
using _Game.Core;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 5 (Order=80): 多单元模块可视化。
    /// 只渲染锚点格，Quad/Cube 按模块实际尺寸缩放。
    /// 纯色半透明平面 + 道路灰色 Cube + 文字标签。
    /// </summary>
    public class MeshStage : IGenStage
    {
        public int Order => 80;
        public bool Enabled => true;

        private const string ROOT_NAME = "WorldGen_City";

        private static Shader _planeShader;
        private static Shader _roadShader;

        public void Execute(WorldData data)
        {
            ClearPrevious();
            InitShaders();

            var root = new GameObject(ROOT_NAME);
            root.transform.position = Vector3.zero;

            int gs = data.gridSize;
            float baseUnit = GameConstants.MODULE_BASE_UNIT;

            int moduleCount = 0;

            for (int x = 0; x < gs; x++)
            {
                for (int z = 0; z < gs; z++)
                {
                    var cell = data.moduleGrid[x, z];

                    // 只渲染锚点格（每个多格模块只渲染一次）
                    if (!cell.IsAnchor) continue;
                    if (cell.type == ModuleType.Unknown || cell.type == ModuleType.Empty)
                        continue;

                    CreateModuleVisual(root.transform, cell, baseUnit);
                    moduleCount++;
                }
            }

            Debug.Log($"[MeshStage] 可视化完成：{moduleCount} 个模块, 根节点: {ROOT_NAME}");
        }

        // ── 单个模块可视化 ──

        private void CreateModuleVisual(Transform parent, GridCell cell, float baseUnit)
        {
            float mw = cell.moduleWidth;
            float mh = cell.moduleHeight;

            // Footprint 中心世界坐标
            float cx = cell.gridPos.x;
            float cz = cell.gridPos.y;
            float worldX = (cx + mw * 0.5f) * baseUnit;
            float worldZ = (cz + mh * 0.5f) * baseUnit;

            // 模块实际世界尺寸
            float sizeX = mw * baseUnit * GameConstants.MESH_PLANE_SCALE_FACTOR;
            float sizeZ = mh * baseUnit * GameConstants.MESH_PLANE_SCALE_FACTOR;

            bool isRoad = cell.type is ModuleType.Road
                or ModuleType.RoadTee
                or ModuleType.RoadCross;

            // ── 区域平面（平躺 Quad） ──
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = $"{ModuleColors.GetLabel(cell.type)}_{cx}_{cz}";
            plane.transform.SetParent(parent);
            plane.transform.position = new Vector3(worldX, GameConstants.MESH_PLANE_Y_OFFSET, worldZ);
            plane.transform.localScale = new Vector3(sizeX, sizeZ, 1f);
            plane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var renderer = plane.GetComponent<Renderer>();
            var mat = new Material(_planeShader);
            mat.color = ModuleColors.GetColor(cell.type);
            renderer.material = mat;

            // ── 道路额外叠加 Cube ──
            if (isRoad)
            {
                CreateRoadVisual(parent, cell, baseUnit, worldX, worldZ);
            }

            // ── 文字标签 ──
            CreateLabel(parent, cell, baseUnit, worldX, worldZ);
        }

        /// <summary>
        /// 道路可视化：方向感知拉伸。
        /// 横向路 (mw > mh) → X 方向拉长，纵向路 (mh > mw) → Z 方向拉长。
        /// </summary>
        private void CreateRoadVisual(Transform parent, GridCell cell, float baseUnit,
            float worldX, float worldZ)
        {
            float mw = cell.moduleWidth;
            float mh = cell.moduleHeight;

            float roadWidth, roadLength;
            if (mw > mh)
            {
                // 横向路
                roadLength = mw * baseUnit * GameConstants.MESH_ROAD_WIDTH_FACTOR;
                roadWidth = mh * baseUnit * GameConstants.MESH_ROAD_WIDTH_FACTOR;
            }
            else if (mh > mw)
            {
                // 纵向路
                roadLength = mh * baseUnit * GameConstants.MESH_ROAD_WIDTH_FACTOR;
                roadWidth = mw * baseUnit * GameConstants.MESH_ROAD_WIDTH_FACTOR;
            }
            else
            {
                // 正方形 (1×1 路口等)
                roadLength = mw * baseUnit * GameConstants.MESH_ROAD_WIDTH_FACTOR;
                roadWidth = mh * baseUnit * GameConstants.MESH_ROAD_WIDTH_FACTOR;
            }

            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = $"{ModuleColors.GetLabel(cell.type)}_block_{(int)cell.gridPos.x}_{(int)cell.gridPos.y}";
            road.transform.SetParent(parent);
            road.transform.position = new Vector3(worldX, GameConstants.MESH_ROAD_Y_POS, worldZ);
            // Cube 默认是 1×1×1, X=长, Z=宽 (在 Y-up 坐标系中)
            road.transform.localScale = new Vector3(roadLength, GameConstants.MESH_ROAD_THICKNESS, roadWidth);

            var roadRenderer = road.GetComponent<Renderer>();
            var roadMat = new Material(_roadShader);
            roadMat.color = ModuleColors.GetColor(cell.type);
            roadRenderer.material = roadMat;
        }

        private void CreateLabel(Transform parent, GridCell cell, float baseUnit,
            float worldX, float worldZ)
        {
            var labelGo = new GameObject($"Label_{(int)cell.gridPos.x}_{(int)cell.gridPos.y}");
            labelGo.transform.SetParent(parent);
            labelGo.transform.position = new Vector3(worldX, GameConstants.MESH_LABEL_Y_OFFSET, worldZ);
            labelGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 按模块最大边长缩放字体
            float maxDim = Mathf.Max(cell.moduleWidth, cell.moduleHeight) * baseUnit;

            var textMesh = labelGo.AddComponent<TextMesh>();
            textMesh.text = ModuleColors.GetLabel(cell.type);
            textMesh.fontSize = Mathf.RoundToInt(maxDim * GameConstants.MESH_LABEL_FONT_MULTIPLIER);
            textMesh.characterSize = 1f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.black;
            textMesh.fontStyle = FontStyle.Bold;
        }

        // ── Shader 初始化 ──

        private static void InitShaders()
        {
            if (_planeShader == null)
            {
                _planeShader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
                if (_planeShader == null)
                {
                    _planeShader = Shader.Find("Standard");
                    Debug.LogWarning("[MeshStage] 透明 Shader 未找到，使用 Standard（可能不透明）");
                }
            }

            if (_roadShader == null)
            {
                _roadShader = Shader.Find("Standard");
            }
        }

        // ── 清除 ──

        private static void ClearPrevious()
        {
            var old = GameObject.Find(ROOT_NAME);
            if (old != null)
            {
                Object.DestroyImmediate(old);
                Debug.Log("[MeshStage] 已清除旧城市可视化。");
            }
        }
    }
}
