---
title: "Camera 摄像机"
date: 2026-05-07
tags:
  - Unity
  - 团结引擎
  - Camera
aliases: ["camera代码", "Camera类"]
source: Unity学习笔记本
---

# Camera 摄像机

## 一、概念

`Camera` 是 Unity 中负责把 3D 场景"拍成一张图像"渲染到屏幕（或渲染纹理）的组件。场景中可以有多个摄像机并存，各自渲染不同的内容，最终叠加输出。

> `Camera` 本身既是**组件**（挂载在 GameObject 上），也是**类**（提供静态方法和属性）。

---

## 二、用法 —— 全部 API 详解

### 1. 获取摄像机

**静态属性：`Camera.main`**

```csharp
Camera cam = Camera.main;
cam.transform.Rotate(Vector3.up, 90f);
```

- 通过 **Tag 为 "MainCamera"** 的对象查找，只找第一个匹配
- ⚠️ 如果场景里有多个摄像机都打了这个 Tag，只返回第一个；未找到返回 null
- 适合单摄像机场景，不适合多摄像机项目

**静态属性：`Camera.allCamerasCount`**

```csharp
int count = Camera.allCamerasCount;
print("场景摄像机数量：" + count);
```

**静态属性：`Camera.allCameras`**

```csharp
Camera[] allCams = Camera.allCameras;
foreach (Camera cam in allCams)
{
    cam.enabled = false; // 禁用所有摄像机
}
```

- 返回当前启用的所有摄像机数组（已禁用的不算）

---

### 2. 渲染回调委托

三个静态委托，挂在摄像机渲染管线的关键节点上：

**`Camera.onPreCull`** —— 剔除（Culling）之前触发

```csharp
Camera.onPreCull += (cam) =>
{
    // 每帧剔除前调用，可在这里修改 cam 的参数
};
```

**`Camera.onPreRender`** —— 渲染之前触发

```csharp
Camera.onPreRender += (cam) =>
{
    // 渲染当前帧画面之前调用
};
```

**`Camera.onPostRender`** —— 渲染之后触发

```csharp
Camera.onPostRender += (cam) =>
{
    // 渲染完成后调用，可在这里执行后处理
};
```

> 这三个回调的 `cam` 参数就是触发该事件的摄像机实例。

---

### 3. 实例成员（Inspector 参数对应的代码）

Inspector 面板上的每个参数都可以在代码里读取和赋值：

```csharp
Camera cam = GetComponent<Camera>();

// 修改 Depth（渲染顺序）
cam.depth = 10;

// 修改 Field of View（透视模式下的视野角度）
cam.fieldOfView = 60f;

// 修改 Orthographic Size（正交模式下的视野大小）
cam.orthographicSize = 5f;

// 修改 Near Clip Plane / Far Clip Plane
cam.nearClipPlane = 0.3f;
cam.farClipPlane = 1000f;

// 修改 Culling Mask（选择性渲染层级）
cam.cullingMask = 1 << LayerMask.NameToLayer("Player");

// 修改 Target Texture（渲染到纹理）
cam.targetTexture = renderTexture;
```

---

### 4. 世界坐标 → 屏幕坐标

**`Camera.WorldToScreenPoint(Vector3 position)`**

```csharp
Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
print($"屏幕坐标：x={screenPos.x}, y={screenPos.y}, z={screenPos.z}");
```

- **x、y**：屏幕像素坐标（原点为左下角）
- **z**：对象到摄像机 z 轴的距离（常用于计算近大远小）
- 常用于：把 3D 角色的位置传给 UI（如血条、名字标签）

```csharp
// 血条跟随角色，且随距离远近调整大小
float distance = Camera.main.WorldToScreenPoint(transform.position).z;
float scale = Mathf.Clamp(10f / distance, 0.5f, 2f);
healthBarUI.transform.localScale = Vector3.one * scale;
```

---

### 5. 屏幕坐标 → 世界坐标

