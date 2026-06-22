# UI 系统 UGUI 重构 — 设计定稿与实施记录

> 版本: v1.0
> 最后更新: 2026-06-22
> 相关提交: `49601cf` (设计定稿) → `e0970bf` (ServiceLocator 重构) → `c18fc7f` (删除 IMGUI) → `2a5003a` (射线修复)

---

## 一、重构动机

### 1.1 旧架构问题

| 问题 | 影响 |
|------|------|
| 每个 UI 系统自己 `new GameObject` / `new Canvas`，重复代码 ~300 行/文件 | 维护成本高，Canvas 配置不一致 |
| 大量 `OnGUI()` / IMGUI 代码（~3571 行）与 UGUI 两套并存 | 渲染冲突，FSR 不兼容，性能浪费 |
| `FindObjectOfType` 遍布 ~50+ 文件 | 运行时 GC 分配，场景重载后引用悬空 |
| Inspector 硬依赖：必须在编辑器拖拽引用 | 场景丢失后无法自动恢复 |
| HUD Canvas 的 `GraphicRaycaster` 拦截鼠标射线 | 战斗/建造系统无法正常点击 3D 世界 |

### 1.2 重构目标

- **UGUI 永久化**: 删除所有 IMGUI/OnGUI 代码，`UIModeConfig.UseUGUI = true` 硬编码
- **动态构建统一化**: 所有 UI 动态创建走 `UGUIBuilder` 静态工厂，禁止每个文件自己拼 GameObject
- **预构建优先**: Editor 工具可一键将 UI 结构固化到场景 prefab，运行时直接 Find 而非 new
- **依赖注入**: `ServiceLocator` + `PlayerRegistry` 替代所有 `FindObjectOfType` / `FindWithTag`
- **射线穿透**: 纯显示 HUD 不挂 `GraphicRaycaster`；交互 UI 设 `blockingObjects = None`

---

## 二、核心基础设施

### 2.1 UIModeConfig — 渲染模式开关（已固化为永久 UGUI）

**文件**: `Assets/_Game/Core/UIModeConfig.cs`

```csharp
public static class UIModeConfig
{
    public static bool UseUGUI = true;
}
```

- 原有两个 bool (`UseUGUI` / `UseIMGUI`) + F7 快捷键切换 → 已删除
- 所有 UI 脚本在 `Start()` 中检查此字段，`false` 时 `enabled = false` 并 `return`

### 2.2 ServiceLocator — 全局服务定位器（替代 FindObjectOfType）

**文件**: `Assets/_Game/Core/ServiceLocator.cs`

```csharp
// 注册（Awake）
ServiceLocator.Register(this);

// 获取
var inv = ServiceLocator.Get<Inventory>();
```

关键设计:

| 特性 | 说明 |
|------|------|
| `Register<T>(T instance)` | 字典存储，重复注册自动覆盖（场景重载安全） |
| `Get<T>()` | 先从字典取；若 Unity Object 已销毁则自动移除并回退查找 |
| `FallbackFind<T>()` | 未注册时调用 `FindObjectOfType` 兜底，并缓存 + Editor 警告，提示开发者补 `Register` |
| `Clear()` | 场景切换时清空，防止引用泄漏 |
| 影响范围 | ~50+ 文件从 `FindObjectOfType` / Inspector 拖拽迁移到 ServiceLocator |

### 2.3 PlayerRegistry — 玩家引用唯一来源

**文件**: `Assets/_Game/Core/PlayerRegistry.cs`

```csharp
// 注册（GameBootstrap 场景加载后）
PlayerRegistry.Register(player);

// 获取（所有系统）
var player = PlayerRegistry.Get<PlayerController>();
```

- 替代 18 处 `GameObject.FindWithTag("Player")` → 1 处
- 场景重载安全（底层 GameObject 引用跟随新场景）

### 2.4 UGUIBuilder — UI 动态创建静态工厂

**文件**: `Assets/_Game/UI/UGUIBuilder.cs`

#### API 清单

