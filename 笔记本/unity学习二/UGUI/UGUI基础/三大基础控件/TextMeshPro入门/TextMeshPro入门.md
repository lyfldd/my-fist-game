# TextMeshPro 入门

<!-- 建议截图：TMP 与旧 Text 渲染效果对比图，显示同样字号下 TMP 更清晰 -->
<!-- 图片路径示例：images/p1_i1.png -->

---

## 1. 为什么用 TMP 替代旧 Text

### 旧 Text 的局限

| 问题 | 说明 |
|------|------|
| 点阵缩放模糊 | 旧 Text 基于点阵字体，放大后出现严重锯齿和模糊 |
| 无描边/阴影内建 | 需要额外编写 Shader 或叠加多个 Text 来实现描边，效率低 |
| 多语言支持弱 | 对于中文、韩文、阿拉伯文等复杂字形支持不佳，换行和间距控制粗糙 |
| 字体变体有限 | Bold/Italic 实际是模拟（FontStyle 伪造），效果差且不可控 |
| 无富文本扩展 | 仅支持基础的 `<b>`、`<i>`、`<size>`、`<color>`，功能极少 |

<!-- 建议截图：旧 Text 放大后锯齿模糊的效果展示 -->
<!-- 图片路径示例：images/p1_i2.png -->

### TMP 核心优势

| 优势 | 原理 |
|------|------|
| **SDF 字体清晰缩放** | 使用 Signed Distance Field（有符号距离场）技术，每个字形存储距离场纹理，任意缩放保持锐利边缘 |
| **内建特效** | 描边、发光、阴影、浮雕（Bevel）直接通过材质参数调节，无需额外 Shader |
| **性能更优** | 相同顶点数下 TMP 的合批效率更高；SDF 渲染不依赖纹理分辨率 |
| **富文本扩展** | 支持 `<align>`、`<sprite>`、`<gradient>` 等大量标签，打造丰富排版 |
| **字体回退（Fallback）** | 一个字符缺失时自动回退到备用字体，支持多语言混排 |
| **动态字体 SDF** | 运行时生成的 SDF 字体，无需预创建每个字号的字体资源 |

<!-- 建议截图：TMP SDF 缩放下清晰锐利、旧 Text 模糊放大的对比 -->
<!-- 图片路径示例：images/p1_i3.png -->

### 版本兼容性

- **Unity 2017**：TMP 作为 Package 需要手动安装（Asset Store 或 Package Manager）
- **Unity 2018**：TMP 成为内置包（Built-in Package），随编辑器安装
- **Unity 2019+**：官方建议全量迁移，旧 Text 在 Newly Created UI 中默认警告
- **迁移工具**：Window → TextMeshPro → TMP Text Replacer（一键批量替换）

<!-- 建议截图：TMP Text Replacer 工具的界面 -->
<!-- 图片路径示例：images/p2_i1.png -->

---

## 2. 创建 TMP 文本

### 操作步骤

1. 在 Hierarchy 中右键或菜单：**GameObject → UI → Text - TextMeshPro**
2. 第一次使用时会弹出 **Import TMP Essentials** 对话框，点 Import 导入核心资源
3. 自动生成：
   - `TMP Settings`（`Project Settings/TextMesh Pro/Settings`）
   - `LiberationSans SDF` 默认字体资源（Font Asset，`.asset` 文件）
   - 默认材质球（`LiberationSans SDF - Material`）

<!-- 建议截图：创建 TMP Text 的菜单路径 -->
<!-- 图片路径示例：images/p2_i2.png -->

### 与旧 Text 的结构对比

```
旧 Text (Legacy)
├── GameObject
│   └── Text (Legacy) 组件
│       └── 直接引用 .ttf 字体文件

TMP Text
├── GameObject
│   ├── TextMeshProUGUI 组件
│   │   └── 引用 Font Asset (.asset)
│   ├── 引用 SDF 材质 (Material)
│   └── 引用 SDF 纹理图集 (Texture2D)
```

| 对比项 | 旧 Text | TMP Text |
|--------|---------|----------|
| 组件名 | `Text` | `TextMeshProUGUI` |
| 命名空间 | `UnityEngine.UI` | `TMPro` |
| 字体资源 | `.ttf` / `.otf` 原始字体 | `.asset`（Font Asset 封装） |
| 渲染方式 | 点阵栅格化 | SDF 距离场 |
| 材质独立 | 无（共享默认材质） | 每个 TMP 可独立材质 |

---

## 3. 核心参数详解

<!-- 建议截图：TMP Inspector 面板全貌 -->
<!-- 图片路径示例：images/p3_i1.png -->

