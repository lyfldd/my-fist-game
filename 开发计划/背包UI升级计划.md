# 🎒 背包 UI 升级计划

> **日期**: 2026-06-24 · **状态**: 设计阶段 · **依赖**: 背包系统.md（核心数据层已完成）
> **背景**: 背包系统核心（Inventory/InventoryContainer/PlacedItem）已稳定，但 UI 层（InventoryUI/DragDropManager）存在操作体验问题，需要专项升级。
> 🔍 迭代追踪 | 第1轮: ✅ (3个根因+4个方案缺陷+2个设计遗漏) | 第2轮: ⬜ | 第3轮: ⬜ | 最后审查: 2026-06-24
>
> **2026-06-24 第1轮审查发现问题**（结合项目代码逐行验证）：
> - **P0 根因诊断**：§2.2 问题B"CleanCells杀边框"的描述不完整。实际链路是 `OnInventoryChanged`(:300) → `ShowOverview()` → `ClearCells()`(:351) → `RemoveBorderFromOverlay()`(:143) → 遍历 `_selectedOverlayRt` 子节点 Destroy 白框条。**但 `_selectedOverlayRt` 本身在 ClearCells 之前就可能已被 Destroy**（因为 overlay 是 scrollContent 的子节点，:347 ClearContainer 会先 Destroy 所有子节点）。所以 RemoveBorderFromOverlay 遍历的是已销毁对象的子节点，`childCount` 为 0，白框条已经随 overlay 一起被 Destroy 了——不是被"杀"的，是随父节点陪葬的。
> - **P0 根因诊断**：§2.2 问题C"空格子无法点击"的描述遗漏了关键点。空格子有 Image 背景但 `raycastTarget` 的值不确定——`InventoryUI.cs:444` scrollContent 的 Image 设了 `raycastTarget=false`，但空格子本身的 Image 没有显式设置（默认 true）。真正的问题是**空格子没有挂任何 IPointerHandler 组件**，UGUI 不会把事件路由到它身上，raycastTarget=true 只决定是否参与射线遮挡，不决定事件接收。
> - **P1 方案缺陷**：§4.1 白框持久化方案"用 GetWorldCorners 获取目标格子屏幕坐标"——但 `RefreshSelectionBorder`(:258) 是在 `ShowOverview` 重建完成后调用的，此时 `_cellRegions` 已被 ClearCells 清空并重新注册。新的 CellRegion 指向新的格子对象，但 `SelectedItem` 里存的 `gridX/gridY/container` 可能与重建后的容器布局不匹配（如果物品在重建过程中被移动或容器尺寸变化）。
> - **P1 方案缺陷**：§4.2 方案B"Update 中 Input.GetMouseButtonDown"——当前代码 `LateUpdate`(:101) 已经在用这个方式做"点击空白取消选中"，但它与 ItemDragHandler 的时序冲突文档已记录。真正的问题是：**DragDropManager.LateUpdate 的 `_clickConsumed` 标志在 :103 每帧重置为 false，但 ItemDragHandler 设置 `_clickConsumed=true` 是在 OnPointerDown 中，OnPointerDown 和 LateUpdate 的执行顺序在同一帧内是 UGUI 事件→LateUpdate，所以理论上是通的**。文档说"某些路径未正确设值"，实际需要检查的是 ItemDragHandler 是否在所有点击路径（包括空格子点击）都正确设置了 `_clickConsumed`。
> - **P1 方案缺陷**：§4.3 增量刷新"将 ShowOverview 拆分为 RefreshContainer(container)"——但当前 `ShowOverview`(:329) 每次都重建标签栏（:337 DestroyImmediate oldBar + :340 CreateTopTabBar），而且容器折叠状态 `_containerCollapsed` 字典在重建后会丢失（因为是实例字段不是静态的）。增量刷新如果不处理这两点，折叠状态会错乱。
> - **P2 设计遗漏**：`TryMoveToContainer`(:572) 创建新 PlacedItem 时 `new PlacedItem(itemData, count, gridX, gridY)` **丢失了 instanceId 和 itemDurability**。PlacedItem 构造函数(:31) 把 instanceId 设为 0，itemDurability 设为 0f。拖拽移动物品后，物品的 instanceId 变成 0（存档系统无法定位），耐久变成 0（视为满耐久，但实际可能是低耐久）。这是一个已有 bug，不仅是升级计划要处理的，当前拖拽功能就存在。
> - **P2 设计遗漏**：`OnInventoryChanged`(:300) 调用 `ShowOverview()` 全量重建，但 `ShowOverview` 内部(:347) `ClearContainer` 用 `Destroy`（不是 DestroyImmediate），子对象要等到帧末才真正销毁。如果同一帧内有多次 InventoryChanged 事件（如拖拽移动触发 moved + PublishView 触发 viewChanged），第一次 ShowOverview 创建的子对象还没销毁，第二次又叠加创建，导致格子重复注册到 DragDropManager._cellRegions。

