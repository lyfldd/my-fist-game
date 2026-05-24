# 地图生成 Phase 1+2 问题修复

## 问题清单

### 1. 地形紫色（顶点色 Shader 不生效）
- 当前 SimpleVertexColor.shader 在 URP 下不显示顶点颜色
- 需要重写 URP 兼容的顶点色 Shader，包含 ForwardLit / DepthOnly / ShadowCaster Pass
- 或者用 Shader Graph 制作一个 VertexColor URP Shader

### 2. 道路随机不连接聚落
RoadStage 的 Delaunay 节点当前用的是 Voronoi 地貌种子点，不是聚落中心点。修复方案：

**改执行顺序**：SettlementStage（Order=50）先于 RoadStage（Order=40）执行
→ 把 RoadStage 的 Order 改为 55（聚落之后）
→ SettlementStage 的 Order 改为 40（道路之前）

或者更合理：**在不改 Order 的前提下，RoadStage 从 WorldData 读取聚落位置**
→ RoadStage 的节点 = WorldData.settlements 的中心点列表
→ 对聚落中心做 Delaunay → 主干道连接聚落
→ 如果聚落列表为空，回退到用 Voronoi 种子点（兼容 Phase 1 独立测试）

**聚落内部路网缺失**：SettlementStage 里需要添加内部道路生成逻辑
- 主干道穿城而过（沿聚落长轴）
- 支路呈十字/网格状分支
- 入户小路连接建筑

### 3. 编辑器缺少 Phase 2 独立测试按钮
WorldGenEditorWindow 需要增加按钮：
- "生成地形"（Phase 1：Seed+Voronoi+Height+Mesh）
- "生成道路"（仅 RoadStage，从已生成的 settlements 取节点）
- "生成聚落"（仅 SettlementStage，使用已生成的地形）
- "全部生成"（完整 Pipeline）

## 需要修改的文件

### Assets/_Game/Shaders/SimpleVertexColor.shader
重写为 URP 兼容版本：
```hlsl
// 需要三个 Pass：
// 1. ForwardLit - 显示顶点颜色作为基础色
// 2. DepthOnly - 用于阴影接收
// 3. ShadowCaster - 用于投射阴影
// 顶点着色器传递 color 属性，片元直接输出 color
```
或者直接创建一个 Shader Graph 资产放到 Assets/_Game/Shaders/ 下

### Assets/_Game/Systems/WorldGen/Stages/RoadStage.cs
1. 节点改为从 settlements 取中心点
2. 如果 settlements 为空，回退到 Voronoi 种子点
3. 主干道平滑扰动增强（加 Perlin 噪声偏移）
4. 生成聚落内部支路+入户小路

### Assets/_Game/Systems/WorldGen/Stages/SettlementStage.cs
1. 建筑放置后，在 Settlement 结构体中记录内部道路节点
2. 生成穿城主干道+内部支路网络
3. 道路存储在 WorldData.roads 中

### Assets/_Game/Systems/WorldGen/Data/SettlementData.cs
添加内部道路相关字段（或扩展 Road 结构体增加 roadType 枚举）

### Assets/_Game/Systems/WorldGen/Core/WorldData.cs
确保 spawnPoint 在编辑器窗口中可手动设置（方便测试）

### Assets/_Game/Editor/WorldGen/WorldGenEditorWindow.cs
增加独立测试按钮和各自对应的 Pipeline 执行方法

## 测试方法
1. 打开 Tools/WorldGen/Preview
2. 先点"生成地形" → 看到彩色地形
3. 点"生成聚落" → 看到建筑
4. 点"生成道路" → 看到道路连接聚落 + 聚落内部有路
5. 点"全部生成" → 一次跑完所有