| 参数 | 说明 | 与旧 Text 对比 |
|------|------|---------------|
| **Text** | 文本内容，支持富文本标签 | 相同但扩展性更强 |
| **Font Asset** | 字体资源文件（`.asset`） | 需要预设，不能直接用 `.ttf` |
| **Font Style** | Normal / Bold / Italic / Bold Italic | 类似，但 TMP 支持更逼真 |
| **Font Size / Auto Size** | 字号 / 自动适配 | Best Fit 的升级版，更稳定 |
| **Alignment** | 9 种对齐方式（水平 x 垂直） | 完全相同 |
| **Wrapping / Overflow** | 换行 / 溢出模式 | 增多 Truncate、Ellipsis、Page、Linked 等 |
| **Color / Vertex Color** | 颜色 / 顶点渐变色 | **新增：支持渐变** |
| **Spacing Options** | 字符 / 单词 / 行 / 段落间距 | 更精细可调 |
| **Extra Settings** | 大量扩展选项（见下文） | 完全新增 |

### Extra Settings 关键参数

| 参数 | 作用 |
|------|------|
| `Margins` | 文本边距，控制文本与 RectTransform 边界的距离 |
| `Character Rotation` | 全局字符角度偏移 |
| `First / Regular / Last Character Offset` | 首、中、尾字符的位移微调 |
| `Scale to Fit` | 类似 Auto Size，但按宽度优先自动缩放到填满 |
| `Wrapping Speed` | 换行检测间隔（提升性能用） |
| `Geometry Sorting` | 背面剔除排序模式 |
| `Raycast Target` | 是否接收射线检测（建议非交互控件关闭） |

<!-- 建议截图：Extra Settings 折叠面板内容 -->
<!-- 图片路径示例：images/p3_i2.png -->

---

## 4. TMP 富文本扩展

### 旧 Text 支持的基础标签

```
<b>粗体</b>        <i>斜体</i>
<size=24>字号</size>  <color=#ff0000>颜色</color>
```

### TMP 新增标签

| 标签 | 用法示例 | 效果 |
|------|---------|------|
| `<align>` | `<align="center">内容</align>` | 对齐方式 |
| `<sprite>` | `<sprite="MyAtlas" index=0>` | 内联图集精灵 |
| `<font>` | `<font="OtherSDF">文字</font>` | 临时切换字体 |
| `<mark>` | `<mark=#ffff00aa>高亮</mark>` | 文字高亮底色 |
| `<u>` | `<u>下划线</u>` | 下划线 |
| `<strikethrough>` | `<s>删除线</s>` | 删除线 |
| `<gradient>` | `<gradient="MyGrad">渐变</gradient>` | 文字渐变色 |
| `<alpha>` | `<alpha=#80>半透明</alpha>` | 单独控制透明度 |
| `<style>` | `<style="MyStyle">样式</style>` | 全局样式表 |
| `<nobr>` | `<nobr>不换行</nobr>` | 强制不换行 |
| `<cspace>` | `<cspace=1.5>字间距</cspace>` | 临时字间距调整 |
| `<voffset>` | `<voffset=0.5em>上标</voffset>` | 上下标偏移 |
| `<mspace>` | `<mspace=2em>等宽</mspace>` | 等宽间隔 |

### 常用组合示例

```csharp
// 基本排版
"<b>粗体</b> 和 <i>斜体</i> 混合"

// 内联颜色和字号
"<color=#FF5500><size=150%>大标题</size></color>"

// 内联精灵图（需要 Sprite Asset）
"生命值：<sprite=\"HealthAtlas\" index=0> <sprite=\"HealthAtlas\" index=1>"

// 高亮标记
"这是<mark=#FFFF00>关键信息</mark>请留意"

// 渐变色（需要先创建 Gradient Preset）
"<gradient=\"WarningGrad\">危险提示</gradient>"

// 多字体混合
"English text <font=\"ChineseSDF\">中文混排</font>"
```

<!-- 建议截图：各种富文本标签在 Game 视图中的实际渲染效果 -->
<!-- 图片路径示例：images/p4_i1.png -->

---

## 5. TMP 材质与特效

### 材质类型

TMP 本质上有 4 种渲染模式，由 Material 的 Shader 决定：

| 类型 | Shader 名称 | 适用场景 |
|------|------------|---------|
| **Bitmap** | `TextMeshPro/Bitmap` | 纯位图渲染，不使用 SDF（兼容旧项目） |
| **SDF** | `TextMeshPro/Distance Field` | 基础 SDF，清晰缩放（推荐） |
| **SDFAA** | `TextMeshPro/Distance Field AA` | SDF + 抗锯齿，边缘更柔和 |
| **SignedDistanceField** | `TextMeshPro/Mobile/Distance Field` | 移动端优化版 SDF，性能更优 |

<!-- 建议截图：4 种 Shader 渲染效果的对比 -->
<!-- 图片路径示例：images/p5_i1.png -->

### 内建特效参数

在 TMP 材质 Inspector 中可调节：

