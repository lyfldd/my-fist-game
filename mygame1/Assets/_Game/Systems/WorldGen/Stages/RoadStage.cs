using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 4: 路网生成。基于聚落中心构建主干道路网 + 各聚落内部路网。
    /// </summary>
    public class RoadStage : IGenStage
    {
        public int Order => 35;
        public bool Enabled => false; // 新管线已废弃，改用 MainRoadStage

        private const string RoadsRootName = "WorldGen_Roads";

        // 道路段厚度
        private const float RoadThickness = 0.05f;

        // 蜿蜒参数
        private const float WobbleScale = 0.12f;
        private const float WobbleAmp = 3f;

        // 地形过滤阈值
        private const float MaxSlopeAngle = 30f;
        private const float MinHeightForWater = -0.5f;

        // 材质缓存
        private static Material _mainRoadMat;
        private static Material _branchRoadMat;
        private static Material _pathMat;
        private static Shader _roadShader;

        public void Execute(WorldData data)
        {
            InitMaterials();

            if (data.settlements == null || data.settlements.Count < 2)
            {
                Debug.LogWarning("[RoadStage] 聚落不足，跳过路网生成。");
                return;
            }

            // 1. 创建道路父节点
            GameObject roadsRoot = CreateRoadsRoot(data.parentTransform);
            data.roads.Clear();

            // 2. 跨聚落主干道
            BuildInterSettlementRoads(data, roadsRoot.transform);

            // 3. 各聚落内部路网
            for (int s = 0; s < data.settlements.Count; s++)
            {
                BuildSettlementInternalRoads(data.settlements[s], s, data, roadsRoot.transform);
            }

        }

        // ── 材质 ────────────────────────────────────

        private void InitMaterials()
        {
            if (_roadShader == null)
            {
                _roadShader = Shader.Find("Standard");
            }
            if (_mainRoadMat == null)
            {
                _mainRoadMat = new Material(_roadShader) { color = new Color(0.2f, 0.2f, 0.2f) };
            }
            if (_branchRoadMat == null)
            {
                _branchRoadMat = new Material(_roadShader) { color = new Color(0.35f, 0.32f, 0.28f) };
            }
            if (_pathMat == null)
            {
                _pathMat = new Material(_roadShader) { color = new Color(0.5f, 0.45f, 0.4f) };
            }
        }

        // ── 跨聚落主干道 ────────────────────────────

        private struct Edge
        {
            public int i, j;
            public float dist;
        }

        private void BuildInterSettlementRoads(WorldData data, Transform parent)
        {
            int count = data.settlements.Count;
            var edges = GenerateCandidateEdges(data.settlements, count);
            var validEdges = FilterEdges(edges, data);

            // 度数统计
            int[] degree = new int[count];
            foreach (var e in validEdges) { degree[e.i]++; degree[e.j]++; }

            foreach (var e in validEdges)
            {
                bool isMain = (degree[e.i] >= 2 && degree[e.j] >= 2);
                float width = isMain ? SettlementConfig.MainRoadWidth : SettlementConfig.SideRoadWidth;
                Vector2 start = data.settlements[e.i].center;
                Vector2 end = data.settlements[e.j].center;

                BuildRoadSegment(start, end, width, isMain ? RoadType.MainRoad : RoadType.BranchRoad,
                    -1, data, parent);

                data.roads.Add(new Road
                {
                    start = start,
                    end = end,
                    width = width,
                    roadType = isMain ? RoadType.MainRoad : RoadType.BranchRoad,
                    settlementId = -1
                });
            }
        }

        private List<Edge> GenerateCandidateEdges(List<Settlement> settlements, int count)
        {
            var edges = new List<Edge>();
            var added = new HashSet<long>();

            for (int i = 0; i < count; i++)
            {
                var neighbors = new List<(int idx, float dist)>();
                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    float d = Vector2.SqrMagnitude(settlements[i].center - settlements[j].center);
                    neighbors.Add((j, d));
                }
                neighbors.Sort((a, b) => a.dist.CompareTo(b.dist));

                int connectCount = Mathf.Min(3, neighbors.Count);
                for (int k = 0; k < connectCount; k++)
                {
                    int j = neighbors[k].idx;
                    long key = MakeEdgeKey(i, j);
                    if (!added.Add(key)) continue;

                    edges.Add(new Edge { i = i, j = j, dist = Mathf.Sqrt(neighbors[k].dist) });
                }
            }
            return edges;
        }

        private long MakeEdgeKey(int a, int b)
        {
            return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        }

        private List<Edge> FilterEdges(List<Edge> edges, WorldData data)
        {
            var valid = new List<Edge>();
            if (data.heightMap == null)
            {
                valid.AddRange(edges);
                return valid;
            }

            int vCx = data.worldSize.x * data.resolution + 1;
            int vCz = data.worldSize.y * data.resolution + 1;
            float sp = data.chunkSize / data.resolution;
            float maxSlope = Mathf.Tan(MaxSlopeAngle * Mathf.Deg2Rad);

            foreach (var e in edges)
            {
                Vector2 a = data.settlements[e.i].center;
                Vector2 b = data.settlements[e.j].center;
                float len = Vector2.Distance(a, b);
                int samples = Mathf.Max(4, Mathf.CeilToInt(len / (sp * 2f)));

                bool buildable = true;
                for (int s = 0; s <= samples && buildable; s++)
                {
                    float t = (float)s / samples;
                    Vector2 p = Vector2.Lerp(a, b, t);
                    int vx = Mathf.Clamp(Mathf.RoundToInt(p.x / sp), 0, vCx - 1);
                    int vz = Mathf.Clamp(Mathf.RoundToInt(p.y / sp), 0, vCz - 1);
                    float h = data.heightMap[vx, vz];

                    if (h < MinHeightForWater) { buildable = false; break; }

                    if (s > 0)
                    {
                        float prevT = (float)(s - 1) / samples;
                        Vector2 prevP = Vector2.Lerp(a, b, prevT);
                        int pvx = Mathf.Clamp(Mathf.RoundToInt(prevP.x / sp), 0, vCx - 1);
                        int pvz = Mathf.Clamp(Mathf.RoundToInt(prevP.y / sp), 0, vCz - 1);
                        float prevH = data.heightMap[pvx, pvz];
                        float horiz = Vector2.Distance(p, prevP);
                        if (horiz > 0.001f && Mathf.Abs(h - prevH) / horiz > maxSlope)
                            buildable = false;
                    }
                }

                if (buildable) valid.Add(e);
            }
            return valid;
        }

        // ── 聚落内部路网 ────────────────────────────

        private void BuildSettlementInternalRoads(Settlement settlement, int settleIdx,
            WorldData data, Transform parent)
        {
            Vector2 center = settlement.center;
            float radius = settlement.radius;

            switch (settlement.size)
            {
                case SettlementSize.Small:
                    BuildSmallRoads(settlement, settleIdx, data, parent);
                    break;
                case SettlementSize.Medium:
                    BuildMediumRoads(settlement, settleIdx, data, parent);
                    break;
                case SettlementSize.Large:
                    BuildLargeRoads(settlement, settleIdx, data, parent);
                    break;
            }
        }

        /// <summary>小型聚落：1 条穿城主路 + 2~4 支路</summary>
        private void BuildSmallRoads(Settlement settlement, int settleIdx,
            WorldData data, Transform parent)
        {
            Vector2 center = settlement.center;
            float radius = settlement.radius;

            // 确定主轴方向
            Vector2 mainDir = GetMainAxis(settlement, data);

            // 穿城主路
            Vector2 s0 = center - mainDir * radius;
            Vector2 s1 = center + mainDir * radius;
            BuildRoadSegment(s0, s1, SettlementConfig.MainRoadWidth, RoadType.MainRoad,
                settleIdx, data, parent);
            AddRoadData(s0, s1, SettlementConfig.MainRoadWidth, RoadType.MainRoad, settleIdx, data);

            // 2~4 条垂直支路
            Vector2 perp = new Vector2(-mainDir.y, mainDir.x);
            int branchCount = Random.Range(2, 5);
            for (int b = 0; b < branchCount; b++)
            {
                float along = Random.Range(-radius * 0.6f, radius * 0.6f);
                Vector2 bCenter = center + mainDir * along;
                Vector2 b0 = bCenter - perp * radius * 0.5f;
                Vector2 b1 = bCenter + perp * radius * 0.5f;
                BuildRoadSegment(b0, b1, SettlementConfig.SideRoadWidth, RoadType.BranchRoad,
                    settleIdx, data, parent);
                AddRoadData(b0, b1, SettlementConfig.SideRoadWidth, RoadType.BranchRoad,
                    settleIdx, data);
            }
        }

        /// <summary>中型聚落：十字主路 + 网格支路 + 小路</summary>
        private void BuildMediumRoads(Settlement settlement, int settleIdx,
            WorldData data, Transform parent)
        {
            Vector2 center = settlement.center;
            float radius = settlement.radius;

            Vector2 mainDir = GetMainAxis(settlement, data);
            Vector2 perp = new Vector2(-mainDir.y, mainDir.x);

            // 十字主路
            Vector2 h0 = center - mainDir * radius;
            Vector2 h1 = center + mainDir * radius;
            BuildRoadSegment(h0, h1, SettlementConfig.MainRoadWidth, RoadType.MainRoad,
                settleIdx, data, parent);
            AddRoadData(h0, h1, SettlementConfig.MainRoadWidth, RoadType.MainRoad,
                settleIdx, data);

            Vector2 v0 = center - perp * radius;
            Vector2 v1 = center + perp * radius;
            BuildRoadSegment(v0, v1, SettlementConfig.MainRoadWidth, RoadType.MainRoad,
                settleIdx, data, parent);
            AddRoadData(v0, v1, SettlementConfig.MainRoadWidth, RoadType.MainRoad,
                settleIdx, data);

            // 网格支路（平行于主轴，间距 32~48m）
            int branchCount = Mathf.CeilToInt(radius * 2f / 40f);
            for (int i = 1; i <= branchCount; i++)
            {
                float offset = i * 40f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float along = offset * side;
                    if (Mathf.Abs(along) > radius) continue;
                    Vector2 bCenter = center + perp * along;
                    Vector2 b0 = bCenter - mainDir * radius * 0.8f;
                    Vector2 b1 = bCenter + mainDir * radius * 0.8f;
                    BuildRoadSegment(b0, b1, SettlementConfig.SideRoadWidth, RoadType.BranchRoad,
                        settleIdx, data, parent);
                    AddRoadData(b0, b1, SettlementConfig.SideRoadWidth, RoadType.BranchRoad,
                        settleIdx, data);
                }
            }

            // 小路（垂直于支路，间距 20~32m）
            int pathCount = Mathf.CeilToInt(radius * 2f / 28f);
            for (int i = 1; i <= pathCount; i++)
            {
                float offset = i * 28f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float along = offset * side;
                    if (Mathf.Abs(along) > radius) continue;
                    Vector2 pCenter = center + mainDir * along;
                    Vector2 p0 = pCenter - perp * radius * 0.6f;
                    Vector2 p1 = pCenter + perp * radius * 0.6f;
                    float pathWidth = SettlementConfig.SideRoadWidth * 0.75f;
                    BuildRoadSegment(p0, p1, pathWidth, RoadType.Path,
                        settleIdx, data, parent);
                    AddRoadData(p0, p1, pathWidth, RoadType.Path,
                        settleIdx, data);
                }
            }
        }

        /// <summary>大型聚落：井字主路（2×2 网格）+ 密集支路 + 小路</summary>
        private void BuildLargeRoads(Settlement settlement, int settleIdx,
            WorldData data, Transform parent)
        {
            Vector2 center = settlement.center;
            float radius = settlement.radius;

            Vector2 mainDir = GetMainAxis(settlement, data);
            Vector2 perp = new Vector2(-mainDir.y, mainDir.x);

            // 井字网格：2 条水平 + 2 条垂直主路
            float gridStep = radius * 0.65f; // 两条平行主路的间距

            // 水平方向 2 条
            for (int h = -1; h <= 1; h += 2)
            {
                float offset = gridStep * 0.5f * h;
                Vector2 hCenter = center + perp * offset;
                Vector2 h0 = hCenter - mainDir * radius;
                Vector2 h1 = hCenter + mainDir * radius;
                BuildRoadSegment(h0, h1, SettlementConfig.MainRoadWidth, RoadType.MainRoad,
                    settleIdx, data, parent);
                AddRoadData(h0, h1, SettlementConfig.MainRoadWidth, RoadType.MainRoad,
                    settleIdx, data);
            }

            // 垂直方向 2 条
            for (int v = -1; v <= 1; v += 2)
            {
                float offset = gridStep * 0.5f * v;
                Vector2 vCenter = center + mainDir * offset;
                Vector2 v0 = vCenter - perp * radius;
                Vector2 v1 = vCenter + perp * radius;
                BuildRoadSegment(v0, v1, SettlementConfig.MainRoadWidth, RoadType.MainRoad,
                    settleIdx, data, parent);
                AddRoadData(v0, v1, SettlementConfig.MainRoadWidth, RoadType.MainRoad,
                    settleIdx, data);
            }

            // 密集支路（平行于主轴，间距 24~36m）
            int branchCount = Mathf.CeilToInt(radius * 2f / 32f);
            for (int i = 1; i <= branchCount; i++)
            {
                float offset = i * 32f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float along = offset * side;
                    if (Mathf.Abs(along) > radius) continue;
                    Vector2 bCenter = center + perp * along;
                    Vector2 b0 = bCenter - mainDir * radius * 0.9f;
                    Vector2 b1 = bCenter + mainDir * radius * 0.9f;
                    BuildRoadSegment(b0, b1, SettlementConfig.SideRoadWidth, RoadType.BranchRoad,
                        settleIdx, data, parent);
                    AddRoadData(b0, b1, SettlementConfig.SideRoadWidth, RoadType.BranchRoad,
                        settleIdx, data);
                }
            }

            // 小路（填充间隙，间距 16~24m）
            int pathCount = Mathf.CeilToInt(radius * 2f / 20f);
            for (int i = 1; i <= pathCount; i++)
            {
                float offset = i * 20f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float along = offset * side;
                    if (Mathf.Abs(along) > radius) continue;
                    Vector2 pCenter = center + mainDir * along;
                    Vector2 p0 = pCenter - perp * radius * 0.7f;
                    Vector2 p1 = pCenter + perp * radius * 0.7f;
                    float pathWidth = SettlementConfig.SideRoadWidth * 0.75f;
                    BuildRoadSegment(p0, p1, pathWidth, RoadType.Path,
                        settleIdx, data, parent);
                    AddRoadData(p0, p1, pathWidth, RoadType.Path,
                        settleIdx, data);
                }
            }
        }

        /// <summary>根据聚落形态确定内部路主轴方向</summary>
        private Vector2 GetMainAxis(Settlement settlement, WorldData data)
        {
            switch (settlement.morph)
            {
                case SettlementMorph.RoadStretch:
                    // 朝向最近的跨聚落主干道方向
                    // 如果还没有路，用随机方向
                    return GetNearestRoadDirection(settlement.center, data);

                case SettlementMorph.RiverBank:
                    // 东西走向（沿河）
                    return Vector2.right;

                case SettlementMorph.MountainSide:
                    // 沿山体等高线（简化为南北）
                    return new Vector2(0.5f, 0.866f).normalized; // 约 60°

                case SettlementMorph.CenterRadiate:
                default:
                    // 随机偏转
                    float angle = Random.Range(0f, Mathf.PI);
                    return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        }

        private Vector2 GetNearestRoadDirection(Vector2 center, WorldData data)
        {
            if (data.roads == null || data.roads.Count == 0)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            // 找最近的跨聚落道路
            float bestDist = float.MaxValue;
            Vector2 bestDir = Vector2.right;
            foreach (var road in data.roads)
            {
                if (road.settlementId >= 0) continue; // 跳过内部道路
                float d = PointToSegmentDist(center, road.start, road.end);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestDir = (road.end - road.start).normalized;
                }
            }
            return bestDir;
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

        // ── 道路段建造 ──────────────────────────────

        private void BuildRoadSegment(Vector2 start, Vector2 end, float width,
            RoadType roadType, int settleIdx, WorldData data, Transform parent)
        {
            float len = Vector2.Distance(start, end);
            int segCount = Mathf.Max(4, Mathf.CeilToInt(len / 4f));
            float segStep = len / segCount;
            Vector2 dir = (end - start).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            float spacing = data.chunkSize / data.resolution;
            int vCx = data.worldSize.x * data.resolution + 1;
            int vCz = data.worldSize.y * data.resolution + 1;

            Material mat;
            switch (roadType)
            {
                case RoadType.MainRoad: mat = _mainRoadMat; break;
                case RoadType.BranchRoad: mat = _branchRoadMat; break;
                default: mat = _pathMat; break;
            }

            for (int si = 0; si < segCount; si++)
            {
                float t0 = (float)si / segCount;
                float t1 = (float)(si + 1) / segCount;
                float midT = (t0 + t1) * 0.5f;

                // Perlin 蜿蜒（仅跨聚落道路或主路）
                float wb0 = 0f, wb1 = 0f;
                if (settleIdx < 0 || roadType == RoadType.MainRoad)
                {
                    wb0 = Wobble(midT * len);
                    wb1 = Wobble(midT * len + segStep);
                }

                Vector2 p0 = Vector2.Lerp(start, end, t0) + perp * wb0;
                Vector2 p1 = Vector2.Lerp(start, end, t1) + perp * wb1;

                float h0 = SampleHeight(p0, data.heightMap, spacing, vCx, vCz);
                float h1 = SampleHeight(p1, data.heightMap, spacing, vCx, vCz);

                Vector3 wA = new Vector3(p0.x, h0 + RoadThickness, p0.y);
                Vector3 wB = new Vector3(p1.x, h1 + RoadThickness, p1.y);
                Vector3 mid = (wA + wB) * 0.5f;
                Vector3 segDir = wB - wA;
                float segLen = segDir.magnitude;

                string prefix;
                switch (roadType)
                {
                    case RoadType.MainRoad: prefix = "RoadM"; break;
                    case RoadType.BranchRoad: prefix = "RoadB"; break;
                    default: prefix = "RoadP"; break;
                }

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"{prefix}_{si}";
                seg.transform.SetParent(parent);
                seg.transform.position = mid;
                seg.transform.localScale = new Vector3(width, RoadThickness * 2f, segLen);
                seg.transform.rotation = Quaternion.LookRotation(segDir.normalized, Vector3.up);

                seg.GetComponent<MeshRenderer>().sharedMaterial = mat;

                var col = seg.GetComponent<BoxCollider>();
                if (col) Object.DestroyImmediate(col);
            }
        }

        private void AddRoadData(Vector2 start, Vector2 end, float width,
            RoadType roadType, int settleIdx, WorldData data)
        {
            data.roads.Add(new Road
            {
                start = start,
                end = end,
                width = width,
                roadType = roadType,
                settlementId = settleIdx
            });
        }

        // ── 工具函数 ────────────────────────────────

        private float Wobble(float dist)
        {
            return (Mathf.PerlinNoise(dist * WobbleScale, 0f) - 0.5f) * WobbleAmp;
        }

        private float SampleHeight(Vector2 pos, float[,] heightMap,
            float spacing, int vCx, int vCz)
        {
            if (heightMap == null) return 0f;
            int vx = Mathf.Clamp(Mathf.RoundToInt(pos.x / spacing), 0, vCx - 1);
            int vz = Mathf.Clamp(Mathf.RoundToInt(pos.y / spacing), 0, vCz - 1);
            return heightMap[vx, vz];
        }

        private GameObject CreateRoadsRoot(Transform terrainParent)
        {
            GameObject existing = GameObject.Find(RoadsRootName);
            if (existing) Object.DestroyImmediate(existing);

            GameObject root = new GameObject(RoadsRootName);
            root.transform.SetParent(terrainParent);
            root.transform.position = Vector3.zero;
            return root;
        }
    }
}
