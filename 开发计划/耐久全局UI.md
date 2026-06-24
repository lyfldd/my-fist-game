# 🖥️ 耐久全局 UI — 设计文档

> **日期**: 2026-06-23 · **状态**: 设计定稿 · **依赖**: 耐久 v1.0 + 前置 H/G/F
> 🔍 迭代追踪 | 第1轮: ✅ (6项指导意见) | 第2轮: ⬜ | 第3轮: ⬜ | 最后审查: 2026-06-24
>
> **2026-06-24 第1轮审查指导意见**（结合项目代码交叉验证）：
> - **P0 前置条件**：ItemBrokenEvent EquipSlot 硬编码 None bug 必须先修（DurabilitySystem.cs:72），否则 UI 显示与实际脱节（0% 耐久武器还能用）
> - **P1 接口语义**：IDurabilityTarget 混了 6 类含义不同的"耐久"，AIBot.currentHP 是战斗HP不是磨损，应拆分类型枚举
> - **P1 准星复用**：应复用 PlayerInteraction 的检测结果而非独立射线，避免重复检测
> - **P2 刷新机制**：三种实体变化频率差几个数量级，需分开设计（AIBot HP 轮询/设备事件/车辆事件）
> - **P2 地面物品过滤**：90% 地面物品无耐久（堆叠物），只有 hasDurability==true 才显示
> - **P2 前置K状态**：原"✅已实现"标注错误，实际未做，应归入本阶段统一实现

> 设计哲学：玩家不需要记住每个东西的耐久——看就完了。准星对准什么，屏幕上就显示什么。

---

## 一、覆盖范围

| 实体 | 耐久来源 | 类型 | 显示时机 | 显示方式 |
|------|----------|------|----------|----------|
| **建造物**（墙/门/路障/家具/工作台/工业） | `PlacedStructure._currentHealth` | 结构血量 | 准星对准建造物 | 屏幕中上方浮一条血条（ScreenOverlay） |
| **生产设备** | `ProductionDevice._deviceDurability` | 生产磨损 | 打开设备交互面板 | 面板内耐久条 |
| **AIBot 机器人** | `AIBot.currentHP` + 武器耐久 | 战斗HP + 物品磨损 | 打开 AIBot 管理面板 | 面板内血量条 + 武器图标耐久条 |
| **车辆** | `VehicleController.CurrentHealth` | 碰撞血量 | 驾驶中 + 准星对准车辆 | HUD 血量条（已有 TopLeftHUD 扩展） |
| **玩家装备** | `PlacedItem.itemDurability` | 物品磨损 | 始终（装备槽+快捷栏） | ⬜ 本阶段统一实现（原前置K，已归入此处） |
| **地面物品** | `WorldItem.itemDurability` | 物品磨损 | 准星对准地面物品（仅 hasDurability==true） | 物品标签旁小耐久条 |

> **类型说明**：结构血量=被攻击消耗；生产磨损=生产周期消耗；战斗HP=被攻击消耗（AIBot 本体）；物品磨损=使用消耗。UI 显示标签应区分（"血量"/"耐久"/"磨损"）。

---

## 二、实现方式

### 2.1 IDurabilityTarget 接口（拆分类型）

> **第1轮指导意见 P1**：原接口只有 Ratio+Name，6 类实体的"耐久"语义不同，拆出类型枚举供 UI 区分显示。

```csharp
/// <summary> 耐久显示类型 — 决定 UI 标签文案和颜色 </summary>
public enum DurabilityDisplayType
{
    Health,       // 血量（建造物/车辆/AIBot本体）— 红色系
    Durability,   // 耐久（武器/护甲/工具/电子设备）— 绿/黄/红色系
    Wear,         // 磨损（生产设备）— 橙色系
}

public interface IDurabilityTarget
{
    float DurabilityRatio { get; }       // 0~1
    string DisplayName { get; }
    DurabilityDisplayType DisplayType { get; }  // UI 据此选择标签和颜色
}
```

各实体实现的 `DisplayType`：

| 实体 | DisplayType | UI 标签 | 颜色方案 |
|------|------------|---------|---------|
| PlacedStructure | Health | "结构" | 红→黄→绿 |
| ProductionDevice | Wear | "磨损" | 橙→黄→绿 |
| AIBot (本体HP) | Health | "血量" | 红→黄→绿 |
| AIBot 武器 | Durability | "耐久" | 绿→黄→红 |
| VehicleController | Health | "车况" | 红→黄→绿 |
| WorldItem (有耐久) | Durability | "耐久" | 绿→黄→红 |

