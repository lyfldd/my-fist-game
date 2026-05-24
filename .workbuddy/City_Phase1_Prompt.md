# 程序化城市生成 — Phase 1 代码实现

## 项目信息
- 路径：C:\Users\Administrator\Desktop\Unity学习一\mygame\mygame1
- 设计文档：项目根目录 `程序化城市生成算法文档.md`
- 代码根目录：Assets/_Game/Systems/WorldGen/

## 背景
整个地图生成系统已从"地形+Voronoi"方案**全面废弃**，改为**纯平面城市生成**。
旧 Stage 文件（VoronoiStage/HeightStage/RoadStage/SettlementStage/SettlementCenterStage/SettlementBuildingStage等）
全部设为 **Enabled=false**，不删文件。

**核心原则：**
- 纯 C# 数据逻辑，不依赖 Unity MonoBehaviour，仅 MeshStage 做可视化
- System.Random(seed) 确定性生成，同种子输出完全一致
- Stage 顺序固定，只读写 WorldData

## 新 Pipeline（仅 Phase 1 启用 5 个 Stage）

| Order | Stage | 启用 | 说明 |
|-------|-------|------|------|
| 10 | SeedStage | ✅ | 全局种子初始化，用 System.Random |
| 20 | **CityLayoutStage** | ✅ **新增** | 城市布局规划 |
| 30 | **MainRoadStage** | ✅ **新增（简化版）** | 直线路网 + Delaunay |
| 35 | **BlockStage** | ✅ **新增（简化版）** | 不规则多边形街区 |
| 40 | **BuildingStage** | ✅ **新增（简化版）** | 沿街排布+内部二分填充 |
| 80 | **MeshStage** | ✅ **新增** | 彩色 Cube 可视化 |

## Stage 实现要求

### 1. CityLayoutStage（Order=20）
文件：Assets/_Game/Systems/WorldGen/Stages/CityLayoutStage.cs

**功能：** 随机生成城市布局

**规则：**
- 地图范围 640m×640m（与之前一致）
- 随机生成 1~3 个城市，间距 ≥400m
- 避开出生点（出生点位置：WorldData.spawnPoint）
- 每个城市分配：
  - 四种风格之一（中心辐射/沿路延伸/沿河两岸/临森林边缘）
  - 三种规模之一（小/中/大）
  - 三种繁华等级之一（贫瘠/普通/繁华）
  - **同一张地图至少出现 2 种不同风格**
- 每个城市按风格划分功能区（CBD/住宅/工业/绿地）
- 为每个功能区生成 KeyNodes（关键节点，3~8个/区）

**新增数据结构（SettlementData.cs 或新建 CityData.cs）：**
```csharp
struct District {
    Vector2 center;
    float radius;
    CityStyle style;          // 城市风格
    ZoneType zoneType;        // 功能区类型
    int wealthLevel;          // 繁华等级 0/1/2
    int sizeLevel;            // 规模等级 0/1/2
    List<Vector2> keyNodes;   // 关键节点
}

enum CityStyle {
    Radial,         // 中心辐射型
    Linear,         // 沿路延伸型
    River,          // 沿河两岸型
    ForestEdge      // 临森林边缘型
}

enum ZoneType {
    CBD,            // 商业中心
    Residential,    // 住宅区
    Industrial,     // 工业区
    Green            // 绿地
}
```

存入 `WorldData.districts`（List<District>）。

### 2. MainRoadStage（Order=30，简化版）
文件：Assets/_Game/Systems/WorldGen/Stages/MainRoadStage.cs

**功能：** 生成主干道网络（Phase 1 先不做弯曲曲线，用直线）

**规则：**
- 收集所有 District 的 KeyNodes + 城市外围出入口节点
- Delaunay 三角剖分 → 节点连通图
- 过滤穿越城市核心的无效边线
- **Phase 1 先走直线**（Catmull-Rom 弯曲留给 Phase 2）
- 道路宽度按繁华等级：贫瘠 3m / 普通 4~5m / 繁华 6~8m
- 存入 WorldData.roads

**新增数据结构：**
```csharp
struct Road {
    Vector2 start;
    Vector2 end;
    float width;
    RoadType type;      // MainRoad / BranchRoad
    int districtId;     // 所属城区
}

enum RoadType { MainRoad, BranchRoad }
```

### 3. BlockStage（Order=35，简化版）
文件：Assets/_Game/Systems/WorldGen/Stages/BlockStage.cs

**功能：** 用道路围合生成不规则多边形街区

**规则：**
- 以主干道为基准分割平面 → 生成不规则多边形
- 每个多边形归属对应 District
- **递归细分：**
  - 在多边形内取最长对角线，生成支路
  - 支路将多边形一分为二
  - 递归直到面积小于阈值
