# 地图生成 Phase 2 重构：先路后建筑

## 背景
当前问题是道路随机不连聚落、聚落内部没路、路像后补的。
原因：先摆了建筑再拉路（顺序反了）。
修复方案：改成**先路后建筑**，现实世界就是这样。

## 核心改动：Pipeline 重排

| 旧的 Order | 旧的 Stage | → | 新的 Order | 新的 Stage |
|-----------|-----------|:--:|-----------|-----------|
| 40 | RoadStage | → | **35** | **SettlementCenterStage**（新增） |
| 50 | SettlementStage | → | **40** | **RoadStage** |
| - | - | → | **45** | **SettlementBuildingStage**（新增） |

### 新 Pipeline 完整顺序

```
10  SeedStage                 种子
20  VoronoiStage              地貌
30  HeightStage               地形高度
35  SettlementCenterStage     新增：只定聚落中心点+等级+形态（不摆建筑）
40  RoadStage                 改造：节点 = 聚落中心点，主干道连聚落+内部路网
45  SettlementBuildingStage   新增：沿路排布建筑
50  VehicleStage              车辆
55  WorldLootStage            野外物资
60  LootStage                 容器
70  SpawnStage                僵尸
80  MeshStage                 Chunk 实例化
```

## 需要改/新增的文件

### 1. 新增 SettlementCenterStage.cs（Order=35）
文件：Assets/_Game/Systems/WorldGen/Stages/SettlementCenterStage.cs

只做定位，不摆建筑：
- 扫描平原区域，选聚落中心点
- 按 60/30/10 分配尺寸（小/中/大）
- 按 40/40/20 分配等级（简陋/普通/繁华）
- 出生点 500m 保护
- 根据地貌确定聚落形态类型（中心辐射/沿路延伸/沿河两岸/依山傍山）
- 存入 WorldData.settlements（只填中心点、直径、等级、形态）

### 2. 改造 RoadStage.cs（Order=40）
文件：Assets/_Game/Systems/WorldGen/Stages/RoadStage.cs

节点来源改为 settlements 中心点（非 Voronoi 点）：
1. 取所有聚落中心点作为 Delaunay 节点
2. Delaunay 三角剖分 → 生成主干道候选边
3. 筛选：剔除穿越陡坡/深水的边
4. 平滑微扰动（Perlin 噪声偏移），自然蜿蜒
5. 每片聚落内部生成路网：
   - 小型聚落：一条穿城主干道 + 2~4 条支路
   - 中型聚落：十字主干道 + 网格状支路 + 入户小路
   - 大型聚落：井字主干道 + 密集支路网
6. 存入 WorldData.roads（roadType 区分：主干道/支路/小路）

### 3. 新增 SettlementBuildingStage.cs（Order=45）
文件：Assets/_Game/Systems/WorldGen/Stages/SettlementBuildingStage.cs

沿路排布建筑：
1. 遍历每个聚落
2. 沿聚落内部道路两侧撒建筑位置
   - 建筑面朝道路（正门对路）
   - 建筑间距 4m 基础网格
   - 道路两侧对称或交错排列
3. 按聚落等级选品类（简陋只有民宅+废墟，繁华全品类）
4. 按聚落等级缩放尺寸（0.75/1.0/1.25）
5. 碰撞检测 + 重试
6. 内圈（离主干道近）→ 高规格建筑
   外圈（离主干道远）→ 稀疏/废墟
7. 存入 WorldData.buildings

### 4. 删除或废弃旧的 SettlementStage.cs
保留文件但设为 Enabled=false，代码迁移到两个新 Stage

### 5. 修改 SettlementData.cs
- 添加 RoadType 枚举（MainRoad / BranchRoad / Path）
- Road 结构体添加 roadType、width、settlementId 字段

### 6. 修改 WorldGenEditorWindow.cs
增加独立测试按钮：
- "生成地形"（Phase 1：Seed+Voronoi+Height+Mesh）
- "生成聚落中心"（仅 SettlementCenterStage）
- "生成道路"（仅 RoadStage + 已生成的聚落中心）
- "生成建筑"（仅 SettlementBuildingStage + 已生成的道路）
- "全部生成"（完整 Pipeline）

### 7. 修复 SimpleVertexColor.shader（紫色地形问题）
重写 URP 兼容版本，包含 ForwardLit / DepthOnly / ShadowCaster 三个 Pass
确保 Mesh 顶点颜色能正确渲染

## 测试方法
1. 打开 Tools/WorldGen/Preview
2. 点"生成地形" → 看到彩色地形
3. 点"生成聚落中心" → 看到聚落标记点（无建筑）
4. 点"生成道路" → 看到主干道连聚落 + 内部路网
5. 点"生成建筑" → 看到建筑沿路排列
6. 点"全部生成" → 一次跑完所有

## 注意事项
- 新 Stage 的 Enabled 已自动设为 true（替换旧的）
- 旧的 SettlementStage 设为 Enabled=false（保留代码）
- 不要破坏 Phase 1 的地形/Voronoi/高度功能
- 所有阶段受种子控制