| 方法 | 用途 | 关键参数 |
|------|------|----------|
| `CreateCanvas(name, sortOrder)` | ScreenSpaceOverlay Canvas + CanvasScaler (1920x1080 ref, 0.5 match) | sortOrder=100 |
| `CreateFullscreenBG(name, parent, color)` | 全屏拉伸 Image 背景 | raycastTarget=false |
| `CreateCenteredPanel(name, parent, w, h, color)` | 居中固定尺寸面板 | |
| `CreateStretchPanel(name, parent, color)` | 全尺寸拉伸面板 | |
| `CreateText(name, parent, text, fontSize, style, align, w, h, color)` | 标准 Text | Arial 14pt 懒加载字体 |
| `CreateTextAnchored(name, parent, text, anchor, pos, w, h, ...)` | 锚定 Text | 指定 anchor + anchoredPosition |
| `CreateButton(name, parent, label, bgColor, w, h, fontSize)` | 按钮（Image+Button+Label 子对象） | 自动 Bold 标签 |
| `CreateProgressBar(name, parent, w, h, bgColor, fillColor, out barGo, out bgImage)` | 进度条（背景+填充，返回 fill Image） | 填充 anchor 靠左 |
| `SetBarFill(fillImage, percent)` | 设置进度条填充比例 (0~1) | |
| `CreateScrollView(name, parent, w, h, out scrollGo)` | ScrollRect（Viewport+Content） | 竖直滚动，Content 顶部对齐 |
| `Stretch(rt)` | RectTransform 全拉伸 | anchorMin=0, anchorMax=1 |
| `Center(rt, w, h)` | RectTransform 居中固定尺寸 | |
| `AnchorAt(rt, anchor, pos)` | 锚定到指定点 | |
| `SetSize(rt, w, h)` | 设置固定尺寸 | |

#### 设计原则

- 所有方法返回具体类型（`Canvas`, `Button`, `Text`, `Image`），调用侧无需手动 GetComponent
- 参数有合理默认值（fontSize=14, color=white, w=200, h=30）
- 字体懒加载（`Font.CreateDynamicFontFromOSFont("Arial", 14)`），首次调用时创建
- "未来可改为加载 prefab 而不改调用侧"——调用侧不依赖内部实现细节

---

## 三、pre-build vs fallback 双路径模式

每个 UI 脚本在 `BuildUI()` / `CreateUI()` 中遵循同一模式：

```
Start()
  ├── 检查 UIModeConfig.UseUGUI（false → enabled=false 并 return）
  ├── TryFindExisting()  ← 优先查找 PreconfigureUI 预构建的子 Canvas
  │   ├── 找到 → 缓存引用，RefreshAll()，return
  │   └── 未找到 ↓
  └── BuildFromCode()    ← 兜底：运行时用 UGUIBuilder 逐 GameObject 构建
```

### 3.1 代码模式（以 SurvivalHUD 为例）

```csharp
private void BuildUI()
{
    // 步骤 1: 优先使用 PreconfigureUI 预构建的
    var existing = transform.Find("SurvivalHUD_Canvas");
    if (existing != null)
    {
        _canvasGo = existing.gameObject;
        FindBars(existing);  // 通过 Transform.Find 缓存子对象引用
        RefreshAll();
        return;
    }

    // 步骤 2: 兜底——运行时动态创建
    var canvas = UGUIBuilder.CreateCanvas("SurvivalHUD_Canvas", 100);
    canvas.transform.SetParent(transform, false);
    _canvasGo = canvas.gameObject;
    // ... 逐个创建 UI 子元素 ...
}
```

### 3.2 各 HUD 预构建查找键

| UI 脚本 | Canvas 名称 | SortingOrder | PreconfigureUI 构建方法 |
|---------|-------------|--------------|------------------------|
| `CrosshairUI` | `CrosshairCanvas` | 999 | `BuildCrosshair()` |
| `DecibelHUD` | `DecibelHUD_Canvas` | 50 | `BuildDecibelHUD()` |
| `WeatherHUD` | `WeatherHUD_Canvas` | 50 | `BuildWeatherHUD()` |
| `TopLeftHUD` | `TopLeftHUD_Canvas` | 50 | `BuildTopLeftHUD()` |
| `SurvivalHUD` | `SurvivalHUD_Canvas` | 100 | `BuildSurvivalHUD()` |
| `QuickItemBar` | `QuickItemBar_Canvas` | 90 | `BuildQuickItemBar()` |
| `MainMenuUI` | `MainMenu_Canvas` | 300 | `BuildMainMenuUI()` |