### 2.2 准星检测复用 PlayerInteraction

> **第1轮指导意见 P1**：避免与 PlayerInteraction 重复射线检测。

**方案**：`PlayerInteraction` 暴露当前 target，`DurabilityHUD` 直接读取。

```csharp
// PlayerInteraction.cs — 新增 public 属性
public GameObject CurrentTarget => _currentTarget;  // 原 private 字段

// DurabilityHUD.cs — 复用而非独立射线
void Update()
{
    var interaction = GetComponent<PlayerInteraction>();
    if (interaction != null && interaction.CurrentTarget != null)
    {
        var dur = interaction.CurrentTarget.GetComponent<IDurabilityTarget>();
        if (dur != null && dur.DurabilityRatio < 1f)  // 满血不显示
        {
            ShowBar(dur.DisplayName, dur.DurabilityRatio, dur.DisplayType);
            return;
        }
    }
    HideBar();
}
```

> 驾驶中（车辆）不经过 PlayerInteraction，单独从 VehicleInteraction 拿当前车辆引用。

### 2.3 地面物品耐久过滤

> **第1轮指导意见 P2**：90% 地面物品是堆叠物（弹药/食物/材料），无耐久。

`WorldItem` 实现接口时加 `hasDurability` 判断：

```csharp
// WorldItem.cs 实现 IDurabilityTarget
public DurabilityDisplayType DisplayType => DurabilityDisplayType.Durability;
public float DurabilityRatio =>
    itemData != null && itemData.hasDurability && itemData.maxDurability > 0f
    ? Mathf.Clamp01(itemDurability / itemData.maxDurability)
    : 1f;  // 无耐久物品返回 1.0，DurabilityHUD 不显示
public string DisplayName => itemData != null ? itemData.itemName : "";
```

DurabilityHUD 中 `if (dur.DurabilityRatio >= 1f) HideBar();` 自动过滤无耐久物品。

### 2.4 面板内耐久条（三种刷新机制分开）

> **第1轮指导意见 P2**：三种实体变化频率差异大，刷新机制分开设计。

| 面板 | 实体 | 变化频率 | 刷新方式 |
|------|------|---------|---------|
| ProductionDeviceUI | 生产设备 | 低频（分钟级） | 事件驱动：订阅 `DeviceBrokenEvent` + 打开面板时读一次 |
| AIBotUI | AIBot 本体HP | 高频（战斗中每帧） | Update 轮询（仅面板打开时） |
| AIBotUI | AIBot 武器耐久 | 中频（每次开火） | 事件驱动：订阅 `DurabilityChangedEvent` |
| TopLeftHUD | 车辆血量 | 极低频（碰撞时） | 事件驱动：订阅 `VehicleDestroyedEvent` + 轮询血量百分比 |

```csharp
// AIBotUI.cs — 面板打开时 Update 轮询 HP
void Update()
{
    if (!gameObject.activeSelf || _bot == null) return;
    hpBar.fillAmount = _bot.HealthPercent;
}
```

```csharp
// ProductionDeviceUI.cs — 事件驱动 + 打开时读一次
void OnEnable()
{
    RefreshDurabilityBar();
    EventBus.Subscribe<DeviceBrokenEvent>(OnDeviceBroken);
}
void OnDeviceBroken(DeviceBrokenEvent evt)
{
    if (evt.Device == _currentDevice) RefreshDurabilityBar();
}
```

### 2.5 玩家装备耐久条（原前置K，归入本阶段）

**装备槽**（InventoryUI）：图标底部叠加 4px 高彩色耐久条（Image filled）：
- 🟢 >70% 🟡 30-70% 🔴 <30% ⚫ 0%（闪烁）

**快捷栏**（QuickItemBar）：每格右下角 3px 耐久条，颜色同上。

**刷新**：订阅 `DurabilityChangedEvent`，只刷新受影响格子（非全量重建）。

```csharp
// InventoryUI.cs / QuickItemBar.cs
void OnEnable() { EventBus.Subscribe<DurabilityChangedEvent>(OnDurabilityChanged); }
void OnDurabilityChanged(DurabilityChangedEvent evt)
{
    var slot = FindSlotByInstanceId(evt.InstanceId);
    if (slot != null)
    {
        slot.durabilityBar.fillAmount = evt.Ratio;
        slot.durabilityBar.color = GetColor(evt.Ratio);
    }
}
```

