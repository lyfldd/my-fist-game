# 程序化城市生成 — Phase 1 一级模块代码实现

## 项目信息
- 路径：C:\Users\Administrator\Desktop\Unity学习一\mygame\mygame1
- 设计文档：项目根目录 `程序化城市生成算法文档.md`
- 代码根目录：Assets/_Game/Systems/WorldGen/
- Pipeline 已在 Assets/_Game/Systems/WorldGen/Core/ 下有 IGenStage、WorldData、WorldGenerator

## 背景
整个系统已全面转为**三层模块化架构**，Phase 1 只做一级模块（城市区块级）。

**旧 Stage 全部设为 Enabled=false**（文件保留不删）：
- VoronoiStage / HeightStage / RoadStage / SettlementStage / SettlementCenterStage
- SettlementBuildingStage / CityLayoutStage / MainRoadStage / BlockStage / BuildingStage

仅保留：**SeedStage + WorldGenerator + WorldData + IGenStage**

## Pipeline（Phase 1 启用 5 个 Stage）

| Order | Stage | 说明 |
|-------|-------|------|
| 10 | SeedStage | 全局种子（改为 System.Random） |
| 20 | **CityLayoutStage** | **新增**：定城市风格/规模/等级 |
| 30 | **ModuleGridStage** | **新增**：生成待填充的模块网格 |
| 35 | **ModuleAssignmentStage** | **新增**：WFC 约束求解填入模块 |
| 80 | **MeshStage** | **重写**：纯色平面 + 道路Cube + 文字标签 |

**旧 SeedStage 保留但需修改：** 种子用 `System.Random` 而非 `UnityEngine.Random`。

## 需要新增/修改的文件清单

### 新增 5 个文件：
```
Assets/_Game/Systems/WorldGen/Data/ModuleData.cs
Assets/_Game/Systems/WorldGen/Stages/CityLayoutStage.cs
Assets/_Game/Systems/WorldGen/Stages/ModuleGridStage.cs
Assets/_Game/Systems/WorldGen/Stages/ModuleAssignmentStage.cs
Assets/_Game/Systems/WorldGen/Stages/MeshStage.cs
```

### 修改 3 个文件：
```
Assets/_Game/Systems/WorldGen/Core/WorldData.cs
Assets/_Game/Systems/WorldGen/Stages/SeedStage.cs
Assets/_Game/Editor/WorldGen/WorldGenEditorWindow.cs
```

## 1. ModuleData.cs — 数据定义

```csharp
// 文件：Assets/_Game/Systems/WorldGen/Data/ModuleData.cs
using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen.Data
{
    public enum ModuleType
    {
        Unknown,
        Commercial,        // 商业区（红色）
        ResidentialDense,  // 住宅区密集（蓝色）
        ResidentialSparse, // 住宅区稀疏（浅蓝）
        Industrial,        // 工业区（灰色）
        Suburban,          // 郊区（浅绿）
        Road,              // 主干道直段（深灰 Cube）
        RoadTee,           // T字路口（深灰 Cube）
        RoadCross,         // 十字路口（深灰 Cube）
        Water,             // 水域（浅蓝）
        Forest,            // 森林边界（深绿）
        Empty              // 空地/禁建
    }

    public enum CityStyle { Radial, Linear, River, ForestEdge }
    public enum WealthLevel { Poor, Normal, Rich }

    [System.Serializable]
    public struct GridCell
    {
        public ModuleType type;
        public List<ModuleType> candidates;
        public bool collapsed;
        public Vector2Int gridPos;
        public float worldX, worldZ;
        public float size;
    }

    // 颜色映射
    public static class ModuleColors
    {
        public static Color GetColor(ModuleType t) => t switch
        {
            ModuleType.Commercial => new Color(0.90f, 0.22f, 0.21f, 0.6f),     // 红半透
            ModuleType.ResidentialDense => new Color(0.12f, 0.56f, 0.90f, 0.6f), // 蓝半透
            ModuleType.ResidentialSparse => new Color(0.39f, 0.71f, 0.96f, 0.6f), // 浅蓝半透
            ModuleType.Industrial => new Color(0.62f, 0.62f, 0.62f, 0.6f),       // 灰半透
            ModuleType.Suburban => new Color(0.65f, 0.84f, 0.65f, 0.6f),        // 浅绿半透
            ModuleType.Road or ModuleType.RoadTee or ModuleType.RoadCross
                => new Color(0.38f, 0.38f, 0.38f),                              // 深灰
            ModuleType.Water => new Color(0.56f, 0.79f, 0.98f, 0.4f),           // 浅蓝半透
            ModuleType.Forest => new Color(0.22f, 0.55f, 0.24f, 0.6f),          // 深绿半透
            _ => new Color(0.3f, 0.3f, 0.3f)
        };

        public static string GetLabel(ModuleType t) => t switch
        {
            ModuleType.Commercial => "商业区",
            ModuleType.ResidentialDense => "住宅(密)",
            ModuleType.ResidentialSparse => "住宅(疏)",
            ModuleType.Industrial => "工业区",
            ModuleType.Suburban => "郊区",
            ModuleType.Road => "道路",
            ModuleType.RoadTee => "T字路",
            ModuleType.RoadCross => "十字路",
            ModuleType.Water => "水域",
            ModuleType.Forest => "森林",
            _ => ""
        };
    }
}
```

