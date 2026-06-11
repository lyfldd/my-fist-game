---
title: "GameObject 成员变量和属性"
date: 2026-04-29
tags:
  - Unity
  - 团结引擎
aliases: ["gameobject的成员变量"]
source: Unity学习笔记本
---

# GameObject 成员变量和属性

## 概念

GameObject 的成员变量反映了这个对象在 Inspector 面板上配置的基本信息。通过 `this.gameObject.xxx` 或直接 `gameObject.xxx` 访问（MonoBehaviour 脚本中可省略 `this.`）。

## 用法

### 1. 名字（name）

```csharp
print(this.gameObject.name);
```

> 对应 Inspector 最顶部的输入框。可以通过赋值来改名：`gameObject.name = "新名字";`

### 2. 是否失活（activeSelf / activeInHierarchy）

```csharp
print(this.gameObject.activeSelf);       // 自身 SetActive 的状态
print(this.gameObject.activeInHierarchy); // 自身 + 所有父对象是否都激活
```

`activeSelf`：只看我自己的 SetActive，和父对象无关。`activeInHierarchy`：看我以及所有祖先是否都激活了。任何一个父对象失活，这里就返回 false。

### 3. 是否静态（isStatic）

```csharp
print(this.gameObject.isStatic);
```

对应 Inspector 右上角 Static 勾选框。设置为 true 后，Unity 在构建时会将这个对象视为静态（不会移动、不会缩放），从而做大量优化。

### 4. 层级（layer）

```csharp
print(this.gameObject.layer);
```

返回 int（0~31），对应 Layer 列表中的索引。可用 `LayerMask.LayerToName` 转换成字符串：

```csharp
string layerName = LayerMask.LayerToName(gameObject.layer);
```

### 5. 标签（tag）

```csharp
print(this.gameObject.tag);
```

返回字符串。Unity 推荐用 `CompareTag` 而不是直接比较 `==`，因为 `CompareTag` 内部做了缓存优化：

```csharp
if (this.gameObject.CompareTag("Player"))
{
    // ...
}
```

### 6. Transform 组件（transform）

```csharp
print(this.gameObject.transform);
```

每个 GameObject 上必有且仅有一个 Transform 组件，用来管理位置、旋转、缩放和父子关系。

## 坑点

- `activeSelf` 和 `activeInHierarchy` 完全不同——子对象即使 `SetActive(true)`，只要父对象是失活的，`activeInHierarchy` 就返回 false，且子对象的 Update 不会运行。
- `isStatic` 一旦在 Inspector 勾选，运行时通过代码改 `isStatic` 不会生效，需通过 `GameObjectUtility.SetStaticEditorFlags`。
- 标签是字符串比较，直接用 `==` 和 `CompareTag` 效果一样，但 `CompareTag` 更快。

## 应用场景

| 需求 | 对应成员 |
|------|----------|
| 改名 | `gameObject.name` |
| 判断是否显示 | `gameObject.activeInHierarchy` |
| 开关对象 | `gameObject.SetActive(bool)` |
| 碰撞检测过滤层 | `gameObject.layer` + `LayerMask` |
| 敌人 AI 判断目标类型 | `gameObject.CompareTag("Enemy")` |
| 获取位置旋转缩放 | `gameObject.transform` |

## 源码（部分原理）

```csharp
// activeInHierarchy 的内部逻辑（简化）
public bool activeInHierarchy
{
    get
    {
        if (!activeSelf) return false;
        if (parent == null) return true;
        return parent.activeInHierarchy; // 递归检查父对象
    }
}
```

所以"失活一个父对象等于失活所有子对象"，这是 Unity 的设计，不是 bug。

```csharp
// transform 成员本质
public Transform transform
{
    get { return GetComponent<Transform>(); } // 每个 GameObject 创建时自动附加
}
```

## 扩展

- `gameObject.tag` 底层是字符串，Unity 在编辑器里会把它缓存为索引，所以运行时比较用 `CompareTag` 更优。
- 层级（Layer）最大支持 32 个（0~31），前 8 个是 Unity 内置的（Default、TransparentFX、IgnoreRaycast 等），后 24 个自定义。
