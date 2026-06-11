# UI 事件导航与面板架构

<!-- 建议截图：UGUI 导航系统在游戏手柄/键盘操作下的实际效果截图 -->

## 1. UI 导航系统（Navigation）

### 1.1 什么是 Navigation

Navigation（导航）是指用户通过**键盘**（方向键 / Tab / 回车）或**手柄**（方向键 / 确认键）在 UI 元素之间移动焦点的功能。当游戏需要脱离纯鼠标操作（如主机移植、无障碍支持、快速键盘操作）时，Navigation 是必不可少的。

每个 Selectable 控件（Button、Toggle、Slider、Dropdown 等）都有一个 **Navigation** 属性，位于 Inspector 中 Selectable 组件底部。

<!-- 建议截图：Button Inspector 面板中 Navigation 属性的展开状态截图 -->

### 1.2 五种导航模式

| 模式 | 说明 | 适用场景 | 注意事项 |
|------|------|---------|---------|
| **None** | 无导航，控件不接受键盘/手柄焦点 | 纯鼠标交互操作（如 PC 端纯点击界面） | 所有方向键操作都被忽略 |
| **Horizontal** | 仅支持左右方向导航 | 横向滚动菜单、关卡选择行 | 上下方向不移动焦点 |
| **Vertical** | 仅支持上下方向导航 | 纵向列表、聊天记录 | 左右方向不移动焦点 |
| **Automatic** | 自动计算最近的 Selectable 作为导航目标 | 通用默认值，适合简单布局 | 复杂布局可能错判目标 |
| **Explicit** | 手动为四个方向分别指定目标控件 | 精确控制导航路径（如表单、网格） | 最灵活但也最需要手动维护 |

### 1.3 Explicit 模式的高级用法

```csharp
// 通过代码设置 Explicit 导航目标
using UnityEngine.UI;

public class NavigationSetup : MonoBehaviour
{
    public Button[] menuButtons;

    void Start()
    {
        for (int i = 0; i < menuButtons.Length; i++)
        {
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;

            // 上：上一个按钮（循环）
            nav.selectOnUp = (i == 0) ? menuButtons[menuButtons.Length - 1] : menuButtons[i - 1];
            // 下：下一个按钮（循环）
            nav.selectOnDown = (i == menuButtons.Length - 1) ? menuButtons[0] : menuButtons[i + 1];

            menuButtons[i].navigation = nav;
        }
    }
}
```

### 1.4 Visualize 按钮——可视化导航连线

在 Navigation 属性右侧有一个 **Visualize** 按钮。点击后，Scene 视图中会以蓝线绘制从当前控件到各方向导航目标的连线。

> **调试技巧**：使用 Visualize 可以快速发现导航路径错误。如果连线指向了错误的对象，说明 Automatic 计算不准确，此时应切换到 Explicit 模式手动指定。

<!-- 建议截图：Visualize 按钮点击后 Scene 视图中显示的导航连线截图 -->

### 1.5 导航的热键映射

EventSystem 通过 `StandaloneInputModule`（或 `InputSystemUIInputModule`）处理键盘和手柄输入：

| 输入事件 | 默认按键 | 说明 |
|---------|---------|------|
| Submit（确认） | Return / Space / Joystick Button 0 | 触发按钮点击 |
| Cancel（取消） | Escape / Joystick Button 1 | 后退/关闭面板 |
| Move Up | UpArrow / Joystick 左摇杆上 | 向上导航 |
| Move Down | DownArrow / Joystick 左摇杆下 | 向下导航 |
| Move Left | LeftArrow / Joystick 左摇杆左 | 向左导航 |
| Move Right | RightArrow / Joystick 左摇杆右 | 向右导航 |

---

## 2. EventSystem 深入

### 2.1 EventSystem 的核心职责

EventSystem 是 UGUI 事件系统的中枢，每帧执行三个步骤：

1. **射线检测（Raycast）**：检测鼠标/触摸位置下有哪些 UI 元素
2. **事件分发（Event Dispatch）**：将输入事件发送到检测到的 UI 元素
3. **输入处理（Input Processing）**：将原始输入（鼠标、键盘、触摸）转换为标准事件

```
EventSystem 每帧流程：
├── ① 更新输入模块（InputModule 处理原始输入）
├── ② 执行射线检测（Raycaster 检测 UI 元素）
├── ③ 计算当前悬停/选中的对象
├── ④ 发送事件（ExecuteEvents.Execute）
└── ⑤ 更新选中状态（SetSelectedGameObject）
```

