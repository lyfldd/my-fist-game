using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen
{
    // ── 枚举 ──────────────────────────────────────────

    /// <summary>城市风格</summary>
    public enum CityStyle
    {
        Radial,         // 中心辐射型
        Linear,         // 沿路延伸型
        River,          // 沿河两岸型
        ForestEdge      // 临森林边缘型
    }

    /// <summary>功能区类型</summary>
    public enum ZoneType
    {
        CBD,            // 商业中心
        Residential,    // 住宅区
        Industrial,     // 工业区
        Green           // 绿地
    }

    /// <summary>道路类型（新管线）</summary>
    public enum CityRoadType
    {
        MainRoad,       // 主干道
        BranchRoad      // 支路
    }

    /// <summary>建筑类型（新管线）</summary>
    public enum CityBuildingType
    {
        Shop,           // 商铺
        House,          // 民宅
        Apartment,      // 公寓
        Office,          // 办公
        Factory,        // 工厂
        Warehouse,      // 仓库
        Park            // 公园
    }

    // ── 结构体 ────────────────────────────────────────

    /// <summary>功能区（城区）数据结构</summary>
    [System.Serializable]
    public struct CityDistrict
    {
        public Vector2 center;              // 城区中心世界坐标 (XZ)
        public float radius;                // 城区半径（米）
        public CityStyle style;             // 城市风格
        public ZoneType zoneType;           // 功能区类型
        public int wealthLevel;             // 繁华等级 0=贫瘠/1=普通/2=繁华
        public int sizeLevel;               // 规模等级 0=小/1=中/2=大
        public List<Vector2> keyNodes;      // 关键节点列表
    }

    /// <summary>道路数据结构（新管线）</summary>
    [System.Serializable]
    public struct CityRoad
    {
        public Vector2 start;               // 起点世界坐标 (XZ)
        public Vector2 end;                 // 终点世界坐标 (XZ)
        public float width;                 // 路宽（米）
        public CityRoadType roadType;       // 道路类型
        public int districtId;              // 所属城区索引 (-1 表示跨城区)
    }

    /// <summary>街区数据结构</summary>
    [System.Serializable]
    public struct CityBlock
    {
        public Vector2[] vertices;          // 多边形顶点（逆时针）
        public Rect bounds;                 // 包围盒
        public ZoneType zoneType;           // 功能区类型
        public int districtId;              // 所属城区索引
        public float area;                  // 面积（平方米）
    }

    /// <summary>建筑数据结构（新管线）</summary>
    [System.Serializable]
    public struct CityBuilding
    {
        public Vector2 position;            // 世界坐标 (XZ) — 建筑中心
        public float rotation;              // 朝向角度（度）
        public float width;                 // 占地面积宽（米）
        public float depth;                 // 占地面积深（米）
        public int floors;                  // 层数
        public CityBuildingType type;       // 建筑类型
        public int districtId;              // 所属城区索引
        public int blockId;                 // 所属街区索引
    }

    // ── 风格参数 ──────────────────────────────────────

    /// <summary>城市风格参数</summary>
    [System.Serializable]
    public struct CityStyleParams
    {
        public float mainRoadOffset;        // 主干道弯曲偏移（Phase 2 使用）
        public float branchOffset;          // 支路偏移（Phase 2 使用）
        public float angleDeviation;        // 建筑偏角范围（度）
        public float setback;               // 建筑退让距离（米）
        public float openSpaceRatio;        // 空地占比 (0~1)
        public float crossroadAllowRate;    // 十字路口允许率
    }

    // ── 风格配置 ──────────────────────────────────────

    /// <summary>城市风格静态配置</summary>
    public static class CityStyleConfig
    {
        public static CityStyleParams GetParams(CityStyle style)
        {
            switch (style)
            {
                case CityStyle.Radial:
                    return new CityStyleParams
                    {
                        mainRoadOffset = 30f,
                        branchOffset = 15f,
                        angleDeviation = 8f,
                        setback = 3f,
                        openSpaceRatio = 0.25f,
                        crossroadAllowRate = 0.05f
                    };
                case CityStyle.Linear:
                    return new CityStyleParams
                    {
                        mainRoadOffset = 15f,
                        branchOffset = 8f,
                        angleDeviation = 5f,
                        setback = 5f,
                        openSpaceRatio = 0.20f,
                        crossroadAllowRate = 0.20f
                    };
                case CityStyle.River:
                    return new CityStyleParams
                    {
                        mainRoadOffset = 25f,
                        branchOffset = 12f,
                        angleDeviation = 15f,
                        setback = 2f,
                        openSpaceRatio = 0.30f,
                        crossroadAllowRate = 0.10f
                    };
                case CityStyle.ForestEdge:
                    return new CityStyleParams
                    {
                        mainRoadOffset = 25f,
                        branchOffset = 15f,
                        angleDeviation = 20f,
                        setback = 4f,
                        openSpaceRatio = 0.35f,
                        crossroadAllowRate = 0f
                    };
                default:
                    return new CityStyleParams();
            }
        }

        /// <summary>根据规模和繁华等级获取道路宽度</summary>
        public static float GetRoadWidth(int sizeLevel, int wealthLevel, CityRoadType roadType)
        {
            // 基础宽度
            float baseWidth = roadType == CityRoadType.MainRoad ? 5f : 3f;

            // 规模修正
            float sizeMult = sizeLevel switch
            {
                0 => 0.8f,   // 小
                1 => 1.0f,   // 中
                2 => 1.3f,   // 大
                _ => 1.0f
            };

            // 繁华修正
            float wealthMult = wealthLevel switch
            {
                0 => 0.7f,   // 贫瘠
                1 => 1.0f,   // 普通
                2 => 1.3f,   // 繁华
                _ => 1.0f
            };

            return baseWidth * sizeMult * wealthMult;
        }

        /// <summary>根据规模和繁华等级获取建筑层数</summary>
        public static int GetFloors(int sizeLevel, int wealthLevel, CityBuildingType type)
        {
            int baseFloors = type switch
            {
                CityBuildingType.House => 1,
                CityBuildingType.Shop => 2,
                CityBuildingType.Apartment => 3,
                CityBuildingType.Office => 3,
                CityBuildingType.Factory => 2,
                CityBuildingType.Warehouse => 1,
                CityBuildingType.Park => 0,
                _ => 1
            };

            if (type == CityBuildingType.Park) return 0;

            int sizeBonus = sizeLevel switch { 0 => 0, 1 => 1, 2 => 3, _ => 0 };
            int wealthBonus = wealthLevel switch { 0 => -1, 1 => 0, 2 => 2, _ => 0 };

            return Mathf.Max(1, baseFloors + sizeBonus + wealthBonus);
        }

        /// <summary>建筑类型 Cube 颜色</summary>
        public static Color GetBuildingColor(CityBuildingType type)
        {
            switch (type)
            {
                case CityBuildingType.Shop:       return new Color(0.898f, 0.208f, 0.208f);  // #E53935 红
                case CityBuildingType.House:      return new Color(0.118f, 0.533f, 0.898f);  // #1E88E5 蓝
                case CityBuildingType.Apartment:  return new Color(0.992f, 0.847f, 0.208f);  // #FDD835 黄
                case CityBuildingType.Office:     return Color.white;                         // #FFFFFF 白
                case CityBuildingType.Factory:    return new Color(0.620f, 0.620f, 0.620f);  // #9E9E9E 灰
                case CityBuildingType.Warehouse:  return new Color(0.380f, 0.380f, 0.380f);  // #616161 深灰
                case CityBuildingType.Park:       return new Color(0.263f, 0.627f, 0.278f);   // #43A047 绿
                default:                          return Color.magenta;
            }
        }

        /// <summary>区域大小（按规模等级）</summary>
        public static float GetDistrictRadius(int sizeLevel)
        {
            return sizeLevel switch
            {
                0 => 60f,   // 小
                1 => 120f,  // 中
                2 => 180f,  // 大
                _ => 120f
            };
        }

        /// <summary>城区最小间距</summary>
        public static float MinDistrictSpacing(int sizeLevelA, int sizeLevelB)
        {
            float avgSize = (sizeLevelA + sizeLevelB) / 2f;
            return 200f + avgSize * 100f;
        }
    }
}
