using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 4.5: 建筑放置。沿各聚落内部道路两侧排布建筑，按距中心远近分层。
    /// </summary>
    public class SettlementBuildingStage : IGenStage
    {
        public int Order => 40;
        public bool Enabled => false; // 新管线已废弃，改用 BuildingStage

        private const string BuildingsRootName = "WorldGen_Settlements";

        // 建筑最小间距（米）
        private const float BuildingGap = 2f;

        // 沿路采样步长（米）
        private const float RoadSampleStep = 8f;

        // 道路退距（建筑离路边的距离）
        private const float RoadSetback = 3f;

        // 密度参数
        private const float InnerDensity = 0.7f;  // 内圈
        private const float MidDensity = 0.4f;   // 中圈
        private const float OuterDensity = 0.2f; // 外圈

        // 材质缓存
        private static Material _buildingMat;
        private static Shader _buildingShader;

        public void Execute(WorldData data)
        {
            InitMaterial();

            if (data.settlements == null || data.settlements.Count == 0)
            {
                Debug.LogWarning("[SettlementBuildingStage] 没有聚落，跳过建筑放置。");
                return;
            }

            if (data.roads == null || data.roads.Count == 0)
            {
                Debug.LogWarning("[SettlementBuildingStage] 没有道路，跳过建筑放置。");
                return;
            }

            GameObject root = CreateBuildingsRoot(data.parentTransform);
            data.buildings.Clear();

            int globalBldIdx = 0;
            for (int s = 0; s < data.settlements.Count; s++)
            {
                Settlement settlement = data.settlements[s];
                settlement.buildingIndices = new List<int>();

                PlaceBuildingsAlongRoads(settlement, s, data, root.transform, ref globalBldIdx);

                // 回写 buildingIndices
                data.settlements[s] = settlement;
            }

        }

        // ── 沿路排布建筑 ────────────────────────────

        private void PlaceBuildingsAlongRoads(Settlement settlement, int settleIdx,
            WorldData data, Transform parent, ref int globalBldIdx)
        {
            Vector2 center = settlement.center;
            float radius = settlement.radius;
            var placedRects = new List<Rect>();

            // 收集该聚落的内部道路
            var localRoads = new List<Road>();
            foreach (var road in data.roads)
            {
                if (road.settlementId == settleIdx)
                    localRoads.Add(road);
            }

            if (localRoads.Count == 0)
            {
                return;
            }

            // 对每条内部道路，沿路两侧放置建筑
            foreach (var road in localRoads)
            {
                PlaceBuildingsOnRoad(road, settlement, settleIdx, center, radius,
                    data, parent, placedRects, ref globalBldIdx);
            }
        }

        private void PlaceBuildingsOnRoad(Road road, Settlement settlement, int settleIdx,
            Vector2 center, float radius, WorldData data, Transform parent,
            List<Rect> placedRects, ref int globalBldIdx)
        {
            Vector2 roadStart = road.start;
            Vector2 roadEnd = road.end;
            Vector2 roadDir = (roadEnd - roadStart).normalized;
            Vector2 roadPerp = new Vector2(-roadDir.y, roadDir.x);
            float roadLen = Vector2.Distance(roadStart, roadEnd);

            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(roadLen / RoadSampleStep));

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                Vector2 roadPos = Vector2.Lerp(roadStart, roadEnd, t);

                // 到聚落中心距离
                float distToCenter = Vector2.Distance(roadPos, center);

                // 超出聚落范围则跳过
                if (distToCenter > radius) continue;

                // 确定圈层密度
                float density;
                bool isInner;
                if (distToCenter < radius * 0.3f)
                {
                    density = InnerDensity;
                    isInner = true;
                }
                else if (distToCenter < radius * 0.65f)
                {
                    density = MidDensity;
                    isInner = false;
                }
                else
                {
                    density = OuterDensity;
                    isInner = false;
                }

                // 概率跳过（密度越大越密集）
                if (Random.value > density) continue;

                // 道路两侧各尝试放置
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 bldPos = roadPos + roadPerp * (RoadSetback + road.width * 0.5f) * side;
                    PlaceOneBuilding(bldPos, roadDir, side, isInner,
                        settlement, settleIdx, data, parent, placedRects, ref globalBldIdx);
                }
            }
        }

        private void PlaceOneBuilding(Vector2 pos, Vector2 roadDir, int side, bool isInner,
            Settlement settlement, int settleIdx, WorldData data, Transform parent,
            List<Rect> placedRects, ref int globalBldIdx)
        {
            // 对齐 4m 网格
            pos = AlignToGrid(pos);

            SettlementTier tier = settlement.tier;
            BuildingCategory cat = PickCategory(tier, isInner);

            // 品类解锁检查
            if (SettlementConfig.UnlockTier(cat) > tier) return;

            Vector2 sizeRange = SettlementConfig.SizeRange(cat);
            float tierScale = SettlementConfig.TierScale(tier);
            float bw = Random.Range(sizeRange.x, sizeRange.y) * tierScale;
            float bd = Random.Range(sizeRange.x * 0.7f, sizeRange.y) * tierScale;
            bw = Mathf.Max(4f, Mathf.Round(bw / SettlementConfig.GridSize) * SettlementConfig.GridSize);
            bd = Mathf.Max(4f, Mathf.Round(bd / SettlementConfig.GridSize) * SettlementConfig.GridSize);

            Vector2 bSize = new Vector2(bw, bd);

            // 建筑朝向道路（正面朝路）
            // roadDir 是道路方向，建筑正面朝向道路即 垂直于 roadDir
            float rotY = Mathf.Atan2(roadDir.y, roadDir.x) * Mathf.Rad2Deg + 90f * side;
            rotY = Mathf.Round(rotY / 90f) * 90f; // 对齐 90°

            Vector2 halfExt = bSize * 0.5f;
            Rect bRect = new Rect(pos.x - halfExt.x, pos.y - halfExt.y, bSize.x, bSize.y);

            // 碰撞检测
            if (HasOverlap(bRect, placedRects, BuildingGap)) return;

            placedRects.Add(bRect);
            CreateBuilding(cat, pos, bSize, rotY, settlement, settleIdx, data, parent, ref globalBldIdx);
        }

        // ── 品类选取 ────────────────────────────────

        private BuildingCategory PickCategory(SettlementTier tier, bool isInner)
        {
            if (isInner)
            {
                // 内圈 / 路边：商业和服务业为主
                int r = Random.Range(0, 100);
                if (r < 25) return BuildingCategory.House;
                if (r < 45) return BuildingCategory.ConvenienceStore;
                if (r < 65) return BuildingCategory.Restaurant;
                if (r < 80) return BuildingCategory.Clinic;
                if (r < 95) return BuildingCategory.Townhouse;
                return BuildingCategory.Warehouse;
            }

            // 普通圈层
            int roll = Random.Range(0, 100);
            if (roll < 50) return BuildingCategory.House;
            if (roll < 70) return BuildingCategory.Townhouse;
            if (roll < 85) return BuildingCategory.Warehouse;
            return BuildingCategory.Ruins;
        }

        // ── 碰撞检测 ────────────────────────────────

        private bool HasOverlap(Rect test, List<Rect> placed, float gap)
        {
            Rect expanded = new Rect(
                test.x - gap, test.y - gap,
                test.width + gap * 2f, test.height + gap * 2f);

            foreach (var r in placed)
            {
                if (expanded.Overlaps(r)) return true;
            }
            return false;
        }

        // ── 建筑实例化 ──────────────────────────────

        private void CreateBuilding(BuildingCategory cat, Vector2 pos, Vector2 bSize,
            float rot, Settlement settlement, int settleIdx, WorldData data,
            Transform parent, ref int globalBldIdx)
        {
            float spacing = data.chunkSize / data.resolution;
            int vCx = data.worldSize.x * data.resolution + 1;
            int vCz = data.worldSize.y * data.resolution + 1;
            float groundH = SampleHeightAt(pos, data.heightMap, spacing, vCx, vCz);

            float buildingHeight = (cat == BuildingCategory.Supermarket ||
                                    cat == BuildingCategory.PoliceStation)
                ? SettlementConfig.TallBuildingHeight : SettlementConfig.DefaultBuildingHeight;

            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"Bld_{cat}_{globalBldIdx}";
            obj.transform.SetParent(parent);
            obj.transform.position = new Vector3(pos.x, groundH + buildingHeight * 0.5f, pos.y);
            obj.transform.localScale = new Vector3(bSize.x, buildingHeight, bSize.y);
            obj.transform.rotation = Quaternion.Euler(0f, rot, 0f);

            var mr = obj.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(_buildingShader);
            mr.sharedMaterial.color = SettlementConfig.GetCategoryColor(cat);

            data.buildings.Add(new Building
            {
                category = cat,
                position = pos,
                size = bSize,
                heightY = buildingHeight,
                rotation = rot,
                settlementIndex = settleIdx
            });

            settlement.buildingIndices.Add(globalBldIdx);
            globalBldIdx++;
        }

        // ── 工具函数 ────────────────────────────────

        private Vector2 AlignToGrid(Vector2 p)
        {
            float g = SettlementConfig.GridSize;
            return new Vector2(Mathf.Round(p.x / g) * g, Mathf.Round(p.y / g) * g);
        }

        private float SampleHeightAt(Vector2 pos, float[,] heightMap,
            float spacing, int vCx, int vCz)
        {
            if (heightMap == null) return 0f;
            int vx = Mathf.Clamp(Mathf.RoundToInt(pos.x / spacing), 0, vCx - 1);
            int vz = Mathf.Clamp(Mathf.RoundToInt(pos.y / spacing), 0, vCz - 1);
            return heightMap[vx, vz];
        }

        private void InitMaterial()
        {
            if (_buildingShader == null)
            {
                _buildingShader = Shader.Find("Standard");
                if (_buildingShader == null)
                {
                    Debug.LogError("[SettlementBuildingStage] 找不到 Standard shader，建筑将不可见！");
                }
            }
        }

        private GameObject CreateBuildingsRoot(Transform terrainParent)
        {
            GameObject existing = GameObject.Find(BuildingsRootName);
            if (existing) Object.DestroyImmediate(existing);

            GameObject root = new GameObject(BuildingsRootName);
            root.transform.SetParent(terrainParent);
            root.transform.position = Vector3.zero;
            return root;
        }
    }
}