<!-- 建议截图：Hierarchy 中 EventSystem 对象的 Inspector 截图，标注各组件 -->
<!-- 建议截图：EventSystem 工作原理流程图 -->

### 2.2 多个 EventSystem 时的行为

- **同一场景中只应有一个 EventSystem**。多个 EventSystem 会导致输入冲突、焦点混乱
- 特殊情况：多个 Canvas 使用不同的 Event Camera 时，仍然只需要一个 EventSystem，通过多个 PhysicsRaycaster / GraphicRaycaster 处理
- UI 场景 + 3D 场景切换时，可以用一个 EventSystem 同时处理 UI（GraphicRaycaster）和 3D 对象（PhysicsRaycaster）

### 2.3 自定义 Input Module 扩展事件处理

可以通过继承 `BaseInputModule` 或 `PointerInputModule` 来扩展事件处理。常见场景：

```csharp
using UnityEngine.EventSystems;

/// <summary>
/// 自定义输入模块示例：在特定条件下屏蔽输入
/// </summary>
public class CustomInputModule : StandaloneInputModule
{
    public bool inputBlocked = false;

    public override void Process()
    {
        if (inputBlocked) return;  // 屏蔽所有输入
        base.Process();
    }

    /// <summary>
    /// 屏蔽输入指定时间
    /// </summary>
    public void BlockInputForSeconds(float seconds)
    {
        StartCoroutine(BlockCoroutine(seconds));
    }

    private System.Collections.IEnumerator BlockCoroutine(float seconds)
    {
        inputBlocked = true;
        yield return new WaitForSeconds(seconds);
        inputBlocked = false;
    }
}
```

### 2.4 常用事件接口速查表（14 个接口）

UGUI 提供了 14 个标准事件接口，满足绝大多数交互需求：

| 接口 | 回调方法 | 触发条件 | 典型用途 |
|------|---------|---------|---------|
| `IPointerEnterHandler` | `OnPointerEnter` | 鼠标/触摸进入控件区域 | 悬停高亮、Tooltip 显示 |
| `IPointerExitHandler` | `OnPointerExit` | 鼠标/触摸离开控件区域 | 恢复默认样式、Tooltip 隐藏 |
| `IPointerDownHandler` | `OnPointerDown` | 鼠标/触摸在控件上按下 | 按钮按下状态展示 |
| `IPointerUpHandler` | `OnPointerUp` | 鼠标/触摸在控件上抬起 | 按钮弹起状态恢复 |
| `IPointerClickHandler` | `OnPointerClick` | 完整点击（Down + Up 在同一对象上） | 按钮点击逻辑 |
| `IBeginDragHandler` | `OnBeginDrag` | 拖拽开始（移动一定距离后） | 记录拖拽起始数据 |
| `IDragHandler` | `OnDrag` | 拖拽持续中 | 跟随鼠标移动 |
| `IEndDragHandler` | `OnEndDrag` | 拖拽结束 | 处理放置/取消 |
| `IDropHandler` | `OnDrop` | 有其他对象拖拽到本对象上释放 | 背包格接收道具 |
| `IScrollHandler` | `OnScroll` | 鼠标滚轮滚动 | 列表滚动、缩放 |
| `ISelectHandler` | `OnSelect` | 控件被导航选中 | 选中高亮效果 |
| `IDeselectHandler` | `OnDeselect` | 控件取消选中 | 取消高亮效果 |
| `IMoveHandler` | `OnMove` | 导航方向键操作 | 自定义方向键行为 |
| `ISubmitHandler` | `OnSubmit` | 按下确认键（Return/Space） | 确认操作 |
| `ICancelHandler` | `OnCancel` | 按下取消键（Escape） | 返回上一级 |

```csharp
// 综合示例：一个可拖拽、可点击、有悬停效果的 UI 道具图标
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableItem : MonoBehaviour, 
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{
    [SerializeField] private Image itemIcon;
    private RectTransform rectTransform;
    private Vector2 originalPosition;

    void Awake() => rectTransform = GetComponent<RectTransform>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 悬停放大
        transform.DOScale(1.1f, 0.1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOScale(1f, 0.1f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = rectTransform.anchoredPosition;
        itemIcon.raycastTarget = false; // 拖拽时穿透射线
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        itemIcon.raycastTarget = true;
        // 检查是否拖拽到目标区域（通过 IDropHandler 处理）
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
            Debug.Log("双击道具——使用道具");
    }
}
```