> **前置条件**：DurabilitySystem 发布 DurabilityChangedEvent 时需携带正确 EquipSlot（见第1轮 P0 bug 修复）。

---

## 三、文件清单

| 文件 | 动作 | 说明 |
|------|:--:|------|
| `Core/IDurabilityTarget.cs` | 新建 | 统一接口 + DurabilityDisplayType 枚举 |
| `Core/DurabilityDisplayType.cs` | 新建 | 枚举（可合并入 IDurabilityTarget.cs） |
| `UI/DurabilityHUD.cs` | 新建 | 准星检测（复用 PlayerInteraction）+ 浮条显示 |
| `Editor/PreconfigureUI.cs` | 改 | 预建 DurabilityHUD_Canvas |
| `Systems/Interaction/PlayerInteraction.cs` | 改 | 暴露 CurrentTarget public 属性 |
| `Systems/World/PlacedStructure.cs` | 改 | 实现 IDurabilityTarget (DisplayType=Health) |
| `Systems/Crafting/ProductionDevice.cs` | 改 | 实现 IDurabilityTarget (DisplayType=Wear) |
| `Systems/AIBot/AIBot.cs` | 改 | 实现 IDurabilityTarget (DisplayType=Health) |
| `Systems/Vehicle/VehicleController.cs` | 改 | 实现 IDurabilityTarget (DisplayType=Health) |
| `Systems/World/WorldItem.cs` | 改 | 实现 IDurabilityTarget (DisplayType=Durability，hasDurability 过滤) |
| `UI/ProductionDeviceUI.cs` | 改 | +耐久条（事件驱动） |
| `UI/AIBotUI.cs` | 改 | +血量条（轮询）+ 武器耐久条（事件） |
| `UI/TopLeftHUD.cs` | 改 | +车辆血量条（事件） |
| `UI/InventoryUI.cs` | 改 | +装备槽耐久条（订阅 DurabilityChangedEvent） |
| `UI/QuickItemBar.cs` | 改 | +快捷栏耐久条（订阅 DurabilityChangedEvent） |

---

## 四、依赖关系与执行时机

```
前置K (玩家装备UI) ⬜ 归入本阶段
       │
       ▼
前置H (建造物) ──┐
前置G (生产设备) ─┼──→ 耐久全局UI (本系统)
前置F (AIBot)  ──┤
前置E (车辆)   ──┘
```

**执行顺序**：

1. **先修 P0 bug** — DurabilitySystem 发布 ItemBrokenEvent 时填正确 EquipSlot（Inventory 加 `GetEquipSlotByInstanceId` 反向查询）。否则 UI 显示 0% 耐久的武器还能用。
2. 前置 H/G/F/E 核心机制完成（已完成 ✅）
3. 本阶段 UI 实施

---

## 五、指导意见落实清单

| # | 意见 | 落实位置 | 状态 |
|---|------|---------|:--:|
| 1 | 前置K 状态标注错误 | §一表格改为"⬜ 本阶段统一实现" + §四依赖图改为"⬜ 归入本阶段" | ✅ |
| 2 | IDurabilityTarget 语义混乱 | §2.1 拆出 DurabilityDisplayType 枚举 + 各实体 DisplayType 对照表 | ✅ |
| 3 | 准星检测复用 PlayerInteraction | §2.2 改为复用 CurrentTarget + PlayerInteraction 暴露属性 | ✅ |
| 4 | 地面物品耐久过滤 | §2.3 WorldItem 实现 Ratio 1.0 过滤 + DurabilityHUD 满血不显示 | ✅ |
| 5 | 三种实体刷新机制分开 | §2.4 对照表（AIBot 轮询/设备事件/车辆事件） | ✅ |
| 6 | P0 bug 前置条件 | §四执行顺序第1步 + §2.5 前置条件说明 | ✅ |

---

## 📜 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| `v1.0` | 2026-06-23 | 设计定稿：IDurabilityTarget 统一接口 + 准星检测 + 面板内耐久条 |
| `v1.1` | 2026-06-24 | 第1轮审查指导意见落实：接口拆类型枚举/准星复用/地面物品过滤/刷新机制分开/前置K归入本阶段/P0 bug前置条件 |
