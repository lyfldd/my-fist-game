---
title: "Input 输入相关"
date: 2026-04-30
tags:
  - Unity
  - 团结引擎
  - Input
aliases: ["输入相关input", "Input系统"]
source: Unity学习笔记本
---

# Input 输入相关

## 一、概念

`Input` 是 Unity 内置的**静态类**，封装了所有输入设备的状态读取。通过它可以获取：

- 鼠标位置、鼠标按键状态、滚轮状态
- 键盘按键状态
- 虚拟轴（方向输入），用于控制角色移动

> 所有 `Input` 读取都应放在 `Update()` 中，每帧轮询一次。

---

## 二、用法 —— 全部 API 详解

### 1. 鼠标位置

```csharp
Vector3 pos = Input.mousePosition;
print(Input.mousePosition);
```

- 返回 `Vector3`，z 值始终为 0
- **原点是屏幕左下角**，右为 x 正方向，上为 y 正方向
- 单位是**像素**，不是世界坐标

---

### 2. 鼠标按键检测

三个方法，参数 `button`：`0` = 左键，`1` = 右键，`2` = 中键

**`Input.GetMouseButtonDown(int button)`** —— 按下瞬间（仅触发一次）

```csharp
if (Input.GetMouseButtonDown(0))
{
    print("左键按下");
}
```

**`Input.GetMouseButtonUp(int button)`** —— 抬起瞬间（仅触发一次）

```csharp
if (Input.GetMouseButtonUp(0))
{
    print("左键抬起");
}
```

**`Input.GetMouseButton(int button)`** —— 长按（持续按住时每帧都返回 true）

```csharp
if (Input.GetMouseButton(1))
{
    print("右键持续按住");
}
```

---

### 3. 鼠标滚轮

```csharp
Vector2 scroll = Input.mouseScrollDelta;
print(Input.mouseScrollDelta);
```

- 返回 `Vector2`，滚轮改变的是 **y 值**
- 向上滚：y = 1；向下滚：y = -1；不滚：y = 0

---

### 4. 键盘按键检测

**`Input.GetKeyDown(KeyCode key)`** —— 按下瞬间，推荐用法

```csharp
if (Input.GetKeyDown(KeyCode.W))
{
    print("W 键按下");
}
```

**`Input.GetKeyDown(string name)`** —— 按下瞬间，字符串重载

```csharp
if (Input.GetKeyDown("q"))
{
    print("Q 键按下");
}
```

> ⚠️ 字符串**只能传小写**，传 `"Q"` 不识别。推荐用 KeyCode 枚举，有编译时检查。

**`Input.GetKeyUp(KeyCode key)`** —— 抬起瞬间

```csharp
if (Input.GetKeyUp(KeyCode.W))
{
    print("W 键抬起");
}
```

**`Input.GetKeyUp(string name)`** —— 抬起瞬间，字符串重载

```csharp
if (Input.GetKeyUp("w"))
{
    print("W 键抬起");
}
```

**`Input.GetKey(KeyCode key)`** —— 长按（持续按住时每帧返回 true）

```csharp
if (Input.GetKey(KeyCode.W))
{
    print("W 一直按住");
}
```

**`Input.GetKey(string name)`** —— 长按，字符串重载

```csharp
if (Input.GetKey("q"))
{
    print("Q 一直按住");
}
```

---

### 5. 默认轴输入（GetAxis / GetAxisRaw）

Unity 内置了几条虚拟轴，可以在 **编辑器 → 项目设置 → Input Manager** 里查看和修改：

![[Pasted image 20260430225108.png]]

**`Input.GetAxis(string axisName)`** —— 返回 float，范围 -1 ~ 1，带**缓动过渡**（值会渐变）

```csharp
// 水平轴：A键/左箭头 → 趋向 -1，D键/右箭头 → 趋向 1
float h = Input.GetAxis("Horizontal");
print(h);

// 垂直轴：S键/下箭头 → 趋向 -1，W键/上箭头 → 趋向 1
float v = Input.GetAxis("Vertical");
print(v);

// 鼠标横向移动速度，向右为正
float mouseX = Input.GetAxis("Mouse X");
print(mouseX);

// 鼠标纵向移动速度，向上为正
float mouseY = Input.GetAxis("Mouse Y");
print(mouseY);
```

**`Input.GetAxisRaw(string axisName)`** —— 返回 float，**只返回 -1 / 0 / 1**，无缓动

```csharp
float h = Input.GetAxisRaw("Horizontal");
print(h); // 只会输出 -1、0、1
```

| 方法 | 返回值范围 | 特点 |
|---|---|---|
| `GetAxis` | -1.0 ~ 1.0（连续） | 有缓入缓出，手感平滑 |
| `GetAxisRaw` | -1 / 0 / 1（离散） | 立即响应，无过渡 |

---

## 三、坑点

1. **`GetKeyDown` 字符串只能小写**：`"Q"` 无效，必须 `"q"`。建议统一用 `KeyCode` 枚举。
2. **`GetMouseButtonDown` 只在按下那一帧触发**：如果放在 `FixedUpdate` 里可能漏检（帧率不同步），一定要放在 `Update` 里。
3. **`mousePosition` 是屏幕像素坐标**：不能直接当世界坐标用，需要用 `Camera.main.ScreenToWorldPoint()` 转换。
4. **`mouseScrollDelta` 在不同平台精度不同**：PC 一般是整数 ±1，触摸板可能是小数。
5. **轴名大小写敏感**：`"horizontal"` 会报错，必须是 `"Horizontal"`。

---

## 四、应用场景

**发射子弹**

```csharp
void Update()
{
    if (Input.GetMouseButtonDown(0))
    {
        // 在枪口位置生成子弹
        Instantiate(bulletPrefab, gunTip.position, gunTip.rotation);
    }
}
```

**角色移动（GetAxis 版，手感平滑）**

```csharp
void Update()
{
    float h = Input.GetAxis("Horizontal");
    float v = Input.GetAxis("Vertical");
    transform.Translate(new Vector3(h, 0, v) * speed * Time.deltaTime);
}
```

**摄像机旋转**

```csharp
void Update()
{
    float rotX = Input.GetAxis("Mouse X") * sensitivity;
    float rotY = Input.GetAxis("Mouse Y") * sensitivity;
    transform.Rotate(Vector3.up, rotX);
    transform.Rotate(Vector3.right, -rotY);
}
```

**滚轮缩放镜头**

```csharp
void Update()
{
    float scroll = Input.mouseScrollDelta.y;
    Camera.main.fieldOfView -= scroll * zoomSpeed;
    Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView, 20f, 80f);
}
```

---

## 五、原理

`Input` 类在每帧 `Update` 前由引擎底层收集操作系统的原始输入事件，缓存成一帧快照。调用 `GetKeyDown` 等方法时，读的是这一帧的快照，而不是实时轮询硬件。这就是为什么：

- `GetKeyDown` 只在**那一帧**返回 true（下一帧快照清零）
- 不应在协程里依赖 `GetKeyDown`（协程可能不在第一帧就继续）

---

## 六、扩展

- **新版输入系统**：Unity 2019+ 提供 `Input System` 包（Package Manager 安装），支持手柄、触摸等更复杂设备，通过 `InputAction` 配置，与旧版 `Input` 类并行存在。团结引擎 2022 两套都能用，老项目用旧版即可。
- **Touch 触摸输入**：移动端用 `Input.touchCount` 和 `Input.GetTouch(int index)` 获取多点触控数据。
- **Joystick/手柄**：轴名 `"Joystick Axis 1"` 等，或用新版 Input System。