<!-- 建议截图：拖拽道具的完整交互过程截图，包括悬停、拖拽、释放三个阶段 -->

---

## 3. 面板管理架构（Panel Manager）

### 3.1 为什么需要面板管理

没有面板管理时，UI 代码容易演变成"意大利面条"：

```
❌ 混乱的方式：
  SettingsButton.OnClick → settingsPanel.SetActive(true)  ← 到处都是 SetActive
  BackButton.OnClick → settingsPanel.SetActive(false)       ← 难以管理层级
  OpenShopButton → shopPanel.SetActive(true)               ← 忘记关上一个面板
```

**面板管理器**带来：

- **统一生命周期**：每个面板有 OnEnter / OnPause / OnResume / OnExit 四个阶段
- **层级控制**：Normal / Top / Modal 三级，Modal 面板遮罩可禁止下层交互
- **栈式管理**：天然支持多级面板导航（A → B → C 逐级返回）
- **动画集成**：Show/Hide 统一处理进入/退出动画
- **解耦调用**：其他模块只需 `PanelManager.Instance.PushPanel<T>()`，无需关心具体实现

### 3.2 经典面板管理架构

```
PanelManager（单例）
├── PushPanel<T>() / PopPanel()
├── PanelStack（面板栈：记录打开顺序）
│   ├── Peek()  → 获取栈顶（当前活动面板）
│   ├── Push()  → 推入新面板
│   └── Pop()   → 弹出栈顶
├── 层级控制（Normal / Top / Modal）
│   ├── Normal：普通面板
│   ├── Top：覆盖在 Normal 之上（如弹窗）
│   └── Modal：模态遮罩，禁止下层交互
└── 生命周期
    ├── OnEnter()  → 面板进入时（播放进入动画）
    ├── OnPause()  → 被覆盖时（暂停活动）
    ├── OnResume() → 重新回到栈顶时（恢复活动）
    └── OnExit()   → 面板退出时（播放退出动画）
```

### 3.3 代码实现示例

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 面板管理器——统一管理所有 UI 面板的打开、关闭、层级、生命周期
/// </summary>
public class PanelManager : MonoBehaviour
{
    public static PanelManager Instance { get; private set; }

    // 面板栈——记录打开顺序
    private Stack<BasePanel> panelStack = new Stack<BasePanel>();

    // 面板层级定义
    public enum PanelLayer
    {
        Normal,   // 普通面板层
        Top,      // 顶层面板（弹窗、提示）
        Modal     // 模态层（禁止下层交互）
    }

    [SerializeField] private Transform normalRoot;  // Normal 面板的父节点
    [SerializeField] private Transform topRoot;     // Top 面板的父节点  
    [SerializeField] private Transform modalRoot;   // Modal 面板的父节点

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 打开面板（入栈）
    /// </summary>
    public void PushPanel(BasePanel panel, PanelLayer layer = PanelLayer.Normal)
    {
        // 暂停当前栈顶面板
        if (panelStack.Count > 0)
            panelStack.Peek().OnPause();

        // 设置层级父节点
        Transform parent = layer switch
        {
            PanelLayer.Top => topRoot,
            PanelLayer.Modal => modalRoot,
            _ => normalRoot
        };
        panel.transform.SetParent(parent, false);

        panelStack.Push(panel);
        panel.Show();
    }

    /// <summary>
    /// 关闭当前面板（出栈）
    /// </summary>
    public void PopPanel()
    {
        if (panelStack.Count <= 0) return;

        BasePanel top = panelStack.Pop();
        top.Hide();

        // 恢复上一个面板
        if (panelStack.Count > 0)
            panelStack.Peek().OnResume();
    }

    /// <summary>
    /// 直接关闭到指定面板（用于回到特定界面）
    /// </summary>
    public void PopToPanel(BasePanel target)
    {
        while (panelStack.Count > 0 && panelStack.Peek() != target)
        {
            BasePanel top = panelStack.Pop();
            top.Hide();
        }

        if (panelStack.Count > 0)
            panelStack.Peek().OnResume();
    }

