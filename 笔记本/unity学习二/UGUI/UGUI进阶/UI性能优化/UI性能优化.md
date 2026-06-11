# UI 性能优化

<!-- 建议截图：Profiler 中 UI 性能瓶颈的前后对比（优化前高 DrawCall vs 优化后合批效果） -->
<!-- 图片路径示例：images/p1_i1.png -->

---

## 1. 合批（Batching）机制

### 什么是合批

合批（Batching）是指将多个**独立的绘制调用（DrawCall）** 合并为一个批次发送给 GPU，减少 CPU 与 GPU 之间的通信开销。UGUI 的合批由 **Canvas（画布）** 自动完成，基于其内部的 **Batching 算法**。

### UGUI 合批条件

三个条件**必须同时满足**才能合批：

| 条件 | 说明 |
|------|------|
| **同材质（Material）** | 使用完全相同的 Material 实例 |
| **同纹理（Texture）** | 使用同一张纹理图（包含图集中的子图） |
| **同 Z 层级** | 在同一 Canvas 的 sorting order 内，且相邻层级未被不同材质打断 |

> **重要**：UGUI 的合批按 Canvas 内 Graphic 组件的**渲染顺序**进行。如果 A、B 使用相同材质，C 夹在中间使用不同材质，则 A 和 B 不能合批。

<!-- 建议截图：合批与不合批的 Canvas 渲染顺序示意图 -->
<!-- 图片路径示例：images/p1_i2.png -->

### 合批打断原因

| 打断原因 | 根本问题 | 解决方案 |
|---------|---------|---------|
| **不同材质** | 每个材质产生独立的 DrawCall 批次 | 共享材质，使用 Property Block 替代实例化材质 |
| **不同纹理** | 每张纹理需要独立的材质批次 | 使用图集（Sprite Atlas）合并到同一纹理 |
| **插入不同 Z 层级的元素** | 中间元素改变材质 → 打断前后合批 | 按材质排序 Hierarchy 层级 |
| **使用 Mask 组件** | Mask 本身增加额外 DrawCall | 尽量使用 RectMask2D 替代 Mask |
| **动态元素插入** | 运行时频繁增删 Element 导致批处理重建 | 动静分离 |

### 常见误区

- **误区**：同一个 Atlas 中不同 Sprite 一定会合批
- **事实**：同 Atlas **只是纹理相同**，还要检查材质是否一致、Z 层级是否被打断

- **误区**：Canvas 越多越好
- **事实**：Canvas 越多，Split 的弊端越大（每个 Canvas 独立渲染），合理数量是 2~4 个

---

## 2. 动静分离（Canvas 拆分）

### 核心原则

**静态 UI（不变）和动态 UI（频繁变化）必须分属不同 Canvas。**

### 为什么必须拆分

当 Canvas 中任意一个 Graphic 元素的属性发生变化（如位置、文本、颜色）时，Unity 会触发该 Canvas 的 **Rebuild** 操作，**重新计算该 Canvas 下所有 Graphic 的顶点数据和批处理队列**。

- 如果 100 个静态元素和 1 个动态元素在同一个 Canvas 下
- 每次动态元素更新 → 101 个元素全部重建
- 静态元素被拖累，造成**不必要的性能浪费**

<!-- 建议截图：同一个 Canvas 下所有 Graphic 一起 Rebuild 的 Profiler 截图 -->
<!-- 图片路径示例：images/p2_i1.png -->

### 推荐拆分策略

```
场景结构示例：

Canvas-Static（不变层级）
├── 背景图（Image）
├── 框架装饰（Images）
├── 固定标题（TMP Text）
└── 按钮背景（仅在点击时变化，不影响底图）

Canvas-Dynamic（频繁更新层级）
├── 血量数值（TMP Text，每帧更新）
├── 进度条（Slider，每帧更新）
├── 伤害飘字（TMP Text，持续生成）
└── 滚动列表（Scroll Rect，滚动时更新）

Canvas-Overlay（独立弹出层级）
├── 弹窗（独立出现/消失）
├── 系统提示（短暂展示）
└── 特效（粒子/动画覆盖层）
```

