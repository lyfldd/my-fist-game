# Canvas 渲染模式的控制

> 以下为 AI 生成的图文笔记内容

---

## 一、主要学习内容

**三大学习要点：**

- Canvas 组件的基本功能
- 场景中允许多个 Canvas 对象共存
- Canvas 组件的三种渲染方式

![主要学习内容](images_canvas/page1_img1.png)

---

## 二、Canvas 组件功能解析

### 1. 核心功能

- **渲染基础**：作为 UGUI 系统中所有 UI 元素显示的根本，必须依附于 Canvas 游戏对象
- **子对象依赖**：任何 UI 控件必须作为 Canvas 的子对象才能被渲染，否则将不可见（通过拖拽 Image 对象实验验证）
- **参数控制**：通过修改 Canvas 组件参数可以调整 UI 渲染方式

### 2. 实践验证

- **创建验证**：在空场景中创建 Image 时会自动生成 Canvas 和 EventSystem 对象

![创建 Image 自动生成 Canvas](images_canvas/page1_img2.png)

- **父子关系实验**：
  - 当 Image 脱离 Canvas 子对象层级时立即消失
  - 重新作为子对象后恢复正常显示

![父子关系实验](images_canvas/page1_img3.png)

![父子关系实验（续）](images_canvas/page2_img1.png)

> **重要性说明**：该组件控制着所有 UI 控件的可视性，是 UI 系统的基础渲染容器。

---

## 三、场景中可以有多个 Canvas 对象

- **允许数量**：场景中允许存在多个 Canvas 对象，可以分别管理不同画布的渲染方式和分辨率适应方式等参数
- **常规使用**：如果没有特殊需求，一般情况下场景中只需要一个 Canvas 对象即可
- **特殊需求应用**：当需要不同面板采用不同的分辨率适配规则或渲染方式时，可以通过多个 Canvas 对象实现

### 1. 创建多个 Canvas 的方法

- **创建路径**：在 Hierarchy 窗口中右键 → UI → Canvas，可以单独创建新的 Canvas 对象
- **组件特性**：新创建的 Canvas 对象会自动附带渲染相关组件、分辨率自适应组件和射线检测组件

![创建多个 Canvas](images_canvas/page2_img2.png)

### 2. 多 Canvas 的应用场景

- **差异化需求**：当不同 UI 面板需要不同的渲染规则或分辨率适配规则时使用
- **实际案例**：
  - 两个面板需要不同的分辨率适配规则
  - 需要不同的渲染方式
- **使用频率**：这种多 Canvas 的应用场景在实际开发中使用相对较少

![多 Canvas 应用场景](images_canvas/page2_img3.png)

![多 Canvas 场景展示](images_canvas/page3_img1.png)

---

## 四、Canvas 组件的三种渲染方式

### 1. Screen Space - Overlay 模式

- **显示特性**：UI 始终显示在 3D 模型之前
- **应用场景**：适用于普通 UI 界面
- **参数位置**：在 Canvas 组件的 Render Mode 参数中设置

### 2. Screen Space - Camera 模式

- **显示特性**：3D 物体可以显示在 UI 之前
- **应用场景**：
  - 游戏人物面板中显示 3D 模型
  - 需要 3D 模型与 UI 有前后层级关系的场景
- **区别说明**：与 Overlay 模式的主要区别在于 3D 物体和 UI 的显示顺序

### 3. World Space 模式

- **显示特性**：3D 模式下的 UI
- **应用场景**：
  - UI 控件需要围绕人物旋转的效果
  - VR 虚拟现实中的 UI 制作
- **特殊用途**：常用于需要 UI 在 3D 空间中定位的场景

![三种渲染模式](images_canvas/page3_img2.png)

---

## 五、Canvas 组件的 3 种渲染方式详解

### 1. 覆盖模式（Screen Space - Overlay）

- **模式特点**：UI 始终显示在场景内容前方，会覆盖整个场景的游戏画面
- **应用场景**：适用于需要 UI 始终置顶显示的常规 UI 界面

![覆盖模式效果](images_canvas/page4_img1.png)

#### 1）覆盖模式简介

- **渲染原理**：不依赖摄像机，直接绘制在屏幕最上层
- **遮挡关系**：无论 3D 模型如何调整位置（包括 Z 轴），UI 都会遮挡场景内容
- **性能消耗**：相比其他模式性能开销较小

#### 2）Pixel Perfect 参数

- **功能**：开启无锯齿精确渲染
- **代价**：以性能换取更好的 UI 显示效果
- **使用建议**：
  - 当 UI 图片本身制作不够精细时可开启
  - 一般情况下可不开启，默认效果已能满足需求
  - 需要权衡性能与视觉效果

![Pixel Perfect 参数](images_canvas/page4_img2.png)

#### 3）Sort Order 参数

- **作用**：控制多个 Canvas 的渲染先后顺序
- **规则**：
  - 数值越小越先渲染
  - 数值越大越后渲染（显示在前方）
- **示例**：
  - 当红色 Canvas 的 Sort Order 设为 1，蓝色 Canvas 为 0 时
  - 红色 Canvas 会显示在蓝色 Canvas 上方