## 2. CityLayoutStage.cs — 城市布局规划

```csharp
// 文件：Assets/_Game/Systems/WorldGen/Stages/CityLayoutStage.cs
using _Game.Systems.WorldGen.Data;

namespace _Game.Systems.WorldGen.Stages
{
    public class CityLayoutStage : IGenStage
    {
        public int Order => 20;
        public bool Enabled => true;

        public void Execute(WorldData data)
        {
            var rng = data.rng;

            // 随机定风格
            data.cityStyle = (CityStyle)rng.Next(4);

            // 随机定规模（小5 / 中6 / 大8）
            int[] sizes = { 5, 6, 8 };
            data.gridSize = sizes[rng.Next(3)];

            // 随机定繁华等级
            data.wealthLevel = (WealthLevel)rng.Next(3);

            // 格子尺寸 = 800m / 网格数（总范围800m）
            data.cellSize = 800f / data.gridSize;

            // 按等级算各模块数量上下限
            // 数据见下方"数量配比表"
            data.moduleCounts = GetCounts(data.wealthLevel, data.gridSize);
        }

        // 数量配比表（贫瘠/普通/繁华 × 网格大小）
        (int min, int max)[] GetCounts(WealthLevel w, int size)
        {
            // 返回数组索引对应 ModuleType 枚举顺序
            // (Unknown不用, Commercial, ResDense, ResSparse, Industrial, Suburban, Road, RoadTee, RoadCross, Water, Forest, Empty)
            // 具体数值参考算法文档
        }
    }
}
```

**数量配比表（贫瘠 / 普通 / 繁华）：**

| 模块类型 | 贫瘠(5×5) | 普通(6×6) | 繁华(8×8) |
|---------|-----------|-----------|----------|
| Commercial | 1 | 2~3 | 3~4 |
| ResidentialDense | 0~1 | 1~2 | 2~3 |
| ResidentialSparse | 2~3 | 1~2 | 0~1 |
| Industrial | 0~1 | 1~2 | 2~3 |
| Suburban | 2~3 | 1~2 | 0~1 |
| Road / RoadTee / RoadCross | 3~5 | 5~8 | 8~12 |
| Water | 0~1 | 1 | 1~2 |
| Forest | 0~1 | 0~1 | 0~1 |
| Empty | 剩余 | 剩余 | 剩余 |

**城市风格作用：** 只影响 ModuleGridStage 的**初始候选集缩小**，不在这里处理。

## 3. ModuleGridStage.cs — 网格生成

