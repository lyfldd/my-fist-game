# UI 动画控制

<!-- 建议截图：UI 动画在游戏中的实际应用效果，如面板弹出/收起动效截图 -->

## 1. 为什么 UI 需要动画

实际游戏中的 UI 几乎都离不开动画。静态 UI 虽然能完成功能，但缺乏视觉反馈和操作引导，用户体验很差。UI 动画的核心价值：

- **视觉反馈**：用户操作后有即时响应（按钮按下、面板弹出），让交互更具确定感
- **引导注意力**：动画可以引导玩家视线，突出重要信息（如任务完成提示、获得道具动画）
- **提升品质感**：流畅的过渡动画让游戏显得精致专业，直接影响玩家对游戏品质的第一印象
- **状态切换过渡**：页面切换、显隐控制不再是生硬的瞬间变化，而是平滑过渡

> **CanvasGroup Alpha 控制显隐是最基础的入口**，配合动画可以实现淡入淡出效果，是后续所有 UI 动画的起点。

<!-- 建议截图：CanvasGroup 组件的 Inspector 面板截图，标注 Alpha 属性位置 -->

---

## 2. 使用 Unity Animator 控制 UI 动画

Unity 内置的 Animator + Animation Clip 是实现 UI 动画最直接的方式，适合做复杂的状态机动画。

### 2.1 创建 Animator Controller 并关联到 UI 对象

1. 在 UI 对象上添加 `Animator` 组件
2. 在项目中创建 `Animator Controller` 资源（右键 → Create → Animator Controller）
3. 将 Controller 拖拽到 UI 对象的 Animator 组件中
4. 创建 Animation Clip（在 Animation 窗口中点击 Create）
5. 录制关键帧动画

<!-- 建议截图：Animator Controller 创建流程，以及 Animation 窗口录制 UI 动画的关键帧面板截图 -->

### 2.2 UI 常用动画类型

| 动画类型 | 驱动属性 | 说明 | 典型场景 |
|---------|---------|------|---------|
| 淡入淡出（Fade） | `CanvasGroup.alpha` | 透明度从 0 → 1 或 1 → 0 | 面板显隐、弹窗出现/消失 |
| 位移（Slide） | `RectTransform.anchoredPosition` | UI 元素从屏幕外移入/移出 | 侧边栏滑入、通知栏弹出 |
| 缩放（Scale） | `RectTransform.localScale` | 由小变大或由大变小 | 按钮点击反馈、图标高亮 |
| 旋转（Rotate） | `RectTransform.localRotation` | 绕轴旋转 | Loading 图标、技能冷却指示器 |
| 组合动画 | 同时驱动多个参数 | 同时改变位置、大小、透明度 | 面板整体弹出、Boss 出场动画 |

### 2.3 UI Animation Clip 关键帧设置技巧

- **线性与曲线**：默认关键帧间为线性过渡，在 Animation 窗口中可以调整曲线（Curves）获得缓动效果
- **复制粘贴关键帧**：选中关键帧 Ctrl+C / Ctrl+V 可快速复制状态到其他时间点
- **预览循环**：勾选 Animation Clip 的 Loop Time 可预览循环动画
- **From/To 技巧**：起始帧设初始值，结束帧设目标值，Unity 自动补间

### 2.4 Trigger / Bool / Float 参数驱动动画切换

在 Animator Controller 中定义参数来控制动画状态切换：

| 参数类型 | 用途 | 示例 |
|---------|------|------|
| Trigger | 一次性触发切换 | `Show` 触发面板进入动画 |
| Bool | 双状态切换（开/关） | `IsOpen` 控制面板开关 |
| Float | 连续数值驱动 | `Progress` 控制进度条动画 |

```csharp
// 使用 Trigger 触发面板动画
Animator animator = GetComponent<Animator>();
animator.SetTrigger("Show");   // 切换到显示状态
animator.SetTrigger("Hide");   // 切换到隐藏状态
```

<!-- 建议截图：Animator Controller 的状态机连线图，显示 Entry → Idle → Show → Hide → Idle 的完整状态流转 -->

---

## 3. 面板切换动画系统

### 3.1 面板堆栈（Panel Stack）原理

