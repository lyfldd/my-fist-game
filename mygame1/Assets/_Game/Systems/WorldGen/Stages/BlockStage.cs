using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 4 (Order=35): 街区生成（简化版）。
    /// 以主干道为基准分割平面 → 生成不规则多边形街区。
    /// 递归细分：取最长对角线生成支路 → 一分为二 → 直到面积达标。
    /// </summary>
    public class BlockStage : IGenStage
    {
        public int Order => 35;
        public bool Enabled => false; // Phase 1 一级模块：已废弃，保留文件

        private const float MinBlockArea = 400f;    // 最小街区面积（平方米），达到后停止细分
        private const float RoadSnapThreshold = 8f; // 道路容差阈值

        public void Execute(WorldData data)
        {
            if (data.districts == null || data.districts.Count == 0)
            {
                Debug.LogWarning("[BlockStage] 没有城区数据，跳过。");
                return;
            }

            var rng = data.random;
            data.blocks = new List<CityBlock>();
            int nextRoadId = data.cityRoads.Count;

            // 1. 为每个城区生成初始街区
            for (int d = 0; d < data.districts.Count; d++)
            {
                var district = data.districts[d];

                // 用 KeyNodes 构建凸包作为初始街区多边形
                List<Vector2> hull = ComputeConvexHull(district.keyNodes);

                if (hull.Count < 3) continue;

                // 2. 用穿越该城区的道路切割初始多边形
                List<List<Vector2>> subBlocks = new List<List<Vector2>> { hull };

                foreach (var road in data.cityRoads)
                {
                    if (road.districtId != d && road.districtId != -1) continue;

                    // 检查道路是否穿过任何现有子街区
                    List<List<Vector2>> newSubBlocks = new List<List<Vector2>>();
                    foreach (var poly in subBlocks)
                    {
                        if (RoadIntersectsPolygon(road, poly))
                        {
                            var split = SplitPolygonByLine(poly, road.start, road.end);
                            foreach (var sp in split)
                            {
                                if (sp.Count >= 3)
                                    newSubBlocks.Add(sp);
                            }
                        }
                        else
                        {
                            newSubBlocks.Add(poly);
                        }
                    }
                    subBlocks = newSubBlocks;
                }

                // 3. 递归细分每个街区直到面积达标
                var finalBlocks = new List<List<Vector2>>();
                foreach (var poly in subBlocks)
                {
                    SubdivideRecursive(poly, district, d, data, rng, ref nextRoadId, finalBlocks);
                }

                // 4. 添加到全局列表
                foreach (var poly in finalBlocks)
                {
                    if (poly.Count < 3) continue;
                    float area = PolygonArea(poly);
                    if (area < MinBlockArea * 0.25f) continue; // 太小了就丢弃

                    Rect bounds = ComputeBounds(poly);

                    data.blocks.Add(new CityBlock
                    {
                        vertices = poly.ToArray(),
                        bounds = bounds,
                        zoneType = district.zoneType,
                        districtId = d,
                        area = area
                    });
                }
            }

            Debug.Log($"[BlockStage] 街区完成: {data.blocks.Count} 个街区, " +
                      $"支路 {data.cityRoads.Count - nextRoadId} 条");
        }

        // ── 递归细分 ────────────────────────────────

        private void SubdivideRecursive(List<Vector2> polygon, CityDistrict district, int districtId,
            WorldData data, System.Random rng, ref int nextRoadId, List<List<Vector2>> results)
        {
            float area = PolygonArea(polygon);
            if (area < MinBlockArea)
            {
                results.Add(polygon);
                return;
            }

            int n = polygon.Count;
            if (n < 4)
            {
                results.Add(polygon);
                return;
            }

            // 找最长对角线（不相邻顶点之间）
            float maxDist = 0f;
            int bestI = 0, bestJ = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    // 排除相邻和首尾相邻
                    if (j == i + 1) continue;
                    if (i == 0 && j == n - 1) continue;

                    float d = Vector2.Distance(polygon[i], polygon[j]);
                    if (d > maxDist)
                    {
                        maxDist = d;
                        bestI = i;
                        bestJ = j;
                    }
                }
            }

            if (maxDist < 8f)
            {
                results.Add(polygon);
                return;
            }

            // 生成支路
            Vector2 splitStart = polygon[bestI];
            Vector2 splitEnd = polygon[bestJ];

            int wealthLevel = district.wealthLevel;
            int sizeLevel = district.sizeLevel;
            float branchWidth = CityStyleConfig.GetRoadWidth(sizeLevel, wealthLevel, CityRoadType.BranchRoad);

            data.cityRoads.Add(new CityRoad
            {
                start = splitStart,
                end = splitEnd,
                width = branchWidth,
                roadType = CityRoadType.BranchRoad,
                districtId = districtId
            });

            // 沿对角线直接分割（bestI 和 bestJ 是多边形顶点）
            // 子多边形 1: 顶点 bestI → bestI+1 → ... → bestJ
            List<Vector2> poly1 = new List<Vector2>();
            for (int k = bestI; k <= bestJ; k++)
                poly1.Add(polygon[k]);

            // 子多边形 2: 顶点 bestJ → bestJ+1 → ... → bestI (绕一圈)
            List<Vector2> poly2 = new List<Vector2>();
            for (int k = bestJ; k < n; k++)
                poly2.Add(polygon[k]);
            for (int k = 0; k <= bestI; k++)
                poly2.Add(polygon[k]);

            // 递归细分两半
            if (poly1.Count >= 3)
                SubdivideRecursive(poly1, district, districtId, data, rng, ref nextRoadId, results);
            if (poly2.Count >= 3)
                SubdivideRecursive(poly2, district, districtId, data, rng, ref nextRoadId, results);
        }

        // ── 道路与多边形相交检查 ─────────────────────

        private bool RoadIntersectsPolygon(CityRoad road, List<Vector2> polygon)
        {
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % n];

                if (SegmentsIntersect(road.start, road.end, a, b))
                    return true;
            }
            return false;
        }

        // ── 线段相交判断 ────────────────────────────

        private bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float d1 = Cross2D(p2 - p1, p3 - p1);
            float d2 = Cross2D(p2 - p1, p4 - p1);
            float d3 = Cross2D(p4 - p3, p1 - p3);
            float d4 = Cross2D(p4 - p3, p2 - p3);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            // 共线（容差）
            float eps = 0.001f;
            if (Mathf.Abs(d1) < eps && OnSegment(p1, p2, p3)) return true;
            if (Mathf.Abs(d2) < eps && OnSegment(p1, p2, p4)) return true;
            if (Mathf.Abs(d3) < eps && OnSegment(p3, p4, p1)) return true;
            if (Mathf.Abs(d4) < eps && OnSegment(p3, p4, p2)) return true;

            return false;
        }

        private float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private bool OnSegment(Vector2 a, Vector2 b, Vector2 c)
        {
            return Mathf.Min(a.x, b.x) <= c.x + 0.001f && c.x - 0.001f <= Mathf.Max(a.x, b.x) &&
                   Mathf.Min(a.y, b.y) <= c.y + 0.001f && c.y - 0.001f <= Mathf.Max(a.y, b.y);
        }

        // ── 用线段分割多边形 ──────────────────────────

        /// <summary>用线段将凸多边形一分为二，产生两个子多边形</summary>
        private List<List<Vector2>> SplitPolygonByLine(List<Vector2> polygon, Vector2 lineA, Vector2 lineB)
        {
            int n = polygon.Count;
            if (n < 3) return new List<List<Vector2>> { new List<Vector2>(polygon) };

            // 找到与多边形边界的所有交点
            var hitInfo = new List<(int edgeIdx, Vector2 point)>();
            for (int i = 0; i < n; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % n];

                // 跳过多边形自身的边（a 或 b 就是 lineA/lineB）
                bool isSelfEdge = (ApproxEqual(a, lineA) && ApproxEqual(b, lineB)) ||
                                  (ApproxEqual(a, lineB) && ApproxEqual(b, lineA));
                if (isSelfEdge) continue;

                // 如果 a 或 b 直接就是分割线的端点，这也是分割点
                if (ApproxEqual(a, lineA) || ApproxEqual(a, lineB))
                {
                    hitInfo.Add((i, a));
                    continue;
                }

                // 检查线段相交
                if (SegmentsIntersect(lineA, lineB, a, b))
                {
                    var inter = SegmentIntersectionPoint(lineA, lineB, a, b);
                    if (inter.HasValue && !ApproxEqual(inter.Value, a) && !ApproxEqual(inter.Value, b))
                    {
                        hitInfo.Add((i, inter.Value));
                    }
                }
            }

            // 如果不足 2 个交点，不分割
            if (hitInfo.Count < 2)
                return new List<List<Vector2>> { new List<Vector2>(polygon) };

            // 按边索引排序，取前两个
            hitInfo.Sort((a, b) => a.edgeIdx.CompareTo(b.edgeIdx));
            var hit0 = hitInfo[0];
            var hit1 = hitInfo[1];

            // 构建两个子多边形（沿多边形边走）
            // 子多边形1: 交点1 → 顺时针走到交点2 → 沿分割线回到交点1
            // 子多边形2: 交点2 → 顺时针走到交点1 → 沿分割线回到交点2

            List<Vector2> poly1 = WalkPolygonSegment(polygon, hit0, hit1, n);
            List<Vector2> poly2 = WalkPolygonSegment(polygon, hit1, hit0, n);

            var result = new List<List<Vector2>>();
            if (poly1.Count >= 3) result.Add(poly1);
            if (poly2.Count >= 3) result.Add(poly2);

            return result.Count > 0 ? result
                : new List<List<Vector2>> { new List<Vector2>(polygon) };
        }

        /// <summary>沿多边形边从交点 from 走到交点 to，收集顶点</summary>
        private List<Vector2> WalkPolygonSegment(List<Vector2> polygon,
            (int edgeIdx, Vector2 point) from,
            (int edgeIdx, Vector2 point) to, int n)
        {
            var result = new List<Vector2>();
            result.Add(from.point);

            // 从 from 边之后的下一个顶点开始
            int cur = (from.edgeIdx + 1) % n;

            // 走到 to 边长度的起点
            int targetVertex = to.edgeIdx; // to 在 edgeIdx 到 edgeIdx+1 之间

            while (cur != targetVertex)
            {
                result.Add(polygon[cur]);
                cur = (cur + 1) % n;

                // 防止无限循环
                if (result.Count > n + 5) break;
            }

            // 添加 to 边起点（如果不同于前一个顶点）
            if (!ApproxEqual(result[result.Count - 1], polygon[targetVertex]))
                result.Add(polygon[targetVertex]);

            result.Add(to.point);

            return result;
        }

        private bool ApproxEqual(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(a, b) < 0.01f;
        }

        private Vector2? SegmentIntersectionPoint(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float x1 = p1.x, y1 = p1.y;
            float x2 = p2.x, y2 = p2.y;
            float x3 = p3.x, y3 = p3.y;
            float x4 = p4.x, y4 = p4.y;

            float denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Mathf.Abs(denom) < 0.0001f) return null;

            float t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            return new Vector2(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
        }

        // ── 计算多边形面积（鞋带公式） ───────────────

        private float PolygonArea(List<Vector2> polygon)
        {
            int n = polygon.Count;
            if (n < 3) return 0f;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += polygon[i].x * polygon[j].y;
                area -= polygon[j].x * polygon[i].y;
            }
            return Mathf.Abs(area) * 0.5f;
        }

        // ── 计算凸包 (Graham Scan) ──────────────────

        private List<Vector2> ComputeConvexHull(List<Vector2> points)
        {
            if (points.Count < 3) return new List<Vector2>(points);

            var sorted = new List<Vector2>(points);
            sorted.Sort((a, b) =>
            {
                if (a.y != b.y) return a.y.CompareTo(b.y);
                return a.x.CompareTo(b.x);
            });

            Vector2 pivot = sorted[0];
            sorted.RemoveAt(0);
            sorted.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.y - pivot.y, a.x - pivot.x);
                float angleB = Mathf.Atan2(b.y - pivot.y, b.x - pivot.x);
                if (Mathf.Abs(angleA - angleB) < 0.0001f)
                    return Vector2.Distance(pivot, a).CompareTo(Vector2.Distance(pivot, b));
                return angleA.CompareTo(angleB);
            });

            var hull = new List<Vector2> { pivot, sorted[0] };
            for (int i = 1; i < sorted.Count; i++)
            {
                while (hull.Count >= 2)
                {
                    Vector2 a = hull[hull.Count - 2];
                    Vector2 b = hull[hull.Count - 1];
                    Vector2 c = sorted[i];
                    if (Cross2D(b - a, c - b) <= 0)
                        hull.RemoveAt(hull.Count - 1);
                    else
                        break;
                }
                hull.Add(sorted[i]);
            }
            return hull;
        }

        private Rect ComputeBounds(List<Vector2> polygon)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var v in polygon)
            {
                if (v.x < minX) minX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.x > maxX) maxX = v.x;
                if (v.y > maxY) maxY = v.y;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