```csharp
// 文件：Assets/_Game/Systems/WorldGen/Stages/ModuleGridStage.cs
using System.Collections.Generic;
using _Game.Systems.WorldGen.Data;

namespace _Game.Systems.WorldGen.Stages
{
    public class ModuleGridStage : IGenStage
    {
        public int Order => 30;
        public bool Enabled => true;

        public void Execute(WorldData data)
        {
            int gs = data.gridSize;
            float cs = data.cellSize;
            float offsetX = (800f - gs * cs) * 0.5f; // 居中
            float offsetZ = (800f - gs * cs) * 0.5f;

            data.moduleGrid = new GridCell[gs, gs];

            var allTypes = GetAllModuleTypes(); // 所有有效类型

            for (int x = 0; x < gs; x++)
            for (int z = 0; z < gs; z++)
            {
                data.moduleGrid[x, z] = new GridCell
                {
                    type = ModuleType.Unknown,
                    candidates = new List<ModuleType>(allTypes),
                    collapsed = false,
                    gridPos = new Vector2Int(x, z),
                    worldX = offsetX + x * cs + cs * 0.5f,
                    worldZ = offsetZ + z * cs + cs * 0.5f,
                    size = cs
                };
            }

            // 按城市风格缩小初始候选集
            ApplyStyleConstraints(data);
        }

        void ApplyStyleConstraints(WorldData data)
        {
            int gs = data.gridSize;
            int center = gs / 2;

            switch (data.cityStyle)
            {
                case CityStyle.Radial:
                    // 中心格只允许商业或道路
                    SetCandidates(data.moduleGrid[center, center],
                        new[] { ModuleType.Commercial, ModuleType.Road });
                    break;

                case CityStyle.Linear:
                    // 中轴线所有格子只允许道路
                    for (int i = 0; i < gs; i++)
                    {
                        SetCandidates(data.moduleGrid[i, center], new[] { ModuleType.Road });
                        SetCandidates(data.moduleGrid[center, i], new[] { ModuleType.Road });
                    }
                    break;

                case CityStyle.River:
                    // 中轴格子只允许水域
                    for (int i = 0; i < gs; i++)
                        SetCandidates(data.moduleGrid[i, center], new[] { ModuleType.Water });
                    break;

                case CityStyle.ForestEdge:
                    // 第一列格子只允许森林或空地
                    for (int z = 0; z < gs; z++)
                    {
                        SetCandidates(data.moduleGrid[0, z],
                            new[] { ModuleType.Forest, ModuleType.Empty });
                    }
                    break;
            }
        }

        void SetCandidates(GridCell cell, ModuleType[] allowed)
        {
            cell.candidates.Clear();
            cell.candidates.AddRange(allowed);
        }
    }
}
```

## 4. ModuleAssignmentStage.cs — WFC 约束求解（核心）

```csharp
// 文件：Assets/_Game/Systems/WorldGen/Stages/ModuleAssignmentStage.cs
using System.Collections.Generic;
using System.Linq;
using _Game.Systems.WorldGen.Data;

namespace _Game.Systems.WorldGen.Stages
{
    public class ModuleAssignmentStage : IGenStage
    {
        public int Order => 35;
        public bool Enabled => true;

        // 邻接表：记录不能相邻的配对
        static readonly HashSet<(ModuleType, ModuleType)> DenyAdjacency = new()
        {
            (ModuleType.Commercial, ModuleType.Industrial),
            (ModuleType.Commercial, ModuleType.Suburban),
            (ModuleType.Commercial, ModuleType.Forest),
            (ModuleType.ResidentialDense, ModuleType.Industrial),
            (ModuleType.ResidentialSparse, ModuleType.Industrial),
            (ModuleType.Industrial, ModuleType.ResidentialDense),
            (ModuleType.Industrial, ModuleType.ResidentialSparse),
            (ModuleType.Industrial, ModuleType.Water),
        };

        public void Execute(WorldData data)
        {
            var grid = data.moduleGrid;
            int gs = data.gridSize;

            // 循环直到所有格子坍缩
            while (true)
            {
                // 找熵最小格子
                var cell = FindLowestEntropy(grid, gs);
                if (cell == null) break; // 全部坍缩完了

                // 坍缩
                var chosen = WeightedRandom(cell.candidates, data.rng);
                cell.type = chosen;
                cell.candidates.Clear();
                cell.collapsed = true;

                // 传播约束
                Propagate(grid, gs, cell.gridPos.x, cell.gridPos.y);
            }
        }

        GridCell FindLowestEntropy(GridCell[,] grid, int gs)
        {
            GridCell result = null;
            int minCount = int.MaxValue;
            for (int x = 0; x < gs; x++)
            for (int z = 0; z < gs; z++)
            {
                var c = grid[x, z];
                if (c.collapsed) continue;
                if (c.candidates.Count < minCount)
                {
                    minCount = c.candidates.Count;
                    result = c;
                }
            }
            return result;
        }

        void Propagate(GridCell[,] grid, int gs, int cx, int cz)
        {
            var collapsed = grid[cx, cz];
            var neighbors = GetNeighbors(grid, gs, cx, cz);

            foreach (var n in neighbors)
            {
                if (n.collapsed) continue;
                n.candidates.RemoveAll(cand =>
                    DenyAdjacency.Contains((collapsed.type, cand)) ||
                    DenyAdjacency.Contains((cand, collapsed.type)));

                // 候选集空了 → 用允许的类型填充（简单兜底）
                if (n.candidates.Count == 0)
                {
                    n.candidates.Add(ModuleType.Suburban);
                    n.candidates.Add(ModuleType.ResidentialSparse);
                }
            }
        }

        List<GridCell> GetNeighbors(GridCell[,] grid, int gs, int x, int z)
        {
            var list = new List<GridCell>();
            if (x > 0) list.Add(grid[x - 1, z]); // 左
            if (x < gs - 1) list.Add(grid[x + 1, z]); // 右
            if (z > 0) list.Add(grid[x, z - 1]); // 下
            if (z < gs - 1) list.Add(grid[x, z + 1]); // 上
            return list;
        }

        ModuleType WeightedRandom(List<ModuleType> candidates, System.Random rng)
        {
            return candidates[rng.Next(candidates.Count)];
        }
    }
}
```

