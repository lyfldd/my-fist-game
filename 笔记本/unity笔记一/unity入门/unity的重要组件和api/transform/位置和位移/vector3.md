---
title: "Vector3"
date: 2026-04-29
tags:
  - Unity
  - 团结引擎
aliases: ["vector3"]
source: Unity学习笔记本
---

# Vector3

## 概念

Vector3 是 Unity 用来表示三维向量（或三维坐标系中的点）的结构体。它既可以表示一个方向（向量），也可以表示一个位置（坐标点）。所有加减乘除运算符均已重载，可以直接像数学向量一样运算。

## 用法

### 声明方式

```csharp
// 方式1：先声明后赋值
Vector3 v = new Vector3();
v.x = 10;
v.y = 10;
v.z = 10;

// 方式2：构造函数一步到位
Vector3 v2 = new Vector3(1, 2, 3);

// 方式3：先声明，再逐分量赋值
Vector3 v3;
v3.x = 10;
v3.y = 10;
v3.z = 10;
```

### 基本运算

`+` `-` `*` `/` `==` `!=` 全部重载，直接使用：

```csharp
Vector3 a = new Vector3(1, 0, 0);
Vector3 b = new Vector3(0, 1, 0);

Vector3 sum = a + b;     // (1, 1, 0)
Vector3 diff = a - b;    // (1, -1, 0)
Vector3 scaled = a * 2;  // (2, 0, 0)
bool equal = a == b;     // false
```

### 常用静态常量

| 静态常量 | 值 | 用途 |
|----------|-----|------|
| `Vector3.zero` | (0, 0, 0) | 原点 |
| `Vector3.one` | (1, 1, 1) | 全1向量 |
| `Vector3.up` | (0, 1, 0) | 世界Y轴正方向 |
| `Vector3.down` | (0, -1, 0) | 世界Y轴负方向 |
| `Vector3.right` | (1, 0, 0) | 世界X轴正方向 |
| `Vector3.left` | (-1, 0, 0) | 世界X轴负方向 |
| `Vector3.forward` | (0, 0, 1) | 世界Z轴正方向 |
| `Vector3.back` | (0, 0, -1) | 世界Z轴负方向 |

### 常用静态方法

**Distance：计算两点距离**

```csharp
Vector3.Distance(v1, v2);  // 等价于 (v1 - v2).magnitude
```

**Magnitude：向量的模长（长度）**

```csharp
float len = v.magnitude;
```

**normalized：单位向量（方向，模长为1）**

```csharp
Vector3 dir = v.normalized;  // 不改变原向量
v.Normalize();               // 就地修改，模长变为1
```

**Lerp：线性插值**

```csharp
Vector3 pos = Vector3.Lerp(start, end, t); // t = 0~1
```

**MoveTowards：向目标移动**

```csharp
Vector3 pos = Vector3.MoveTowards(current, target, maxDistanceDelta);
```

**Cross：叉积（算垂直方向）**

```csharp
Vector3 cross = Vector3.Cross(a, b);
```

**Dot：点积（判断角度）**

```csharp
float dot = Vector3.Dot(a, b);
// dot > 0 锐角，dot == 0 直角，dot < 0 钝角
```

**Angle / SignedAngle：两向量夹角**

```csharp
float angle = Vector3.Angle(from, to);
float signedAngle = Vector3.SignedAngle(from, to, axis);
```

**SqrMagnitude：模长的平方（比较大小更快，不开方）**

```csharp
if ((target - pos).sqrMagnitude < range * range) { }
```

## 坑点

- `v.normalized` 返回新向量（不修改原向量），而 `v.Normalize()` 直接修改原向量为1。
- 比较两个 Vector3 不要用 `==`，浮点数精度问题可能导致意外结果。用 `Vector3.Distance(a, b) < 0.001f` 代替。
- `Vector3.forward` 在本地坐标系下会受旋转影响（不是固定的 (0,0,1)），只有 `Transform.forward` 才是旋转后的方向。要访问世界固定方向用 `Vector3.forward`。
- `sqrMagnitude` 比 `magnitude` 快，因为省去了 `sqrt` 计算。

## 应用场景

| 需求 | 对应方法 |
|------|----------|
| 计算两点距离 | `Vector3.Distance` |
| 朝某方向移动 | `direction.normalized * speed * Time.deltaTime` |
| 匀速插值 | `Vector3.Lerp` |
| 判断是否在攻击范围内 | `sqrMagnitude < range * range` |
| 物体自动转向目标 | `transform.forward = (target - pos).normalized` |

## 源码（部分原理）

```csharp
// Vector3 的运算符重载（简化）
public static Vector3 operator +(Vector3 a, Vector3 b)
{
    return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
}

public static Vector3 operator *(Vector3 a, float d)
{
    return new Vector3(a.x * d, a.y * d, a.z * d);
}

public float Magnitude => Mathf.Sqrt(x * x + y * y + z * z);
public float SqrMagnitude => x * x + y * y + z * z;
```

`Magnitude` 底层调用了 `Mathf.Sqrt`，所以涉及大量比较时优先用 `SqrMagnitude`。

## 扩展

- 方向向量乘以速度再乘以 `Time.deltaTime` 就是帧位移：`Vector3 dir * speed * Time.deltaTime`。
- Transform 的 `right`、`up`、`forward` 是**世界坐标系下的方向**，会随旋转变化（不是固定值）。
- `Vector3.Scale` 逐分量相乘：`Vector3.Scale(v1, v2)` 等价于 `new Vector3(v1.x*v2.x, v1.y*v2.y, v1.z*v2.z)`。