面板以"栈"结构管理——每次打开新面板时推入栈顶，关闭时从栈顶弹出。栈顶的面板处于活动状态，其下的面板被暂停或隐藏。这种设计天然支持多级面板导航（如：主菜单 → 设置页 → 音量子页 → 返回）。

```
栈结构示意（越往上越靠近屏幕）：
  ┌──────────────┐ ← 栈顶（当前活动面板：音量子页）
  │  AudioPanel  │
  ├──────────────┤
  │  SettingsPanel│ ← 被暂停
  ├──────────────┤
  │  MainMenuPanel│ ← 被隐藏/暂停
  └──────────────┘
```

### 3.2 通用面板基类设计

```csharp
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 面板基类 —— 所有具体面板继承此类
/// </summary>
public abstract class BasePanel : MonoBehaviour
{
    protected CanvasGroup canvasGroup;
    protected Animator animator;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 播放进入动画（淡入）
    /// </summary>
    public virtual void Show()
    {
        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.DOFade(1f, 0.3f);
        }

        // 可选：配合位移动画
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = new Vector2(0, -100f);
            rect.DOAnchorPos(Vector2.zero, 0.4f).SetEase(Ease.OutBack);
        }

        OnEnter();
    }

    /// <summary>
    /// 播放退出动画（淡出）
    /// </summary>
    public virtual void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.2f).OnComplete(() =>
            {
                gameObject.SetActive(false);
                OnExit();
            });
        }
        else
        {
            gameObject.SetActive(false);
            OnExit();
        }
    }

    public abstract void OnEnter();   // 进入面板时调用
    public abstract void OnExit();    // 退出面板时调用
    public virtual void OnPause() { }   // 被其他面板覆盖时调用
    public virtual void OnResume() { }  // 重新回到栈顶时调用
}
```

<!-- 建议截图：BasePanel 在 Inspector 中的组件组成截图，标注 CanvasGroup + Animator + 脚本三个必备组件 -->

### 3.3 面板管理器的简单实现

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 面板管理器 —— 单例模式，统一管理所有面板的生命周期
/// </summary>
public class PanelManager : MonoBehaviour
{
    public static PanelManager Instance { get; private set; }

    private Stack<BasePanel> panelStack = new Stack<BasePanel>();

