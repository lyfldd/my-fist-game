---
title: "Camera 可编辑参数详解"
date: 2026-05-07
tags:
  - Unity
  - 团结引擎
  - Camera
aliases: ["camera的可编辑参数", "Camera Inspector 参数"]
source: Unity学习笔记本
---

# Camera 可编辑参数详解

## 一、概念

Camera 组件的 Inspector 面板上暴露了大量渲染相关参数。本篇详解每个参数的含义、作用，以及什么场景下需要调整。

---

## 二、逐参数详解

### 1. Clear Flags（清除标志）

决定摄像机"背景"如何填充。

| 选项 | 效果 |
|---|---|
| **Skybox** | 渲染天空盒（默认），3D 游戏通常选这个 |
| **Solid Color** | 填充纯色，2D 游戏或 UI 摄像机选这个 |
| **Depth Only** | 只画该层物体，背景保持透明，用于叠加渲染 |
| **Don't Clear** | 不清除，颜色+深度+模板缓冲都保留，极少使用 |

**典型用法：Depth Only + 多摄像机叠加**
> 摄像机 A 渲染背景（depth=-1），摄像机 B depth=0 且 Clear Flags = Depth Only，只画角色层，背景透明 → 角色叠加在背景上 → 适合分层的游戏画面。

---

### 2. Culling Mask（剔除遮罩）

按 **Layer** 选择哪些层的对象会被渲染。

- 勾选 = 渲染该层；取消勾选 = 跳过该层
- 常见分层策略：
  - `Default` —— 所有未分层的对象
  - `Player` —— 玩家相关
  - `UI` —— UI 专用层（UI 摄像机只渲染 UI 层）
  - 自定义层（如 `Enemies`、`Environment`）

**代码控制：**

```csharp
cam.cullingMask = 1 << LayerMask.NameToLayer("Player");  // 只渲染 Player 层
cam.cullingMask = 1 << LayerMask.NameToLayer("Default"); // 只渲染 Default 层
cam.cullingMask = -1;                                     // 渲染所有层（按位全1）
```

---

### 3. Projection（投影模式）

| 模式 | 效果 | 适用场景 |
|---|---|---|
| **Perspective** | 透视投影，近大远小 | 3D 游戏 |
| **Orthographic** | 正交投影，无透视 | 2D 游戏、俯视角游戏 |

**Perspective 模式专属参数：**

- **Field of View**：垂直视野角度（°），值越大看到的范围越广（类似望远镜的倍数）
- **FOV Axis**：决定 FOV 是按竖直方向还是水平方向计算（默认竖直）

**Orthographic 模式专属参数：**

- **Size**：视口半高的大小，决定能看到多少景物。Size=5 表示从中心往上/下各 5 个单位

---

### 4. Clipping Planes（裁切平面）

| 参数 | 含义 |
|---|---|
| **Near** | 摄像机能"看到"的最近距离，近于此距离的对象不渲染 |
| **Far** | 摄像机能"看到"的最远距离，远于此距离的对象不渲染 |

> Near/Far 的差值越大，z-buffer 精度越低，可能产生"近处闪烁"问题。尽量在满足需求的前提下把范围设小。

---

### 5. Depth（渲染深度）

数值越大，**后渲染**，会覆盖数值小的摄像机。

```
摄像机 depth=0 先渲染
摄像机 depth=1 后渲染，覆盖 depth=0 的画面
```

配合 Clear Flags = Depth Only 使用：depth 大的摄像机只画物体，背景透明 → 叠加在先渲染的摄像机画面上。

---

### 6. Target Texture（目标渲染纹理）

将摄像机画面渲染到一张 **Render Texture**，而不是直接输出到屏幕。

典型应用：
- **小地图**：俯视摄像机绑定到 Render Texture → 显示在 UI 上
- **后视镜**、**监控画面**、**镜子**

创建流程：
1. `Project` 窗口右键 → `Create` → `Render Texture`
2. 将 Render Texture 拖入 Camera 的 `Target Texture` 字段
3. 创建一个 `RawImage`（UI），把该 Render Texture 赋给它

---

### 7. Occlusion Culling（遮挡剔除）

勾选后，**被其他物体完全遮挡的对象不会被渲染**，提升性能。

> 适用于 3D 场景中有大量物体互相遮挡的情况（如室内场景、密集建筑群）。简单开放场景效果不明显。

---

### 8. Viewport Rect（视口矩形）

控制摄像机渲染画面在屏幕上的**显示区域**（归一化坐标）：

| 参数 | 含义 |
|---|---|
| **X** | 左下角 x 坐标（0~1） |
| **Y** | 左下角 y 坐标（0~1） |
| **W** | 宽度（0~1） |
| **H** | 高度（0~1） |

**典型用法：分屏双摄像机**

```
摄像机A：Viewport Rect = (0, 0, 0.5, 1)  渲染左半屏
摄像机B：Viewport Rect = (0.5, 0, 0.5, 1) 渲染右半屏
```

---

### 9. Rendering Path（渲染路径）

选择摄像机的渲染方式，一般保持 **Default（Use Player Settings）**。

| 路径 | 说明 |
|---|---|
| **Use Player Settings** | 跟随全局 Project Settings |
| **Forward** | 传统前向渲染 |
| **Deferred** | 延迟渲染，适合灯光多的大型场景 |
| **Legacy Vertex Lit** | 遗留顶点光照，最老最快，不支持实时阴影 |

> 新手保持默认即可，不需要深究。

---

### 10. Other（其他参数）

- **Allow MSAA**：是否开启抗锯齿
- **Allow Dynamic Resolution**：是否允许动态分辨率调整
- **Allow HDR**：是否允许 HDR（高动态范围）

---

## 三、坑点

1. **Near/Far 设置不合理会导致 Z-Fighting**（两个面重叠时闪烁），Near 不要设太小（≥0.1），Far 不要设太大。
2. **Target Texture 开启后摄像机不再渲染到屏幕**，而是渲染到纹理。如果纹理分辨率太高会很吃性能。
3. **Depth Only 需要配合正确的渲染顺序**：背景摄像机先渲染（depth 小），前景摄像机后渲染（depth 大），且前景的 Clear Flags 要设为 Depth Only。
4. **Orthographic 的 Size 是半高**：Size=5 意味着垂直方向总共看到 10 个单位，不是 5 个。
5. **Viewport Rect 和 UI Canvas 冲突**：UI Canvas 默认覆盖全屏，用了 Viewport Rect 的摄像机渲染内容可能被 Canvas 挡住，需要设置 Canvas 为 Screen Space - Camera 并引用对应摄像机。

---

## 四、组合应用

**场景：2D 游戏，背景摄像机 + 角色摄像机**

```
摄像机A（背景）：
  Clear Flags = Solid Color（纯色）
  Culling Mask = Background层
  Depth = -1

摄像机B（角色）：
  Clear Flags = Depth Only
  Culling Mask = Player层
  Depth = 0
  Projection = Orthographic
```

**场景：小地图**

```
小地图摄像机：
  Projection = Orthographic
  Culling Mask = Ground层 | Player层（只渲染地形和玩家）
  Target Texture = 小地图RenderTexture（64x64）
```

---

## 五、原理

Viewport Rect 的 W/H > 1 会导致画面被拉伸；X/Y 超出 0~1 会超出屏幕边界。摄像机每帧将视口范围映射到实际屏幕像素，根据 Resolution 和 DPI 进行缩放。
