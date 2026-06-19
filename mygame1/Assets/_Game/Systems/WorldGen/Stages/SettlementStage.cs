using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// [DEPRECATED] 已拆分为 SettlementCenterStage (Order=30) + SettlementBuildingStage (Order=40)。
    /// 保留文件备用，不参与管线。
    /// </summary>
    public class SettlementStage : IGenStage
    {
        public int Order => 50;
        public bool Enabled => false;

        private const string SettlementRootName = "WorldGen_Settlements";

        // 地形扫描步长（米）
        private const float ScanStep = 16f;

        // 平坦度阈值：区域内最大高差 < 此值视为平坦
        private const float MaxFlatDelta = 2.5f;

        // 目标聚落数量
        private const int TargetSettlementCount = 8;

        // 建筑间最小间距（米）
        private const float BuildingGap = 2f;

        // 密度参数
        private const float InnerDensity = 0.6f;  // 内圈密度
        private const float MidDensity = 0.35f;   // 中圈密度
        private const float OuterDensity = 0.15f; // 外圈密度

        // 材质缓存
        private static Material _buildingMat;
        private static Shader _buildingShader;

        public void Execute(WorldData data)
        {
            InitMaterial();

            float worldW = data.worldSize.x * data.chunkSize;
            float worldH = data.worldSize.y * data.chunkSize;
            float spacing = data.chunkSize / data.resolution;

            // 1. 扫描候选区域
            var candidates = ScanFlatRegions(data, worldW, worldH, spacing);

            // 2. 选取聚落中心（含间距约束 + 出生点保护）
            var centers = SelectSettlementCenters(candidates, data, worldW, worldH);

            // 3. 为每个聚落确定形态并放置建筑
            GameObject root = CreateSettlementRoot(data.parentTransform);

            int buildingIndex = 0;
            for (int s = 0; s < centers.Count; s++)
            {
                Settlement settlement = centers[s];
                placementSettlementBuildings(settlement, s, data, root.transform, ref buildingIndex);
                data.settlements.Add(settlement);
            }
        }

        // ── 地形扫描 ────────────────────────────────

        private struct Candidate
        {
            public Vector2 center;
            public float flatnessScore; // 越低越平坦
        }

        private List<Candidate> ScanFlatRegions(WorldData data, float worldW, float worldH, float spacing)
        {
            var candidates = new List<Candidate>();
            int vCx = data.worldSize.x * data.resolution + 1;
            int vCz = data.worldSize.y * data.resolution + 1;

            // 扫描半径（检查 Patch 大小 = Medium 聚落的检查范围）
            float checkRadius = SettlementConfig.MediumSettlementDia * 0.5f;

            for (float cx = checkRadius; cx < worldW - checkRadius; cx += ScanStep)
            {
                for (float cz = checkRadius; cz < worldH - checkRadius; cz += ScanStep)
                {
                    float score = EvaluateFlatness(cx, cz, checkRadius, data.heightMap, spacing, vCx, vCz);
                    if (score < MaxFlatDelta)
                        candidates.Add(new Candidate { center = new Vector2(cx, cz), flatnessScore = score });
                }
            }

            // 按平坦度排序（最平坦优先）
            candidates.Sort((a, b) => a.flatnessScore.CompareTo(b.flatnessScore));
            return candidates;
        }

        private float EvaluateFlatness(float cx, float cz, float radius,
            float[,] heightMap, float spacing, int vCx, int vCz)
        {
            if (heightMap == null) return 0f;

            int samples = 8; // 圆形采样点数
            float minH = float.MaxValue, maxH = float.MinValue;

            for (int i = 0; i < samples; i++)
            {
                float angle = (float)i / samples * Mathf.PI * 2f;
                float sx = cx + Mathf.Cos(angle) * radius * 0.6f;
                float sz = cz + Mathf.Sin(angle) * radius * 0.6f;

                int vx = Mathf.Clamp(Mathf.RoundToInt(sx / spacing), 0, vCx - 1);
                int vz = Mathf.Clamp(Mathf.RoundToInt(sz / spacing), 0, vCz - 1);

                float h = heightMap[vx, vz];

                // 水域直接跳过
                if (h < -0.3f) return float.MaxValue;

                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }

            return maxH - minH;
        }

        // ── 聚落中心选取 ────────────────────────────

        private List<Settlement> SelectSettlementCenters(List<Candidate> candidates,
            WorldData data, float worldW, float worldH)
        {
            var settlements = new List<Settlement>();
            var centersPlaced = new List<Vector2>();
            var sizesPlaced = new List<SettlementSize>();

            // 大小计数器
            int smallCount = 0, mediumCount = 0, largeCount = 0;
            // 等级计数器
            int shabbyCount = 0, normalCount = 0, prosperCount = 0;

            foreach (var cand in candidates)
            {
                if (settlements.Count >= TargetSettlementCount) break;

                // 分配大小
                SettlementSize size = AssignSize(settlements.Count, TargetSettlementCount, ref smallCount, ref mediumCount, ref largeCount);

                // 间距检查
                float minSpacing = GetMinSpacing(size);
                bool tooClose = false;
                for (int p = 0; p < centersPlaced.Count; p++)
                {
                    // 双向检查
                    float myMin = GetMinSpacing(size);
                    float theirMin = GetMinSpacing(sizesPlaced[p]);
                    float effectiveSpacing = Mathf.Max(myMin, theirMin);
                    if (Vector2.Distance(cand.center, centersPlaced[p]) < effectiveSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                // 出生点保护：500m 内只放小型
                float distToSpawn = Vector2.Distance(cand.center, data.spawnPoint);
                if (distToSpawn < SettlementConfig.SpawnProtectRadius && size != SettlementSize.Small)
                    continue;

                // 分配等级
                SettlementTier tier = AssignTier(settlements.Count, TargetSettlementCount, ref shabbyCount, ref normalCount, ref prosperCount);

                float diameter = GetDiameter(size);

                // 确定形态（Phase 2 简化：只用中心辐射和沿路延伸）
                SettlementMorph morph = DetermineMorph(cand.center, size, data, diameter);

                settlements.Add(new Settlement
                {
                    center = cand.center,
                    size = size,
                    tier = tier,
                    morph = morph,
                    diameter = diameter,
                    buildingIndices = new List<int>()
                });

                centersPlaced.Add(cand.center);
                sizesPlaced.Add(size);
            }

            return settlements;
        }

        private SettlementSize AssignSize(int idx, int total,
            ref int small, ref int medium, ref int large)
        {
            float ratioSmall = 0.6f, ratioMed = 0.3f;
            int targetSmall = Mathf.Max(1, Mathf.RoundToInt(total * ratioSmall));
            int targetMed   = Mathf.Max(1, Mathf.RoundToInt(total * ratioMed));

            if (small < targetSmall)   { small++;   return SettlementSize.Small; }
            if (medium < targetMed)    { medium++;  return SettlementSize.Medium; }
            large++;
            return SettlementSize.Large;
        }

        private SettlementTier AssignTier(int idx, int total,
            ref int shabby, ref int normal, ref int prosper)
        {
            int targetShabby  = Mathf.Max(1, Mathf.RoundToInt(total * 0.4f));
            int targetNormal  = Mathf.Max(1, Mathf.RoundToInt(total * 0.4f));

            if (shabby < targetShabby)  { shabby++;  return SettlementTier.Shabby; }
            if (normal < targetNormal)  { normal++;  return SettlementTier.Normal; }
            prosper++;
            return SettlementTier.Prosperous;
        }

        private float GetMinSpacing(SettlementSize size)
        {
            switch (size)
            {
                case SettlementSize.Small:  return SettlementConfig.SmallSpacing;
                case SettlementSize.Medium: return SettlementConfig.MediumSpacing;
                case SettlementSize.Large:  return SettlementConfig.LargeSpacing;
                default: return SettlementConfig.SmallSpacing;
            }
        }

        private float GetDiameter(SettlementSize size)
        {
            switch (size)
            {
                case SettlementSize.Small:  return SettlementConfig.SmallSettlementDia;
                case SettlementSize.Medium: return SettlementConfig.MediumSettlementDia;
                case SettlementSize.Large:  return SettlementConfig.LargeSettlementDia;
                default: return SettlementConfig.SmallSettlementDia;
            }
        }

        // ── 形态判定 ────────────────────────────────

        private SettlementMorph DetermineMorph(Vector2 center, SettlementSize size,
            WorldData data, float diameter)
        {
            // Phase 2 简化：沿河/依山暂不检测，优先沿路→中心辐射
            // 检查是否有主干道穿过
            float checkDist = diameter * 0.6f;
            bool hasRoad = false;
            foreach (var road in data.roads)
            {
                if (road.roadType != RoadType.MainRoad || road.settlementId != -1) continue;
                float d = PointToSegmentDist(center, road.start, road.end);
                if (d < checkDist)
                {
                    hasRoad = true;
                    break;
                }
            }

            if (hasRoad) return SettlementMorph.RoadStretch;
            return SettlementMorph.CenterRadiate;
        }

        private float PointToSegmentDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f) return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            Vector2 proj = a + t * ab;
            return Vector2.Distance(p, proj);
        }

        // ── 建筑放置 ────────────────────────────────

        private void placementSettlementBuildings(Settlement settlement, int settleIdx,
            WorldData data, Transform parent, ref int globalBldIdx)
        {
            float radius = settlement.radius;
            Vector2 center = settlement.center;
            var placedRects = new List<Rect>(); // 碰撞检测用

            // 按形态分发布局
            switch (settlement.morph)
            {
                case SettlementMorph.CenterRadiate:
                    PlaceCenterRadiate(settlement, settleIdx, data, parent, placedRects, ref globalBldIdx);
                    break;
                case SettlementMorph.RoadStretch:
                    PlaceRoadStretch(settlement, settleIdx, data, parent, placedRects, ref globalBldIdx);
                    break;
                default:
                    PlaceCenterRadiate(settlement, settleIdx, data, parent, placedRects, ref globalBldIdx);
                    break;
            }
        }

        // ── 中心辐射型布局 ──────────────────────────

        private void PlaceCenterRadiate(Settlement settlement, int settleIdx,
            WorldData data, Transform parent, List<Rect> placedRects, ref int globalBldIdx)
        {
            float radius = settlement.radius;
            Vector2 center = settlement.center;
            SettlementTier tier = settlement.tier;

            // 三个圈层
            float innerR = radius * 0.3f;
            float midR   = radius * 0.65f;
            float outerR = radius;

            // 内圈：地标建筑
            TryPlaceRing(center, 0f, innerR, InnerDensity, tier, true,
                settlement, settleIdx, data, parent, placedRects, ref globalBldIdx);

            // 中圈：民宅 + 普通建筑
            TryPlaceRing(center, innerR, midR, MidDensity, tier, false,
                settlement, settleIdx, data, parent, placedRects, ref globalBldIdx);

            // 外圈：废墟 + 稀疏建筑
            TryPlaceRing(center, midR, outerR, OuterDensity, tier, false,
                settlement, settleIdx, data, parent, placedRects, ref globalBldIdx);
        }

        private void TryPlaceRing(Vector2 center, float minR, float maxR, float density,
            SettlementTier tier, bool isLandmarkRing,
            Settlement settlement, int settleIdx, WorldData data, Transform parent,
            List<Rect> placedRects, ref int globalBldIdx)
        {
            float ringWidth = maxR - minR;
            if (ringWidth < 4f) ringWidth = 4f;

            int attempts = Mathf.CeilToInt(ringWidth * maxR * Mathf.PI * density / (SettlementConfig.GridSize * SettlementConfig.GridSize));
            attempts = Mathf.Min(attempts, 40); // 上限

            for (int a = 0; a < attempts; a++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(minR, maxR);
                Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                // 对齐4m网格
                pos = AlignToGrid(pos);

                BuildingCategory cat = PickCategory(tier, isLandmarkRing);

                // 品类是否解锁
                if (SettlementConfig.UnlockTier(cat) > tier) continue;

                Vector2 sizeRange = SettlementConfig.SizeRange(cat);
                float tierScale = SettlementConfig.TierScale(tier);
                float bw = Random.Range(sizeRange.x, sizeRange.y) * tierScale;
                float bd = Random.Range(sizeRange.x * 0.7f, sizeRange.y) * tierScale;

                // 对齐4m
                bw = Mathf.Max(4f, Mathf.Round(bw / SettlementConfig.GridSize) * SettlementConfig.GridSize);
                bd = Mathf.Max(4f, Mathf.Round(bd / SettlementConfig.GridSize) * SettlementConfig.GridSize);

                Vector2 bSize = new Vector2(bw, bd);
                float rot = Random.Range(0f, 4f) * 90f;

                // 检查是否在聚落范围内（建筑边缘不超出）
                Vector2 halfExt = bSize * 0.5f;
                if (Vector2.Distance(pos + halfExt, center) > maxR ||
                    Vector2.Distance(pos - halfExt, center) > maxR)
                    continue;

                Rect bRect = new Rect(pos.x - halfExt.x, pos.y - halfExt.y, bSize.x, bSize.y);

                // 碰撞检测（带间距）
                if (HasOverlap(bRect, placedRects, BuildingGap)) continue;

                // 放置建筑
                placedRects.Add(bRect);
                CreateBuilding(cat, pos, bSize, rot, settlement, settleIdx, data, parent, ref globalBldIdx);
            }
        }

        // ── 沿路延伸型布局 ──────────────────────────

        private void PlaceRoadStretch(Settlement settlement, int settleIdx,
            WorldData data, Transform parent, List<Rect> placedRects, ref int globalBldIdx)
        {
            float radius = settlement.radius;
            Vector2 center = settlement.center;

            // 找最近主干道，确定带状方向
            Vector2 roadDir = Vector2.right; // 默认
            float bestDist = float.MaxValue;
            Vector2 bestStart = Vector2.zero, bestEnd = Vector2.zero;
            foreach (var road in data.roads)
            {
                if (road.roadType != RoadType.MainRoad || road.settlementId != -1) continue;
                float d = PointToSegmentDist(center, road.start, road.end);
                if (d < bestDist)
                {
                    bestDist = d;
                    roadDir = (road.end - road.start).normalized;
                    bestStart = road.start;
                    bestEnd = road.end;
                }
            }

            Vector2 perpDir = new Vector2(-roadDir.y, roadDir.x);

            // 沿路放置建筑
            int bandCount = 6; // 道路每侧 3 列
            float bandSpacing = radius / (bandCount / 2);

            SettlementTier tier = settlement.tier;

            for (int side = -1; side <= 1; side += 2) // 道路两侧
            {
                for (int b = 1; b <= bandCount / 2; b++)
                {
                    float offset = b * bandSpacing;
                    float density = b == 1 ? InnerDensity : (b <= bandCount / 4 ? MidDensity : OuterDensity);

                    int attempts = Mathf.CeilToInt(radius * 2f / SettlementConfig.GridSize * density);
                    for (int a = 0; a < attempts; a++)
                    {
                        float along = Random.Range(-radius, radius);
                        Vector2 pos = center + roadDir * along + perpDir * offset * side;
                        pos = AlignToGrid(pos);

                        if (Vector2.Distance(pos, center) > radius) continue;

                        bool isRoadside = (b == 1);
                        BuildingCategory cat = PickCategory(tier, isRoadside);
                        if (SettlementConfig.UnlockTier(cat) > tier) continue;

                        Vector2 sizeRange = SettlementConfig.SizeRange(cat);
                        float tierScale = SettlementConfig.TierScale(tier);
                        float bw = Random.Range(sizeRange.x, sizeRange.y) * tierScale;
                        float bd = Random.Range(sizeRange.x * 0.7f, sizeRange.y) * tierScale;
                        bw = Mathf.Max(4f, Mathf.Round(bw / SettlementConfig.GridSize) * SettlementConfig.GridSize);
                        bd = Mathf.Max(4f, Mathf.Round(bd / SettlementConfig.GridSize) * SettlementConfig.GridSize);

                        Vector2 bSize = new Vector2(bw, bd);
                        float rot = Random.Range(0f, 4f) * 90f;

                        Vector2 halfExt = bSize * 0.5f;
                        Rect bRect = new Rect(pos.x - halfExt.x, pos.y - halfExt.y, bSize.x, bSize.y);
                        if (HasOverlap(bRect, placedRects, BuildingGap)) continue;

                        placedRects.Add(bRect);
                        CreateBuilding(cat, pos, bSize, rot, settlement, settleIdx, data, parent, ref globalBldIdx);
                    }
                }
            }
        }

        // ── 品类选取 ────────────────────────────────

        private BuildingCategory PickCategory(SettlementTier tier, bool isLandmark)
        {
            if (isLandmark)
            {
                // 地标区放置特殊建筑
                int r = Random.Range(0, 4);
                switch (r)
                {
                    case 0: return BuildingCategory.Restaurant;
                    case 1: return BuildingCategory.ConvenienceStore;
                    case 2: return BuildingCategory.Clinic;
                    case 3: return BuildingCategory.Warehouse;
                }
            }

            // 普通区：民宅为主，偶尔废墟
            int roll = Random.Range(0, 100);
            if (roll < 50) return BuildingCategory.House;
            if (roll < 75) return BuildingCategory.Townhouse;
            if (roll < 90) return BuildingCategory.Warehouse;
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

            // Cube 实例
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"Bld_{cat}_{globalBldIdx}";
            obj.transform.SetParent(parent);

            obj.transform.position = new Vector3(pos.x, groundH + buildingHeight * 0.5f, pos.y);
            obj.transform.localScale = new Vector3(bSize.x, buildingHeight, bSize.y);
            obj.transform.rotation = Quaternion.Euler(0f, rot, 0f);

            // 材质颜色
            var mr = obj.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(_buildingShader);
            mr.sharedMaterial.color = SettlementConfig.GetCategoryColor(cat);

            // 写数据
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
            }
        }

        private GameObject CreateSettlementRoot(Transform terrainParent)
        {
            GameObject existing = GameObject.Find(SettlementRootName);
            if (existing) Object.DestroyImmediate(existing);

            GameObject root = new GameObject(SettlementRootName);
            root.transform.SetParent(terrainParent);
            root.transform.position = Vector3.zero;
            return root;
        }
    }
}
