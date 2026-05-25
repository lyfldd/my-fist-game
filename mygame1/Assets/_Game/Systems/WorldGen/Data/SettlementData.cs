using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen
{
    // ── 枚举 ──────────────────────────────────────────

    /// <summary>聚落大小</summary>
    public enum SettlementSize { Small, Medium, Large }

    /// <summary>聚落等级</summary>
    public enum SettlementTier { Shabby, Normal, Prosperous }

    /// <summary>聚落布局形态</summary>
    public enum SettlementMorph
    {
        CenterRadiate,  // 中心辐射型
        RoadStretch,    // 沿路延伸型
        RiverBank,      // 沿河两岸型 (Phase 2 简化)
        MountainSide    // 依山傍山型 (Phase 2 简化)
    }

    /// <summary>建筑品类</summary>
    public enum BuildingCategory
    {
        House,          // 独栋民宅
        Townhouse,      // 联排别墅
        ConvenienceStore, // 便利店
        Clinic,         // 诊所
        Restaurant,     // 餐馆
        Warehouse,      // 仓库
        Supermarket,    // 超市
        PoliceStation,  // 警局
        Ruins           // 废墟（装饰型）
    }

    // ── 结构体 ────────────────────────────────────────

    /// <summary>聚落数据结构</summary>
    [System.Serializable]
    public struct Settlement
    {
        public Vector2 center;          // 聚落中心世界坐标 (XZ)
        public SettlementSize size;     // 大小
        public SettlementTier tier;     // 等级
        public SettlementMorph morph;   // 布局形态
        public float diameter;          // 直径（米）
        public float radius => diameter * 0.5f;
        public List<int> buildingIndices; // 所属建筑的索引列表
    }

    /// <summary>建筑数据结构</summary>
    [System.Serializable]
    public struct Building
    {
        public BuildingCategory category; // 品类
        public Vector2 position;          // 世界坐标 (XZ) — 建筑中心
        public Vector2 size;              // 占地尺寸 (宽, 深)
        public float heightY;             // Y轴高度（米）
        public float rotation;            // Y轴旋转角度（度）
        public int settlementIndex;       // 所属聚落索引 (-1 表示不属于任何聚落)
    }

    /// <summary>道路类型</summary>
    public enum RoadType
    {
        MainRoad,   // 跨聚落主干道 / 聚落内部主路
        BranchRoad, // 支路
        Path        // 小路
    }

    /// <summary>道路数据结构</summary>
    [System.Serializable]
    public struct Road
    {
        public Vector2 start;       // 起点世界坐标 (XZ)
        public Vector2 end;         // 终点世界坐标 (XZ)
        public float width;         // 路宽（米）— MainRoad 4m / BranchRoad 2m / Path 1.5m
        public RoadType roadType;   // 道路类型
        public int settlementId;    // -1 = 跨聚落主干道; >=0 = 所属聚落索引（内部道路）
    }

    // ── 尺寸配置 ──────────────────────────────────────

    /// <summary>聚落/建筑相关静态配置数据</summary>
    public static class SettlementConfig
    {
        // 聚落尺寸：直径
        public const float SmallSettlementDia  = 96f;
        public const float MediumSettlementDia = 192f;
        public const float LargeSettlementDia  = 288f;

        // 聚落间距最小值（米）
        public const float SmallSpacing  = 256f;
        public const float MediumSpacing = 384f;
        public const float LargeSpacing  = 512f;

        // 出生点保护半径（米）
        public const float SpawnProtectRadius = 500f;

        // 建筑格网（米）
        public const float GridSize = 4f;

        // 建筑高度（米）— 占位值，后续可调整
        public const float DefaultBuildingHeight = 3f;
        public const float TallBuildingHeight     = 5f;

        // 主干道/支路宽度
        public const float MainRoadWidth = 4f;
        public const float SideRoadWidth = 2f;

        // ── 按聚落等级的尺寸倍率 ──
        public static float TierScale(SettlementTier tier)
        {
            switch (tier)
            {
                case SettlementTier.Shabby:    return 0.75f;
                case SettlementTier.Normal:    return 1.0f;
                case SettlementTier.Prosperous: return 1.25f;
                default: return 1.0f;
            }
        }

        // ── 按品类获取解锁等级 ──
        public static SettlementTier UnlockTier(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.House:
                case BuildingCategory.Townhouse:
                case BuildingCategory.Warehouse:
                case BuildingCategory.Ruins:
                    return SettlementTier.Shabby; // 全等级可用
                case BuildingCategory.ConvenienceStore:
                case BuildingCategory.Clinic:
                case BuildingCategory.Restaurant:
                    return SettlementTier.Normal; // 普通+
                case BuildingCategory.Supermarket:
                case BuildingCategory.PoliceStation:
                    return SettlementTier.Prosperous; // 繁华
                default: return SettlementTier.Shabby;
            }
        }

        // ── 按品类获取尺寸范围 ──
        public static Vector2 SizeRange(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.House:          return new Vector2(8f, 12f);
                case BuildingCategory.Townhouse:      return new Vector2(12f, 20f);
                case BuildingCategory.ConvenienceStore: return new Vector2(8f, 10f);
                case BuildingCategory.Clinic:         return new Vector2(8f, 12f);
                case BuildingCategory.Restaurant:     return new Vector2(10f, 16f);
                case BuildingCategory.Warehouse:      return new Vector2(12f, 20f);
                case BuildingCategory.Supermarket:    return new Vector2(20f, 32f);
                case BuildingCategory.PoliceStation:  return new Vector2(16f, 20f);
                case BuildingCategory.Ruins:          return new Vector2(6f, 12f);
                default: return new Vector2(8f, 8f);
            }
        }

        // ── 建筑颜色（调试用） ──
        public static Color GetCategoryColor(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.House:          return new Color(0.76f, 0.60f, 0.42f); // 米色
                case BuildingCategory.Townhouse:      return new Color(0.65f, 0.50f, 0.35f); // 深棕
                case BuildingCategory.ConvenienceStore: return new Color(0.40f, 0.70f, 0.90f); // 浅蓝
                case BuildingCategory.Clinic:         return Color.white;
                case BuildingCategory.Restaurant:     return new Color(0.95f, 0.55f, 0.20f); // 橙色
                case BuildingCategory.Warehouse:      return new Color(0.50f, 0.50f, 0.55f); // 灰色
                case BuildingCategory.Supermarket:    return new Color(0.85f, 0.25f, 0.25f); // 红色
                case BuildingCategory.PoliceStation:  return new Color(0.15f, 0.20f, 0.50f); // 深蓝
                case BuildingCategory.Ruins:          return new Color(0.35f, 0.35f, 0.38f); // 深灰
                default: return Color.magenta;
            }
        }
    }
}