    // 面板层级定义（可扩展）
    public enum PanelLayer
    {
        Normal,   // 普通层
        Top,      // 顶层（覆盖 Normal）
        Modal     // 模态层（禁止下层交互）
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// 打开面板（入栈）
    /// </summary>
    public void PushPanel(BasePanel panel)
    {
        // 暂停当前栈顶面板
        if (panelStack.Count > 0)
            panelStack.Peek().OnPause();

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
    /// 关闭所有面板
    /// </summary>
    public void PopAll()
    {
        while (panelStack.Count > 0)
        {
            BasePanel top = panelStack.Pop();
            top.Hide();
        }
    }
}
```

---

## 4. 使用 DoTween 实现 UI 动画（推荐方案）

### 4.1 为什么推荐 DoTween

| 特性 | DoTween | Unity Animator |
|------|---------|----------------|
| 文件体积 | ~300KB（极轻量） | 内置（无额外体积） |
| 代码控制 | 链式调用，一行代码完成 | 需要创建 Controller + Clip |
| 缓动函数 | 内置 30+ Ease 函数 | 需要手动调整曲线 |
| 运行时创建 | 完全代码控制 | 需要预创建 Animation Clip |
| 性能 | 零 GC 分配（Pro 版） | 中等（Animator 开销） |
| 学习成本 | 低（API 直观） | 高（需要理解状态机） |

**结论**：对于 90% 的 UI 动画需求，DoTween 是更优选择。Animator 只在复杂状态机场景（如角色动作 + UI 联动）时使用。

### 4.2 DoTween 安装方法

- **Package Manager 方式**：Window → Package Manager → 搜索 "DOTween" → Install（需要先添加 git 包或从 Asset Store 安装）
- **Asset Store 方式**：从 Unity Asset Store 下载 DOTween（免费）→ 导入后点击 Tools → Demigiant → DOTween Utility Panel → Setup
- **Git URL 方式**：在 Package Manager 中添加 git URL：`https://github.com/Demigiant/dotween.git?path=src`

安装后需调用一次 `DOTween.Init()` 初始化（通常放在游戏启动时）。

<!-- 建议截图：DOTween Utility Panel 设置界面截图 -->

### 4.3 基础用法

```csharp
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIAnimationDemo : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public RectTransform rectTransform;
    public Image progressImage;

    void Start()
    {
        // 1. 淡入（Alpha 0 → 1，300ms）
        canvasGroup.DOFade(1f, 0.3f);

        // 2. 位移动画 + OutBack 缓动（带弹性效果）
        rectTransform.DOAnchorPos(new Vector2(0, 0), 0.5f).SetEase(Ease.OutBack);

        // 3. 缩放脉冲（放大到 1.2 倍，然后恢复，循环 2 次）
        rectTransform.DOScale(1.2f, 0.2f).SetLoops(2, LoopType.Yoyo);

        // 4. 序列动画（依次执行多个动画）
        Sequence seq = DOTween.Sequence();
        seq.Append(rectTransform.DOScale(0, 0.3f));                  // 先缩到 0
        seq.Append(rectTransform.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack)); // 弹回 1.2
        seq.Append(rectTransform.DOScale(1f, 0.1f));                // 恢复到 1.0

        // 5. 带回调的动画
        canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
        {
            Debug.Log("淡出完成，可以销毁对象");
            Destroy(gameObject);
        });

        // 6. 无限旋转（Loading 图标）
        rectTransform.DORotate(new Vector3(0, 0, -360), 2f, RotateMode.FastBeyond360)
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.Linear);
    }
}
```

### 4.4 缓动函数（Ease）选择指南

| Ease 模式 | 效果描述 | 推荐场景 |
|-----------|---------|---------|
| `Ease.OutBack` | 超过目标后弹回，有弹性感 | 面板弹出、按钮弹窗出现 |
| `Ease.OutBounce` | 落地弹跳效果 | 掉落提示、宝箱出现 |
| `Ease.InOutQuad` | 先慢后快再慢，平滑过渡 | 淡入淡出、通用过渡 |
| `Ease.OutElastic` | 夸张的弹性伸缩 | 提示气泡、成就解锁 |
| `Ease.Linear` | 匀速运动 | 进度条、旋转动画 |
| `Ease.InBack` | 先缩回过目标再展开 | 收起/关闭动画 |
| `Ease.OutCirc` | 快速减速，自然停止 | 滑动菜单到位 |

```csharp
// 使用示例：推荐组合
// 面板弹出：OutBack
panel.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
// 面板收起：InBack
panel.DOScale(0f, 0.3f).SetEase(Ease.InBack);
// 进度条：Linear
slider.DOValue(1f, 2f).SetEase(Ease.Linear);
```

<!-- 建议截图：DoTween Ease 曲线可视化图（官方文档中的 Ease 曲线一览图） -->

---

## 5. 常用 UI 动画示例

### 5.1 按钮按动反馈

```csharp
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float duration = 0.1f;

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.DOScale(pressScale, duration).SetEase(Ease.InOutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.DOScale(1f, duration).SetEase(Ease.OutBack);
    }
}
```

### 5.2 消息弹窗（上滑 + 淡入）

```csharp
// 消息弹窗：从上方 -200px 处滑入并淡入
RectTransform msgRect = notification.GetComponent<RectTransform>();
CanvasGroup msgCG = notification.GetComponent<CanvasGroup>();

// 初始状态
msgRect.anchoredPosition = new Vector2(0, -200f);
msgCG.alpha = 0;

// 同时播放位移动画和淡入
Sequence msgSeq = DOTween.Sequence();
msgSeq.Join(msgRect.DOAnchorPosY(0, 0.5f).SetEase(Ease.OutBack));
msgSeq.Join(msgCG.DOFade(1f, 0.4f));
msgSeq.AppendInterval(2f);  // 停留 2 秒
msgSeq.Append(msgCG.DOFade(0f, 0.3f)); // 淡出消失
```

### 5.3 数值跳动（分数/金币变化）

```csharp
using TMPro;
using DG.Tweening;

public class ScoreAnim : MonoBehaviour
{
    public TextMeshProUGUI scoreText;

    public void AnimateScoreChange(int from, int to, float duration = 0.5f)
    {
        // 利用 DoTween 的数值变化回调更新 UI
        DOTween.To(() => from, value =>
        {
            scoreText.text = value.ToString();
            from = value;
        }, to, duration).SetEase(Ease.OutCubic);

        // 数值变化同时配合缩放脉冲
        scoreText.transform.DOScale(1.3f, 0.15f).SetLoops(2, LoopType.Yoyo);
    }
}
```

### 5.4 列表入场（Stagger 效果）

```csharp
// 列表子项逐个飞入，每个间隔 0.05 秒
for (int i = 0; i < itemList.Count; i++)
{
    RectTransform item = itemList[i];
    item.anchoredPosition = new Vector2(200f, 0);
    item.localScale = Vector3.zero;

    item.DOAnchorPosX(0, 0.3f).SetDelay(i * 0.05f).SetEase(Ease.OutBack);
    item.DOScale(1f, 0.2f).SetDelay(i * 0.05f).SetEase(Ease.OutBack);
}
```

### 5.5 Loading 旋转动画

```csharp
// 方式一：Image.FillAmount 循环
public Image fillImage;
fillImage.DOFillAmount(1f, 2f).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear);

// 方式二：RectTransform 持续旋转
loadingIcon.DORotate(new Vector3(0, 0, -360), 2f, RotateMode.FastBeyond360)
    .SetLoops(-1, LoopType.Restart)
    .SetEase(Ease.Linear);
```

<!-- 建议截图：各个 UI 动画示例在游戏中的实际效果截图，如列表 Stagger 入场、Loading 旋转等 -->

---

## 6. UI 动画性能注意

| 注意事项 | 说明 |
|---------|------|
| DoTween 自动生命周期 | DoTween 自动管理 Tween 对象的创建和销毁，但注意在对象销毁时调用 `DOTween.Kill(target)` 清理残留动画 |
| Animator 适用场景 | Animator 适合复杂状态机动画（如多状态 UI 联动），简单动画用 DoTween 更轻量 |
| 大量 DoTween 控制 | 同时播放大量 DoTween 会影响帧率，使用 `DOTween.Pause()` / `DOTween.Resume()` 或 `DOTween.KillAll()` 控制批量动画 |
| Canvas Rebuild 避免 | 动画播放期间尽量不触发 Canvas Rebuild（如修改 Layout Group 参数），否则会造成卡顿。动画和布局更新不要在同一帧触发 |
| 对象池结合 | UI 对象回收入池前记得 Kill 所有 Tween，避免野指针报错 |

```csharp
// 安全销毁：对象被回收时 Kill 相关动画
private void OnDestroy()
{
    this.transform.DOKill();    // Kill 该对象上的所有 Tween
    // 或：DOTween.Kill(this.transform);
}
```

---

## 7. 知识小结表格

| 知识点 | 核心要点 | 推荐程度 | 实战提示 |
|--------|---------|---------|---------|
| CanvasGroup Alpha | UI 显隐控制的基础属性 | ★★★★★ | 配合 DoTween.DOFade 使用 |
| Unity Animator | 复杂状态机动画 | ★★★☆☆ | 用于多状态 UI 联动 |
| DoTween | 轻量级代码动画库 | ★★★★★ | 90% UI 动画首选方案 |
| 面板基类 BasePanel | 统一面板生命周期 | ★★★★★ | 所有面板项目必备 |
| 面板栈管理 | 多级面板导航 | ★★★★★ | 避免 Spaghetti 代码 |
| 缓动函数选择 | OutBack/InOutQuad/Linear 等 | ★★★★☆ | 不同场景选不同 Ease |
| 序列动画 Sequence | 多个动画依次/同时播放 | ★★★★☆ | 组合动画首选 |
| Stagger 效果 | 列表子项逐个入场 | ★★★☆☆ | 提升列表展示品质感 |
| 性能优化 | Kill/对象池/Canvas Rebuild | ★★★★★ | 上线前必须做性能检查 |

<!-- 建议截图：完整的面板弹出/收起动效流程 GIF，展示从点击按钮到面板弹出再到关闭的完整动画链 -->