    /// <summary>
    /// 获取栈顶面板
    /// </summary>
    public BasePanel GetTopPanel()
    {
        return panelStack.Count > 0 ? panelStack.Peek() : null;
    }
}
```

### 3.4 带模态遮罩的面板

当打开 Modal 面板时，需要在其下方放置一个半透明遮罩，拦截所有下层交互：

```csharp
/// <summary>
/// 模态面板——自动关联遮罩
/// </summary>
public class ModalPanel : BasePanel
{
    [SerializeField] private GameObject mask; // 半透明遮罩

    public override void OnEnter()
    {
        mask.SetActive(true);
        mask.GetComponent<CanvasGroup>().alpha = 0;
        mask.GetComponent<CanvasGroup>().DOFade(0.5f, 0.2f); // 遮罩淡入
    }

    public override void OnExit()
    {
        mask.GetComponent<CanvasGroup>().DOFade(0f, 0.2f).OnComplete(() =>
        {
            mask.SetActive(false);
        });
    }

    // 点击遮罩可关闭面板（点击穿透拦截）
    public void OnMaskClick()
    {
        PanelManager.Instance.PopPanel();
    }
}
```

<!-- 建议截图：Modal 面板 + 半透明遮罩覆盖下层 UI 的效果截图 -->

---

## 4. 事件分发中心（Event Dispatcher）

### 4.1 什么是事件分发中心

当一个 UI 操作需要影响多个模块时（如点击"使用道具"→ 扣减道具数量 + 播放使用动画 + 更新角色属性 + 刷新 UI），如果每个模块都直接引用对方，代码会变得高度耦合。事件分发中心提供**观察者模式**的松耦合方案：

```
传统方式（耦合）：
  背包UI → 角色模块.使用道具()
          → 属性模块.更新UI()
          → 任务模块.检测进度()         ← 背包UI需要知道所有模块

事件分发（解耦）：
  背包UI → EventDispatcher.Dispatch("UseItem", itemData)
  角色模块 ← Listen("UseItem") → 处理使用逻辑
  属性模块 ← Listen("UseItem") → 更新界面
  任务模块 ← Listen("UseItem") → 检查任务     ← 背包UI只发事件，不关心谁在听
```

<!-- 建议截图：事件分发中心架构图，展示事件源 → 分发中心 → 多个监听器的流程 -->

### 4.2 简单实现（C# Action）

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 事件分发中心——解耦 UI 与业务逻辑
/// </summary>
public static class EventDispatcher
{
    // 事件存储：事件名 → 回调列表
    private static Dictionary<string, Action<object>> eventDictionary
        = new Dictionary<string, Action<object>>();

    /// <summary>
    /// 注册事件监听
    /// </summary>
    public static void Listen(string eventName, Action<object> callback)
    {
        if (eventDictionary.ContainsKey(eventName))
            eventDictionary[eventName] += callback;
        else
            eventDictionary[eventName] = callback;
    }

    /// <summary>
    /// 移除事件监听
    /// </summary>
    public static void Remove(string eventName, Action<object> callback)
    {
        if (eventDictionary.ContainsKey(eventName))
        {
            eventDictionary[eventName] -= callback;
            if (eventDictionary[eventName] == null)
                eventDictionary.Remove(eventName);
        }
    }

    /// <summary>
    /// 派发事件
    /// </summary>
    public static void Dispatch(string eventName, object data = null)
    {
        if (eventDictionary.TryGetValue(eventName, out var callback))
        {
            callback?.Invoke(data);
        }
    }

    /// <summary>
    /// 清空所有事件（场景切换时调用）
    /// </summary>
    public static void ClearAll()
    {
        eventDictionary.Clear();
    }
}
```

### 4.3 使用场景：背包系统解耦