**`Camera.ScreenToWorldPoint(Vector3 position)`**

```csharp
Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
```

> ⚠️ `Input.mousePosition` 的 z 值默认是 0，必须手动赋值为一个有意义的数值（如对象到摄像机的距离），否则 `ScreenToWorldPoint` 会把所有对象投影到近裁切平面（nearClipPlane），导致结果错误。

**正确用法：**

```csharp
public class MouseToWorld : MonoBehaviour
{
    void Update()
    {
        Vector3 mp = Input.mousePosition;
        mp.z = Camera.main.WorldToScreenPoint(transform.position).z; // 用对象的实际距离做 z
        transform.position = Camera.main.ScreenToWorldPoint(mp);
    }
}
```

- 应用场景：鼠标点击地面移动角色、鼠标控制物体跟随准心

---

### 6. 屏幕坐标转视口坐标

**`Camera.ScreenToViewportPoint(Vector3 position)`**

```csharp
Vector3 viewport = Camera.main.ScreenToViewportPoint(Input.mousePosition);
// x,y 范围为 0~1，原点为左下角
```

---

### 7. 视口坐标转屏幕坐标

**`Camera.ViewportToScreenPoint(Vector3 position)`**

```csharp
Vector3 screen = Camera.main.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 10f));
```

---

## 三、坑点

1. **`Camera.main` 依赖 Tag**：如果主摄像机没打 "MainCamera" 标签，返回 null。
2. **`ScreenToWorldPoint` 必须给 z 赋值**：不赋值则 z=0，结果在 nearClipPlane 上，距离完全错误。
3. **`WorldToScreenPoint` 在屏幕外的 x/y 可能超出 0~分辨率范围**：判断是否在屏幕内要同时检查 x∈[0,Screen.width]、y∈[0,Screen.height]、z>0。
4. **多摄像机渲染顺序由 `depth` 决定**：depth 大的后渲染，会覆盖 depth 小的（见 `camera的可编辑参数.md`）。
5. **`onPreRender` 等回调在协程里使用要小心**：它们在同一帧的多个地方触发，协程的执行时机可能不符合预期。
6. **`allCameras` 不包含已禁用的摄像机**：禁用 `cam.enabled = false` 后该摄像机从数组里消失。

---

## 四、应用场景

**鼠标点击地面，移动角色**

```csharp
void Update()
{
    if (Input.GetMouseButtonDown(0))
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            targetPos = hit.point;
        }
    }
    transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
}
```

**跟随摄像机**

```csharp
void LateUpdate()
{
    transform.position = player.position + offset;
    transform.LookAt(player);
}
```

**实现小地图（Target Texture）**

```csharp
// 俯视摄像机挂在场景上方
// 创建 Render Texture 并赋值给该摄像机
// 在 UI 的 RawImage 上显示这张纹理
```

---

## 五、原理

摄像机每帧渲染流程：

1. **Culling（剔除）**：遍历所有对象，按 Culling Mask 过滤，决定哪些对象进入渲染队列（`onPreCull` 在此之前）
2. **投影变换**：根据 Projection 模式（Perspective/Orthographic）将 3D 坐标投影到 2D 裁切空间
3. **裁切**：去掉 Near/Far Clip Plane 范围外的对象
4. **渲染**：GPU 绘制几何体（`onPreRender` 在此之前，`onPostRender` 在此之后）

---

## 六、扩展

- **多摄像机叠加**：UI 摄像机用 `Depth Only` 的 Clear Flags，depth 设得最高，这样 UI 始终在最上层。
- **Camera CameraType**：`cam.cameraType` 可区分是 Game、SceneView、Preview 还是 RenderTexture 摄像机。
- **WorldToViewportPoint / ViewportToWorldPoint**：视口坐标（0~1）版本，用于屏幕适配。
- **新渲染管线（URP/HDRP）**：Camera 参数有调整，但核心 API 基本不变。
