using System.Collections.Generic;
using _Game.Systems.WorldGen.Data;
using UnityEngine;
using _Game.Core;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 3 (Order=30): 生成 20×20 基础网格。
    /// 每格候选集包含 ModuleCandidate（含尺寸），按边界位置过滤不适配的尺寸。
    /// 按城市风格缩小初始候选集。
    /// </summary>
    public class ModuleGridStage : IGenStage
    {
        public int Order => 30;
        public bool Enabled => true;

        public void Execute(WorldData data)
        {
            int gs = data.gridSize;  // 20
            float cs = data.cellSize; // 40m
            data.moduleGrid = new GridCell[gs, gs];

            for (int x = 0; x < gs; x++)
            {
                for (int z = 0; z < gs; z++)
                {
                    data.moduleGrid[x, z] = new GridCell
                    {
                        type = ModuleType.Unknown,
                        candidates = BuildCandidatesForCell(x, z, gs),
                        collapsed = false,
                        occupied = false,
                        moduleId = 0,
                        gridPos = new Vector2Int(x, z),
                        worldX = x * cs + cs * 0.5f,
                        worldZ = z * cs + cs * 0.5f,
                        size = cs,
                    };
                }
            }

            // 按城市风格缩小候选集
            ApplyStyleConstraints(data);

            int totalCandidates = 0;
            for (int x = 0; x < gs; x++)
                for (int z = 0; z < gs; z++)
                    totalCandidates += data.moduleGrid[x, z].candidates.Count;

            Debug.Log($"[ModuleGrid] {gs}×{gs} 网格已初始化, 总候选 {totalCandidates}, " +
                      $"平均每格 {totalCandidates / (gs * gs)} 个");
        }

        // ── 候选集构建 ──

        /// <summary>
        /// 为指定位置构建候选集。
        /// 只保留 footprint 不超出边界的 (type, w, h) 组合。
        /// </summary>
        private static List<ModuleCandidate> BuildCandidatesForCell(int x, int z, int gs)
        {
            var result = new List<ModuleCandidate>();

            foreach (var kv in ModuleSizes.Table)
            {
                ModuleType type = kv.Key;
                if (type == ModuleType.Unknown || type == ModuleType.Empty)
                    continue;

                foreach (var (w, h) in kv.Value)
                {
                    // 检查 footprint 是否在网格内
                    if (x + w <= gs && z + h <= gs)
                    {
                        result.Add(new ModuleCandidate(type, w, h));
                    }
                }
            }

            return result;
        }

        // ── 风格约束 ──

        private void ApplyStyleConstraints(WorldData data)
        {
            int gs = data.gridSize;
            int mid = gs / 2; // 10

            switch (data.cityStyle)
            {
                case CityStyle.Radial:
                    ApplyRadialConstraints(data, gs, mid);
                    break;

                case CityStyle.Linear:
                    ApplyLinearConstraints(data, gs, mid);
                    break;

                case CityStyle.River:
                    ApplyRiverConstraints(data, gs, mid);
                    break;

                case CityStyle.ForestEdge:
                    ApplyForestEdgeConstraints(data, gs);
                    break;
            }
        }

        /// <summary> 中心辐射：中心区域只允许商业/道路。 </summary>
        private void ApplyRadialConstraints(WorldData data, int gs, int mid)
        {
            // 中心 2×2 区域 → 仅商业/道路/路口
            for (int x = mid - 1; x <= mid; x++)
            {
                for (int z = mid - 1; z <= mid; z++)
                {
                    if (x >= 0 && x < gs && z >= 0 && z < gs)
                        KeepOnly(data.moduleGrid[x, z],
                            ModuleType.Commercial, ModuleType.Road,
                            ModuleType.RoadTee, ModuleType.RoadCross);
                }
            }
        }

        /// <summary> 沿路延伸：十字中轴 → 仅道路。 </summary>
        private void ApplyLinearConstraints(WorldData data, int gs, int mid)
        {
            for (int i = 0; i < gs; i++)
            {
                KeepOnly(data.moduleGrid[i, mid],
                    ModuleType.Road, ModuleType.RoadCross);
                KeepOnly(data.moduleGrid[mid, i],
                    ModuleType.Road, ModuleType.RoadCross);
            }
        }

        /// <summary> 沿河两岸：中轴行为水域，紧邻行允许河岸类型。 </summary>
        private void ApplyRiverConstraints(WorldData data, int gs, int mid)
        {
            // 中轴 → 仅水域
            for (int i = 0; i < gs; i++)
                KeepOnly(data.moduleGrid[i, mid], ModuleType.Water);

            // 紧邻行移除工业候选（河岸不建工厂）
            for (int i = 0; i < gs; i++)
            {
                if (mid - 1 >= 0)
                    RemoveType(data.moduleGrid[i, mid - 1], ModuleType.Industrial);
                if (mid + 1 < gs)
                    RemoveType(data.moduleGrid[i, mid + 1], ModuleType.Industrial);
            }
        }

        /// <summary> 临森林边缘：第一列 → 仅森林/空地。 </summary>
        private void ApplyForestEdgeConstraints(WorldData data, int gs)
        {
            for (int z = 0; z < gs; z++)
                KeepOnly(data.moduleGrid[0, z], ModuleType.Forest, ModuleType.Empty);

            // 第二列 → 移除工业（森林边缘不建工厂）
            for (int z = 0; z < gs; z++)
                RemoveType(data.moduleGrid[1, z], ModuleType.Industrial);
        }

        // ── 候选集操作工具 ──

        /// <summary> 保留仅含指定类型的候选（含所有尺寸）。 </summary>
        private static void KeepOnly(GridCell cell, params ModuleType[] allowed)
        {
            var set = new HashSet<ModuleType>(allowed);
            cell.candidates.RemoveAll(c => !set.Contains(c.Type));
        }

        /// <summary> 移除指定类型的候选。 </summary>
        private static void RemoveType(GridCell cell, ModuleType toRemove)
        {
            cell.candidates.RemoveAll(c => c.Type == toRemove);
        }
    }
}
