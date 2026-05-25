using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 2: 多层噪声地形生成。纯噪声叠加生成连续海拔，地貌由海拔派生。
    ///
    /// 4 层公式：
    ///   Layer1 = 超低频 Perlin(波长512m) × 8.0    → 大山脉/大盆地
    ///   Layer2 = 低频   Perlin(波长128m) × 3.0    → 丘陵/山谷
    ///   Layer3 = 中频   Perlin(波长32m)  × 1.0    → 局部起伏
    ///   Layer4 = FBM(4~16m, 4八度)  × 0.3         → 地表细节
    ///   最终高度 = Layer1 + Layer2 + Layer3 + Layer4
    ///
    /// 海拔 → 顶点颜色：
    ///   ≥ 6m    → 山地（棕色）
    ///   2~6m    → 丘陵/森林（黄绿）
    ///   0~2m    → 平原（草绿）
    ///   &lt; 0     → 湖泊（蓝色）
    ///
    /// 坡度约束：相邻顶点高度差 ≤ tan(30°) × 水平距离。
    /// </summary>
    public class HeightStage : IGenStage
    {
        public int Order => 20;
        public bool Enabled => false; // 新管线已废弃，改用 CityLayout

        // ── 噪声层参数 ──────────────────────────────

        // Layer1: 大尺度山脉/盆地
        private const float L1_Frequency = 1f / 512f;  // 波长 512m
        private const float L1_Amplitude = 8.0f;

        // Layer2: 丘陵/山谷
        private const float L2_Frequency = 1f / 128f;  // 波长 128m
        private const float L2_Amplitude = 3.0f;

        // Layer3: 局部起伏
        private const float L3_Frequency = 1f / 32f;   // 波长 32m
        private const float L3_Amplitude = 1.0f;

        // Layer4: FBM 地表细节
        private const float L4_BaseFrequency = 1f / 16f; // 基准波长 16m
        private const int   L4_Octaves = 4;
        private const float L4_Lacunarity = 2.0f;
        private const float L4_Persistence = 0.5f;
        private const float L4_Amplitude = 0.3f;

        // ── 坡度约束 ────────────────────────────────
        private const float MaxSlopeDeg = 30f;
        // tan(30°) ≈ 0.577，但我们需要对应到顶点间距的动态值

        // ── 海拔颜色阈值 ────────────────────────────
        private const float AltitudeMountain = 6f;
        private const float AltitudeHillLow    = 2f;
        private const float AltitudeWater      = 0f; // 低于此处为湖泊

        public void Execute(WorldData data)
        {
            int vertCountX = data.worldSize.x * data.resolution + 1;
            int vertCountZ = data.worldSize.y * data.resolution + 1;
            float spacing = data.chunkSize / data.resolution; // 2m

            data.heightMap = new float[vertCountX, vertCountZ];
            data.vertexColorMap = new Color[vertCountX, vertCountZ];

            // Seed 偏移（不同种子不同地形）
            float seedOx = data.seed * 0.1237f;
            float seedOz = data.seed * 0.5673f;

            // ── Pass 1: 逐顶点计算原始高度 ──────────
            for (int vx = 0; vx < vertCountX; vx++)
            {
                for (int vz = 0; vz < vertCountZ; vz++)
                {
                    float worldX = vx * spacing;
                    float worldZ = vz * spacing;

                    float height = ComputeRawHeight(worldX, worldZ, seedOx, seedOz);
                    data.heightMap[vx, vz] = height;
                }
            }

            // ── Pass 2: 坡度约束 ──────────────────
            float maxDelta = Mathf.Tan(MaxSlopeDeg * Mathf.Deg2Rad) * spacing;
            ApplySlopeConstraint(data.heightMap, vertCountX, vertCountZ, maxDelta);

            // ── Pass 3: 海拔 → 顶点颜色 ────────────
            for (int vx = 0; vx < vertCountX; vx++)
            {
                for (int vz = 0; vz < vertCountZ; vz++)
                {
                    float h = data.heightMap[vx, vz];
                    data.vertexColorMap[vx, vz] = HeightToColor(h);
                }
            }

            Debug.Log($"[HeightStage] 多层噪声地形生成完成 " +
                      $"(尺寸 {vertCountX}×{vertCountZ}, 高程范围估算完成)");
        }

        // ── 计算原始高度（4 层叠加）──────────────────

        private float ComputeRawHeight(float wx, float wz, float seedOx, float seedOz)
        {
            // Layer 1: 超低频大尺度
            float l1 = SamplePerlin(wx * L1_Frequency + seedOx,
                                    wz * L1_Frequency + seedOz) * L1_Amplitude;

            // Layer 2: 低频丘陵
            float l2 = SamplePerlin(wx * L2_Frequency + seedOx * 1.7f,
                                    wz * L2_Frequency + seedOz * 1.7f) * L2_Amplitude;

            // Layer 3: 中频局部起伏
            float l3 = SamplePerlin(wx * L3_Frequency + seedOx * 2.9f,
                                    wz * L3_Frequency + seedOz * 2.9f) * L3_Amplitude;

            // Layer 4: FBM 地表细节
            float l4 = FBM(wx, wz, seedOx, seedOz) * L4_Amplitude;

            return l1 + l2 + l3 + l4;
        }

        /// <summary>
        /// 将 Mathf.PerlinNoise [0,1] 映射到 [-1, 1]
        /// </summary>
        private float SamplePerlin(float x, float z)
        {
            return (Mathf.PerlinNoise(x, z) - 0.5f) * 2f;
        }

        /// <summary>
        /// 分形布朗运动（Fractal Brownian Motion）。
        /// 基准波长 16m → 4 个八度（16m, 8m, 4m, 2m 等效）。
        /// </summary>
        private float FBM(float wx, float wz, float seedOx, float seedOz)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = L4_BaseFrequency;
            float maxValue = 0f;

            for (int i = 0; i < L4_Octaves; i++)
            {
                float nx = wx * frequency + seedOx * (i + 1) * 0.73f;
                float nz = wz * frequency + seedOz * (i + 1) * 0.37f;
                value += amplitude * Mathf.PerlinNoise(nx, nz);
                maxValue += amplitude;
                amplitude *= L4_Persistence;
                frequency *= L4_Lacunarity;
            }

            // 归一化到 [-1, 1]
            float normalized = value / maxValue;
            return (normalized - 0.5f) * 2f;
        }

        // ── 坡度约束 ────────────────────────────────

        private void ApplySlopeConstraint(float[,] heightMap, int vCx, int vCz, float maxDelta)
        {
            // 对每个顶点检查 4 邻居，钳制高度差
            for (int pass = 0; pass < 2; pass++) // 两次遍历确保收敛
            {
                for (int vx = 0; vx < vCx; vx++)
                {
                    for (int vz = 0; vz < vCz; vz++)
                    {
                        float h = heightMap[vx, vz];

                        // 检查 +X 邻居
                        if (vx + 1 < vCx)
                        {
                            float diff = heightMap[vx + 1, vz] - h;
                            if (diff > maxDelta)
                                heightMap[vx + 1, vz] = h + maxDelta;
                            else if (diff < -maxDelta)
                                heightMap[vx + 1, vz] = h - maxDelta;
                        }

                        // 检查 +Z 邻居
                        if (vz + 1 < vCz)
                        {
                            float diff = heightMap[vx, vz + 1] - h;
                            if (diff > maxDelta)
                                heightMap[vx, vz + 1] = h + maxDelta;
                            else if (diff < -maxDelta)
                                heightMap[vx, vz + 1] = h - maxDelta;
                        }

                        // 检查 -X 邻居（从另一端钳制当前顶点）
                        if (vx > 0)
                        {
                            float diff = h - heightMap[vx - 1, vz];
                            if (diff > maxDelta)
                                heightMap[vx, vz] = heightMap[vx - 1, vz] + maxDelta;
                            else if (diff < -maxDelta)
                                heightMap[vx, vz] = heightMap[vx - 1, vz] - maxDelta;
                        }

                        // 检查 -Z 邻居
                        if (vz > 0)
                        {
                            float diff = h - heightMap[vx, vz - 1];
                            if (diff > maxDelta)
                                heightMap[vx, vz] = heightMap[vx, vz - 1] + maxDelta;
                            else if (diff < -maxDelta)
                                heightMap[vx, vz] = heightMap[vx, vz - 1] - maxDelta;
                        }
                    }
                }
            }
        }

        // ── 海拔 → 顶点颜色 ────────────────────────

        private Color HeightToColor(float h)
        {
            if (h >= AltitudeMountain)
            {
                // 山地：棕色，越高越偏灰白
                float t = Mathf.InverseLerp(AltitudeMountain, AltitudeMountain + 4f, h);
                return Color.Lerp(
                    new Color(0.55f, 0.38f, 0.22f),  // 棕
                    new Color(0.65f, 0.55f, 0.40f),  // 浅棕
                    t);
            }
            if (h >= AltitudeHillLow)
            {
                // 丘陵/森林：黄绿
                float t = Mathf.InverseLerp(AltitudeHillLow, AltitudeMountain, h);
                return Color.Lerp(
                    new Color(0.50f, 0.72f, 0.20f),  // 黄绿
                    new Color(0.60f, 0.65f, 0.18f),  // 偏黄
                    t);
            }
            if (h >= AltitudeWater)
            {
                // 平原：草绿
                float t = Mathf.InverseLerp(AltitudeWater, AltitudeHillLow, h);
                return Color.Lerp(
                    new Color(0.32f, 0.65f, 0.20f),  // 深草绿
                    new Color(0.50f, 0.72f, 0.20f),  // 黄绿
                    t);
            }
            // 湖泊/水域：蓝色，越深越暗
            {
                float t = Mathf.InverseLerp(-3f, AltitudeWater, h);
                return Color.Lerp(
                    new Color(0.08f, 0.22f, 0.55f),  // 深蓝
                    new Color(0.18f, 0.45f, 0.72f),  // 浅蓝
                    t);
            }
        }
    }
}
