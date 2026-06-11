---
title: "Screen 屏幕相关"
date: 2026-04-30
tags:
  - Unity
  - 团结引擎
  - Screen
aliases: ["屏幕相关screen", "Screen类"]
source: Unity学习笔记本
---

# Screen 屏幕相关

## 一、概念

`Screen` 是 Unity 的**静态类**，用于获取和控制屏幕属性：分辨率、全屏模式、屏幕方向（移动端）、屏幕休眠等。

> `Screen` 读取的是**游戏窗口**的属性，不一定等于显示器物理分辨率。

---

## 二、用法 —— 全部 API 详解

### 1. 获取屏幕分辨率

```csharp
// 游戏窗口当前宽高（像素）
int w = Screen.width;
int h = Screen.height;
print($"当前分辨率：{w} x {h}");
```

- `Screen.width` / `Screen.height`：只读，返回 `int`
- 在编辑器里返回的是 Game 窗口大小；打包后返回实际窗口大小

---

### 2. 获取当前分辨率（完整信息）

```csharp
Resolution cur = Screen.currentResolution;
print($"{cur.width} x {cur.height} @ {cur.refreshRate}Hz");
```

- 返回 `Resolution` 结构体，包含 `width`、`height`、`refreshRate`
- 与 `Screen.width/height` 的区别：这里返回的是**显示器当前使用的分辨率**，不是窗口大小

---

### 3. 获取所有支持的分辨率

```csharp
Resolution[] resolutions = Screen.resolutions;
foreach (Resolution r in resolutions)
{
    print($"{r.width} x {r.height} @ {r.refreshRate}Hz");
}
```

- 返回 `Resolution[]`，列出当前显示器支持的全部分辨率
- 常用于游戏的分辨率设置菜单

---

### 4. 设置分辨率

**`Screen.SetResolution(int width, int height, bool fullscreen)`** —— 基础重载

```csharp
// 切换到 1920x1080 全屏
Screen.SetResolution(1920, 1080, true);

// 切换到 1280x720 窗口模式
Screen.SetResolution(1280, 720, false);
```

**`Screen.SetResolution(int width, int height, FullScreenMode fullScreenMode)`** —— 指定全屏模式

```csharp
Screen.SetResolution(1920, 1080, FullScreenMode.ExclusiveFullScreen);
Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
```

**`Screen.SetResolution(int width, int height, FullScreenMode fullScreenMode, int preferredRefreshRate)`** —— 完整重载，含刷新率

```csharp
Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow, 144);
```

`FullScreenMode` 枚举值：

| 枚举值 | 说明 |
|---|---|
| `ExclusiveFullScreen` | 独占全屏（性能最好，但切换慢） |
| `FullScreenWindow` | 全屏无边框窗口（Alt+Tab 切换快） |
| `MaximizedWindow` | 最大化窗口（仅 macOS） |
| `Windowed` | 普通窗口模式 |

---

### 5. 全屏开关

```csharp
// 读取当前是否全屏
bool isFullscreen = Screen.fullScreen;

// 切换全屏 / 窗口
Screen.fullScreen = true;   // 进入全屏
Screen.fullScreen = false;  // 退出全屏
```

> `Screen.fullScreen = true` 等价于 `Screen.SetResolution(Screen.width, Screen.height, true)`。

---

### 6. 全屏模式（读取/设置）

```csharp
// 读取当前全屏模式
FullScreenMode mode = Screen.fullScreenMode;

// 设置全屏模式
Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
```

---

### 7. 屏幕休眠超时（移动端）

```csharp
// 永不休眠（游戏运行时保持亮屏）
Screen.sleepTimeout = SleepTimeout.NeverSleep;

// 使用系统默认休眠设置
Screen.sleepTimeout = SleepTimeout.SystemSetting;
```

- `SleepTimeout.NeverSleep` = -1
- `SleepTimeout.SystemSetting` = -2
- 也可以直接填秒数：`Screen.sleepTimeout = 60;`（60秒后休眠）

---

### 8. 屏幕方向（移动端）

```csharp
// 读取当前方向
ScreenOrientation ori = Screen.orientation;

// 强制横屏
Screen.orientation = ScreenOrientation.LandscapeLeft;

// 强制竖屏
Screen.orientation = ScreenOrientation.Portrait;

// 自动旋转
Screen.orientation = ScreenOrientation.AutoRotation;
```

---

## 三、坑点

1. **`Screen.width/height` 在编辑器不等于显示器分辨率**：返回的是 Game 窗口的像素大小，缩放 Game 窗口后值会变，打包后才是真实游戏分辨率。
2. **`SetResolution` 不是立即生效**：下一帧渲染时才真正切换，不要在同一帧里读取 `Screen.width` 来验证。
3. **`Screen.resolutions` 在编辑器可能不准**：打包后运行才能获取准确的显示器支持分辨率列表。
4. **`fullScreenMode` 部分值平台不支持**：`MaximizedWindow` 仅 macOS 有效；WebGL 不支持独占全屏。
5. **移动端设置 `Screen.orientation` 后不要反复调用**：频繁切换方向会导致界面抖动。

---

## 四、应用场景

**游戏启动时读取当前分辨率**

```csharp
void Start()
{
    print($"游戏分辨率：{Screen.width} x {Screen.height}");
}
```

**实现分辨率设置菜单**

```csharp
Resolution[] resolutions;
int currentIndex = 0;

void Start()
{
    resolutions = Screen.resolutions;
}

// 用户选择某个分辨率后应用
public void ApplyResolution(int index)
{
    Resolution r = resolutions[index];
    Screen.SetResolution(r.width, r.height, Screen.fullScreen);
}
```

**切换全屏 / 窗口按钮**

```csharp
public void ToggleFullscreen()
{
    Screen.fullScreen = !Screen.fullScreen;
}
```

**移动端防止息屏**

```csharp
void Start()
{
    Screen.sleepTimeout = SleepTimeout.NeverSleep;
}
```

---

## 五、原理

`Screen` 类是对底层平台窗口系统的封装。PC 端修改 `fullScreen` 或调用 `SetResolution` 时，Unity 会通知操作系统的窗口管理器重新创建窗口（或切换显示模式），这一操作需要一帧的缓冲时间，所以设置后不会立即反映到 `Screen.width/height`。

---

## 六、扩展

- **UI 适配**：`Screen.width/height` 配合 `Canvas Scaler` 的 `Reference Resolution` 使用，实现不同分辨率下的 UI 自适应。
- **`Camera.main.ScreenToWorldPoint()`**：把 `Input.mousePosition` 这种屏幕坐标，转换成世界坐标，需要传入 `Screen.width/height` 计算参考中心。
- **`Display` 类**：Unity 支持多显示器（Multi-Display），`Display.displays` 可获取所有连接的显示器，`Display.Activate()` 激活指定显示器。
