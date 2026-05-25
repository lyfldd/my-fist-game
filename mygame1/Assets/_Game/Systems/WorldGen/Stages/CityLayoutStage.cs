using System.Collections.Generic;
using _Game.Systems.WorldGen.Data;
using UnityEngine;
using _Game.Core;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 2 (Order=20): 城市布局规划。
    /// 固定 20×20 网格 (40m/格 = 800m 总范围)。
    /// 随机决定城市风格和繁华等级，并按等级设置各模块类型的数量配额。
    /// </summary>
    public class CityLayoutStage : IGenStage
    {
        public int Order => 20;
        public bool Enabled => true;

        public void Execute(WorldData data)
        {
            var rng = data.rng;

            // 随机定风格（4种）
            data.cityStyle = (CityStyle)rng.Next(4);

            // 随机定繁华等级
            data.wealthLevel = (WealthLevel)rng.Next(3);

            // ── 固定网格：20×20, 每格 40m ──
            data.gridSize = GameConstants.MODULE_BASE_GRID_SIZE; // 20
            data.cellSize = GameConstants.MODULE_BASE_UNIT;       // 40m

            // ── 模块数量配额（按财富等级） ──
            data.moduleQuotas = BuildQuotas(data.wealthLevel);

            Debug.Log($"[CityLayout] 风格={data.cityStyle}, 网格={data.gridSize}×{data.gridSize} " +
                      $"({data.cellSize}m/格), 等级={data.wealthLevel}");
        }

        /// <summary>
        /// 按财富等级构建每种模块类型的最小/最大数量。
        /// 数量指模块实例数（非格数），一个 3×3 商业区算 1 个模块。
        /// </summary>
        private static Dictionary<ModuleType, (int min, int max)> BuildQuotas(WealthLevel level)
        {
            return level switch
            {
                WealthLevel.Poor => new()
                {
                    { ModuleType.Commercial,        (1, 1) },
                    { ModuleType.ResidentialDense,  (1, 2) },
                    { ModuleType.ResidentialSparse, (2, 3) },
                    { ModuleType.Industrial,        (0, 1) },
                    { ModuleType.Suburban,          (2, 3) },
                    { ModuleType.Forest,            (1, 3) },
                },
                WealthLevel.Normal => new()
                {
                    { ModuleType.Commercial,        (1, 2) },
                    { ModuleType.ResidentialDense,  (2, 3) },
                    { ModuleType.ResidentialSparse, (1, 2) },
                    { ModuleType.Industrial,        (1, 2) },
                    { ModuleType.Suburban,          (1, 2) },
                },
                WealthLevel.Rich => new()
                {
                    { ModuleType.Commercial,        (2, 4) },
                    { ModuleType.ResidentialDense,  (2, 4) },
                    { ModuleType.ResidentialSparse, (1, 2) },
                    { ModuleType.Industrial,        (2, 3) },
                    { ModuleType.Suburban,          (0, 1) },
                },
                _ => new()
            };
        }
    }
}