| 特效 | 参数 | 说明 |
|------|------|------|
| **Face** | Color / Texture / Dilate / Softness | 正面颜色、纹理、膨胀、边缘柔和度 |
| **Outline** | Color / Width / Softness | 描边颜色、宽度、柔和度 |
| **Underlay** | Color / Offset X/Y / Dilate / Softness | 底部阴影（偏移位置、膨胀、柔和） |
| **Bevel** | Amount / Width / Roundness / Opacity | 浮雕斜角效果 |
| **Glow** | Color / Offset / Inner / Outer | 发光效果（内发光/外发光） |
| **Lighting** | Light Angle / Diffuse / Specular | 光照方向与反射 |

<!-- 建议截图：内建特效在材质面板中的位置 -->
<!-- 图片路径示例：images/p5_i2.png -->

### 创建自定义 Material Preset

当项目需要多套字体样式（如正常、高亮、禁用）时：

1. 在 Project 中选中已有 TMP 材质 → **右键 → Create → Material**
2. 命名如 `TMP_Yellow_Outline`
3. 修改参数（如 Face Color = Yellow，Outline = Orange，Width = 0.15）
4. 将材质拖拽到目标 TMP 组件的 **Font Material** 槽位
5. 多个 TMP 可共享同一材质（享有合批加成）

```
推荐命名规范：
  TMP_[用途]_[颜色]_[特效]
  示例：TMP_Button_White_Outline
        TMP_Title_Gold_Glow
        TMP_Disable_Gray_Underlay
```

---

## 6. 代码控制

### 基础 API

```csharp
using TMPro;

public class TMPDemo : MonoBehaviour
{
    private TextMeshProUGUI tmp;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        
        // 基础属性
        tmp.text = "Hello TMP";
        tmp.fontSize = 24;
        tmp.color = Color.red;
        
        // 面（Face）颜色
        tmp.faceColor = new Color32(255, 255, 255, 255);
        
        // 描边
        tmp.outlineColor = Color.black;
        tmp.outlineWidth = 0.2f;
        
        // 阴影（Underlay 需要操作材质）
        Material mat = tmp.fontMaterial;
        mat.SetColor("_UnderlayColor", Color.black);
        mat.SetFloat("_UnderlayOffsetX", 1f);
        mat.SetFloat("_UnderlayOffsetY", -1f);
        
        // 对齐方式
        tmp.alignment = TextAlignmentOptions.Center;
        
        // 自动适配字号
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12;
        tmp.fontSizeMax = 48;
    }
}
```

### 高级 API

```csharp
// === 富文本动态生成 ===
float hp = 0.75f;
int hpMax = 100;
tmp.text = $"HP: <color=#{(hp > 0.5f ? "00FF00" : "FF0000")}>{hp * hpMax:F0}</color>/{hpMax}";

// === 顶点颜色渐变 ===
tmp.colorGradientPreset = Resources.Load<TMP_ColorGradient>("Gradients/HealthGrad");
tmp.enableVertexGradient = true;

// === 字体切换 ===
TMP_FontAsset chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseSDF");
tmp.font = chineseFont;

// === 超链接检测（结合 <link> 标签）===
// 文本: "详情请点击 <link=\"https://...\">这里</link>"
// 配合 IPointerClickHandler 判断 link 索引
```

<!-- 建议截图：代码控制 TMP 属性后 Game 视图的实时效果 -->
<!-- 图片路径示例：images/p6_i1.png -->

---

## 7. 知识小结

| 核心内容 | 考试重点 | 难度系数 |
|---------|---------|---------|
| SDF 原理与对比旧 Text | TMP 三大优势（清晰缩放、特效、性能） | ⭐⭐ |
| 创建 TMP Text 步骤 | 首次使用须 Import TMP Essentials | ⭐ |
| 核心参数表 | Font Asset vs ttf 的区别 | ⭐⭐ |
| 富文本标签 | `<sprite>`、`<gradient>`、`<font>` 等新增标签 | ⭐⭐⭐ |
| 材质 Shader 类型 | SDF vs Bitmap vs Mobile/Distance Field | ⭐⭐ |
| 内建特效参数 | Outline、Underlay、Glow、Bevel | ⭐⭐ |
| 自定义 Material Preset | 创建、命名、共享流程 | ⭐⭐⭐ |
| 代码控制 API | `outlineColor`、`enableAutoSizing`、`font` | ⭐⭐⭐ |
| 性能与合批 | 相同材质和字体共享批次，避免频繁修改材质 | ⭐⭐⭐⭐ |

### 常见踩坑点

| 问题 | 解决方案 |
|------|---------|
| 导入 TMP 后 TextMeshProUGUI 组件丢失 | 重新 Import TMP Essentials |
| 字体显示为方块（Unicode 缺失） | 添加 Fallback Font Assets |
| 多语言混排部分文字不显示 | 检查 Fallback Chain 或重新生成 SDF 字体 |
| TMP 文本性能比预期差 | 检查是否每帧修改 text 触发重建；使用对象池优化 |
| SDF 描边在移动端出现毛刺 | 改用 Mobile/Distance Field Shader，降低 Dilate 值 |

<!-- 建议截图：TMP 常见坑位的错误效果与修复后对比 -->
<!-- 图片路径示例：images/p7_i1.png -->