---

## 一、当前架构

### 1.1 面板体系

| 面板 | 快捷键 | 说明 |
|------|--------|------|
| 总览面板 (Overview Panel) | Tab | 完整的装备管理界面，含顶部标签栏 |
| 快捷面板 (Quick Panel) | V | 单容器快显，循环切换 |
| 浮动提示 (Floating Toast) | 自动 | 2.5秒后自动消失的提示文本 |

### 1.2 总览面板布局

```
┌─────────────────────────────────────────────────┐
│  [装备容器] [角色] [制作] [地图] [设置]          │ ← TopTabBar
├──────────────┬──────────────────────────────────┤
│   左面板      │         右面板                    │
│  (28%)        │        (72%)                     │
│               │                                  │
│  ┌─武器槽─┐   │  ▼ 胸挂 (军用胸挂)  5/12        │
│  │主武│副武│   │  ┌──┬──┬──┬──┐                │
│  │小刀│手枪│   │  │  │  │  │  │                │
│  └───────┘   │  ├──┼──┼──┼──┤                │
│               │  │  │  │  │  │                │
│  ┌─纸娃娃─┐  │  └──┴──┴──┴──┘                │
│  │ 头部    │  │  ▶ 上衣口袋 (战术上衣) 2/4     │
│  │ 胸挂    │  │  ▶ 腰带  3/6                  │
│  │ 上衣    │  │  ▼ 裤子口袋  1/4              │
│  │ 防弹衣  │  │  ┌──┬──┐                      │
│  │ 腰带    │  │  │物│  │                      │
│  │ 裤子    │  │  ├──┼──┤                      │
│  │ 背包    │  │  │  │  │                      │
│  └─────────┘ │  └──┴──┘                      │
│               │  ▶ 背包 (军用背包)  8/12       │
│  ┌─属性面板┐ │                                  │
│  │护甲:5.0 │ │                                  │
│  │保暖:2.3 │ │                                  │
│  │负重:25/ │ │                                  │
│  │    30   │ │                                  │
│  └─────────┘ │                                  │
└──────────────┴──────────────────────────────────┘
```

**左面板 (28%)**:
1. **武器槽** (2×2 方格): 主武/副武/小刀/手枪 — 小刀和手枪在腰带未装备时锁定
2. **纸娃娃装备区**: 纵向排列 7 个装备槽（头/胸挂/上衣/防弹衣/腰带/裤子/背包），每槽显示装备名或"空"，支持双击卸下和拖拽装备
3. **属性面板**: 护甲进度条 + 保暖进度条 + 负重进度条（颜色按比例变化: 绿→黄→红）

**右面板 (72%)**:
- 5 个**可折叠容器区块** (胸挂/上衣/腰带/裤子/背包)，每块显示:
  - 折叠箭头 ▶/▼ 切换展开
  - 容器名 + 装备名 + 容量 Used/Total
  - 展开后显示完整网格（RectTransform 绝对坐标），每个格子独立 Image + 注册拖拽
  - 跨格物品以 overlay RectTransform 覆盖多个格子的视觉区域
  - 物品覆盖层分两部分: 上半部分显示物品名称（Bold），右下角金色 xN 显示数量