### 3.3 查找子对象引用的辅助方法

各 UI 脚本提供私有方法通过 `Transform.Find` 路径重新绑定引用：

- `SurvivalHUD.FindBars(canvasRoot)` → 按 "HealthBar/Slider", "HungerBar/Slider" 等路径找 Slider+Text
- `QuickItemBar.FindSlotRefs()` → 按 "SlotGrid/Slot_{i}/Count" 等路径找 Image+Text
- `CrosshairUI` → 直接 `transform.Find("CrosshairCanvas/Crosshair")`
- `DecibelHUD` → `existing.Find("NoiseText")`
- `TopLeftHUD` → `existing.Find("MainText")`, `existing.Find("WeaponText")`, `existing.Find("HPText")`

---

## 四、PreconfigurePlayer — 一键预配置 30+ 组件

**文件**: `Assets/_Game/Editor/PreconfigurePlayer.cs`  
**菜单**: `Tools → Preconfigure Player`

### 4.1 组件注入顺序（与 GameBootstrap 完全一致）

按依赖顺序在 Player GameObject 上添加组件：

| # | 组件 | 类型 |
|---|------|------|
| 1 | `Rigidbody` | Unity 基础（FreezeRotation） |
| 2 | `CapsuleCollider` | Unity 基础（center=1, height=2, radius=0.3） |
| 3 | `Animator` | Unity 基础（控制器 + Avatar 自动赋值） |
| 4 | `PlayerController` | 玩家移动 |
| 5 | `Inventory` | 背包系统 |
| 6 | `PlayerInteraction` | 交互系统 |
| 7 | `PlayerCharacter` | 角色属性 |
| 8 | `SurvivalSystem` | 生存数值 |
| 9 | `PlayerCombat` | 战斗系统 |
| 10 | `WeaponShooting` | 射击 |
| 11 | `WeaponAiming` | 瞄准 |
| 12 | `WeaponHolder` | 武器挂载 |
| 13 | `SpreadVisualizer` | 散布可视化 |
| 14 | `SurvivalHUD` | 生存 HUD |
| 15 | `QuickItemBar` | 快捷物品栏 |
| 16 | `CrosshairUI` | 准星 |
| 17 | `DecibelHUD` | 噪声显示 |
| 18 | `WeatherHUD` | 天气显示 |
| 19 | `TopLeftHUD` | 时间/车速/AI |
| 20 | `BuildMenuUI` | 建造菜单 |
| 21 | `GhostPreview` | 建造预览 |
| 22 | `CraftingUI` | 合成 UI |
| 23 | `ChemicalResearchManager` | 化学研究管理器 |
| 24 | `ChemicalResearchUI` | 化学研究 UI |
| 25 | `MouseGroundProjector` | 鼠标地面投影 |
| 26 | `StaminaSystem` | 体力系统 |
| 27 | `SurvivalXPSystem` | 生存经验 |
| 28 | `BuildModeController` | 建造控制器 |
| 29 | `BuildModeInputLock` | 建造输入锁 |
| 30 | `DamageablePlayer` | 玩家受伤 |
| 31 | `FactionComponent` | 阵营 |
| 32 | `WeaponSwitcher` | 武器切换 |
| 33 | `ProfessionApplier` | 职业应用 |
| 34 | `VehicleInputLock` | 车辆输入锁 |
| 35 | `InventoryTest` | 测试工具 |

### 4.2 Animator 冲突解决

**问题**: Player GameObject 和其子模型对象上可能各有一个 Animator，导致动画状态机混乱。

**解决方案** (`PreconfigurePlayer.cs` 第 117-123 行):