- **应用场景**：需要控制 UI 层级关系时使用

![Sort Order 参数](images_canvas/page4_img3.png)

![Sort Order 示例](images_canvas/page5_img1.png)

#### 4）Target Display 参数

- **功能**：指定 UI 在哪个显示设备上显示
- **应用场景**：
  - 多显示器设备（如任天堂双屏设备）
  - 需要将不同 UI 输出到不同显示器时
- **使用注意**：
  - 手游开发通常不需要修改
  - 需要配合摄像机的 Target Display 设置使用

#### 5）Additional Shader Channels 参数

- **作用**：决定着色器可以读取哪些额外数据
- **可选数据**：
  - 法线
  - 切线
  - 其他着色器相关数据
- **使用注意**：
  - 常规 UI 开发基本不需要修改
  - 主要用于特殊着色器效果开发

![Target Display 与 Additional Shader Channels](images_canvas/page5_img2.png)

#### 6）覆盖模式效果演示

- **遮挡测试**：
  - 创建立方体并调整位置
  - 无论立方体 Z 轴如何变化，UI 始终显示在前
- **模式特点总结**：
  - 最简单的 UI 渲染模式
  - 适合大多数基础 UI 需求
  - 不依赖摄像机参数设置

![覆盖模式遮挡测试](images_canvas/page6_img1.png)

---

### 2. 摄像机模式（Screen Space - Camera）

#### 1）摄像机模式概述

- **模式特点**：3D 物体可以显示在 UI 之前，通过专门摄像机控制渲染顺序
- **核心参数**：包含 Render Camera、Plane Distance、Sorting Layer 和 Order in Layer 四个关键参数

#### 2）摄像机模式切换

- **切换方法**：在 Canvas 组件中将 Render Mode 从 "Screen Space - Overlay" 改为 "Screen Space - Camera"
- **参数变化**：切换后会新增 Render Camera 和 Plane Distance 两个参数，Pixel Perfect 参数保持不变

![摄像机模式切换](images_canvas/page6_img2.png)

#### 3）Render Camera 参数

- **功能作用**：指定用于渲染 UI 的摄像机，不设置时行为类似覆盖模式
- **使用注意**：
  - 不推荐使用主摄像机，会导致 3D 物体全部显示在 UI 前（如立方体遮挡 UI）
  - 新建专用摄像机时需设置：
    - Culling Mask 仅勾选 UI 层
    - 深度值高于主摄像机（如主摄像机 -1，UI 摄像机 0）
    - 取消勾选 "Render Skybox"
    - 启用 "Depth Only" 渲染方式

![Render Camera 参数](images_canvas/page6_img3.png)

![Render Camera 设置](images_canvas/page7_img1.png)

#### 4）Plane Distance 参数

- **物理意义**：UI 平面在摄像机前方的距离，类似整体 Z 轴偏移
- **数值影响**：值越小 UI 离摄像机越近，显示层级越高（示例中 100→70 可使 UI 显示在立方体前）
- **实际应用**：当使用专用 UI 摄像机时，此参数意义不大

![Plane Distance 参数](images_canvas/page7_img2.png)

#### 5）UI 与 3D 物体渲染顺序控制

- **解决方案**：
  - 创建专用 UI 摄像机（Culling Mask 仅 UI 层）
  - 主摄像机取消 UI 层渲染
  - Canvas 关联新摄像机
  - 调整摄像机 Depth 值（主摄像机 -1，UI 摄像机 0）
- **3D 物体显示**：
  - 需作为 UI 子物体创建
  - 缩放需足够大（示例中需改为 100 倍）
  - 层级设为 UI 层
  - 通过 Z 轴控制前后顺序

![渲染顺序控制](images_canvas/page7_img3.png)

![3D 物体显示设置](images_canvas/page8_img1.png)

#### 6）Sorting Layer 与 Order in Layer

- **Sorting Layer**：
  - 通过 Tags → Layers → Add Sorting Layer 添加新层
  - 新建层默认显示在更上层（如 NewLayer 高于 Default）
- **Order in Layer**：
  - 同层内的渲染顺序控制
  - 值越大显示越靠前（示例中 -5 显示在 0 的后面）
- **组合使用**：层级优先级高于同层序号

![Sorting Layer 与 Order in Layer](images_canvas/page8_img2.png)

#### 7）摄像机模式选择建议

- **适用场景**：
  - 需要 3D 模型显示在 UI 前时使用摄像机模式（如角色面板）
  - 手游 UI 推荐使用摄像机模式
- **关键区别**：能否控制 3D 物体与 UI 的显示顺序
- **必要设置**：必须使用专用 UI 摄像机才能正确控制渲染顺序

---

### 3. 3D 模式（World Space）

- **应用场景**：常用于 VR（虚拟现实）或 AR（增强现实）场景，如玩家周围跟随移动旋转的 UI 元素
- **处理方式**：将 UI 对象像 3D 物体一样处理，使其具有 3D 空间属性

![3D 模式](images_canvas/page9_img1.png)

#### 1）Event Camera 的作用