**交互特性**:
- 双击装备槽卸下物品（生成 WorldItem 在玩家前方 1.2m）
- 拖拽物品到装备槽直接装备（支持纸娃娃槽和武器槽 2×2 方格）
- 拖拽物品到容器网格移动/放入
- 按 T 键旋转物品（拖拽中/选中时/原地均可旋转）
- 打开背包时自动退出建造模式、隐藏 HUD 和快捷栏
- ESC 关闭面板，恢复 HUD 显示；关闭时自动取消选中

### 1.3 快捷面板 (V键)

循环顺序: 胸挂 → 上衣 → 腰带 → 裤子 → 背包（跳过未装备容器）
- 显示当前容器的网格内容（GridLayoutGroup 实现）
- 标题行显示容器名 + 容量 + 负重百分比
- 物品格子上叠加半透明图标

### 1.4 数据快照架构

```
InventoryViewData (struct)            ← BuildViewData() 构建
├── containers: List<ContainerViewData>
│   ├── containerName, equipSlot, gridWidth, gridHeight
│   └── items: List<ItemOnGrid>
│       ├── itemName, count, gridX, gridY, gridWidth, gridHeight
│       └── icon, itemData
├── equippedNames: Dictionary<EquipSlot, string>
├── totalArmor, totalWarmth
├── currentWeight, maxWeight
├── isHardOverloaded, overloadRatio
```

UI 订阅 `InventoryViewChangedEvent`，在 `OnViewChanged()` 中只更新内容区（不重建标签栏），实现增量刷新。`_currentTab` 保留当前选中的标签。

### 1.5 DragDropManager — 拖拽系统

**类型**: MonoBehaviour 单例 (DontDestroyOnLoad)

**核心流程**:
1. **ItemDragHandler** (挂载在每个可拖拽格子上) 接收 IPointerDownHandler/IPointerUpHandler 事件
2. 转交 DragDropManager，判断是否为拖拽（移动距离 >= 8px 阈值）
3. 拖拽时创建半透明拖拽图标跟随鼠标
4. 松手时通过坐标检测找到目标 CellRegion（遍历已注册的 RectTransform 列表，从后往前）
5. 计算目标格子位置，执行 `TryMoveToContainer()`

**CellRegion 结构**: 记录每个格子的 RectTransform + 所属容器 + 网格坐标 + 是否武器槽标记，支持坐标→容器的反向查找。

**拖拽放置规则**:
- 目标为空闲格子 → 放入
- 目标为装备槽且物品可装备该槽 → 触发 EquipItem
- 目标为同容器不同位置 → 移动（原地旋转校验）
- 目标无效 → 回弹到原位
- 所有情况下都显示 Toast 提示

**旋转系统** (T键):
- **拖拽中旋转**: toggle `_dragRotated` 标记，松手落点时以旋转状态校验放置
- **原地旋转**: 拖拽开始前按 T，在源容器内旋转。公式: `(rx, ry) → (ry, rx)`，保持点击点不动
- **选中旋转**: 物品已选中时按 T，在容器内旋转

**选中状态**:
- 点击物品 → 白色边框高亮（四条子 RectTransform: SelTop/SelBot/SelLeft/SelRight）
- 再次点击选中的物品 → 取消选中
- 点击空白处（LateUpdate 中拦截 MouseButtonDown） → 取消选中
- UI 重建后通过 `RefreshSelectionBorder()` 恢复选中框（遍历 CellRegion 查找同名 overlay）

---

## 二、当前问题

### 2.1 操作体验问题

| # | 问题 | 影响 |
|---|------|------|
| 1 | **拖拽距离太长** | 背包格在右下，纸娃娃装备槽在左上，拖拽跨越大半屏幕，鼠标空间不够 |
| 2 | **选中白框闪瞬** | 点击物品后白框只显示一瞬间就消失。根因：`OnInventoryChanged` → `ShowOverview()` 全量重建 UI → `ClearCells` → `RemoveBorderFromOverlay` 直接删框。`RefreshSelectionBorder` 靠 overlay 名字找回，但 overlay 已被 Destroy |
| 3 | **缺少点击移动** | 只能拖拽移动物品，没有"选中物品→点击目标格子→移动"的点击操作方式 |
| 4 | **UI 频繁重建** | 每次 `InventoryChanged` 事件都触发 `ShowOverview` 全量重建（Destroy + Instantiate 所有格子），选中状态和拖拽状态丢失 |