| 层级 | 变化频率 | 典型内容 | 注意事项 |
|------|---------|---------|---------|
| **Canvas-Static** | 几乎不更新 | 背景、框架、固定文字、静态图标 | 可在 Start 中关闭 Rebuild 监听（优化极限场景） |
| **Canvas-Dynamic** | 频繁（每帧/每秒） | 数值、进度条、滚动列表 | 控制更新数据源，避免过度刷新 |
| **Canvas-Overlay** | 偶发 | 弹窗、提示、动画特效 | 使用独立 Canvas 避免影响主 UI 合批 |

### 多个 Canvas 渲染顺序控制

```csharp
// 通过 Sorting Order 控制多个 Canvas 的前后顺序
// 数值越大渲染越靠前

GetComponent<Canvas>().sortingOrder = 10;   // Overlay 在最前
GetComponent<Canvas>().sortingOrder = 0;    // Static 在底层
GetComponent<Canvas>().sortingOrder = 5;    // Dynamic 在中间

// 也可以通过 Canvas 的 Render Mode = Screen Space - Camera 配合 Plane Distance
// 距离摄像机越远的 Canvas 先渲染（更底层）
```

> **推荐**：一个场景中 Canvas 数量**建议控制在 3~5 个**。过多的 Canvas 会增加 GPU 批次间的切换开销。

---

## 3. 图集（Sprite Atlas）打包策略

### 打图集的目标

将**频繁同屏显示**的图片打包到同一张纹理中，使它们共享同一个材质批次，减少 DrawCall。

### 常见打包策略

| 策略 | 说明 | 适用场景 |
|------|------|---------|
| **按 UI 面板打包** | 每个面板（Panel/Window）一个图集 | 单个面板内部 Sprite 数量多，且面板间不常同时显示 |
| **按功能打包** | 通用图标集、通用按钮集单独打包 | 多个面板共享同一套图标/按钮 |
| **按场景打包** | 每个场景独立图集 | 场景间 UI 完全不同 |
| **整体打包** | 所有 UI 放进同一张图集 | 小项目（≤ 1024x1024 图集空间够用） |

### 控制图集大小

| 目标平台 | 推荐最大图集尺寸 |
|---------|----------------|
| PC / 主机 | 4096 x 4096 |
| 中高端手机 | 2048 x 2048 |
| 低端手机 | 1024 x 1024 |

> 图集过大会导致：加载时间长、内存占用高、部分老旧 GPU 不支持。

<!-- 建议截图：Sprite Atlas 窗口中的打包配置 -->
<!-- 图片路径示例：images/p3_i1.png -->

### 使用 Sprite Atlas（Unity 2017.1+）

```csharp
// 操作步骤
// 1. Asset → Create → Sprite Atlas
// 2. 在 Objects for Packing 中添加 Sprite
// 3. 选中图集 → Inspector 点击 Pack Preview 预览
// 4. 调整 Type → Master / Variant（Variant 用于多分辨率）
// 5. 写脚本预加载图集：

using UnityEngine.U2D;

public class AtlasLoader : MonoBehaviour
{
    public SpriteAtlas uiAtlas;

    void Start()
    {
        // 获取图集中的 Sprite
        Sprite btnSprite = uiAtlas.GetSprite("btn_confirm");
        GetComponent<Image>().sprite = btnSprite;
    }
}
```

### 跨图集合批

**不同图集 = 不同纹理 = 无法合批。**

如果需要优化，将需要合批的 Sprite 放到同一个图集中。若必须跨图集，建议分属不同 Canvas（动静分离），避免跨批次的性能干扰。

---

## 4. Overdraw（过度绘制）优化

### 什么是 Overdraw

Overdraw 指**同一像素被多次绘制**。在 UGUI 中，多个 Image 层叠时，上层会覆盖绘制下层，但 GPU 仍然为每个层执行了渲染操作。

### UI 中的 Overdraw 来源

| 来源 | 说明 | 优化措施 |
|------|------|---------|
| **多层图片叠加** | 背景 + 装饰 + 图标层叠 | 合并非必要层，PS 处理好后再导入 |
| **不可见图层** | 被完全遮挡但未禁用的 Graphic | 禁用或删除被遮挡的元素 |
| **未关闭 RaycastTarget** | Image/Text 默认 raycastTarget = true | 非交互控件关闭 |
| **透明区域纹理** | 全透明 Sprite 的浪费绘制 | 裁剪 Sprite 空白区域 |