```csharp
// Animator — 只在模型子对象上保留一个（Parent 不需要，避免冲突）
var anim = player.GetComponentInChildren<Animator>();
if (anim == null) { anim = player.AddComponent<Animator>(); added++; }

// 如果 Animator 在子对象上，且 Player 本身也有一个多余的，删除它
var selfAnim = player.GetComponent<Animator>();
if (selfAnim != null && selfAnim != anim)
{
    Object.DestroyImmediate(selfAnim);
    Debug.Log("[PreconfigurePlayer] 清理 Player 上多余 Animator，模型子对象已有");
}
```

**GameBootstrap 对应逻辑** (第 55-56 行):
```csharp
// Animator — 模型子对象已有则不再添加
if (player.GetComponentInChildren<Animator>() == null)
    player.AddComponent<Animator>();
```

规则：**Animator 优先归属模型子对象；Player 根节点上仅当子对象无 Animator 时作为兜底添加。**

---

## 五、PreconfigureUI — 一键预构建 6 个 HUD Canvas + MainMenu

**文件**: `Assets/_Game/Editor/PreconfigureUI.cs`

### 5.1 菜单功能

| 菜单项 | 功能 |
|--------|------|
| `Tools → Preconfigure All UI` | 一键构建全部 6 个 HUD Canvas 到 Player |
| `Tools → Clean Orphan UI` | 清理场景中不在 Player 下的孤立 UI GameObject |
| `Tools → Preconfigure MainMenu` | 专门在 MainMenu.scene 中预构建 MainMenu Canvas |

### 5.2 BuildCrosshair (`CrosshairCanvas`, sortOrder=999)

- 创建名为 `Crosshair` 的子 GameObject（Image 组件）
- 程序化生成 32x32 空心圆环+中心点准星纹理
- `preserveAspect=true`, `raycastTarget=false`, `sizeDelta=(24,24)`

### 5.3 BuildDecibelHUD (`DecibelHUD_Canvas`, sortOrder=50)

- 创建 `NoiseText`：左上角锚定 (0,1)，位置 (10,-45)，300x24，fontSize=16

### 5.4 BuildWeatherHUD (`WeatherHUD_Canvas`, sortOrder=50)

- `WeatherLabel`: 左上升锚定 (0,1)，位置 (10,-75)，300x22，fontSize=15
- `TempLabel`: 左上升锚定 (0,1)，位置 (10,-95)，300x22，fontSize=15

### 5.5 BuildTopLeftHUD (`TopLeftHUD_Canvas`, sortOrder=50)

- `MainText`: 左上升锚定 (0,1)，位置 (10,-10)，350x60，fontSize=18，`supportRichText=true`
- `WeaponText`: 左上升锚定 (0,1)，位置 (10,-110)，350x24，fontSize=15，`supportRichText=true`
- `HPText`: 左上升锚定 (0,1)，位置 (10,-132)，350x24，fontSize=15，`supportRichText=true`

### 5.6 BuildSurvivalHUD (`SurvivalHUD_Canvas`, sortOrder=100)

4 条统计条，右上角锚定 (1,1)，每条间距 `barHeight+6`：

```
HealthBar  → (-20, -20)    红色
HungerBar  → (-20, -46)    橙黄 (0.8,0.6,0)
ThirstBar  → (-20, -72)    蓝色 (0,0.5,1)
TempBar    → (-20, -98)    橙色 (1,0.5,0)
```

每条结构: `[Name] → Label (55x20, fontSize=14, 左中对齐) + Slider (内含 Background + Fill)`

### 5.7 BuildQuickItemBar (`QuickItemBar_Canvas`, sortOrder=90)

- `SlotGrid` + `GridLayoutGroup` (FixedColumnCount=10, cellSize=50x50, spacing=3)
- 10 个 `Slot_{i}` (Image)，每个内含:
  - `KeyNum` (左上角数字标签 1-0)
  - `ItemName` (顶部居中物品名)
  - `Count` (底部居中数量 xN)
  - `Border` (4 条白色边框色条，默认隐藏)
- `ArrowLeft` / `ArrowRight` (翻页箭头，`<` / `>` 文字)
- `GraphicRaycaster.blockingObjects = None` (允许点击穿透到 3D 场景)

