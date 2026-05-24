# 地图生成：地形算法重构 + Phase 2 路优先重排

## 项目信息
- 路径：C:\Users\Administrator\Desktop\Unity学习一\mygame\mygame1
- 地图设计文档：项目根目录 地图生成设计文档.md
- 当前代码在 Assets/_Game/Systems/WorldGen/
- **顶点色 Shader 已修复**，不需要动

---

## 改动 A：地形算法彻底重写（HeightStage + 删 Voronoi）

### 当前问题
- Voronoi 切块导致刀切蛋糕、边界硬断崖
- 山地突兀高墙、瞬间断崖
- 无全局宏观地形骨架
- 平原死平、山地孤立

### 新方案：多层噪声 → 海拔决定地貌

彻底放弃 Voronoi 分块做地形。改为纯噪声叠加生成连续海拔，地貌由海拔派生。

**HeightStage 新公式：**
```
Layer1 = 超低频Perlin(波长512m) × 强度8.0   ← 大山脉/大盆地
Layer2 = 低频Perlin(波长128m) × 强度3.0     ← 丘陵/山谷
Layer3 = 中频Perlin(波长32m) × 强度1.0      ← 局部起伏
Layer4 = FBM噪声(波长4~16m, 4八度) × 0.3   ← 地表细节

最终高度 = Layer1 + Layer2 + Layer3 + Layer4
```

**海拔 → 地貌的顶点颜色：**
```
≥ 6m  → 山地（棕色 #8B5A2B）
2~6m → 丘陵/森林（黄绿 #6B8E23 / 深绿 #228B22）
0~2m → 平原（草绿 #4CAF50）
< 0  → 湖泊/水域（蓝色 #2196F3）
```

**坡度约束：** 相邻顶点高度差 / 水平距离 ≤ 30°。超出则平滑压低/抬高。

### 需要改的文件

| 文件 | 改动 |
|------|------|
| Stages/HeightStage.cs | **全重写**。4层噪声叠加 + 坡度约束 + 水位线 |
| Stages/VoronoiStage.cs | **Enabled=false**。保留文件不删 |
| Core/WorldData.cs | 删 voronoiSites/voronoiBiomes 字段 |
| Data/BiomeType.cs | 保留枚举，作为派生属性 |
| Meshing/ChunkMeshBuilder.cs | 顶点颜色按海拔上色（之前按地貌枚举） |
| Stages/MeshStage.cs | 读取 heightMap，着色按海拔 |

---

## 改动 B：Phase 2 重排为先路后建筑

### 当前问题
- RoadStage 节点用了 Voronoi 点，不连聚落
- 聚落内部没路网
- 建筑压在路上
- 先摆建筑后拉路，布局乱

### 新 Pipeline

```
10  SeedStage                   种子
20  HeightStage                 地形（新算法）
30  SettlementCenterStage       **新增**：只定聚落中心+等级+形态（不摆建筑）
35  RoadStage                   **改造**：节点=聚落中心，主干道+内部路网
40  SettlementBuildingStage     **新增**：沿路排布建筑（贴在道路两侧）
50  VehicleStage
55  WorldLootStage
60  LootStage
70  SpawnStage
80  MeshStage
```

### 新增 SettlementCenterStage.cs（Order=30）
只定位置：
- 在平缓区域选聚落中心点
- 60/30/10 分尺寸（小/中/大），40/40/20 分等级（简陋/普通/繁华）
- 出生点 500m 保护
- 根据地貌确定形态类型
- 存入 WorldData.settlements（中心/直径/等级/形态）

### 改造 RoadStage.cs（Order=35）
节点 = settlements 中心点：
- Delaunay 三角剖分→主干道候选
- 筛选穿越陡坡/深水的边
- Perlin 噪声偏移蜿蜒
- 每个聚落内部生成路网：
  - 小型：穿城主干道+2~4支路
  - 中型：十字主干道+网格支路
  - 大型：井字主干道+密集支路网
- 存入 WorldData.roads（带 roadType/width/settlementId）

### 新增 SettlementBuildingStage.cs（Order=40）
**沿路两侧排布建筑**（彻底解决建筑压路问题）：
```
for each (聚落中的道路段) {
    leftSide = roadPos + 垂直方向 × 4m
    rightSide = roadPos - 垂直方向 × 4m
    for (d = 0; d < roadLength; d += 8m + buildingWidth) {
        在 leftSide+d 尝试放建筑（碰撞检测）
        在 rightSide+d 尝试放建筑
    }
}
```
- 建筑面朝道路
- 4m 网格对齐
- 按聚落等级选品类+缩放（0.75/1.0/1.25）
- 内圈→高规格，外圈→稀疏/废墟
- 存入 WorldData.buildings

### 废弃旧的 SettlementStage.cs
Enabled=false，保留文件不删

### 修改 SettlementData.cs
- 加 RoadType 枚举（MainRoad / BranchRoad / Path）
- Road 结构体加 roadType / width / settlementId

### 修改 WorldGenEditorWindow.cs
独立按钮：
- "生成地形"（Seed+Height+Mesh）
- "生成聚落中心"（CenterStage）
- "生成道路"（RoadStage）
- "生成建筑"（BuildingStage）
- "全部生成"

---

## 测试流程
1. 点"生成地形" → 连续起伏的彩色地形，无断崖
2. 点"生成聚落中心" → 看到标记点
3. 点"生成道路" → 主干道连聚落 + 内部路网
4. 点"生成建筑" → 建筑整齐沿路排列，不压路
5. 同种子每次一致

## 注意事项
- 旧 VoronoiStage / SettlementStage 只设 Enabled=false，不删文件
- 所有输出受种子控制