### 2.2 架构问题

| # | 问题 | 说明 |
|---|------|------|
| A | **选中状态依附于 UI 元素** | 白框画在 overlay GameObject 上，UI 重建时 overlay 被 Destroy，白框丢失。选中数据虽然保存在 DragDropManager，但视觉无法恢复 |
| B | **CleanCells 杀边框** | `ClearCells()` 中 `RemoveBorderFromOverlay()` 主动删除白框，即使格子没被销毁 |
| C | **空格子无法点击** | 只有被物品占用的格子有 `ItemDragHandler`，空格子没有任何点击接收器。点击移动必须依赖空格子可点击 |
| D | **UI 重建粒度太粗** | `ShowOverview` 全量重建所有容器网格，而大多数操作只需要移动一个物品、更新一个格子的显示 |

### 2.3 待完成功能（从背包系统.md §9 继承）

- [ ] 物品右键菜单（"使用"/"丢弃"/"拆分堆叠"）
- [ ] 容器内物品排序（按名称/重量/类别）
- [ ] 物品对比提示（鼠标悬停显示装备槽当前物品 vs 背包物品属性差异）

---

## 三、升级目标

### 3.1 核心目标

1. **点击移动**：选中物品后，点击目标空格子（或装备槽），物品移动到该位置
2. **白框常驻**：选中白框不受 UI 重建影响，始终可见直到取消选中
3. **保留拖拽**：拖拽移动保持原样，不删除

### 3.2 架构改进

1. **选区管理器独立**：选中白框用自己的持久 GameObject，不依附任何格子或 overlay。UI 重建前摘下，重建后重新挂到新格子。
2. **空格子可点击**：每个已注册的格子都挂点击处理器
3. **增量刷新**：移动单个物品时只更新受影响的格子，不全量重建面板

### 3.3 可选增强

- 右键菜单（使用/丢弃/拆分堆叠）
- 物品悬停对比
- 容器内排序

---

## 四、技术方案思路

### 4.1 白框持久化

将选中白框从 overlay 的子节点改为独立 GameObject，由 DragDropManager 持有：

- 创建时挂到最外层 Canvas（避免 UI 重建时被 Destroy）
- 选中时用屏幕坐标定位到目标格子的世界角点
- `ClearCells` 时不销毁白框，只隐藏
- `RefreshSelectionBorder` 用格子坐标重新计算位置并显示

### 4.2 点击移动

两种可行方案：

**方案 A — 给格子加点击处理器**：
- 在 `RegisterCellRect` 时为每个格子挂 `CellClickHandler`（实现 `IPointerClickHandler`）
- 点击空格子 → 回调 `DragDropManager.HandleCellClick(container, gridX, gridY)`
- 点击已被物品占用的格子 → overlay 的 `ItemDragHandler` 优先接收

**方案 B — Input 层统一检测**：
- 在 `Update` 中检测 `Input.GetMouseButtonDown(0)` + 有选中物品 + 非拖拽中
- 调用 `FindCellAtPosition` 找到鼠标下的格子
- 执行移动逻辑

### 4.3 增量刷新

将 `ShowOverview` 拆分为：
- `RefreshContainer(container)` — 只重建指定容器的网格
- `RefreshEquipSlots()` — 只重建纸娃娃和武器槽
- 大多数 InventoryChanged 事件只需要刷新涉及的容器

---

## 五、已验证不可行的方案

> 2026-06-24 多次尝试后记录，避免重复踩坑。

### 5.1 白框持久化 — 已尝试方案