## 5. MeshStage.cs — 可视化

```csharp
// 文件：Assets/_Game/Systems/WorldGen/Stages/MeshStage.cs
using UnityEngine;
using _Game.Systems.WorldGen.Data;

namespace _Game.Systems.WorldGen.Stages
{
    public class MeshStage : IGenStage
    {
        public int Order => 80;
        public bool Enabled => true;

        private const string ROOT_NAME = "WorldGen_City";

        public void Execute(WorldData data)
        {
            // 清旧
            ClearPrevious();

            // 建根
            var root = new GameObject(ROOT_NAME);

            // 遍历网格
            int gs = data.gridSize;
            float cs = data.cellSize;

            for (int x = 0; x < gs; x++)
            for (int z = 0; z < gs; z++)
            {
                var cell = data.moduleGrid[x, z];
                if (cell.type == ModuleType.Unknown || cell.type == ModuleType.Empty)
                    continue;

                // 画区域平面
                var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                plane.name = cell.type.ToString();
                plane.transform.SetParent(root.transform);
                plane.transform.position = new Vector3(cell.worldX, 0.01f, cell.worldZ);
                plane.transform.localScale = new Vector3(cs, cs, 1);
                plane.transform.rotation = Quaternion.Euler(90, 0, 0); // 平躺

                var renderer = plane.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Unlit/Transparent"));
                mat.color = ModuleColors.GetColor(cell.type);
                renderer.material = mat;

                // 如果是道路 → 加灰色 Cube
                if (cell.type is ModuleType.Road or ModuleType.RoadTee or ModuleType.RoadCross)
                {
                    var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    road.name = cell.type.ToString() + "_Road";
                    road.transform.SetParent(root.transform);
                    road.transform.position = new Vector3(cell.worldX, 0, cell.worldZ);
                    road.transform.localScale = new Vector3(cs * 0.6f, 0.2f, cs * 0.6f);
                    var roadMat = new Material(Shader.Find("Unlit/Transparent"));
                    roadMat.color = ModuleColors.GetColor(cell.type);
                    road.GetComponent<Renderer>().material = roadMat;
                }
            }
        }

        void ClearPrevious()
        {
            var old = GameObject.Find(ROOT_NAME);
            if (old != null) Object.DestroyImmediate(old);
        }
    }
}
```

**注意：** 如果 `Shader.Find("Unlit/Transparent")` 返回 null（团结引擎特有），改用 `Shader.Find("Standard")` 并设置透明模式。

## 6. WorldData.cs 修改

```csharp
// 在现有 WorldData.cs 中添加字段：
using _Game.Systems.WorldGen.Data;

public partial class WorldData
{
    // 新增字段
    public System.Random rng;
    public CityStyle cityStyle;
    public WealthLevel wealthLevel;
    public int gridSize;                    // 5/6/8
    public float cellSize;                  // 格子边长
    public GridCell[,] moduleGrid;          // 模块网格
}
```

## 7. SeedStage.cs 修改

```csharp
public class SeedStage : IGenStage
{
    public int Order => 10;
    public bool Enabled => true;

    public void Execute(WorldData data)
    {
        data.rng = new System.Random(data.seed);
    }
}
```

## 8. WorldGenEditorWindow.cs 更新

```csharp
// Tools/WorldGen/Preview 窗口
// 添加两个按钮：
// - "生成城市" → 跑完整 Pipeline
// - "清除全部" → 删除 WorldGen_City 根对象
```

## 9. 旧 Stage 禁用

所有旧 Stage（VoronoiStage/HeightStage/RoadStage/SettlementStage 等）统一设置：
```csharp
public bool Enabled => false;
```

## 测试方法
1. 打开 Tools/WorldGen/Preview
2. 输入种子，点"生成城市"
3. 看到：彩色半透明平面排成网格 + 道路灰块 + 标签
4. 不同种子 → 不同布局
5. 同种子 → 完全一致

## 注意事项
- 所有旧 Stage 文件保留不删，只设 Enabled=false
- 纯数据驱动，MeshStage 只做可视化不改变数据
- System.Random 保证确定性
- 总代码量约 450~550 行