### 5.8 BuildMainMenuUI (MainMenu_Canvas, sortOrder=300)

仅能在 **MainMenu.scene** 中运行，依赖场景中的 `MainMenuRoot` GameObject：

- 全屏黑色半透明背景 (0.02, 0.02, 0.05, 0.98)
- 居中面板 700x620，深色 (0.05, 0.05, 0.08)
- 标题 "末日生存" (fontSize=32) + 副标题 "选择存档" (fontSize=14, 灰色)
- 5 个存档槽位 `Slot_0` ~ `Slot_4` (70px 高, 8px 间距)，深灰色背景
- `ExitBtn` "退出游戏" 按钮，红色系 (0.3, 0.1, 0.1)
- Canvas 默认隐藏 (`SetActive(false)`，由 `MainMenuUI.Start()` 控制显示)

---

## 六、Clean Orphan UI — 孤立 UI 清理

**菜单**: `Tools → Clean Orphan UI`  
**实现**: `PreconfigureUI.CleanupStandaloneUI()`

### 6.1 问题

重构前，UI 组件被挂载在场景根级别的独立 GameObject 上（不在 Player 下），导致：
- PreconfigureUI 无法找到正确的父对象
- 场景重载时 UI 可能重复创建
- 与 Player 生命周期不同步

### 6.2 清理逻辑

```csharp
var roots = SceneManager.GetActiveScene().GetRootGameObjects();
foreach (var go in roots)
{
    if (go == null || go == player) continue;
    bool hasHudComp = go.GetComponent<SurvivalHUD>() != null
        || go.GetComponent<QuickItemBar>() != null
        || go.GetComponent<DecibelHUD>() != null
        || go.GetComponent<WeatherHUD>() != null
        || go.GetComponent<TopLeftHUD>() != null
        || go.GetComponent<CrosshairUI>() != null;
    if (hasHudComp) { Object.DestroyImmediate(go); count++; }
}
```

- 扫描场景根对象，任何挂有 HUD 组件且不在 Player 下的 GameObject → 直接删除
- 保留 Player 及其子对象上的 HUD 组件

---

## 七、ServiceLocator 注册修复

### 7.1 问题

早期代码中，许多系统（如 `SurvivalSystem`, `Inventory`, `WeatherManager`）的 `Awake()` 未调用 `ServiceLocator.Register(this)`，导致：
- 其他系统调用 `ServiceLocator.Get<T>()` 时找不到已存在的实例
- 触发 `FallbackFind` → `FindObjectOfType`（性能差 + Editor 警告刷屏）

### 7.2 修复方案

在以下核心系统的 `Awake()` 中添加 `ServiceLocator.Register(this)`:

| 系统 | 文件 |
|------|------|
| `SurvivalSystem` | `_Game/Systems/Survival/SurvivalSystem.cs` |
| `Inventory` | `_Game/Systems/Inventory/Inventory.cs` |
| `PlayerCharacter` | `_Game/Systems/Character/PlayerCharacter.cs` |
| `WeatherManager` | `_Game/Systems/Weather/WeatherManager.cs` |
| `TimeManager` | `_Game/Systems/Time/TimeManager.cs` |
| `DecibelSystem` | `_Game/Systems/Audio/DecibelSystem.cs` |
| `FactionSystem` | `_Game/Systems/Threat/FactionSystem.cs` |
| `StaminaSystem` | `_Game/Systems/Character/StaminaSystem.cs` |
| `SurvivalXPSystem` | `_Game/Systems/Character/SurvivalXPSystem.cs` |
| `CraftingSystem` | `_Game/Systems/Crafting/CraftingSystem.cs` |
| `ItemGraphManager` | `_Game/Systems/ItemGraph/ItemGraphManager.cs` |
| `MuzzleFlashSystem` | `_Game/Systems/VFX/MuzzleFlashSystem.cs` |
| `ChunkManager` | `_Game/Systems/WorldGen/ChunkManager.cs` |
| `ZombieSpawner` | `_Game/Systems/Zombie/ZombieSpawner.cs` |
| `ThreatSystem` | `_Game/Systems/Threat/ThreatSystem.cs` |