| # | 方案 | 问题 |
|---|------|------|
| 1 | **白框挂到独立 Canvas，屏幕坐标定位** | 位置计算错误，白框飞到屏幕其他位置，与格子不对齐 |
| 2 | **白框挂到格子上（SetParent），ClearCells 前摘下再挂回** | 格子被 Destroy 时白框作为子节点一起被 Destroy，`DontDestroyOnLoad` 只防场景加载不防父节点销毁 |
| 3 | **RefreshSelectionBorder 按名字找回 overlay 重建白框** | overlay 已被 Destroy，`parent.Find(name)` 返回 null；改用 `_cellRegions` 按坐标找格子 → 格子是新创建的，但白框条仍挂在旧 overlay 上已被销毁 |

**根因**: 白框依附于任何会被 Destroy 的 GameObject 都会死。需要完全独立的持久化对象。

### 5.2 点击移动 — 已尝试方案

| # | 方案 | 问题 |
|---|------|------|
| 1 | **Update 中 `Input.GetMouseButtonDown` + `FindCellAtPosition`** | UGUI 事件系统先消费了点击，`Input.GetMouseButtonDown` 在 Update 帧的时机不确定；`FindCellAtPosition` 在格子未注册时返回 null |
| 2 | **LateUpdate 中检测点击 + `_clickConsumed` 标志位** | 与 ItemDragHandler 的 PointerDown 时序冲突，`_clickConsumed` 在某些路径未正确设值，空格子仍然无法触发 |
| 3 | **给每个注册格子挂 `CellClickHandler`（IPointerClickHandler）** | 占用格子的 overlay 拦截了点击事件（overlay 有 `raycastTarget=true` 以接收 ItemDragHandler），空格子的 CellClickHandler 能收到点击但 HandleCellClick 未被调用（可能是 InventoryUI.cs 内的 using 冲突或逻辑路径未走到） |
| 4 | **跨容器移动（new PlacedItem 构造）** | 可以编译但运行时 Placement 校验失败，`IsSpaceFree` 通过但 `placedItems.Add` 后未正确刷新 UI |

**根因**: 空格子缺少可靠的事件接收机制，且移动后的 UI 刷新依赖全量 `ShowOverview`，无法局部更新。

### 5.3 为什么拖拽能工作但点击不行

拖拽流程（能工作）:
```
ItemDragHandler(在overlay上) → DragDropManager → FindCellAtPosition → TryMoveToContainer
```
- overlay 有 `raycastTarget=true`，UGUI 事件能到达
- 拖拽全程在 DragDropManager 内部维护状态
- 松手时才触发 UI 刷新，拖拽过程中 UI 不重建

点击流程（不能工作）:
```
空格子(无overlay) → ??? → 无法接收点击 → 无法触发移动
```
- 空格子的 Image 是背景，没有 ItemDragHandler
- 给格子加 Button 会与 overlay 的事件冲突
- 给格子加 IPointerClickHandler 被 overlay 拦截

### 5.4 建议方向

1. **白框**: 创建完全独立的 Canvas + GameObject，用 `GetWorldCorners` 获取目标格子屏幕坐标，转换到白框 Canvas 的局部坐标。定位到空格子或 overlay 都用同一逻辑，不依赖 parent。
2. **点击移动**: 在 DragDropManager.Update 中统一处理，不依赖 UGUI 事件系统。用 `RectTransformUtility.RectangleContainsScreenPoint` 遍历 `_cellRegions` 找鼠标下的格子。关键是**先于 UGUI 事件系统拿到点击**（Input.GetMouseButtonDown 在 Update 中是可用的，但要注意与拖拽的时序冲突）。
3. **增量刷新**: 将 `ShowOverview` 拆小，`HandlerCellClick` 移动成功后只重建涉及的容器，不触发全量刷新。

---

## 五、文件

| 文件 | 改动 |
|------|:--:|
| `UI/DragDropManager.cs` | 改 — 白框持久化 + 点击移动 |
| `UI/InventoryUI.cs` | 改 — 增量刷新 + 接入点击移动 |
| `UI/ItemDragHandler.cs` | 不变 — 保留拖拽功能 |

---

## 📜 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| `v1.0` | 2026-06-24 | 初始版本：从背包系统.md 剥离 UI 相关章节，补充当前问题分析和升级目标 |
