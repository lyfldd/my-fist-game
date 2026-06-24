# 🪟 UI 面板管理器

> **日期**: 2026-06-24 · **状态**: 设计阶段

---

## 一、当前问题

| # | 问题 | 影响 |
|---|------|------|
| 1 | **ESC 关闭不可控** | 多个面板打开时，ESC 关哪个不确定 |
| 2 | **面板不能拖拽** | 面板挡视线时只能关掉重开 |
| 3 | **背包子 UI 无校验** | 右键物品详情可以不打开背包直接弹出 |
| 4 | **关闭按钮缺失** | 大部分面板没有关闭按钮，只能 ESC |
| 5 | **面板互相覆盖** | 背包打开后建造菜单/合成面板还能显示 |

---

## 二、核心设计

### 2.1 UIPanelManager — 全局单例

```
UIPanelManager (MonoBehaviour, DontDestroyOnLoad)
  ├── Stack<UIPanel> _stack          // ESC 按栈顶→栈底顺序关闭
  ├── OpenPanel(panel)              // 入栈 + 显示 + 绑 ESC
  ├── ClosePanel(panel)             // 出栈 + 隐藏
  ├── CloseTopPanel()              // ESC 调用，关栈顶
  └── CloseAll()                    // 全关（场景切换时）
```

**ESC 行为：** `CloseTopPanel()` → 关闭栈顶面板 → 如果栈空了，打开暂停菜单（暂不实现）。

### 2.2 面板层级规则

| 层级 | 面板 | 可拖拽 | 父面板 |
|------|------|:--:|------|
| 底 | 背包总览 (Tab) | ❌ | 无 |
| 底 | 快捷面板 (V) | ❌ | 无 |
| 底 | 合成面板 | ✅ | 无 |
| 底 | 生产设备面板 | ✅ | 无 |
| 底 | AIBot 面板 | ✅ | 无 |
| 底 | 建造菜单 | ✅ | 无 |
| 子 | 物品详情（右键） | ✅ | 背包已打开 |
| 子 | 化学研究面板 | ✅ | 合成面板已打开 |

**规则：**
1. 子面板必须先打开父面板才会打开（`OpenPanel` 时校验）
2. 子面板关闭后父面板仍然打开
3. 关闭父面板时自动关闭所有子面板

### 2.3 ESC 关闭示例

```
操作: 开背包 → 右键物品详情 → ESC
栈: [背包, 物品详情]
ESC 第1次 → 关物品详情，栈: [背包]
ESC 第2次 → 关背包，栈: []
```

### 2.4 面板结构

每个面板自动生成：
```
┌──────────────────────────────┐
│ 物品名称          [_] [X]    │ ← 标题栏（可拖拽）
├──────────────────────────────┤
│                              │
│        面板内容              │
│                              │
└──────────────────────────────┘
```

- `[_]` — 手动折叠（可选）
- `[X]` — 关闭按钮，调 `UIPanelManager.ClosePanel(this)`
- 标题栏拖动 — 挂 `UIPanelDragHandler` 组件

---

## 三、文件清单

| 文件 | 动作 | 说明 |
|------|:--:|------|
| `Core/UIPanelManager.cs` | 新建 | 栈管理 + ESC 绑定 + 子UI 父校验 |
| `Core/UIPanelDragHandler.cs` | 新建 | 挂标题栏，拖动面板 |
| `UI/InventoryUI.cs` | 改 | 注册到栈 + 子UI 父面板标识 |
| `UI/DragDropManager.cs` | 改 | 右键打开详情时校验背包已开 |
| `UI/QuickItemBar.cs` | 改 | 注册到栈 |
| `Systems/Crafting/CraftingUI.cs` | 改 | 注册 + 右上角关闭按钮 |
| `Systems/Crafting/ProductionDeviceUI.cs` | 改 | 注册 + 右上角关闭按钮 |
| `Systems/Crafting/ChemicalResearchUI.cs` | 改 | 注册为合成子面板 |
| `Systems/AIBot/AIBotUI.cs` | 改 | 注册 + 右上角关闭按钮 |
| `Systems/Building/BuildMenuUI.cs` | 改 | 注册 + 右上角关闭按钮 |
| `Editor/PreconfigureUI.cs` | 改 | 预建关闭按钮 + 标题栏 |

---

## 四、UIPanel 基类设计

```csharp
public class UIPanel : MonoBehaviour
{
    public string panelId;            // 唯一标识
    public UIPanel parentPanel;       // 父面板（子UI用）
    public bool isDraggable = true;   // 是否可拖拽
    public GameObject titleBar;       // PreconfigureUI 预建的标题栏
    public Button closeButton;        // PreconfigureUI 预建的关闭按钮

    void Awake() { /* 绑 closeButton.onClick → UIPanelManager.ClosePanel(this) */ }
}
```

所有面板继承 `UIPanel` 即可获得栈管理 + 拖拽能力。

---

## 五、实施

| 步骤 | 内容 | 估时 |
|:--:|------|:--:|
| 1 | UIPanelManager + UIPanelDragHandler 核心 | 0.5天 |
| 2 | 全部面板改继承 UIPanel + 注册 | 1天 |
| 3 | PreconfigureUI 预建标题栏+关闭按钮 | 0.5天 |
| 4 | 右键物品详情改为背包子面板 | 0.5天 |
| 5 | 测试 ESC 逐层关闭 + 拖拽 | 0.5天 |

**总计: 3天**