<!-- 建议截图：通过 Debug 视图或 RenderDoc 查看 Overdraw 区域 -->
<!-- 图片路径示例：images/p4_i1.png -->

### 最佳实践：关闭 RaycastTarget

```csharp
// 在以下控件上关闭 RaycastTarget（不需要交互的）：

// 背景图片
GetComponent<Image>().raycastTarget = false;

// 纯装饰图片
GetComponent<Image>().raycastTarget = false;

// 纯展示文本
GetComponent<TextMeshProUGUI>().raycastTarget = false;

// 图标
GetComponent<Image>().raycastTarget = false;
```

> 编辑器快捷检查：在 Hierarchy 面板中搜索带有 RaycastTarget 的组件，逐项审核。

### 批量关闭工具脚本

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class RaycastCleaner
{
    [MenuItem("Tools/UI/关闭所有非交互控件的 RaycastTarget")]
    public static void DisableAllNonInteractiveRaycast()
    {
        // 关闭所有 Image 的 raycastTarget（除 Button 等交互组件子对象）
        Image[] images = FindObjectsOfType<Image>(true);
        foreach (var img in images)
        {
            if (img.GetComponent<Button>() == null &&
                img.GetComponent<Toggle>() == null &&
                img.GetComponent<InputField>() == null)
            {
                img.raycastTarget = false;
            }
        }

        // 关闭所有 TMP 文本的 raycastTarget
        TextMeshProUGUI[] texts = FindObjectsOfType<TextMeshProUGUI>(true);
        foreach (var txt in texts)
        {
            if (txt.GetComponent<Button>() == null &&
                txt.GetComponent<TMP_InputField>() == null)
            {
                txt.raycastTarget = false;
            }
        }

        Debug.Log("已清理全部非交互控件的 RaycastTarget");
    }
}
```

---

## 5. Layout Rebuild 优化

### 什么是 Layout Rebuild

当 **LayoutGroup**（Horizontal/Vertical/Grid Layout Group）、**ContentSizeFitter**、**AspectRatioFitter** 等布局组件的子对象发生变化时，UGUI 会触发 **Layout Rebuild** 重新计算所有子对象的尺寸和位置。

### 触发条件

| 触发操作 | 影响范围 |
|---------|---------|
| 子对象 SetActive(true/false) | 触发父级 LayoutGroup 重新计算 |
| 子对象尺寸变化（width/height） | 触发父级 LayoutGroup 重新计算 |
| 子对象增减（Instantiate/Destroy） | 触发父级 LayoutGroup 重新计算 |
| LayoutGroup 参数变化（spacing/padding） | 触发自身及其子级重新计算 |

<!-- 建议截图：Profiler 中 Layout Rebuild 的耗时瓶颈 -->
<!-- 图片路径示例：images/p5_i1.png -->

### 优化方案

#### 方案一：使用 LayoutElement 缓存尺寸

```csharp
// 在动态面板中的子元素预设上挂载 LayoutElement
// 通过 preferredWidth / preferredHeight 固定尺寸
// 让 LayoutGroup 无需重新计算即可确定子元素位置

// 示例：Grid 中每个 Item 预设
// LayoutElement.preferredWidth = 100
// LayoutElement.preferredHeight = 100
// → Grid Layout Group 直接取这些值，跳过二次计算
```

#### 方案二：避免频繁 SetActive（用 CanvasGroup 控制显隐）

```csharp
// ❌ 不推荐：频繁 SetActive 触发 Layout Rebuild
item.SetActive(true);   // 触发 LayoutGroup 重建
item.SetActive(false);  // 再次触发 LayoutGroup 重建

// ✅ 推荐：CanvasGroup 控制显隐，不改变 GameObject 激活状态
CanvasGroup cg = item.GetComponent<CanvasGroup>();
cg.alpha = 0;
cg.interactable = false;
cg.blocksRaycasts = false;
// 再用时：
cg.alpha = 1;
cg.interactable = true;
cg.blocksRaycasts = true;