```csharp
// ===== 背包 UI 层（只发事件，不关心业务逻辑） =====
public class BackpackPanel : BasePanel
{
    public void OnItemClicked(ItemData item)
    {
        // 只派发事件，不直接调用业务逻辑
        EventDispatcher.Dispatch("ItemUse", item);
        // 也可以分开派发
        // EventDispatcher.Dispatch("ItemSelected", item);
    }

    public override void OnEnter() { /* 监听业务层发来的数据更新事件 */ }
    public override void OnExit() { /* 注销监听 */ }
}

// ===== 角色模块（监听事件，处理业务逻辑） =====
public class PlayerController : MonoBehaviour
{
    private void OnEnable()
    {
        EventDispatcher.Listen("ItemUse", OnItemUse);
    }

    private void OnDisable()
    {
        EventDispatcher.Remove("ItemUse", OnItemUse);
    }

    private void OnItemUse(object data)
    {
        if (data is ItemData item)
        {
            Debug.Log($"使用道具：{item.itemName}");
            // 处理道具使用逻辑
            // 然后派发道具使用结果事件
            EventDispatcher.Dispatch("ItemUsedResult", new { success = true, item = item });
        }
    }
}

// ===== UI 更新层（监听结果事件，刷新显示） =====
public class ItemCountDisplay : MonoBehaviour
{
    public Text countText;

    private void OnEnable()
    {
        EventDispatcher.Listen("ItemUsedResult", OnItemUsed);
    }

    private void OnDisable()
    {
        EventDispatcher.Remove("ItemUsedResult", OnItemUsed);
    }

    private void OnItemUsed(object data)
    {
        // 更新道具数量显示
        countText.text = $"剩余：{Inventory.Instance.GetItemCount()}";
    }
}
```

---

## 5. MVC 在 UGUI 中的应用（简单模式）

### 5.1 三部分职责

| 层次 | 职责 | UGUI 中的实现 |
|------|------|--------------|
| **Model（模型）** | 数据存储与业务逻辑，不依赖 UI | C# 数据类 / ScriptableObject / JSON 数据文件 |
| **View（视图）** | 界面显示，不包含业务逻辑 | Prefab、Canvas、Image、Text、Slider 等 |
| **Controller（控制器）** | 处理用户交互，连接 Model 和 View | MonoBehaviour 脚本（通常称为 Presenter） |

### 5.2 示例：角色状态面板

```csharp
// ===== Model：纯数据模型，不依赖 Unity API =====
[System.Serializable]
public class PlayerModel
{
    public string playerName;
    public int level;
    public float hp;
    public float maxHp;
    public int gold;

    public bool IsDead => hp <= 0;

    public void TakeDamage(float damage)
    {
        hp = Mathf.Max(0, hp - damage);
    }

    public void Heal(float amount)
    {
        hp = Mathf.Min(maxHp, hp + amount);
    }

    public void AddGold(int amount)
    {
        gold += amount;
    }
}

// ===== Controller（Presenter）：驱动面板显示的脚本 =====
public class PlayerStatusPresenter : MonoBehaviour
{
    [Header("View 引用")]
    [SerializeField] private Text nameText;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Text hpText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text goldText;

    [Header("Model")]
    [SerializeField] private PlayerModel playerModel;

    // 初始化：用 Model 数据刷新 View
    private void Start()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        nameText.text = playerModel.playerName;
        hpSlider.maxValue = playerModel.maxHp;
        hpSlider.value = playerModel.hp;
        hpText.text = $"{playerModel.hp}/{playerModel.maxHp}";
        levelText.text = $"Lv.{playerModel.level}";
        goldText.text = playerModel.gold.ToString();
    }

    // 受到伤害
    public void OnTakeDamage(float damage)
    {
        playerModel.TakeDamage(damage);
        RefreshAll();

        // 播放受伤动画（View 独立行为）
        hpSlider.DOValue(playerModel.hp, 0.3f).SetEase(Ease.InOutQuad);
    }

    // 获得金币
    public void OnGoldChanged()
    {
        goldText.text = playerModel.gold.ToString();
        goldText.transform.DOScale(1.3f, 0.1f).SetLoops(2, LoopType.Yoyo);
    }
}
```

### 5.3 UGUI 中的简化实践

**完全 MVC 在 UGUI 中有时过于笨重**，推荐采用简化的 **Presenter 模式**：

```
Presenter 脚本（挂载在面板 Prefab 上）
│
├── [SerializeField] 引用 View 组件（Text、Slider、Image 等）
├── 持有 Model 引用（或通过 EventDispatcher 获取数据）
│
├── OnEnter() → 注册事件监听、初始化数据
├── RefreshUI() → 用最新数据刷新所有 View
├── OnButtonClick() → Controller 逻辑：处理点击
├── OnEventResponse() → 响应其他模块发来的事件
└── OnExit() → 注销事件监听、清理资源
```

> **核心原则**：View 只负责显示，不包含业务逻辑；Controller 处理逻辑，但不直接操作 UI 组件——而是通过调用 View 引用的方法来更新。

---

## 6. 最佳实践总结

### 6.1 面板管理用栈结构（不是简单的 SetActive）

