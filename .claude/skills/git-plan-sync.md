---
name: git-plan-sync
description: 提交时同步更新开发计划书与详细文档。提交后自动在计划书追记精简改动，在开发计划文件夹更/新建详细文档。
---

# Git-Plan Sync — 提交同步计划书

## 触发条件

每次 `git add` + `git commit` 之后，或用户说"提交"、"commit"、"push" 时，自动执行本 skill。

## 执行流程

### 第 1 步：收集本次改动

从 `git diff --stat HEAD~1`（或未 push 的 commits）中提取所有改动文件，按模块归类：

```
物品系统: ItemData.cs, ItemUsageSystem.cs (删除), ...
耐久系统: DurabilitySystem.cs, PlacedItem.cs, ...
```

### 第 2 步：更新计划书精简记录

在 `C:\Users\Administrator\Desktop\Unity学习一\mygame\开发计划书.md` 中，找到对应模块的章节，在章节末尾追加本次改动摘要。格式：

```markdown
> **2026-06-23 提交 `abc1234`**：
> - 修复：ItemUsageSystem 双重扣减（删除文件）
> - 新增：DurabilitySystem 核心（ConsumeDurability/GetRatio）
> - 改动：PlacedItem +itemDurability/+repairCount
```

如果模块在计划书中有跟踪表，同步更新表中的实现/审查列状态。

### 第 3 步：更新详细文档

在 `C:\Users\Administrator\Desktop\Unity学习一\mygame\开发计划/` 中：

- **已有模块有改动**：在对应的 `.md` 文件末尾的"版本历史"表格中追加一行变更记录
- **新模块/新系统**：新建 `.md` 文件，包含：标题、日期、状态、核心文件清单、代码关键改动、版本历史
- **删除模块**：在文档中标注"已删除"并注明替代方案

### 第 4 步：提交

执行 `git add -A && git commit -m "<message>" && git push`

---

## 格式约束

### 计划书内的记录必须精简

- 只写「做了什么 + 影响哪个文件」，不写设计思维过程
- 一行一条改动用 `-` 开头
- 每条改动 < 30 字

### 详细文档必须具体

- 列出改动的精确代码位置（文件名 + 行号不强制，但关键方法名必写）
- 说明改动前后行为差异
- 标注"新增/改动/删除/修复"四类动作

---

## 示例

用户说 "提交" 后：

```
1. 收集改动 → 耐久系统: DurabilitySystem.cs(新), PlacedItem.cs(改), ...
2. 开发计划书.md → 耐久系统节 追加：
   > **2026-06-23**：v1.0 核心完成（DurabilitySystem 单例 + WeaponShooting/SurvivalSystem/PlayerCombat 接入）
3. 耐久系统.md → 版本历史加一行：| v1.1 | 2026-06-23 | v1.0 代码实现 |
4. git add -A && git commit -m "..." && git push
```
