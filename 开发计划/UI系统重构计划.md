# UI系统 UGUI 重构计划

> **来源**: 开发计划书.md 末尾 · **状态**: 📋 设计定稿 · **日期**: 2026-06-10
> **依赖**: 无（纯UI层改造，不碰系统逻辑）
> **目标**: 消除全部运行时 IMGUI（`OnGUI()`），统一到 UGUI Canvas 体系

---

## 现状

| 系统 | 文件数 | 用途 |
|------|--------|------|
| UGUI (`UnityEngine.UI`) | 8 | 背包、HUD条、拖拽、交互提示、容器窗口 |
| IMGUI (`OnGUI()`) | 14 | 状态HUD、合成、建造、电力、AI机器人、调试 |
| UI Toolkit | 0 | — |

两套系统通过每个面板里的 `CloseOtherUIs()` 手动互斥，无统一管理层。

## 梯度一：简单迁移（~1.5 天）

只换渲染层，不改逻辑。`GUI.Label`→`Canvas Text`，`GUI.Button`→`UGUI Button`。

| 文件 | 方案 |
|------|------|
| `DecibelHUD.cs` | Canvas Text 噪声等级 |
| `WeatherHUD.cs` | Canvas Text 天气 |
| `TopLeftHUD.cs` | Canvas Text 左上状态（时间/车速/武器） |
| `DevTools.cs` | Canvas + VerticalLayout 按钮列表 |
| `DebugPanel.cs` | Canvas Text 面板 |
| `ZombieSpawnDebugWindow.cs` | Canvas Panel + Slider |
| `PowerSourceUI.cs` | Canvas Image + Text（电源状态/燃料条/按钮） |

## 梯度二：中等迁移（~3 天）

需要 ScrollRect + GridLayout 替换滚动列表，核心逻辑不动。

| 文件 | 方案 |
|------|------|
| `CraftingUI.cs` | ScrollRect + GridLayout 配方列表 + 材料预览（参考现有 InventoryUI 动态格子模式） |
| `ProductionDeviceUI.cs` | 同上 + 设备链接按钮 |
| `ChemicalResearchUI.cs` | ScrollRect 科技树面板 |
| `BuildMenuUI.cs` | GridLayout + 按钮预制体（标签栏+物品网格+材料面板） |
| `TerminalUI.cs` | ScrollRect + Text（电网数据） |

## 梯度三：复杂迁移（~3 天）

`AIBotUI` 的 6 个 `GUI.Window` 重构为 UGUI Panel 栈，物品渲染复用 InventoryUI 的格子生成逻辑。

| 文件 | 方案 |
|------|------|
| `AIBotUI.cs` | 主面板 + 3 设置 Panel + 武器管理 Panel + 背包 Panel（`RectTransform` 拖动替代 `GUI.DragWindow`） |
| `AIBotInventoryUI.cs` | 直接用现有 InventoryUI 网格模式 |

## 不会丢失的功能

- **滚动列表** → `ScrollRect`（比 `BeginScrollView` 流畅，无 GC 分配）
- **拖拽窗口** → `IPointerDownHandler` + `IDragHandler`（已有 `ItemDragHandler` 先例）
- **动态物品格子** → `InventoryUI.cs` 已验证（GameObject + Image + Text）
- **实时更新** → Canvas 脏标记驱动，不再每帧 `OnGUI()` 分配 GC
- **输入框/按钮/滑块** → UGUI 原生组件

## 实施策略

- 每个梯度独立可验证，不跨梯度依赖
- 梯度一完成 → 删掉 7 个 `OnGUI()`，HUD 全 UGUI
- 梯度二完成 → 游戏不再有运行时 IMGUI
- 梯度三完成 → 全 UI 体系统一
- 保留 `DevTools.cs` 为 IMGUI 可接受（仅开发用，正式版关闭）

## 工时估算

| 梯度 | 文件 | 工时 |
|------|------|------|
| 一：HUD/调试 | 7 | 1.5 天 |
| 二：建造/合成 | 5 | 3 天 |
| 三：AI 机器人 | 2 | 3 天 |
| **合计** | **14** | **7.5 天** |

---

*本文档是 `开发计划书.md` UI系统UGUI重构计划的详细设计拆分。*
