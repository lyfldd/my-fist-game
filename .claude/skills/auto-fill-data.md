---
name: auto-fill-data
description: 自动填充 Unity ScriptableObject 数据：扫描现有物品/配置，按分类规则生成编辑器脚本，一键写入 LootTable、ContainerProfile、物品注册等重复性数据工作。
---

# Auto-Fill Data — Unity 配置数据自动填充

## 项目环境

- **项目类型**: Unity 团结引擎 3D，内置渲染管线
- **配置根路径**: `Assets/_Game/Config/`
- **编辑器脚本路径**: `Assets/_Game/Editor/`
- **执行方式**: 生成 C# 编辑器脚本 → 用户在 Unity 运行 `Tools → XXX`

## 核心流程（4 步）

### 步骤 1: 扫描现有资源

读取目标相关目录，列出所有可用资产：

- 物品: `Assets/_Game/Config/Consumables/`, `Weapons/`, `Equipment/*/`, `Materials/`
- 容器: `Assets/_Game/Config/LootTables/`, `ContainerProfiles/`
- 其他配置按需扫描

通过读取 `.asset` YAML 文件提取 `itemName`、`category`、`equipSlot` 等字段。

### 步骤 2: 按规则分类

用户提供分配目标（或由 skill 根据物品属性自动推断）：

**通用分类规则**:
| 物品属性 | → 分配方向 |
|----------|-----------|
| Food 类消耗品 | 冰箱、厨房容器 |
| Medical 类消耗品 | 柜子、医疗包、尸体 |
| Material 类 | 板条箱、工具箱 |
| Weapon（近战/手枪） | 尸体、柜子 |
| Weapon（步枪/军用） | 军火箱（未来） |
| 民用装备 (T恤/牛仔裤/皮带) | 柜子、尸体 |
| 军用装备 (战术系列) | 军需品（未来） |
| 头盔（民用） | 柜子、尸体 |
| 头盔（军用） | 军需品（未来） |
| 消耗品权重 | 基础物资 5-8，稀有品 1-2 |
| 材料权重 | 核心建材 7-8，辅助材料 3-5 |

### 步骤 3: 生成编辑器脚本

生成独立、可直接运行的 C# 编辑器脚本，遵循以下模板：

```csharp
using UnityEditor;
using UnityEngine;
using _Game.Config;

public class AutoFill_{TaskName}
{
    [MenuItem("Tools/AutoFill: {TaskName}")]
    public static void Run()
    {
        // 每个目标表的填充逻辑

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("AutoFill 完成: {summary}");
    }

    static LootTable LoadTable(string name) { ... }
    static void AddEntry(LootTable table, string itemPath, float weight, int min, int max) { ... }
}
```

**命名规范**: `AutoFill_{任务描述}.cs`，如 `AutoFill_LootTables.cs`
**菜单路径**: `Tools/AutoFill: {中文任务描述}`

### 步骤 4: 输出摘要 + 执行提醒

生成脚本后，向用户输出：
- 每个目标填充了多少条目
- 哪些物品未被分配（及原因）
- 提醒用户在 Unity 中点击对应菜单执行

## 适用场景

| 场景 | 触发示例 |
|------|---------|
| 填充 LootTable | "把物品按分类填到 4 个容器掉落表" |
| 新增物品批量注册 | "我加了 5 个新食物，帮我注册到冰箱 Loot" |
| 调整权重 | "木板条箱的木头权重从 8 改成 10" |
| 新增容器类型 | "新增军火箱容器，把军用装备和高级武器分配进去" |
| 批量更新物品属性 | "把所有食物的 useTime 加 1 秒" |

## 约束

- 始终通过 AssetDatabase API 操作，不手写 YAML
- 生成的脚本独立可运行，不依赖其他编辑器工具
- 填充前先 `entries.Clear()`，避免重复
- 找不到的资产路径用 `Debug.LogWarning` 而非报错中断
- 保留手动编辑的兼容性（只在用户主动执行时才覆盖）