- ❌ **不要**在按钮 OnClick 中直接 SetActive(true/false)
- ✅ **使用** `PanelManager.PushPanel()` / `PopPanel()` 管理
- 栈结构天然支持多级返回、生命周期统一、动画集成

### 6.2 使用事件系统解耦 UI 与业务逻辑

- UI 模块不知道业务逻辑的存在，只派发事件
- 业务模块不知道 UI 的存在，只监听事件
- 解耦后，任一模块可独立修改、测试、替换

### 6.3 层级管理（Modal 弹窗禁止下层交互）

| 层级 | 交互行为 | 实现方式 |
|------|---------|---------|
| Normal | 只与栈顶面板交互 | 正常栈管理 |
| Top | 覆盖 Normal，但可穿透 | 放置在更高 Sorting Order |
| Modal | 禁止下层交互 | 半透明遮罩阻止射线检测 + 点击遮罩关闭 |

### 6.4 异步加载 UI 时的过渡控制

```csharp
// 异步加载面板资源时的正确做法
public class AsyncPanelLoader : MonoBehaviour
{
    // 先显示 Loading 遮罩，再异步加载
    public void OpenPanelAsync(string panelPath)
    {
        // 1. 显示 Loading
        LoadingManager.Instance.Show();

        // 2. 异步加载面板 Prefab
        ResourceRequest request = Resources.LoadAsync<GameObject>(panelPath);

        request.completed += operation =>
        {
            GameObject prefab = (operation as ResourceRequest).asset as GameObject;
            GameObject panelObj = Instantiate(prefab, transform);

            // 3. 初始不可见（防止闪现）
            panelObj.SetActive(false);

            // 4. 延迟一帧确保布局稳定后再显示动画
            StartCoroutine(DelayedShow(panelObj.GetComponent<BasePanel>()));
        };
    }

    private System.Collections.IEnumerator DelayedShow(BasePanel panel)
    {
        yield return new WaitForEndOfFrame(); // 等 Canvas 布局重建完成
        LoadingManager.Instance.Hide();
        PanelManager.Instance.PushPanel(panel);
    }
}
```

### 6.5 常见陷阱与解决方案

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 连续快速点击导致面板重叠 | 动画播放期间再次触发操作 | 面板过渡期间设 `interactable = false` |
| 导航焦点跳到错误位置 | Automatic 模式判断失误 | 改为 Explicit 手动指定导航目标 |
| 面板销毁后报 Tween 错误 | DoTween 还在播放面板已销毁 | 在 OnDestroy 中调用 `DOKill()` |
| 模态面板还能点击下层 | 遮罩没拦截射线 | 遮罩加 GraphicRaycaster + blocksRaycasts |
| 面板关闭后事件还在触发 | 没有在 OnExit 中注销监听 | 在 OnExit 中调用 `Remove()` 注销所有监听 |

---

## 7. 知识小结表格

| 知识点 | 核心要点 | 推荐程度 | 实战提示 |
|--------|---------|---------|---------|
| Navigation 导航 | 键盘/手柄焦点移动 | ★★★★☆ | 主机移植必备，PC 纯鼠标可设为 None |
| Navigation 五种模式 | None/Horizontal/Vertical/Automatic/Explicit | ★★★★☆ | 复杂布局用 Explicit |
| Visualize 调试 | Scene 视图中可视化导航连线 | ★★★☆☆ | 快速排查导航路径问题 |
| EventSystem 三流程 | 射线检测→事件分发→输入处理 | ★★★★★ | 理解原理才能自定义 |
| 14 个事件接口 | 拖拽/点击/悬停/滚动等 | ★★★★★ | 所有交互的基础 |
| 面板管理器栈结构 | Push/Pop 统一生命周期 | ★★★★★ | 正规项目标配 |
| Modal 模态遮罩 | 半透明遮罩拦截下层交互 | ★★★★★ | 弹窗系统必备 |
| 事件分发中心 | 解耦 UI 与业务逻辑 | ★★★★★ | 中大型项目必备 |
| MVC/Presenter 模式 | Model-View-Controller 分工 | ★★★★☆ | 简化版 Presenter 更适合 UGUI |
| 异步加载过渡 | 等布局稳定再显示动画 | ★★★★☆ | 避免加载时的界面闪烁 |

<!-- 建议截图：完整的面板管理架构运行示意截图，展示多级面板打开/关闭流程 -->
