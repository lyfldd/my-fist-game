using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 3 (Order=30): 主干道网络生成（简化版）。
    /// 收集所有 KeyNodes + 外围出入口节点，
    /// Delaunay 三角剖分 → 节点连通图 → 过滤无效边 → 生成直线道路。
    /// Phase 2 会用 Catmull-Rom 弯曲替换直线。
    /// </summary>
    public class MainRoadStage : IGenStage
    {
        public int Order => 30;
        public bool Enabled => false; // Phase 1 一级模块：已废弃，保留文件

        private const float EntryMargin = 50f;          // 出入口距离地图边界
        private const float MaxRoadLength = 350f;       // 最大道路长度（超过则过滤）
        private const float CrossCenterMargin = 30f;    // 道路不可穿越城市核心的距离

        public void Execute(WorldData data)
        {
            if (data.districts == null || data.districts.Count == 0)
            {
                Debug.LogWarning("[MainRoadStage] 没有城区数据，跳过。");
                return;
            }

            var rng = data.random;
            data.cityRoads = new List<CityRoad>();
            float worldW = data.worldWidth;
            float worldH = data.worldHeight;

            // 1. 收集所有节点
            List<Vector2> allNodes = new List<Vector2>();
            List<int> nodeDistrictIds = new List<int>(); // 节点所属城区

            for (int d = 0; d < data.districts.Count; d++)
            {
                var district = data.districts[d];
                foreach (var node in district.keyNodes)
                {
                    allNodes.Add(node);
                    nodeDistrictIds.Add(d);
                }
            }

            // 2. 生成外围出入口节点（地图四边中点附近）
            Vector2[] entryNodes = new Vector2[]
            {
                new Vector2(worldW * 0.5f, EntryMargin),           // 南
                new Vector2(worldW * 0.5f, worldH - EntryMargin),  // 北
                new Vector2(EntryMargin, worldH * 0.5f),           // 西
                new Vector2(worldW - EntryMargin, worldH * 0.5f),  // 东
            };

            // 3. 合并节点（出入口节点属于外围，districtId=-1）
            List<Vector2> allPoints = new List<Vector2>(allNodes);
            List<int> allDistrictIds = new List<int>(nodeDistrictIds);
            int internalCount = allNodes.Count;

            foreach (var en in entryNodes)
            {
                allPoints.Add(en);
                allDistrictIds.Add(-1); // 外围节点
            }

            int totalNodes = allPoints.Count;
            if (totalNodes < 3)
            {
                Debug.LogWarning("[MainRoadStage] 节点数不足，无法构建路网。");
                return;
            }

            // 4. Delaunay 三角剖分
            List<(int a, int b)> edges = DelaunayTriangulation(allPoints, rng);

            // 5. 过滤并生成道路
            foreach (var edge in edges)
            {
                Vector2 pA = allPoints[edge.a];
                Vector2 pB = allPoints[edge.b];

                // 过滤过长道路
                float length = Vector2.Distance(pA, pB);
                if (length > MaxRoadLength) continue;

                // 过滤两端都是外围出入口的边
                if (allDistrictIds[edge.a] == -1 && allDistrictIds[edge.b] == -1)
                    continue;

                // 确定城区归属
                int distA = allDistrictIds[edge.a];
                int distB = allDistrictIds[edge.b];
                int districtId = distA >= 0 ? distA : distB; // 优先取非 -1 的

                // 过滤穿越非归属城市核心的边
                if (!IsValidRoadEdge(pA, pB, districtId, allPoints, allDistrictIds, data, internalCount))
                    continue;

                // 确定道路宽度（基于城区繁华等级）
                CityRoadType roadType = CityRoadType.MainRoad;
                int wealthLevel = districtId >= 0 ? data.districts[districtId].wealthLevel : 1;
                int sizeLevel = districtId >= 0 ? data.districts[districtId].sizeLevel : 1;
                float width = CityStyleConfig.GetRoadWidth(sizeLevel, wealthLevel, roadType);

                data.cityRoads.Add(new CityRoad
                {
                    start = pA,
                    end = pB,
                    width = width,
                    roadType = roadType,
                    districtId = districtId
                });
            }

            Debug.Log($"[MainRoadStage] 路网完成: {totalNodes} 节点, {data.cityRoads.Count} 条道路");
        }

        /// <summary>检查道路边是否合理（不穿越非归属城市核心）</summary>
        private bool IsValidRoadEdge(Vector2 pA, Vector2 pB, int edgeDistrictId,
            List<Vector2> allPoints, List<int> allDistrictIds, WorldData data, int internalCount)
        {
            // 对每对有 KeyNodes 的城区，检查道路是否穿越其核心
            for (int d = 0; d < data.districts.Count; d++)
            {
                if (d == edgeDistrictId) continue; // 属于同城区的可以通过
                
                // 检查道路线段是否穿过城区核心
                float dist = PointToSegmentDistance(data.districts[d].center, pA, pB);
                if (dist < data.districts[d].radius * 0.4f)
                    return false;
            }

            // 检查两端距离城市核心是否太近（不是该城区的道路却穿越其核心区域）
            for (int i = 0; i < 2; i++)
            {
                Vector2 pt = i == 0 ? pA : pB;
                int nodeId = i == 0 ? FindClosestNode(pt, allPoints) : FindClosestNode(pt, allPoints);
                int nodeDistId = nodeId >= 0 && nodeId < internalCount ? allDistrictIds[nodeId] : -1;

                if (nodeDistId >= 0 && nodeDistId != edgeDistrictId && edgeDistrictId >= 0)
                {
                    // 节点属于不同城区，检查是否太靠近非归属城市核心
                    float distToCore = Vector2.Distance(pt, data.districts[nodeDistId].center);
                    if (distToCore < CrossCenterMargin) return false;
                }
            }

            return true;
        }

        private int FindClosestNode(Vector2 pt, List<Vector2> nodes)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                float d = Vector2.Distance(pt, nodes[i]);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        private float PointToSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            Vector2 ap = p - a;
            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab));
            Vector2 closest = a + t * ab;
            return Vector2.Distance(p, closest);
        }

        // ── Delaunay 三角剖分 (Bowyer-Watson) ──────────────────

        private List<(int a, int b)> DelaunayTriangulation(List<Vector2> points, System.Random rng)
        {
            int n = points.Count;
            if (n < 3) return new List<(int, int)>();

            // 计算超级三角形（包含所有点）
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in points)
            {
                if (p.x < minX) minX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x;
                if (p.y > maxY) maxY = p.y;
            }
            float dx = maxX - minX;
            float dy = maxY - minY;
            float dmax = Mathf.Max(dx, dy) * 10f;
            float midX = (minX + maxX) * 0.5f;
            float midY = (minY + maxY) * 0.5f;

            int superA = n;
            int superB = n + 1;
            int superC = n + 2;

            var superPoints = new List<Vector2>(points);
            superPoints.Add(new Vector2(midX - dmax, midY - dmax));
            superPoints.Add(new Vector2(midX + dmax, midY - dmax));
            superPoints.Add(new Vector2(midX, midY + dmax));

            // 三角形列表：每个三角形存储 3 个顶点索引
            var triangles = new List<(int a, int b, int c)>();
            triangles.Add((superA, superB, superC));

            // 逐点插入
            for (int i = 0; i < n; i++)
            {
                Vector2 pt = points[i];
                var badTriangles = new List<int>();

                // 找出所有外接圆包含当前点的三角形
                for (int t = 0; t < triangles.Count; t++)
                {
                    var tri = triangles[t];
                    Vector2 pa = superPoints[tri.a];
                    Vector2 pb = superPoints[tri.b];
                    Vector2 pc = superPoints[tri.c];

                    if (IsPointInCircumcircle(pt, pa, pb, pc))
                    {
                        badTriangles.Add(t);
                    }
                }

                // 收集坏三角形的所有边
                var edgeSet = new Dictionary<(int, int), int>();
                foreach (int tIdx in badTriangles)
                {
                    var tri = triangles[tIdx];
                    var e1 = (Mathf.Min(tri.a, tri.b), Mathf.Max(tri.a, tri.b));
                    var e2 = (Mathf.Min(tri.b, tri.c), Mathf.Max(tri.b, tri.c));
                    var e3 = (Mathf.Min(tri.c, tri.a), Mathf.Max(tri.c, tri.a));

                    AddOrToggleEdge(edgeSet, e1);
                    AddOrToggleEdge(edgeSet, e2);
                    AddOrToggleEdge(edgeSet, e3);
                }

                // 删除坏三角形（从后往前删）
                badTriangles.Sort();
                for (int idx = badTriangles.Count - 1; idx >= 0; idx--)
                {
                    triangles.RemoveAt(badTriangles[idx]);
                }

                // 用边界边创建新三角形
                foreach (var kvp in edgeSet)
                {
                    if (kvp.Value == 1) // 只出现一次的是边界边
                    {
                        triangles.Add((kvp.Key.Item1, kvp.Key.Item2, i));
                    }
                }
            }

            // 移除包含超级三角形顶点的边
            var resultEdges = new HashSet<(int, int)>();
            foreach (var tri in triangles)
            {
                if (tri.a >= n || tri.b >= n || tri.c >= n) continue;

                AddEdge(resultEdges, tri.a, tri.b);
                AddEdge(resultEdges, tri.b, tri.c);
                AddEdge(resultEdges, tri.c, tri.a);
            }

            return new List<(int, int)>(resultEdges);
        }

        private bool IsPointInCircumcircle(Vector2 pt, Vector2 a, Vector2 b, Vector2 c)
        {
            float ax = a.x - pt.x;
            float ay = a.y - pt.y;
            float bx = b.x - pt.x;
            float by = b.y - pt.y;
            float cx = c.x - pt.x;
            float cy = c.y - pt.y;

            float det = (ax * ax + ay * ay) * (bx * cy - cx * by)
                      - (bx * bx + by * by) * (ax * cy - cx * ay)
                      + (cx * cx + cy * cy) * (ax * by - bx * ay);

            return det > 0;
        }

        private void AddOrToggleEdge(Dictionary<(int, int), int> edgeSet, (int, int) edge)
        {
            if (edgeSet.ContainsKey(edge))
                edgeSet[edge]++;
            else
                edgeSet[edge] = 1;
        }

        private void AddEdge(HashSet<(int, int)> edges, int a, int b)
        {
            int min = Mathf.Min(a, b);
            int max = Mathf.Max(a, b);
            edges.Add((min, max));
        }
    }
}