### 7.3 效果

- 消除所有 `[ServiceLocator] ⚠️ XXX 未注册，已通过 FindObjectOfType 兜底` 的 Editor 警告
- 运行时 `FindObjectOfType` 调用从 ~88 次降至 ~2 次（仅启动时的 CameraFollow、FactionSystem 等）
- 场景重载更安全（字典自动覆盖旧实例）

---

## 八、旧 Setup 脚本移除

### 8.1 SetupPlayerCharacter.cs → PreconfigurePlayer.cs

**旧文件**: `Assets/_Game/Editor/SetupPlayerCharacter.cs`

- 原有 359 行，混杂了组件添加 + 字段赋值 + 资源引用绑定
- 职责不清：既做 Editor 配置又做运行时初始化

**重构**:
- 重命名为 `PreconfigurePlayer.cs`，纯 Editor 工具（菜单驱动）
- 职责单一：仅负责在 Player GameObject 上添加/配置组件
- 运行时初始化逻辑归于 `GameBootstrap.OnAfterSceneLoad()`（自动恢复 + 字段自动填充）

### 8.2 IMGUI/OnGUI 代码全部删除

**提交**: `c18fc7f` — 删除全部 IMGUI/OnGUI 旧版 UI 代码  
**影响**: 16 个文件，净删除 3571 行

| 删除的 OnGUI 代码 | 文件 | 行数 |
|-------------------|------|------|
| AI 驾驶/武器/修理面板 | `AIBotUI.cs` | -1023 |
| 工业设备 UI | `ProductionDeviceUI.cs` | -550 |
| DevTools 调试面板 | `DevTools.cs` | -413 |
| 建造菜单 | `BuildMenuUI.cs` | -386 |
| 合成 UI | `CraftingUI.cs` | -200 |
| 化学研究 UI | `ChemicalResearchUI.cs` | -175 |
| AI 背包 UI | `AIBotInventoryUI.cs` | -165 |
| 终端 UI | `TerminalUI.cs` | -163 |
| 电源 UI | `PowerSourceUI.cs` | -147 |
| 调试面板 | `DebugPanel.cs` | -144 |
| 僵尸调试窗口 | `ZombieSpawnDebugWindow.cs` | -76 |
| 天气 HUD | `WeatherHUD.cs` | -38 |
| 左上 HUD | `TopLeftHUD.cs` | -44 |
| 电缆连接 | `CableLinker.cs` | -20 |
| 噪声 HUD | `DecibelHUD.cs` | -15 |

这些文件的 UGUI 版本均在重构后保留并正常工作，部分直接复用 `UGUIBuilder` 重构。

---

## 九、GraphicRaycaster 射线穿透修复

**提交**: `2a5003a`

### 9.1 问题

所有 HUD Canvas 默认挂有 `GraphicRaycaster`，导致：
- 鼠标点击被 HUD Canvas 拦截，无法到达 3D 场景
- 战斗系统（射击/瞄准）和建造系统（放置方块）无法正常响应鼠标输入

### 9.2 修复

| Canvas | 修复措施 |
|--------|---------|
| `SurvivalHUD_Canvas` | 删除 `GraphicRaycaster` 组件（纯显示面板，无需点击） |
| `QuickItemBar_Canvas` | `GraphicRaycaster.blockingObjects = None`（需要点击格子但允许穿透到 3D） |
| 其他 HUD | `CrosshairCanvas`, `DecibelHUD_Canvas`, `WeatherHUD_Canvas`, `TopLeftHUD_Canvas` — 均不挂 `GraphicRaycaster` |

### 9.3 代码

**SurvivalHUD** — PreconfigureUI 构建时不添加 GraphicRaycaster:

```csharp
// CreateChildCanvas 只创建 Canvas + CanvasScaler，不含 GraphicRaycaster
var canvas = CreateChildCanvas(player, "SurvivalHUD_Canvas", 100);
// Canvas 组件本身不需要 Raycaster
```

**QuickItemBar** — 需要 Raycaster（格子点击检测）但允许穿透:

```csharp
var canvas = UGUIBuilder.CreateCanvas("QuickItemBar_Canvas", 90);
var gr = canvasObject.GetComponent<GraphicRaycaster>();
if (gr != null) gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;
```

---

## 十、运行时兜底：GameBootstrap 自动恢复

**文件**: `Assets/_Game/Core/GameBootstrap.cs`

当场景丢失预配置数据时（如电脑崩溃、场景文件损坏），`GameBootstrap.OnAfterSceneLoad()` 自动执行：

```
1. 查找 Player (tag=Player || name=player || name=Player)
2. 注册到 PlayerRegistry
3. 添加 Unity 基础组件 (Rigidbody, CapsuleCollider, Animator) 兜底
4. 按 PreconfigurePlayer 相同顺序 AddIfMissing 全部 32 个游戏组件
5. 设置玩家阵营为 Player
6. 摄像机跟随
7. 自动解析 ScriptableObject 引用 (SurvivalData, CharacterData, BuildableCatalog, ChemicalResearchData, FactionData)
8. 确保 InputRouter + Managers 对象存在
```

每次 `AddIfMissing` 成功后输出警告:
```
[GameBootstrap] ⚠️ SurvivalHUD 未预置在 Player 上，已运行时补救。请运行 Tools → Preconfigure Player 固化。
```

---

## 十一、MainMenuUI — 主菜单存档选择界面

**文件**: `Assets/_Game/UI/MainMenuUI.cs`  
**场景**: `Assets/Scenes/MainMenu.scene`

### 11.1 架构

- 单例 (`DontDestroyOnLoad`)，场景切换时保持
- 支持 pre-build（从 `PreconfigureUI.BuildMainMenuUI()` 预构建的 `MainMenu_Canvas/Panel/SaveSlots` 查找）或运行时兜底（UGUIBuilder 动态创建）
- 5 个存档槽位，每个槽位根据状态显示：
  - **有存档（存活）**: 信息 + "继续"按钮 + "删除"按钮
  - **有存档（死亡）**: 死亡信息（红色☠）+ "删除"按钮
  - **空槽位**: "空槽位" + "新游戏"按钮

### 11.2 操作流程

```
NewGame(slot) → 设置 _pendingSlot / _pendingLoad=false → 加载 MainScene
LoadGame(slot) → 设置 _pendingSlot / _pendingLoad=true → 加载 MainScene
  ↓ OnGameSceneLoaded 回调
  ↓ SaveLoadManager.NewGame(slot) 或 SaveLoadManager.LoadGame(slot)
  ↓ Destroy(MainMenuUI) — 进入游戏后销毁主菜单
```

### 11.3 与 SaveLoadManager 接口

`SaveLoadManager` 新增两个临时字段（不序列化）:

```csharp
[System.NonSerialized] public int _pendingSlot = -1;   // 待使用的存档槽
[System.NonSerialized] public bool _pendingLoad;        // true=加载, false=新游戏
```

`MainMenuUI` 在场景加载前设置这些字段，`SaveLoadManager` 在 `Start()` 中检查并执行对应操作。

---

## 十二、各 HUD UI 脚本职责速查

| 脚本 | Canvas | SortOrder | 位置 | 显示内容 |
|------|--------|-----------|------|---------|
| `CrosshairUI` | CrosshairCanvas | 999 | 跟随鼠标 | 红色空心圆环准星，瞄准时变亮 |
| `SurvivalHUD` | SurvivalHUD_Canvas | 100 | 右上角 | 生命/饥饿/口渴/体温 4 条 Slider |
| `QuickItemBar` | QuickItemBar_Canvas | 90 | 底部居中 | 10 格快捷栏，滚轮翻页，数字键选择，右键使用 |
| `DecibelHUD` | DecibelHUD_Canvas | 50 | 左上角 | 噪声等级（安静→震耳欲聋，带颜色编码） |
| `WeatherHUD` | WeatherHUD_Canvas | 50 | 左上角 | 天气类型+雨强，环境温度 |
| `TopLeftHUD` | TopLeftHUD_Canvas | 50 | 左上角 | 时间/天数（步行），车速/油量（驾驶），武器名/HP（AI操控） |
| `MainMenuUI` | MainMenu_Canvas | 300 | 全屏 | 5 个存档槽位 + 退出按钮 |

