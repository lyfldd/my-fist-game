using System.Collections.Generic;
using UnityEngine;
using _Game.Core;

namespace _Game.Systems.WorldGen.Data
{
    /// <summary>
    /// 一级模块类型（城市区块级，120~240m）。
    /// </summary>
    public enum ModuleType
    {
        Unknown,
        Commercial,         // 商业区（红色）
        ResidentialDense,   // 住宅区密集（蓝色）
        ResidentialSparse,  // 住宅区稀疏（浅蓝）
        Industrial,         // 工业区（灰色）
        Suburban,           // 郊区（浅绿）
        Road,               // 主干道直段（深灰）
        RoadTee,            // T字路口（深灰）
        RoadCross,          // 十字路口（深灰）
        Water,              // 水域（浅蓝）
        Forest,             // 森林边界（深绿）
        Empty               // 空地/禁建
    }

    /// <summary>
    /// 城市风格枚举。
    /// </summary>
    public enum CityStyle
    {
        Radial,       // 中心辐射
        Linear,       // 沿路延伸
        River,        // 沿河两岸
        ForestEdge    // 临森林边缘
    }

    /// <summary>
    /// 城市繁华等级。
    /// </summary>
    public enum WealthLevel
    {
        Poor = 0,     // 贫瘠
        Normal = 1,   // 普通
        Rich = 2      // 繁华
    }

    // ============================================================
    // ModuleCandidate — WFC 候选（含尺寸）
    // ============================================================

    /// <summary>
    /// WFC 候选条目：模块类型 + 在基础网格中占用的格数。
    /// 基础单元 = 40m，所以 (3,3) = 120m×120m，(5,5) = 200m×200m。
    /// </summary>
    public struct ModuleCandidate
    {
        public ModuleType Type;
        public int CellWidth;   // 占用格子宽度 (X方向)
        public int CellHeight;  // 占用格子高度 (Z方向)

        public ModuleCandidate(ModuleType type, int w, int h)
        {
            Type = type;
            CellWidth = w;
            CellHeight = h;
        }

        public override string ToString() => $"{Type}({CellWidth}×{CellHeight})";
    }

    // ============================================================
    // GridCell — 网格单元格
    // ============================================================

    /// <summary>
    /// 基础网格单元格 (40m×40m)。
    /// class 引用类型 — WFC 原地修改候选集。
    /// </summary>
    [System.Serializable]
    public class GridCell
    {
        public ModuleType type = ModuleType.Unknown;
        public List<ModuleCandidate> candidates = new();
        public bool collapsed;

        /// <summary> 该格是否属于某个多格模块的非锚点部分。 </summary>
        public bool occupied;

        /// <summary> 所属模块的唯一 ID（锚点格和非锚点格共享）。0=未分配。 </summary>
        public int moduleId;

        /// <summary> 如果是锚点，模块占据的格子范围。仅锚点格有效。 </summary>
        public int moduleWidth = 1;
        public int moduleHeight = 1;

        public Vector2Int gridPos;
        public float worldX;
        public float worldZ;

        /// <summary> 该格实际渲染尺寸 (m)。锚点格=宽×基础单元，非锚点=0。 </summary>
        public float size;

        /// <summary> 该格是否是模块的锚点（左上角）。 </summary>
        public bool IsAnchor => !occupied && collapsed && type != ModuleType.Unknown;

        /// <summary> 该格是否能作为候选锚点参与 WFC 坍缩。 </summary>
        public bool CanCollapse => !collapsed && !occupied;
    }

    // ============================================================
    // ModuleSizes — 全局模块尺寸表
    // ============================================================

    /// <summary>
    /// 每种一级模块类型的合法尺寸（基础格数，每格=40m）。
    /// WFC 从此表获取候选尺寸。
    /// </summary>
    public static class ModuleSizes
    {
        /// <summary> 所有模块类型的尺寸选项表。 </summary>
        public static readonly Dictionary<ModuleType, List<(int w, int h)>> Table = new()
        {
            // 道路：1 格宽，多格长用于连接不同区域
            { ModuleType.Road,      new() { (1, 1), (1, 2), (1, 3), (1, 4), (2, 1), (3, 1), (4, 1) } },
            { ModuleType.RoadTee,   new() { (1, 1) } },
            { ModuleType.RoadCross, new() { (1, 1) } },

            // 商业区：120m(3格) 或 160m(4格)
            { ModuleType.Commercial,        new() { (3, 3), (4, 4) } },
            { ModuleType.ResidentialDense,  new() { (3, 3), (4, 4) } },
            { ModuleType.ResidentialSparse, new() { (3, 3), (4, 4) } },

            // 工业/郊区：200m(5格)
            { ModuleType.Industrial, new() { (5, 5) } },
            { ModuleType.Suburban,   new() { (5, 5) } },

            // 水域/森林：可变尺寸
            { ModuleType.Water,  new() { (2, 2), (3, 3), (4, 4) } },
            { ModuleType.Forest, new() { (2, 2), (3, 3), (4, 4) } },

            // 空地：1格填充
            { ModuleType.Empty, new() { (1, 1) } },
        };

        /// <summary> 获取某类型的首选尺寸（渲染/显示用，用最小尺寸）。 </summary>
        public static (int w, int h) GetDefaultSize(ModuleType t)
        {
            if (Table.TryGetValue(t, out var list) && list.Count > 0)
                return list[0];
            return (1, 1);
        }

        /// <summary> 以米为单位获取模块实际大小。 </summary>
        public static float GetSizeMeters(ModuleType t, int width, int height, float baseUnit)
        {
            return Mathf.Max(width, height) * baseUnit;
        }
    }

    // ============================================================
    // ModuleColors — 颜色和标签映射
    // ============================================================

    /// <summary>
    /// 模块颜色和标签映射。
    /// </summary>
    public static class ModuleColors
    {
        public static Color GetColor(ModuleType t) => t switch
        {
            ModuleType.Commercial => new Color(0.90f, 0.22f, 0.21f, 0.6f),            // 红半透
            ModuleType.ResidentialDense => new Color(0.12f, 0.56f, 0.90f, 0.6f),      // 蓝半透
            ModuleType.ResidentialSparse => new Color(0.39f, 0.71f, 0.96f, 0.6f),     // 浅蓝半透
            ModuleType.Industrial => new Color(0.62f, 0.62f, 0.62f, 0.6f),            // 灰半透
            ModuleType.Suburban => new Color(0.65f, 0.84f, 0.65f, 0.6f),             // 浅绿半透
            ModuleType.Road or ModuleType.RoadTee or ModuleType.RoadCross
                => new Color(0.38f, 0.38f, 0.38f, 1f),                               // 深灰不透
            ModuleType.Water => new Color(0.56f, 0.79f, 0.98f, 0.4f),                // 浅蓝半透
            ModuleType.Forest => new Color(0.22f, 0.55f, 0.24f, 0.6f),               // 深绿半透
            _ => new Color(0.3f, 0.3f, 0.3f, 1f)
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
            ModuleType.Empty => "空地",
            _ => ""
        };
    }
}