// ⚠️ 注意：这个方案只在父级没有 LayoutGroup 时有效
// 如果有 LayoutGroup，显隐变化触发重建是无法避免的
// 此时应该用对象池或者禁用 LayoutGroup
```

#### 方案三：动态内容使用对象池（见下一节）

#### 方案四：必要时禁用布局组件

```csharp
// 场景：大批量初始化子对象时，暂时禁用 LayoutGroup
// 全部添加完成后，手动触发一次 Rebuild

LayoutGroup layout = GetComponent<LayoutGroup>();
layout.enabled = false;

for (int i = 0; i < 100; i++)
{
    GameObject item = Instantiate(prefab, transform);
    // 初始化 item...
}

layout.enabled = true;
LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
```

---

## 6. 对象池（Object Pool）优化动态 UI

### 场景

- 聊天列表（不断有新消息插入）
- 排行榜列表（滚动加载）
- 商店列表（大量商品项）
- 伤害飘字（频繁生成/销毁）
- 滚动选择器（Picker / Wheel）

### 不使用对象池的代价

```csharp
// ❌ 每次 Instantiate → 分配内存
// ❌ 每次 Destroy → 触发 GC（垃圾回收）
// ❌ 每次增减 → 触发 Layout Rebuild 风暴
// ❌ GC 时 → 出现明显的卡顿（GC Pause）

// 简单估算：
// 100 条消息，每条 Instantiate + Destroy 一次
// → 200 次对象操作 → 至少 200 次内存分配 + 至少 2 次 GC
// → 用户体验：频繁卡顿
```

<!-- 建议截图：频繁 Instantiate/Destroy 的 GC 峰值在 Profiler 中的表现 -->
<!-- 图片路径示例：images/p6_i1.png -->

### 简易对象池实现

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用 UI 对象池
/// </summary>
public class UIPool : MonoBehaviour
{
    [Header("池配置")]
    public GameObject prefab;
    public int preloadCount = 10;           // 预创建数量
    public Transform parentTransform;       // 池对象的父节点（通常设为 Canvas 下独立节点）

    private Queue<GameObject> pool = new Queue<GameObject>();
    private List<GameObject> activeList = new List<GameObject>();

    void Start()
    {
        // 预创建 N 个对象放入池中
        for (int i = 0; i < preloadCount; i++)
        {
            GameObject obj = Instantiate(prefab, parentTransform);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    /// <summary>
    /// 从池中取出一个对象
    /// </summary>
    public GameObject Get()
    {
        GameObject obj;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
        }
        else
        {
            // 池空了才 Instantiate
            obj = Instantiate(prefab, parentTransform);
        }

        obj.SetActive(true);
        activeList.Add(obj);
        return obj;
    }

    /// <summary>
    /// 回收对象到池中
    /// </summary>
    public void Return(GameObject obj)
    {
        obj.SetActive(false);
        activeList.Remove(obj);
        pool.Enqueue(obj);
    }

    /// <summary>
    /// 回收所有活跃对象
    /// </summary>
    public void ReturnAll()
    {
        // 倒序遍历避免修改集合问题
        for (int i = activeList.Count - 1; i >= 0; i--)
        {
            Return(activeList[i]);
        }
    }

    /// <summary>
    /// 获取当前活跃对象数
    /// </summary>
    public int ActiveCount => activeList.Count;
}
```

### 使用对象池后的优化效果

| 指标 | 不使用对象池 | 使用对象池 |
|------|------------|-----------|
| 内存分配 | 每次新增分配 | 仅在扩容时分配 |
| GC 触发 | 回收时频繁触发 | 几乎不触发（除非池满扩容） |
| Layout Rebuild | 每次 Instantiate/Destroy 触发 | 仅在首次启用时触发 |
| 滚动列表帧率 | 25 ~ 40 FPS（频繁卡顿） | 55 ~ 60 FPS（流畅） |

---

## 7. 使用 Profiler 诊断 UI 性能

### 打开 Profiler

**Window → Analysis → Profiler**（或快捷键 Ctrl+7）

### 关注模块

