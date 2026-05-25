using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 5 (Order=40): 建筑生成（简化版）。
    /// 沿街排布（核心） + 二分法内部填充。
    /// </summary>
    public class BuildingStage : IGenStage
    {
        public int Order => 40;
        public bool Enabled => false; // Phase 1 一级模块：已废弃，保留文件

        private const float StreetSampleMin = 6f;       // 沿街采样最小间距
        private const float StreetSampleMax = 15f;      // 沿街采样最大间距
        private const float PlacementChance = 0.7f;     // 70%位置放建筑
        private const float MinBuildingSize = 4f;       // 最小建筑尺寸
        private const float MaxBuildingSize = 10f;      // 最大建筑尺寸

        public void Execute(WorldData data)
        {
            if (data.blocks == null || data.blocks.Count == 0)
            {
                Debug.LogWarning("[BuildingStage] 没有街区数据，跳过。");
                return;
            }

            var rng = data.random;
            data.cityBuildings = new List<CityBuilding>();

            for (int b = 0; b < data.blocks.Count; b++)
            {
                var block = data.blocks[b];
                if (block.districtId < 0 || block.districtId >= data.districts.Count) continue;

                var district = data.districts[block.districtId];
                CityStyleParams styleParams = CityStyleConfig.GetParams(district.style);

                // ── 第一步：沿街区边界道路排布建筑 ──
                PlaceStreetFrontBuildings(data, block, district, styleParams, rng, b);

                // ── 第二步：街区内部二分法填充 ──
                PlaceInteriorBuildings(data, block, district, styleParams, rng, b);
            }

            Debug.Log($"[BuildingStage] 建筑完成: {data.cityBuildings.Count} 个建筑");
        }

        // ── 沿街排布 ─────────────────────────────────

        private void PlaceStreetFrontBuildings(WorldData data, CityBlock block,
            CityDistrict district, CityStyleParams styleParams, System.Random rng, int blockId)
        {
            var vertices = block.vertices;
            int n = vertices.Length;

            for (int i = 0; i < n; i++)
            {
                Vector2 vA = vertices[i];
                Vector2 vB = vertices[(i + 1) % n];
                Vector2 edgeDir = vB - vA;
                float edgeLen = edgeDir.magnitude;

                if (edgeLen < StreetSampleMin * 2f) continue;

                Vector2 edgeNormal = new Vector2(-edgeDir.y, edgeDir.x).normalized;
                // 确保法线指向多边形内部（用重心法判断）
                Vector2 polyCentroid = GetPolygonCentroid(vertices);
                if (Vector2.Dot(edgeNormal, polyCentroid - (vA + vB) * 0.5f) < 0)
                    edgeNormal = -edgeNormal;

                // 沿边采样
                float sampleDist = StreetSampleMin + (float)rng.NextDouble() * (StreetSampleMax - StreetSampleMin);
                float pos = sampleDist * (float)rng.NextDouble(); // 随机起始偏移

                while (pos < edgeLen - MinBuildingSize)
                {
                    // 70% 概率放建筑
                    if (rng.NextDouble() > PlacementChance)
                    {
                        pos += sampleDist;
                        continue;
                    }

                    Vector2 roadPos = vA + edgeDir.normalized * pos;

                    // 建筑退让
                    float setback = styleParams.setback + (float)(rng.NextDouble() - 0.5) * 2f;
                    Vector2 buildingPos = roadPos + edgeNormal * setback;

                    // 检查是否在街区内
                    if (!IsPointInPolygon(buildingPos, vertices)) continue;

                    // 随机朝向（道路切线 + 随机偏角）
                    float roadAngle = Mathf.Atan2(edgeDir.x, edgeDir.y) * Mathf.Rad2Deg;
                    float devAngle = (float)(rng.NextDouble() - 0.5) * 2f * styleParams.angleDeviation;
                    float rotation = roadAngle + devAngle;

                    // 随机尺寸
                    float bw = MinBuildingSize + (float)rng.NextDouble() * (MaxBuildingSize - MinBuildingSize);
                    float bd = MinBuildingSize + (float)rng.NextDouble() * (MaxBuildingSize - MinBuildingSize);
                    int floors = CityStyleConfig.GetFloors(district.sizeLevel, district.wealthLevel,
                        GetBuildingTypeForZone(district.zoneType, rng));

                    var buildingType = GetBuildingTypeForZone(district.zoneType, rng);
                    if (buildingType == CityBuildingType.Park) floors = 0;

                    // 添加到列表
                    data.cityBuildings.Add(new CityBuilding
                    {
                        position = buildingPos,
                        rotation = rotation,
                        width = bw,
                        depth = bd,
                        floors = floors,
                        type = buildingType,
                        districtId = block.districtId,
                        blockId = blockId
                    });

                    pos += sampleDist;
                }
            }
        }

        // ── 内部二分法填充 ───────────────────────────

        private void PlaceInteriorBuildings(WorldData data, CityBlock block,
            CityDistrict district, CityStyleParams styleParams, System.Random rng, int blockId)
        {
            // 计算必须保留的空地比例
            float openRatio = styleParams.openSpaceRatio;
            float blockArea = block.area;

            // 用随机二分法切割填充
            SubdivideAndFill(block.bounds, block.vertices, district, styleParams,
                blockId, block.districtId, data, rng, 0, 5);
        }

        private void SubdivideAndFill(Rect bounds, Vector2[] polygon, CityDistrict district,
            CityStyleParams styleParams, int blockId, int districtId,
            WorldData data, System.Random rng, int depth, int maxDepth)
        {
            if (depth >= maxDepth) return;

            float area = bounds.width * bounds.height;
            if (area < MinBuildingSize * MinBuildingSize * 4f) return;

            // 决定：继续细分 vs 放建筑 vs 留空
            float rand = (float)rng.NextDouble();

            // 开放空间占比 = 不填充
            if (rand < styleParams.openSpaceRatio)
                return;

            // 放建筑（面积合适时）
            if (area < 400f || depth >= maxDepth - 2)
            {
                TryPlaceInteriorBuilding(bounds, polygon, district, blockId, districtId, data, rng);
                return;
            }

            // 随机方向切割
            bool horizontal = rng.Next(2) == 0;
            float splitRatio = 0.3f + (float)rng.NextDouble() * 0.4f; // 0.3~0.7

            Rect subA, subB;
            if (horizontal)
            {
                float splitY = bounds.yMin + bounds.height * splitRatio;
                subA = new Rect(bounds.x, bounds.y, bounds.width, splitY - bounds.y);
                subB = new Rect(bounds.x, splitY, bounds.width, bounds.yMax - splitY);
            }
            else
            {
                float splitX = bounds.xMin + bounds.width * splitRatio;
                subA = new Rect(bounds.x, bounds.y, splitX - bounds.x, bounds.height);
                subB = new Rect(splitX, bounds.y, bounds.xMax - splitX, bounds.height);
            }

            SubdivideAndFill(subA, polygon, district, styleParams, blockId, districtId, data, rng, depth + 1, maxDepth);
            SubdivideAndFill(subB, polygon, district, styleParams, blockId, districtId, data, rng, depth + 1, maxDepth);
        }

        private void TryPlaceInteriorBuilding(Rect bounds, Vector2[] polygon, CityDistrict district,
            int blockId, int districtId, WorldData data, System.Random rng)
        {
            Vector2 center = bounds.center;

            // 检查是否在街区内
            if (!IsPointInPolygon(center, polygon)) return;

            CityBuildingType type = GetBuildingTypeForZone(district.zoneType, rng);
            int floors = CityStyleConfig.GetFloors(district.sizeLevel, district.wealthLevel, type);
            if (type == CityBuildingType.Park) floors = 0;

            float bw = MinBuildingSize + (float)rng.NextDouble() * (MaxBuildingSize - MinBuildingSize) * 0.8f;
            float bd = MinBuildingSize + (float)rng.NextDouble() * (MaxBuildingSize - MinBuildingSize) * 0.8f;

            // 确保建筑不超出街区
            bw = Mathf.Min(bw, bounds.width * 0.7f);
            bd = Mathf.Min(bd, bounds.height * 0.7f);

            float rotation = (float)rng.NextDouble() * 360f;

            data.cityBuildings.Add(new CityBuilding
            {
                position = center,
                rotation = rotation,
                width = bw,
                depth = bd,
                floors = floors,
                type = type,
                districtId = districtId,
                blockId = blockId
            });
        }

        // ── 功能区 → 建筑类型 ────────────────────────

        private CityBuildingType GetBuildingTypeForZone(ZoneType zone, System.Random rng)
        {
            switch (zone)
            {
                case ZoneType.CBD:
                {
                    // 办公 30% / 商铺 40% / 公寓 30%
                    float r = (float)rng.NextDouble();
                    if (r < 0.4f) return CityBuildingType.Shop;
                    if (r < 0.7f) return CityBuildingType.Office;
                    return CityBuildingType.Apartment;
                }
                case ZoneType.Residential:
                {
                    // 民宅 60% / 联排(公寓) 40%
                    float r = (float)rng.NextDouble();
                    if (r < 0.6f) return CityBuildingType.House;
                    return CityBuildingType.Apartment;
                }
                case ZoneType.Industrial:
                {
                    // 工厂 50% / 仓库 50%
                    float r = (float)rng.NextDouble();
                    if (r < 0.5f) return CityBuildingType.Factory;
                    return CityBuildingType.Warehouse;
                }
                case ZoneType.Green:
                {
                    return CityBuildingType.Park;
                }
                default:
                    return CityBuildingType.House;
            }
        }

        // ── 几何工具 ──────────────────────────────────

        private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            int n = polygon.Length;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                    (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                     (polygon[j].y - polygon[i].y) + polygon[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private Vector2 GetPolygonCentroid(Vector2[] vertices)
        {
            Vector2 sum = Vector2.zero;
            foreach (var v in vertices)
                sum += v;
            return sum / vertices.Length;
        }
    }
}
