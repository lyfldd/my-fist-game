# 程序化城市生成 — Phase 2 代码实现

## 上下文
Phase 1 已完成：城市布局 + 直线路网 + 多边形街区 + 基础建筑 + Cube 可视化。
Phase 2 在 Phase 1 基础上改造，**不改动现有功能**，仅升级路网和街区为弯曲形态。

## 本阶段目标

| 改动 | 文件 | 说明 |
|------|------|------|
| **直线→弯曲** | MainRoadStage.cs | Delaunay 直线边转为 Catmull-Rom 曲线 + Perlin 偏移 |
| **路口错位** | MainRoadStage.cs | 禁止正交十字，生成 T 型/错位岔路口 |
| **支路弯曲** | BlockStage.cs | 次级支路切割时加入弯曲偏移（主干道 0.5 倍） |

## 详细要求

### 1. MainRoadStage 改造

**当前状态（Phase 1）：** Delaunay 三角剖分 → 直线连接 → 存入 roads

**改为：**

**步骤1：Delaunay 保持不动**
- Delaunay 三角剖分逻辑不变，仍从 KeyNodes 生成连通图
- 过滤规则不变

**步骤2：直线→Catmull-Rom 曲线（核心改动）**
对每条 Delaunay 边（直线），转换为 Catmull-Rom 平滑曲线：

```
原始：start ──── end（直线）

改为：start ── 控制点1 ── 控制点2 ── end（曲线）

控制点生成：
  1. 取边的中点 mid
  2. 取垂直方向向量 perp = normalize(垂直于边的方向)
  3. 用 Perlin 噪声采样偏移量：
     offset = Perlin(seed, mid.x/scale, mid.z/scale) × maxBend
     controlPoint1 = mid + perp × offset × 0.5
     controlPoint2 = mid - perp × offset × 0.5
     （两端各一个控制点，错开形成弯曲）
  4. maxBend 按城市风格配置（中心辐射 30m / 沿路 15m / 沿河 25m / 森林 25m）
```

用 4 个点（start, cp1, cp2, end）构建 Catmull-Rom 曲线，沿曲线等距采样生成道路段点列。

**步骤3：路口错位优化（核心改动）**
- 遍历所有路口（多条道路的交点）
- **禁止规整正交十字路口**：
  - 检测：如果两条道路在路口处的夹角在 85°~95° 之间
  - 处理：将其中一条道路的端点沿其方向随机偏移 2~6m
  - 偏移后该道路末端形成 T 型或错位岔路口
- **曲线端点过近（<3m）时自动向内偏移**：
  - 将两条道路的端点沿各自方向向后缩 1~3m
  - 形成自然的"错位岔路口"效果

**步骤4：道路段输出**
- 沿 Catmull-Rom 曲线等距采样（采样间距 2m）
- 生成道路段点列存入 WorldData.roads
- 每条道路保留原始 start/end 用于后续街区划分

### 2. BlockStage 改造

**当前状态（Phase 1）：** 直线主干道分割 → 直线支路递归切割

**改为：**

**步骤1：主干道分割保持不动**
- 仍以弯曲线条路段为基准分割平面
- Phase 1 的直线路段已改为弯曲，分割出来的多边形自动变不规则

**步骤2：支路切割加弯曲偏移**
```
当前：支路 = 直对角线
改为：支路 = 弯曲曲线（偏移幅度 = 对应风格的主干道偏移 × 0.5）
```

在递归切割时，对角线路径用 Catmull-Rom 曲线生成，偏移量为对应风格的支路参数：
- 中心辐射型：15m
- 沿路延伸型：8m
- 沿河两岸型：12m
- 临森林边缘型：15m

**步骤3：新支路加入全局路网**
- 新生成的弯曲支路也加入 WorldData.roads，供 BuildingStage 使用

### 3. WorldData 数据层

无需新增字段。Phase 1 已有的 `Road` 结构体已经包含 start/end/width/type/districtId。

但需要确认 Road 中是否包含**曲线采样点列**（用于建筑朝向计算）。如果没有，添加：
```csharp
struct Road {
    // ... 已有字段
    List<Vector2> curvePoints;  // 新增：沿曲线的采样点列（可选）
}
```

### 4. CurveHelper 工具类（新增推荐）

建议新增 `Assets/_Game/Systems/WorldGen/Data/CurveHelper.cs`，包含：

```csharp
static class CurveHelper {
    /// <summary> 由4个控制点生成 Catmull-Rom 曲线采样点 </summary>
    static List<Vector2> CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int segments);

    /// <summary> 用 Perlin 噪声生成垂直偏移量 </summary>
    static float PerlinOffset(int seed, float x, float z, float scale, float maxOffset);

    /// <summary> 对直线添加弯曲，返回控制点列表 </summary>
    static List<Vector2> MakeCurved(Vector2 start, Vector2 end, int seed, float maxBend);
}
```

这样 MainRoadStage 和 BlockStage 可以共用曲线生成逻辑。

### 5. 风格参数表更新

Phase 1 已经定义了参数表，Phase 2 需要用到的参数：

| 参数 | 中心辐射 | 沿路延伸 | 沿河两岸 | 临森林边缘 |
|------|---------|---------|---------|---------|
| 主干道偏移 maxBend | 30m | 15m | 25m | 25m |
| 支路偏移（主干道×0.5） | 15m | 8m | 12m | 15m |
| 十字路口允许率 | 5% | 20% | 10% | 0% |

**十字路口允许率逻辑：**
- 0% = 强制所有十字路口都错位
- 20% = 每 5 个十字路口只保留 1 个，其余 4 个错位
- 用 System.Random(seed + index) 判断每个路口是否保留

## 注意事项
- 不要破坏 Phase 1 已实现的功能
- 所有偏移量用 System.Random 和 Perlin 噪声保证种子确定性
- 街区分割逻辑保持不变，只改分割用的路径从直线→曲线
- 建筑排布（BuildingStage）保持不变，它会自动适配新的弯曲道路采样点
- MeshStage 保持不变，新弯曲道路会自然显示
