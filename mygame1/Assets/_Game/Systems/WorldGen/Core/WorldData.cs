using UnityEngine;
using System.Collections.Generic;
using _Game.Systems.WorldGen.Data;
using _Game.Core;

namespace _Game.Systems.WorldGen
{
    /// <summary>
    /// 地图生成全局数据层。贯穿整个 Pipeline，每个 Stage 读写自己的数据。
    /// Phase 1 一级模块专用字段 + 旧地形字段（兼容保留）。
    /// </summary>
    public class WorldData
    {
        public int seed;

        // 确定性随机数生成器（Phase 1 一级模块使用 rng）
        public System.Random rng;
        public System.Random random;            // 旧字段，向后兼容

        // ── 世界参数 ──
        public Vector2Int worldSize;            // Chunk 数量 (宽, 高)
        public Transform parentTransform;        // 所有生成对象的父节点
        public float chunkSize = GameConstants.CHUNK_SIZE;
        public int resolution = GameConstants.CHUNK_RESOLUTION;
        public float worldWidth => worldSize.x * chunkSize;
        public float worldHeight => worldSize.y * chunkSize;

        // 出生点
        public Vector2 spawnPoint;              // 玩家出生点 (XZ)

        // ── Phase 1 一级模块数据 ──
        public CityStyle cityStyle;
        public WealthLevel wealthLevel;
        public int gridSize;                    // 基础网格分辨率 (固定 20)
        public float cellSize;                  // 基础格边长 (固定 40m)
        public int nextModuleId;                // 多格模块唯一 ID 分配器
        public Data.GridCell[,] moduleGrid;     // 模块网格

        /// <summary> 各模块类型的目标数量范围 (WFC 配额约束)，CityLayoutStage 设置。 </summary>
        public System.Collections.Generic.Dictionary<Data.ModuleType, (int min, int max)> moduleQuotas;

        // ── 旧城市生成数据（旧管线，已废弃 Stage 使用） ──
        public List<CityDistrict> districts = new();
        public List<CityRoad> cityRoads = new();
        public List<CityBlock> blocks = new();
        public List<CityBuilding> cityBuildings = new();

        // ── 旧地形数据（保留兼容，旧 Stage 已禁用但代码仍引用） ──
        public float[,] heightMap;
        public Color[,] vertexColorMap;

        // 旧道路/聚落/建筑（旧 Stage 使用）
        public List<Road> roads = new();
        public List<Settlement> settlements = new();
        public List<Building> buildings = new();
    }
}