- 新生成的支路加入 WorldData.roads
- Phase 1 支路走直线（弯曲留给 Phase 2）

**新增数据结构：**
```csharp
struct Block {
    Vector2[] vertices;          // 多边形顶点（逆时针）
    Rect bounds;
    ZoneType zoneType;
    int districtId;
    float area;
}
```

### 4. BuildingStage（Order=40，简化版）
文件：Assets/_Game/Systems/WorldGen/Stages/BuildingStage.cs

**功能：** 在街区内排布建筑

**规则：**

**沿街排布（核心）：**
- 沿街区每条边（道路曲线）采样生成建筑点位
- 采样间距随机 6~15m
- 70% 位置放建筑，30% 留白
- 建筑向街区内部退让（按风格参数）
- 建筑朝向 = 道路切线方向 + 随机偏角（按风格参数）

**内部填充（简化版）：**
- 沿街排布后，街区内部剩余空间用**随机二分法切割**填充
- 每次随机选一个方向（水平/垂直），在随机位置切割
- 切割后两个子区域继续填充
- 强制保留 20~30% 空地（不填充的区域）

**建筑类型匹配：**
| 功能区 | 主要建筑类型 | Cube 颜色（MeshStage用） |
|-------|------------|---------------------|
| CBD | 办公/商铺/公寓 | 白/红/黄 |
| 住宅 | 民宅/联排 | 蓝色 |
| 工业 | 厂房/仓库 | 灰/深灰 |
| 绿地 | 公园 | 绿色 |

**新增数据结构：**
```csharp
struct Building {
    Vector2 position;
    float rotation;         // 朝向角度
    float width;            // 占地面积
    float depth;
    int floors;             // 层数
    BuildingType type;
    int districtId;
    int blockId;
}

enum BuildingType {
    Shop, House, Apartment, Office, Factory, Warehouse, Park
}
```

### 5. MeshStage（Order=80，简化可视化）
文件：Assets/_Game/Systems/WorldGen/Stages/MeshStage.cs

**功能：** 遍历所有数据，创建彩色 Cube 可视化

**规则：**
- 遍历所有 Road → 生成长条 Cube 作为道路（灰色）
- 遍历所有 Building → 生成 Cube 按类型上色
- 遍历所有 Block → 可选：生成地面颜色标记
- 用 GameObject.CreatePrimitive(PrimitiveType.Cube)
- 所有对象放在 "WorldGen_..." 层级下
- 每次生成前先删除旧的对象

**颜色表：**
| 类型 | 颜色 |
|------|------|
| 道路 | #666666 灰 |
| 商铺/商业 | #E53935 红 |
| 住宅 | #1E88E5 蓝 |
| 公寓 | #FDD835 黄 |
| 办公 | #FFFFFF 白 |
| 工厂 | #9E9E9E 灰 |
| 仓库 | #616161 深灰 |
| 公园 | #43A047 绿 |

### 6. WorldData.cs
需要添加新字段：
```csharp
Vector2 spawnPoint;             // 出生点（已有）
List<District> districts;       // 新增
List<Road> roads;               // 新增
List<Block> blocks;             // 新增
List<Building> buildings;       // 新增
```

### 7. WorldGenEditorWindow.cs
更新独立按钮：
- "生成城市"（运行完整 Phase 1 Pipeline）
- "清除"（删除所有生成的对象）

### 8. 风格参数表（四种城市风格）
在 BuildingStage 或 CityData 中定义：

| 参数 | 中心辐射 | 沿路延伸 | 沿河两岸 | 临森林边缘 |
|------|---------|---------|---------|---------|
| 主干道弯曲偏移 | 30m | 15m | 25m | 25m |
| 支路偏移 | 15m | 8m | 12m | 15m |
| 建筑偏角 | ±8° | ±5° | ±15° | ±20° |
| 退让距离 | 3m | 5m | 2m | 4m |
| 空地占比 | 25% | 20% | 30% | 35% |
| 十字路口允许率 | 5% | 20% | 10% | 0% |

**Phase 1 中弯曲偏移参数暂不使用**（因为路是直的），但需要保留在代码中为 Phase 2 预留。

## 注意事项
- **不要删除任何旧文件**，只将旧 Stage 的 Enabled 设为 false
- 所有新 Stage 继承 IGenStage 接口并用 `UnityEngine.Random`？不对，用 `System.Random` 配合确定性种子
- 保持 `_Game.Systems.WorldGen` 命名空间
- 算法文档在项目根目录 `程序化城市生成算法文档.md`
- MeshStage 中创建的对象在重新生成时必须先清除
