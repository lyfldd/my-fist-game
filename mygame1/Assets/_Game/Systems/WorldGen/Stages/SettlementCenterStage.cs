using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 3: 聚落中心定位。扫描平坦地形选聚落中心点，分配大小/等级/形态。
    /// 不放置建筑——建筑由 SettlementBuildingStage (Order=40) 负责。
    /// 形态判定基于海拔高度（非 Voronoi）。
    /// </summary>
    public class SettlementCenterStage : IGenStage
    {
        public int Order => 30;
        public bool Enabled => false; // 新管线已废弃，改用 CityLayout

        // 地形扫描步长（米）
        private const float ScanStep = 16f;

        // 平坦度阈值：区域内最大高差 < 此值视为平坦（多级回退）
        private const float MaxFlatDeltaDefault = 5f;
        private const float MaxFlatDeltaRelaxed  = 8f;
        private const float MaxFlatDeltaLast     = 12f;

        // 最少聚落数（低于此数则放宽平坦度重试）
        private const int MinSettlementCount = 3;

        // 目标聚落数量
        private const int TargetSettlementCount = 8;

        public void Execute(WorldData data)
        {
            float worldW = data.worldSize.x * data.chunkSize;
            float worldH = data.worldSize.y * data.chunkSize;
            float spacing = data.chunkSize / data.resolution;

            data.settlements.Clear();

            // 多级放宽平坦度尝试
            float[] flatnessLevels = { MaxFlatDeltaDefault, MaxFlatDeltaRelaxed, MaxFlatDeltaLast };
            List<Settlement> centers = null;

            for (int level = 0; level < flatnessLevels.Length; level++)
            {
                float delta = flatnessLevels[level];
                var candidates = ScanFlatRegions(data, worldW, worldH, spacing, delta);
                centers = SelectSettlementCenters(candidates, data, worldW, worldH);

                if (centers.Count >= MinSettlementCount)
                {
                    Debug.Log($"[SettlementCenterStage] 平坦度阈值={delta}m, 候选={candidates.Count}, 选定={centers.Count}个聚落");
                    break;
                }

                Debug.Log($"[SettlementCenterStage] 平坦度阈值={delta}m 仅找到{centers.Count}个聚落（候选={candidates.Count}），" +
                    $"尝试放宽...");
            }

            if (centers == null || centers.Count == 0)
            {
                Debug.LogWarning("[SettlementCenterStage] 所有平坦度等级都未找到聚落！" +
                    " 请检查地形高度数据。");
                return;
            }

            // 写入 data.settlements
            foreach (var s in centers)
            {
                data.settlements.Add(s);
            }
        }

        // ── 地形扫描 ────────────────────────────────

        private struct Candidate
        {
            public Vector2 center;
            public float flatnessScore; // 越低越平坦
        }

        private List<Candidate> ScanFlatRegions(WorldData data, float worldW, float worldH,
            float spacing, float flatDelta)
        {
            var candidates = new List<Candidate>();

            // 扫描半径（检查 Patch 大小 = Medium 聚落的检查范围）
            float checkRadius = SettlementConfig.MediumSettlementDia * 0.5f;

            for (float cx = checkRadius; cx < worldW - checkRadius; cx += ScanStep)
            {
                for (float cz = checkRadius; cz < worldH - checkRadius; cz += ScanStep)
                {
                    float score = EvaluateFlatness(cx, cz, checkRadius, data.heightMap, spacing,
                        data.worldSize.x * data.resolution + 1,
                        data.worldSize.y * data.resolution + 1);
                    if (score < flatDelta)
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
                SettlementSize size = AssignSize(settlements.Count, TargetSettlementCount,
                    ref smallCount, ref mediumCount, ref largeCount);

                // 间距检查
                float minSpacing = GetMinSpacing(size);
                bool tooClose = false;
                for (int p = 0; p < centersPlaced.Count; p++)
                {
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
                SettlementTier tier = AssignTier(settlements.Count, TargetSettlementCount,
                    ref shabbyCount, ref normalCount, ref prosperCount);

                float diameter = GetDiameter(size);

                // 基于 Voronoi 地块地貌判定形态（此时还没有路）
                SettlementMorph morph = DetermineMorphByBiome(cand.center, data);

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

            Debug.Log($"[SettlementCenterStage] 选取 {settlements.Count} 个聚落 " +
                      $"(小={smallCount} 中={mediumCount} 大={largeCount}, " +
                      $"简陋={shabbyCount} 普通={normalCount} 繁华={prosperCount})");
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

        // ── 形态判定（基于海拔高度，非 Voronoi） ─────

        private SettlementMorph DetermineMorphByBiome(Vector2 center, WorldData data)
        {
            if (data.heightMap == null)
                return SettlementMorph.CenterRadiate;

            float spacing = data.chunkSize / data.resolution;
            int vCx = data.worldSize.x * data.resolution + 1;
            int vCz = data.worldSize.y * data.resolution + 1;

            // 采样聚落中心海拔
            float centerH = SampleHeight(center, data.heightMap, spacing, vCx, vCz);

            // 检查周边是否有水域（海拔 < 0）
            bool nearWater = false;
            float avgH = 0f;
            int samples = 8;
            float checkRadius = 48f; // 48m 范围采样
            for (int i = 0; i < samples; i++)
            {
                float angle = (float)i / samples * Mathf.PI * 2f;
                Vector2 sp = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * checkRadius;
                float h = SampleHeight(sp, data.heightMap, spacing, vCx, vCz);
                avgH += h;
                if (h < -0.3f) nearWater = true;
            }
            avgH /= samples;

            if (avgH > 5f)
                return SettlementMorph.MountainSide;  // 高海拔 → 依山

            if (nearWater || centerH < 0f)
                return SettlementMorph.RiverBank;      // 近水 → 沿河

            if (avgH < 2f)
                return SettlementMorph.RoadStretch;    // 低平 → 沿路延伸

            return SettlementMorph.CenterRadiate;      // 中等 → 中心辐射
        }

        private float SampleHeight(Vector2 pos, float[,] heightMap,
            float spacing, int vCx, int vCz)
        {
            int vx = Mathf.Clamp(Mathf.RoundToInt(pos.x / spacing), 0, vCx - 1);
            int vz = Mathf.Clamp(Mathf.RoundToInt(pos.y / spacing), 0, vCz - 1);
            return heightMap[vx, vz];
        }
    }
}