| 模块 | 关注指标 | 问题定位 |
|------|---------|---------|
| **Rendering** | DrawCall 数量 | 目标：移动端 ≤ 50 DrawCall，PC ≤ 200 DrawCall |
| **UI** | Rebuild 次数、Batch 数 | 查看哪些 Graphic 频繁触发 Rebuild |
| **CPU Usage** | GC Alloc、Scripts 耗时 | 定位高频 Update 中分配内存的脚本 |
| **Memory** | Texture 内存占用 | 检查图集是否过大或未释放 |

### 具体排查步骤

```
Step 1: 运行场景，Profiler 切换到 UI 模块
Step 2: 观察 Batch Count 是否超过阈值
Step 3: 展开 → 哪些 Canvas 的 Batch 最多？→ 检查是否同材质/同图集
Step 4: 切换到 CPU Usage → 搜索 "Canvas" 或 "Rebuild"
Step 5: 找到频繁触发的 LayoutRebuilder 或 Graphic.Rebuild
Step 6: 定位到具体元素 → 查看是否没有动静分离
```

<!-- 建议截图：Profiler 中标记出高 Batch 和频繁 Rebuild 的具体位置 -->
<!-- 图片路径示例：images/p7_i1.png -->

### 常见 Frame Debugger 用法

```csharp
// 更直观的可视化工具
// Window → Analysis → Frame Debugger
// Enable → 逐帧查看每个 DrawCall 的批次信息
// 可以清晰看到：
//   - 哪些元素被合批为同一个 DrawCall
//   - 哪些元素因材质/纹理不同而打断合批
//   - 每个 DrawCall 的顶点数、三角形数
```

---

## 8. 知识小结

| 核心内容 | 考试重点 | 难度系数 |
|---------|---------|---------|
| 合批三条件 | 同材质 + 同纹理 + 同 Z 层级 | ⭐⭐⭐ |
| 合批打断原因 | 不同材质/纹理/中间打断 | ⭐⭐⭐ |
| 动静分离（Canvas 拆分） | Static / Dynamic / Overlay 三层次 | ⭐⭐⭐⭐ |
| 图集打包策略 | 按面板/功能/场景打包 | ⭐⭐⭐ |
| 图集最大尺寸控制 | 移动端 ≤ 2048 | ⭐⭐ |
| Overdraw 优化 | 关闭 RaycastTarget、合并层叠 | ⭐⭐ |
| Layout Rebuild 触发条件 | SetActive、尺寸变化、增删子对象 | ⭐⭐⭐ |
| Layout Rebuild 优化 | LayoutElement、CanvasGroup、对象池 | ⭐⭐⭐⭐ |
| 对象池实现 | 预创建、Get/Return 接口、减少 GC | ⭐⭐⭐⭐ |
| Profiler 诊断 | UI 模块 / Rendering / CPU Usage | ⭐⭐⭐ |
| Frame Debugger | 逐帧查看 DrawCall 合批情况 | ⭐⭐⭐ |

### 性能优化优先级

```
最高优先级（立竿见影）：
  1. 非交互控件关闭 RaycastTarget
  2. 动静分离 Canvas 拆分
  3. 对象池替代频繁 Instantiate/Destroy

中等优先级（效果明显）：
  4. 图集打包，减少纹理切换
  5. 控制图集大小，降低 GPU 压力
  6. 使用 Profiler 定位高消耗元素

最低优先级（锦上添花）：
  7. 合批顺序优化（Hierarchy 按材质排序）
  8. 用 CanvasGroup 代替 SetActive（无 Layout 场景）
  9. 合并非必要层叠图片
```

### 性能优化检查清单

```
[ ] 每个 Canvas 分配了明确的角色（Static / Dynamic / Overlay）
[ ] 所有非交互 Image/TMP 的 raycastTarget 已关闭
[ ] 图集已按功能/面板拆分，单张 ≤ 2048x2048（移动端）
[ ] 频繁动态增删的 UI 使用了对象池
[ ] Profiler 中 Batch Count 在目标范围内
[ ] Profiler 中无频繁 Layout Rebuild
[ ] 所有 UI 材质使用默认/共享材质（无实例化材质泛滥）
[ ] Sprite 的 Read/Write 已关闭（减少内存占用）
[ ] 不使用的 UI 资源已卸载（Resources.UnloadUnusedAssets）
```

<!-- 建议截图：优化前后整体 DrawCall 数量对比图 -->
<!-- 图片路径示例：images/p8_i1.png -->
