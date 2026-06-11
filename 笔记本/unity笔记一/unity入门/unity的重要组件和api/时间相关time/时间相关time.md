---
title: "Time 时间相关"
date: 2026-04-29
tags:
  - Unity
  - 团结引擎
aliases: ["时间相关time"]
source: Unity学习笔记本
---

# Time 时间相关
## 概念

`Time` 是 Unity 提供的时间管理器，所有属性都是 `static`，直接通过 `Time.xxx` 访问。它控制着游戏时间（受 `timeScale` 影响）和实际墙钟时间的分离，是实现动画、计时、暂停等功能的基石。


## 用法

### 1. 时间缩放比例（timeScale）

```csharp
```
// 暂停
Time.timeScale = 0;

// 正常速度
Time.timeScale = 1;

// 2倍速（子弹时间/慢动作）
Time.timeScale = 2;

// 0.5倍速
Time.timeScale = 0.5f;

> ⚠️ `timeScale = 0` 会导致所有 `Time.deltaTime` 为 0，所有使用 `deltaTime` 的移动和动画都会停止。但 `Time.unscaledDeltaTime` 和 `Time.unscaledTime` 不受影响。


### 2. 帧间隔时间（DeltaTime）

```csharp
```
// 受 timeScale 影响（暂停时不前进）
print(Time.deltaTime);

// 不受 timeScale 影响（即使暂停也计时）
print(Time.unscaledDeltaTime);

**用于位移的典型写法：**
```csharp
```
// 暂停时停止移动
transform.Translate(direction * speed * Time.deltaTime);

// 暂停时仍然动画（UI动画等）
uiElement.localScale = Vector3.one * (1 + Time.unscaledTime % 1);


### 3. 游戏开始到现在的时间

```csharp
```
// 受 timeScale 影响
print(Time.time);   // 从游戏开始累计的秒数（受暂停影响）

// 不受 timeScale 影响
print(Time.unscaledTime);  // 从游戏开始累计的真实秒数

典型场景：
- 单机游戏关卡计时：`startTime = Time.time;` → 结束时 `elapsed = Time.time - startTime`
- 倒计时：`countdown = 10f - Time.time;`


### 4. 物理帧间隔时间

```csharp
```
// 受 timeScale 影响（FixedUpdate 中用）
print(Time.fixedDeltaTime);

// 不受 timeScale 影响
print(Time.fixedUnscaledDeltaTime);

> `FixedUpdate` 以固定频率（默认 0.02秒/50Hz）运行，`fixedDeltaTime` 是每次物理更新的间隔。


### 5. 帧数

```csharp
```
// 从开始到现在的总帧数
print(Time.frameCount);

只增不减，常用于判断"第一帧"、跳帧渲染等。


### 一图总结

| 属性 | 用途 | 受 timeScale 影响 |
|------|------|:-----------------:|
| `deltaTime` | 帧间隔（帧位移） | ✅ |
| `unscaledDeltaTime` | 帧间隔（UI动画） | ❌ |
| `time` | 游戏开始后累计时间 | ✅ |
| `unscaledTime` | 真实累计时间 | ❌ |
| `fixedDeltaTime` | 物理帧间隔 | ✅ |
| `fixedUnscaledDeltaTime` | 真实物理帧间隔 | ❌ |
| `frameCount` | 总帧数 | ❌ |
| `timeScale` | 时间缩放比例 | — |


## 坑点

- **最常见的坑**：`Time.timeScale = 0` 后，`Time.deltaTime` 变成 0，所有 `Translate` 和 `Vector3.Lerp` 停止。但 `Time.time` 也变成 0，关卡计时会出错——此时用 `Time.unscaledTime`。
- 协程（Coroutine）中的 `WaitForSeconds` 受 `timeScale` 影响，`WaitForSecondsRealtime` 不受影响。
- 不同帧率下用同一个 `speed` 乘以 `Time.deltaTime` 效果一致，但不同帧率下用同一个帧间隔直接位移会出问题（快帧率走更远）。
- `fixedDeltaTime` 是 `1/50 = 0.02`，`Time.timeScale` 改变时 FixedUpdate 频率不变。


## 应用场景

| 需求 | 推荐属性 |
|------|----------|
| 角色移动/子弹飞行 | `Time.deltaTime` |
| UI 动画（暂停时也动） | `Time.unscaledDeltaTime` |
| 关卡计时 | `Time.time - startTime` |
| 暂停菜单计时 | `Time.unscaledTime` |
| 物理模拟速度 | `rigidbody.velocity * Time.fixedDeltaTime` |
| 判断是否第一帧 | `if (Time.frameCount == 1)` |


## 源码（部分原理）

```csharp
```
// Time.timeScale 的实际影响（简化）
public static float deltaTime
{
```csharp
```
get
{
```csharp
```
return realDeltaTime * timeScale; // 真实间隔 × 缩放比例
}
}

public static float time
{
```csharp
```
get { return startTime + deltaTime累加; } // 暂停时 deltaTime=0，所以 time 停止
}

`timeScale` 是全局乘数，设为 0 时 `deltaTime` 恒为 0，所有依赖它的动画/移动暂停。


## 扩展

- 子弹时间（Bullet Time）效果：平滑过渡 `timeScale` 从 1 到 0.1，不要突变：
  ```csharp
```
  Time.timeScale = Mathf.Lerp(Time.timeScale, targetTimeScale, transitionSpeed * Time.unscaledDeltaTime);
- 如果需要不受 `timeScale` 影响的暂停系统：用 `bool isPaused` 标志，协程中用 `while (isPaused) yield return null;` 而不是 `WaitForSeconds`。