- **核心功能**：用于处理 UI 事件的摄像机
- **注意事项**：
  - 必须设置 Event Camera，否则无法正常注册 UI 事件
  - 未设置时会显示黄色感叹号警告
  - 典型关联对象：主摄像机（Main Camera）

![Event Camera](images_canvas/page9_img2.png)

#### 2）3D 模式与世界坐标系

- **单位统一性**：
  - UI 元素尺寸与世界坐标系单位一致（如 100x100 的 UI 与 1x1 的 3D 物体大小相同）
  - 可自由调整 XYZ 坐标位置
- **交互特性**：
  - UI 可像 3D 物体一样进行旋转、移动等操作
  - 可实现围绕玩家角色旋转的效果

![世界坐标系](images_canvas/page9_img3.png)

#### 3）3D 模式效果展示

- **尺寸调整**：
  - 默认尺寸 100x100 过大
  - 调整为 1x1 后与立方体（Cube）尺寸匹配
- **空间关系**：
  - UI 出现在世界坐标原点 (0,0,0)
  - 与 3D 物体处于同一空间坐标系

![3D 模式效果](images_canvas/page10_img1.png)

#### 4）3D 模式与 VR/AR 应用

- **典型应用**：
  - 实现钢铁侠式裸眼 3D 效果
  - 创建围绕玩家角色旋转的 UI
  - VR 场景中跟随视角变化的界面
- **优势特点**：
  - 处理逻辑与 3D 模型规则一致
  - 支持完整的 3D 空间交互
  - 保持 UI 事件响应能力

![VR/AR 应用](images_canvas/page10_img2.png)

---

## 六、Canvas 组件总结

### 1. 基本功能

- **画布本质**：Canvas 是一个用于渲染显示 UI 控件的画布组件，所有 UI 控件必须作为其子对象才能被显示
- **多 Canvas 场景**：场景中可以存在多个 Canvas 对象，但实际开发中通常只需一个，因为一套渲染规则即可满足大多数游戏需求

### 2. 三种渲染模式

| 模式 | 特点 | 适用场景 |
|------|------|----------|
| **覆盖模式** (Screen Space - Overlay) | UI 始终显示在屏幕最前面 | 传统 2D UI 界面，使用频率较高 |
| **摄像机模式** (Screen Space - Camera) | 允许 3D 物体显示在 UI 之前 | 游戏开发中最常用的模式，核心重点 |
| **3D 模式** (World Space) | 专门用于制作 3D UI 效果 | VR/AR 项目，普通手游较少使用 |

### 3. 使用建议

- **常规选择**：对于大多数 2D/3D 游戏项目，优先选择摄像机模式
- **特殊需求**：只有需要实现 3D UI 效果或 VR/AR 项目时才考虑使用 3D 模式
- **性能考量**：多个 Canvas 会增加渲染开销，应尽量避免不必要的 Canvas 创建

---

## 七、知识小结

| 知识点 | 核心内容 | 考试重点/易混淆点 | 难度系数 |
|--------|----------|-------------------|----------|
| Canvas 组件的作用 | 作为 UI 元素的渲染基础，所有 UI 控件必须作为其子对象才能显示；负责控制 UI 的渲染方式和分辨率适配 | 必须作为子对象才能渲染，否则 UI 不可见；可通过修改参数调整渲染规则 | ⭐⭐ |
| 场景中多个 Canvas 的用途 | 允许多个 Canvas 对象分别管理不同画布的渲染方式、分辨率适配等参数；一般只需一个 Canvas，特殊需求（如多套 UI 规则）时使用多个 | 多个 Canvas 通过 Sort Order 控制渲染顺序（数值越大越靠前） | ⭐⭐ |
| Canvas 的三种渲染模式 | 1. Screen Space Overlay（覆盖模式）：UI 始终显示在 3D 模型前；2. Screen Space Camera（摄像机模式）：3D 物体可显示在 UI 前，需专用摄像机渲染 UI 层；3. World Space（3D 模式）：UI 像 3D 物体处理，适用于 VR/AR 场景 | 摄像机模式需注意：避免用主摄像机渲染 UI，推荐新建专用摄像机；Plane Distance 控制 UI 与摄像机的 Z 轴距离；3D 模式需关联主摄像机以响应 UI 事件 | ⭐⭐⭐⭐ |
| 覆盖模式参数 | Pixel Perfect（抗锯齿）、Sort Order（多 Canvas 渲染顺序）、Target Display（多显示器适配） | Sort Order 决定多个 Canvas 的叠加顺序（数值小先渲染） | ⭐⭐ |
| 摄像机模式关键参数 | Render Camera（专用 UI 摄像机）、Plane Distance（UI 平面距离）、Sorting Layer（层级排序） | 专用摄像机需：仅渲染 UI 层；深度低于主摄像机；勾选 Depth Only 避免遮挡 3D 内容 | ⭐⭐⭐ |
| 3D 模式特点 | UI 控件按世界坐标系处理，单位与 3D 物体一致；适用于环绕玩家的 UI（如 VR 界面） | 必须关联主摄像机以响应点击事件；UI 尺寸需按 3D 比例调整（如 1 单位=1 米） | ⭐⭐⭐ |