### 12.1 建造菜单联动

`SurvivalHUD` 和 `QuickItemBar` 在 `Update()` 中检查 `BuildMenuUI.IsVisible`：
- 建造菜单打开时 → 隐藏自身 Canvas
- 建造菜单关闭时 → 恢复显示

避免 UI 重叠和输入冲突。

### 12.2 车辆/驾驶联动

`DecibelHUD`, `WeatherHUD`, `TopLeftHUD` 订阅 `VehicleEnteredEvent` / `VehicleExitedEvent`：
- 驾驶时调整文本 Y 偏移（为车速显示腾空间）
- `TopLeftHUD` 在驾驶时显示车速/油量/助推状态，替代时间显示

### 12.3 AI 驾驶联动

`TopLeftHUD` 订阅 `AIBotPilotEnteredEvent` / `AIBotPilotExitedEvent`：
- AI 操控时显示当前武器名和 HP
- 切换回手动操控时清除

---

## 十三、文件清单

| 文件路径 | 角色 |
|---------|------|
| `Assets/_Game/Core/UIModeConfig.cs` | UGUI 永驻开关 |
| `Assets/_Game/Core/ServiceLocator.cs` | 全局服务定位器 |
| `Assets/_Game/Core/PlayerRegistry.cs` | 玩家引用注册表 |
| `Assets/_Game/Core/GameBootstrap.cs` | 运行时兜底自动恢复 |
| `Assets/_Game/UI/UGUIBuilder.cs` | UI 动态创建静态工厂 |
| `Assets/_Game/UI/SurvivalHUD.cs` | 生存数值 HUD（右上角 4 条） |
| `Assets/_Game/UI/QuickItemBar.cs` | 快捷物品栏（底部 10 格+翻页） |
| `Assets/_Game/UI/CrosshairUI.cs` | 红色准星（跟随鼠标） |
| `Assets/_Game/UI/DecibelHUD.cs` | 噪声等级显示（左上角） |
| `Assets/_Game/UI/WeatherHUD.cs` | 天气+温度显示（左上角） |
| `Assets/_Game/UI/TopLeftHUD.cs` | 时间/车速/AI 上下文（左上角） |
| `Assets/_Game/UI/MainMenuUI.cs` | 主菜单存档选择 |
| `Assets/_Game/Editor/PreconfigurePlayer.cs` | Editor: 一键预配置 30+ 组件 |
| `Assets/_Game/Editor/PreconfigureUI.cs` | Editor: 一键预构建 6 个 HUD + 清理孤立 UI |

---

## 十四、设计决策记录

1. **UGUI 永久化**: 删除双模式切换（UGUI/IMGUI Toggle + F7），`UIModeConfig.UseUGUI` 硬编码为 `true`。理由：两套渲染代码维护成本高，IMGUI 不支持 FSR 超分辨率。

2. **pre-build 优先 + 运行时兜底**: 每处 UI 都是 `FindExisting → return` 模式，而非纯动态创建。理由：Editor 中可预览、可调试；运行时场景损坏也能自愈。

3. **Canvas 挂在 Player 下**: 所有 HUD Canvas 是 Player 的子对象。理由：随 Player 生命周期自然销毁/重建，无需手动管理；场景切换时自动清理。

4. **不挂 GraphicRaycaster 除非需要点击**: `SurvivalHUD`, `DecibelHUD`, `WeatherHUD`, `TopLeftHUD` 的 Canvas 不带 Raycaster；只有 `QuickItemBar` 需要（且设 `blockingObjects=None`）。

5. **ServiceLocator.Replace 而非 Add**: 重复注册时用新实例覆盖旧实例。理由：场景重载/热重载时避免持有已销毁的 Unity Object 引用。

6. **Animator 放在模型子对象上**: Player 根节点不挂 Animator，避免与子对象上的 Animator 冲突。`PreconfigurePlayer` 和 `GameBootstrap` 均检查 `GetComponentInChildren<Animator>()`。
