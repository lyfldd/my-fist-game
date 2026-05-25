using System.Collections.Generic;
using _Game.Systems.WorldGen.Data;
using UnityEngine;

namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 4 (Order=35): 多单元 WFC 约束求解器。
    /// 在 20×20 基础网格上放置多格模块（3×3~5×5），道路/水域为 1×1~4×1。
    /// 最低熵 + footprint 检查 + 邻接约束传播 + 配额管理。
    /// </summary>
    public class ModuleAssignmentStage : IGenStage
    {
        public int Order => 35;
        public bool Enabled => true;

        /// <summary>
        /// 邻接否决表：记录不能相邻的模块类型配对（方向无关）。
        /// </summary>
        private static readonly HashSet<(ModuleType, ModuleType)> DenyAdjacency = new()
        {
            (ModuleType.Commercial, ModuleType.Industrial),
            (ModuleType.Commercial, ModuleType.Suburban),
            (ModuleType.Commercial, ModuleType.Forest),
            (ModuleType.ResidentialDense, ModuleType.Industrial),
            (ModuleType.ResidentialDense, ModuleType.Forest),
            (ModuleType.ResidentialSparse, ModuleType.Industrial),
            (ModuleType.Industrial, ModuleType.ResidentialDense),
            (ModuleType.Industrial, ModuleType.ResidentialSparse),
            (ModuleType.Industrial, ModuleType.Water),
            (ModuleType.Suburban, ModuleType.Commercial),
        };

        // 四方向偏移
        private static readonly int[] Dx = { -1, 1, 0, 0 };
        private static readonly int[] Dz = { 0, 0, -1, 1 };

        public void Execute(WorldData data)
        {
            var grid = data.moduleGrid;
            int gs = data.gridSize;
            int totalCells = gs * gs;

            // 初始化模块 ID 分配器
            data.nextModuleId = 0;

            // 已放置的模块类型计数器（配额追踪）
            var typeCounts = new Dictionary<ModuleType, int>();

            int iteration = 0;
            int maxIterations = totalCells * 20;

            while (iteration < maxIterations)
            {
                iteration++;

                // 找熵最小且可坍缩的格子
                var cell = FindLowestEntropy(grid, gs);
                if (cell == null) break; // 全部坍缩

                // 坍缩
                bool collapsed = TryCollapse(grid, gs, cell, data, typeCounts);
                if (!collapsed)
                {
                    // 如果坍缩失败（所有候选 footprint 不可用），兜底为道路或空地
                    ForceCollapseAsFallback(grid, gs, cell, data);
                }

                // 配额检查：达到上限的类型从全局移除
                EnforceQuotas(grid, gs, data.moduleQuotas, typeCounts);
            }

            // 处理残留未坍缩格（兜底填充）
            FillRemaining(grid, gs, data);

            int collapsedCount = CountCollapsed(grid, gs);
            Debug.Log($"[ModuleAssignment] WFC 完成：{collapsedCount}/{totalCells} 格子坍缩, " +
                      $"模块 {data.nextModuleId} 个, 迭代 {iteration} 次");
        }

        // ================================================================
        // 最低熵查找
        // ================================================================

        private GridCell FindLowestEntropy(GridCell[,] grid, int gs)
        {
            GridCell result = null;
            int minCount = int.MaxValue;

            for (int x = 0; x < gs; x++)
            {
                for (int z = 0; z < gs; z++)
                {
                    var c = grid[x, z];
                    if (!c.CanCollapse) continue;
                    if (c.candidates.Count == 0) continue;

                    if (c.candidates.Count < minCount)
                    {
                        minCount = c.candidates.Count;
                        result = c;
                        if (minCount == 1) return result; // 唯一候选直接返回
                    }
                }
            }

            return result;
        }

        // ================================================================
        // 坍缩
        // ================================================================

        /// <summary>
        /// 尝试从候选集中坍缩一个单元格。
        /// 遍历候选直到找到 footprint 可用的选项。
        /// 成功返回 true，所有候选都不可用返回 false。
        /// </summary>
        private bool TryCollapse(GridCell[,] grid, int gs, GridCell cell,
            WorldData data, Dictionary<ModuleType, int> typeCounts)
        {
            if (cell.candidates.Count == 0) return false;

            var candidates = new List<ModuleCandidate>(cell.candidates);

            // 打乱顺序，增加随机性
            Shuffle(candidates, data.rng);

            foreach (var cand in candidates)
            {
                int cx = cell.gridPos.x;
                int cz = cell.gridPos.y;

                // 检查 footprint 是否可用
                if (!IsFootprintAvailable(grid, gs, cx, cz, cand.CellWidth, cand.CellHeight))
                    continue;

                // 配额检查
                if (IsQuotaFull(typeCounts, data.moduleQuotas, cand.Type))
                    continue;

                // 成功：分配模块 ID
                int modId = ++data.nextModuleId;

                // 标记所有 footprint 格子
                MarkFootprint(grid, gs, cx, cz, cand.CellWidth, cand.CellHeight,
                    cand.Type, modId);

                // 记录类型计数
                typeCounts[cand.Type] = typeCounts.GetValueOrDefault(cand.Type, 0) + 1;

                // 传播约束到 footprint 周边
                PropagateAroundFootprint(grid, gs, cx, cz, cand.CellWidth, cand.CellHeight);

                return true;
            }

            return false; // 所有候选都不可用
        }

        /// <summary>
        /// 当所有候选都无法放置时，强制坍缩为 1×1 道路或空地。
        /// </summary>
        private void ForceCollapseAsFallback(GridCell[,] grid, int gs,
            GridCell cell, WorldData data)
        {
            int cx = cell.gridPos.x;
            int cz = cell.gridPos.y;
            int modId = ++data.nextModuleId;

            // 尝试道路，被占用则空地
            ModuleType fallback = IsFootprintAvailable(grid, gs, cx, cz, 1, 1)
                ? ModuleType.Road
                : ModuleType.Empty;

            MarkFootprint(grid, gs, cx, cz, 1, 1, fallback, modId);
            PropagateAroundFootprint(grid, gs, cx, cz, 1, 1);
        }

        // ================================================================
        // Footprint 检查
        // ================================================================

        /// <summary>
        /// 检查以 (cx, cz) 为左上角的 w×h 区域是否全部可用（在界内且未坍缩/未占用）。
        /// </summary>
        private bool IsFootprintAvailable(GridCell[,] grid, int gs,
            int cx, int cz, int w, int h)
        {
            if (cx + w > gs || cz + h > gs) return false;

            for (int x = cx; x < cx + w; x++)
                for (int z = cz; z < cz + h; z++)
                    if (grid[x, z].collapsed || grid[x, z].occupied)
                        return false;

            return true;
        }

        /// <summary>
        /// 标记 footprint 内所有格子。
        /// 锚点格 (cx,cz) 设为 collapsed + 记录尺寸，
        /// 其余格设为 occupied。
        /// </summary>
        private void MarkFootprint(GridCell[,] grid, int gs,
            int cx, int cz, int w, int h, ModuleType type, int modId)
        {
            for (int x = cx; x < cx + w; x++)
            {
                for (int z = cz; z < cz + h; z++)
                {
                    var g = grid[x, z];
                    g.type = type;
                    g.candidates.Clear();
                    g.collapsed = true;
                    g.moduleId = modId;

                    if (x == cx && z == cz)
                    {
                        // 锚点
                        g.occupied = false;
                        g.moduleWidth = w;
                        g.moduleHeight = h;
                        g.size = Mathf.Max(w, h) * _Game.Core.GameConstants.MODULE_BASE_UNIT;
                    }
                    else
                    {
                        // 非锚点
                        g.occupied = true;
                        g.moduleWidth = 0;
                        g.moduleHeight = 0;
                        g.size = 0f;
                    }
                }
            }
        }

        // ================================================================
        // 约束传播
        // ================================================================

        /// <summary>
        /// 传播约束到 footprint 周边所有单元格。
        /// 对周边每个未坍缩格，移除与已放置模块不兼容的候选。
        /// </summary>
        private void PropagateAroundFootprint(GridCell[,] grid, int gs,
            int cx, int cz, int w, int h)
        {
            // 收集 perimeter 上的所有格子（去重用 HashSet）
            var perimeter = new HashSet<(int, int)>();

            // 上边界：行 cx-1
            for (int z = cz; z < cz + h; z++)
                if (cx - 1 >= 0) perimeter.Add((cx - 1, z));

            // 下边界：行 cx+w
            for (int z = cz; z < cz + h; z++)
                if (cx + w < gs) perimeter.Add((cx + w, z));

            // 左边界：列 cz-1
            for (int x = cx; x < cx + w; x++)
                if (cz - 1 >= 0) perimeter.Add((x, cz - 1));

            // 右边界：列 cz+h
            for (int x = cx; x < cx + w; x++)
                if (cz + h < gs) perimeter.Add((x, cz + h));

            var collapsedType = grid[cx, cz].type;

            foreach (var (px, pz) in perimeter)
            {
                var neighbor = grid[px, pz];
                if (!neighbor.CanCollapse) continue;

                // 规则 1：移除与已坍缩类型不相容的候选
                neighbor.candidates.RemoveAll(cand =>
                    DenyAdjacency.Contains((collapsedType, cand.Type)) ||
                    DenyAdjacency.Contains((cand.Type, collapsedType)));

                // 规则 2：移除 footprint 会与已放置模块重叠的候选
                neighbor.candidates.RemoveAll(cand =>
                    WouldOverlap(px, pz, cand.CellWidth, cand.CellHeight,
                        cx, cz, w, h));

                // 兜底
                if (neighbor.candidates.Count == 0)
                {
                    AddFallbackCandidates(neighbor);
                }
            }
        }

        /// <summary>
        /// 检查以 (ax, az) 为锚点、尺寸为 (aw, ah) 的候选模块
        /// 是否会与已放置的 (ox, oz, ow, oh) 模块重叠。
        /// </summary>
        private bool WouldOverlap(int ax, int az, int aw, int ah,
            int ox, int oz, int ow, int oh)
        {
            // AABB 重叠检测
            return !(ax >= ox + ow || ax + aw <= ox ||
                     az >= oz + oh || az + ah <= oz);
        }

        /// <summary> 候选集耗尽时添加兜底类型。 </summary>
        private void AddFallbackCandidates(GridCell cell)
        {
            cell.candidates.Add(new ModuleCandidate(ModuleType.Road, 1, 1));
            cell.candidates.Add(new ModuleCandidate(ModuleType.Forest, 2, 2));
            cell.candidates.Add(new ModuleCandidate(ModuleType.Empty, 1, 1));
        }

        // ================================================================
        // 配额管理
        // ================================================================

        /// <summary> 检查某类型是否已到达配额上限。 </summary>
        private bool IsQuotaFull(Dictionary<ModuleType, int> counts,
            Dictionary<ModuleType, (int min, int max)> quotas, ModuleType type)
        {
            if (quotas == null) return false;
            if (!quotas.TryGetValue(type, out var range)) return false;
            int current = counts.GetValueOrDefault(type, 0);
            return current >= range.max;
        }

        /// <summary>
        /// 全局强制配额：达到上限的类型从所有未坍缩格中移除。
        /// </summary>
        private void EnforceQuotas(GridCell[,] grid, int gs,
            Dictionary<ModuleType, (int min, int max)> quotas,
            Dictionary<ModuleType, int> counts)
        {
            if (quotas == null) return;

            foreach (var kv in quotas)
            {
                int current = counts.GetValueOrDefault(kv.Key, 0);
                if (current >= kv.Value.max)
                {
                    // 达到上限 → 从所有未坍缩格移除该类型
                    for (int x = 0; x < gs; x++)
                        for (int z = 0; z < gs; z++)
                        {
                            var c = grid[x, z];
                            if (!c.CanCollapse) continue;
                            int before = c.candidates.Count;
                            c.candidates.RemoveAll(cand => cand.Type == kv.Key);
                            // 如果移除后为空，加兜底
                            if (c.candidates.Count == 0 && before > 0)
                                AddFallbackCandidates(c);
                        }
                }
            }
        }

        // ================================================================
        // 残留处理
        // ================================================================

        /// <summary>
        /// WFC 主循环结束后，用 1×1 道路或空地填充任何残留的未坍缩格。
        /// </summary>
        private void FillRemaining(GridCell[,] grid, int gs, WorldData data)
        {
            for (int x = 0; x < gs; x++)
            {
                for (int z = 0; z < gs; z++)
                {
                    var c = grid[x, z];
                    if (c.collapsed || c.occupied) continue;

                    int modId = ++data.nextModuleId;
                    ModuleType fill = ModuleType.Road;
                    MarkFootprint(grid, gs, x, z, 1, 1, fill, modId);
                }
            }
        }

        // ================================================================
        // 辅助
        // ================================================================

        /// <summary> 数量统计（调试用）。 </summary>
        public static Dictionary<ModuleType, int> GetTypeCounts(GridCell[,] grid, int gs)
        {
            var counts = new Dictionary<ModuleType, int>();
            for (int x = 0; x < gs; x++)
            for (int z = 0; z < gs; z++)
            {
                var c = grid[x, z];
                if (!c.IsAnchor) continue;
                var t = c.type;
                if (t == ModuleType.Unknown) continue;
                counts[t] = counts.GetValueOrDefault(t, 0) + 1;
            }
            return counts;
        }

        private static int CountCollapsed(GridCell[,] grid, int gs)
        {
            int count = 0;
            for (int x = 0; x < gs; x++)
                for (int z = 0; z < gs; z++)
                    if (grid[x, z].collapsed)
                        count++;
            return count;
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}
