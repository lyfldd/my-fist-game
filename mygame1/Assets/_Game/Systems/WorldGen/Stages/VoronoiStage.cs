using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 2: Voronoi 地貌分区。生成地块种子点并分配地貌类型。
    /// </summary>
    public class VoronoiStage : IGenStage
    {
        public int Order => 20;
        public bool Enabled => false; // 已废弃：新地形算法不再使用 Voronoi 分块

        // 地貌比例
        private static readonly (BiomeType biome, float ratio)[] BiomeDistribution =
        {
            (BiomeType.Plain,    0.40f),
            (BiomeType.Hill,     0.20f),
            (BiomeType.Forest,   0.20f),
            (BiomeType.Mountain, 0.10f),
            (BiomeType.Lake,     0.10f),
        };

        public void Execute(WorldData data)
        {
            // [DEPRECATED] 新地形算法不再使用 Voronoi 分块，本 Stage 已禁用。
            // 代码保留供参考，实际不会执行（Enabled=false）。
            Debug.LogWarning("[VoronoiStage] 已废弃，不再执行。新算法使用多层噪声叠加。");
            return;

            /* 原代码保留供参考
            int count = Random.Range(12, 17);
            // ... 原实现 ...
            */
        }
    }
}
