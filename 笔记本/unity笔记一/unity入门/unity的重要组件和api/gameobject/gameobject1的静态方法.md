---
title: "GameObject 静态方法"
date: 2026-04-29
tags:
  - Unity
  - 团结引擎
aliases: ["gameobject1的静态方法"]
source: Unity学习笔记本
---

# GameObject 静态方法

## 概念

GameObject 是 Unity 中所有实体的基类，场景里的每一个对象（空物体、灯光、相机、预设体实例等）都是 GameObject。静态方法不需要实例化，直接通过 `GameObject.方法名()` 调用。

## 用法

### 1. 创建自带集合体

```csharp
GameObject gameobj = GameObject.CreatePrimitive(PrimitiveType.立方体);
gameobj.name = "jay姐姐";
```

`PrimitiveType` 枚举常用值：`Sphere`、`Cube`、`Capsule`、`Cylinder`、`Plane`、`Quad`。

### 2. 查找单个对象

**通过名字查找**

```csharp
GameObject gameobj2 = GameObject.Find("jay姐姐");
if (gameobj2 != null)
{
    print(gameobj2.name);
}
```

> ⚠️ 效率较低，会遍历场景中所有对象。

**通过 Tag 查找（单个）**

```csharp
GameObject gameobj3 = GameObject.FindWithTag("Player");
// 或
GameObject gameobj3 = GameObject.FindGameObjectWithTag("Player");
```

> ⚠️ 无法找到失活（SetActive(false)）的对象。存在多个同名对象时，返回结果不确定。

### 3. 查找多个对象

**通过 Tag 查找（数组）**

```csharp
GameObject[] gameobj4 = GameObject.FindGameObjectsWithTag("Player");
print(gameobj4.Length);
```

> ⚠️ 无法找到失活对象。

**通过脚本类型查找**

```csharp
// 找第一个
lesson3 les = Object.FindObjectOfType<lesson3>();

// 找所有
lesson3[] lesArr = Object.FindObjectsOfType<lesson3>();
```

> 注意：这里的 `Object` 是 UnityEngine.Object，不是 C# 的 System.Object。

### 4. 实例化（克隆对象）

```csharp
public GameObject obj; // 在 Inspector 拖入预设体或场景物体

GameObject obj1 = GameObject.Instantiate(obj);
```

常用场景：子弹、炮弹、敌人等需要动态生成的对象。Instantiate 的完整重载：

```csharp
// 重载1：只传对象
Instantiate(original);

// 重载2：传对象 + 世界坐标位置
Instantiate(original, position, rotation);

// 重载3：传对象 + 世界坐标位置 + 父对象
Instantiate(original, position, rotation, parent);
```

### 5. 删除对象

```csharp
// 一般删除（下一帧执行）
GameObject.Destroy(obj1);

// 延迟删除（第二个参数：秒数）
GameObject.Destroy(obj1, 2f);

// 删除自身脚本
GameObject.Destroy(this);

// 立即删除（慎用，可能破坏 Unity 生命周期）
GameObject.DestroyImmediate(obj1);
```

> ⚠️ Destroy 默认在本帧结束时执行，不是立刻删除。如果继承了 MonoBehaviour，调用时不需要写 `GameObject.`，直接 `Destroy(this)` 即可。

### 6. 过场景不移除

```csharp
DontDestroyOnLoad(传不想被移出的对象);
```

切换场景时，默认所有对象都会被销毁（连同脚本）。用这个方法可以让指定对象（如背景音乐播放器）跨越场景保留。

## 坑点

- `Find` 效率极低，避免在 Update 中调用。
- `FindWithTag` / `FindGameObjectWithTag` 找不到失活对象。
- 多个同名对象时，`Find` 行为不确定。
- `Destroy` 不是立即删除，`DestroyImmediate` 立即删除但可能引发问题。
- `DontDestroyOnLoad` 的对象如果放入场景，切换场景时它会在新场景中残留但不从 Hierarchy 消失。

## 应用场景

| 需求 | 推荐方法 |
|------|----------|
| 生成子弹/敌人 | `Instantiate` |
| 获取单例引用 | `FindObjectOfType` |
| 按名字找物体（调试用） | `Find` |
| 按 Tag 批量获取 | `FindGameObjectsWithTag` |
| 子弹壳、爆炸特效自动消失 | `Destroy(gameObject, 2f)` |
| 背景音乐跨场景 | `DontDestroyOnLoad` |

## 源码（部分原理）

```csharp
// Find 内部遍历（简化版）
public static GameObject Find(string name)
{
    // SceneManager.GetActiveScene().GetRootGameObjects()
    // 遍历所有根对象及其子对象，递归匹配名字
}
```

Unity 内部维护了一个全局对象列表，`Find` 就是顺序遍历这个列表，所以性能差。

```csharp
// Instantiate 内部调用了 Clone
// 最终调用的是 C++ 层的 Object.Instantiate
// 配合预设体（Prefab）使用时会完整复制预设体的所有组件
```

## 扩展

- `Find` / `FindWithTag` / `FindObjectsOfType` 都是 `UnityEngine.Object` 的方法，`GameObject` 是其子类所以可以直接调用。
- 如果需要高频查找，建议用单例模式自己维护引用，而不是反复调用 Find。
- `Instantiate` 本质是"深拷贝"——它会递归复制整个层级树，包括子对象和所有组件。
