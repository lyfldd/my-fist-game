# AI机器人系统设计文档

> 状态：已实现 | 最后更新：2026-06-22

---

## 一、系统概览

AI机器人是玩家的可建造随从单位，具备自主AI（跟随/驻守/巡逻状态机）、多武器战斗、双能量系统、能量盾、独立背包、核反应堆充电、太阳能充电、驾驶模式等完整功能。

### 文件清单（9个文件，实际代码约3500行）

| 文件 | 职责 | 行数 |
|------|------|------|
| `_Game/Systems/AIBot/AIBot.cs` | 主控制器：状态机、双能量、血量、护盾、太阳能/核充电、防自杀、存档/读档 | ~1150 |
| `_Game/Systems/AIBot/AIBotCombat.cs` | 战斗系统：激光+右臂4种+左臂3种、弹药管理(4×4=16格)、ThreatSystem集成、手动开火 | ~800 |
| `_Game/Systems/AIBot/AIBotBuildable.cs` | 建造物组件：继承PlacedStructure，E键交互打开面板，拆除返还逻辑 | ~80 |
| `_Game/Systems/AIBot/AIBotInventory.cs` | 独立背包：6×5=30格、200kg负重、弹药消耗回调 | ~230 |
| `_Game/Systems/AIBot/AIBotInventoryUI.cs` | 背包UI窗口（UGUI ScreenSpaceOverlay，可拖动） | ~320 |
| `_Game/Systems/AIBot/AIBotUI.cs` | 主控制面板（UGUI）：血量/护盾/能量/武器/指令/驾驶/修理/加燃料等全部功能 | ~1000 |
| `_Game/Systems/AIBot/AIBotPilot.cs` | 驾驶控制器：WASD移动、Q切武器、鼠标瞄准、左键开火、E开面板 | ~280 |
| `_Game/Systems/AIBot/AIBotPilotInputLock.cs` | 驾驶输入锁定：挂载玩家身上，进入驾驶时禁用玩家组件+隐藏模型+无敌 | ~140 |
| `_Game/Systems/SaveLoad/Data/AIBotSaveData.cs` | 存档数据结构：位置/血量/能量/武器/聚变核心/背包全量持久化 | ~75 |

### 组件依赖关系图

```
GameObject (AI机器人)
├── NavMeshAgent (RequireComponent)
├── FactionComponent (RequireComponent, FactionType.AIBot)
├── PersistentGUID (存档用)
├── AIBot (主控制器, IDamageable)
│   └── 自动挂载 AIBotPilot (Awake中AddComponent)
├── AIBotCombat (战斗, RequireComponent(typeof(AIBot)))
├── AIBotBuildable (建造物, RequireComponent(typeof(AIBot)), 继承PlacedStructure)
├── AIBotInventory (独立背包)
└── AIBotPilot (驾驶, RequireComponent(typeof(AIBot), typeof(NavMeshAgent)))

玩家GameObject
└── AIBotPilotInputLock (驾驶输入锁定)
```

---

## 二、AIBot — 主控制器

### 2.1 状态机（AIBotCommand 枚举）

| 指令 | 行为 |
|------|------|
| `Follow` | 跟随玩家，保持 `followDistance`（默认3m，范围1-10m）。无法到达时：PathPartial等5s后传送（冷却30s）；掉出NavMesh立即传送；卡住3s重置路径，5s传送。到达玩家身边后自动清除周围僵尸。 |
| `Guard` | 驻守当前位置（`guardPosition`）。检测范围内僵尸自动攻击。超距 `guardAutoRecallDistance`（默认100m）自动切回Follow。 |
| `Patrol` | 在 `patrolRadius`（默认30m，范围10-80m）内随机巡逻。每2s计算新巡逻点（随机角度+半径），到达后暂停3-5s警戒。支持围绕玩家（`patrolAroundPlayer=true`）或围绕固定坐标（`patrolCenterPoint`）。超距 `patrolAutoRecallDistance`（默认200m）自动切回。 |
| `Pilot` | 驾驶模式。Agent停止，由玩家WASD控制移动。 |

指令切换：`SetCommand(cmd)` 重置Agent状态、设置stoppingDistance。`CycleCommand()` 在 Follow→Guard→Patrol 之间循环（不含Pilot）。

### 2.2 双能量系统

| 能量类型 | 字段 | 容量 | 用途 |
|----------|------|------|------|
| 电池（Battery） | `batteryCurrent` / `batteryMax` | 200 | 节能/电力模式的主能源。白天太阳能充电（晴天120/h，阴天60/h）。 |
| 铀燃料（Uranium） | `uraniumCurrent` / `uraniumMax` | 200 | 铀/爆发模式的主能源。由微型核反应堆（3×3槽位）被动产出。 |

- 双能量自动切换：电池耗尽→切铀模式；铀耗尽→切电力模式。
- 两者均耗尽 → `IsShutdown` = true，机器人完全停机。
- 补充方法：
  - `AddBatteryEnergy(float amount)` — 每5个电池组+200。
  - `AddUraniumFuel()` — 1个浓缩铀直接充满（200）。
  - `ConsumeEnergyForAction(float amount)` — 战斗动作消耗，自动按模式倍率调整并处理切换。

### 2.3 四能量模式（EnergyMode 枚举）

| 模式 | 速度倍率 | 消耗倍率 | 冷却倍率 | 激光 | AI辅助 | AI接管 | 护盾 | 消耗能源 |
|------|----------|----------|----------|------|--------|--------|------|----------|
| `EnergySaving`（♻） | 0.75× | 0.5× | 0.5× | ❌ | ❌ | ❌ | ❌ | 电池 |
| `Electric`（●） | 1.0× | 1.0× | 1.0× | ✅ | ✅ | ✅ | ❌ | 电池 |
| `Uranium`（☢） | 1.25× | 1.0× | 2.0× | ✅增强* | ✅ | ✅ | ❌ | 铀 |
| `Burst`（💥） | 1.5× | 1.25× | 2.5× | ✅增强** | ✅ | ✅ | ✅可用 | 铀 |

\* 铀模式激光增强：射程22.5m（+50%），伤害225（+50%）
\** 爆发模式激光增强：射程28.125m（+87.5%），伤害281.25（+87.5%）

模式切换逻辑：
- `SetEnergyMode(mode)` — 重置所有开关（eco/burst/shield），按模式+能量状态设置。
- `ToggleEcoMode()` — 开关式切换节能，自动处理可用性回退。
- `ToggleBurstMode()` — 开关式切换爆发，自动处理可用性回退。
- 每帧 `CheckEnergyFallback()` 检查节能/爆发开关与当前模式的一致性。

### 2.4 能量消耗速率

**活动消耗（/h）：**

| 指令 | 常量 | 消耗速率 |
|------|------|----------|
| Follow | `ENERGY_FOLLOW_PER_H` | 6/h |
| Guard | `ENERGY_GUARD_PER_H` | 3/h |
| Patrol | `ENERGY_PATROL_PER_H` | 10/h |

**移动消耗：** `MOVE_ENERGY_PER_H = 5/h`（仅移动时计费）

**武器消耗（每次）：**

| 武器 | 常量 | 消耗 |
|------|------|------|
| 激光 | `ENERGY_LASER_PER_SHOT` | 2/发 |
| 右臂远程 | `ENERGY_RIGHTARM_PER_SHOT` | 1/发 |
| 左臂近战 | `ENERGY_LEFTARM_PER_SWING` | 0.5/次 |

- 所有消耗乘以 `ConsumptionMultiplier`。
- `ConsumeEnergy()` 每帧调用（包括驾驶时），计算 `(activityRate + moveRate) * ConsumptionMultiplier / 3600 * dt`。

### 2.5 能量盾（Burst模式专属）

```
启动消耗: 30铀
启动时间: 5s (shieldStartupTimer倒计时)
护盾HP上限: 500
满盾维持: 0.1铀/s
未满维持+回复: 0.5铀/s + 5盾HP/s
```

- `ActivateShield()` — 检查可用性（爆发模式、未开启、铀≥30），扣费+启动计时。
- `DeactivateShield()` — 立即关闭，重置启动计时。
- `UpdateShield()` — 每帧调用（驾驶中也生效），满盾低功耗维持，未满消耗+回复。
- 伤害吸收优先级：护盾 > 血量（`TakeDamage` 中先扣护盾）。

### 2.6 血量与防自杀

- 最大HP：500。
- 实现 `IDamageable` 接口。
- **低血量撤退**（`CheckLowHealthRetreat()`，非驾驶时每帧调用）：
  - HP < 30%（`IsLowHP`）：强制切Follow。
  - HP < 10%（`IsCriticalHP`）：强制切Follow + followDistance缩至1m。
- **爆发伤害撤退**（`OnBurstDamage(damage)`）：单次受伤超过 maxHP×30% → 立即切Follow+贴紧。
- 死亡（`OnDeath()`）：设为dead、发布`EntityDeathEvent`通知ThreatSystem、强制退出驾驶、禁用Agent、发布`AIBotDestroyedEvent`。
- 修理：`RepairHP(amount)`，通过UI从玩家背包消耗"高级零件"（+50HP）或"铁锭"（+25HP）。

### 2.7 太阳能充电

```
充电条件: 白天(6:00-18:00) + 天气允许 + 电池未满 + 非停机
天气倍率: Sunny=1.0×, Cloudy=0.5×, Rain/Storm=不充电
基础速率: solarRechargeRate = 120/h
仅充电池, 驾驶中也生效
```

- 通过 `TimeManager.CurrentHour` 判断时间。
- 通过 `WeatherManager.CurrentWeather` 判断天气类型。
- 状态属性：`CurrentSolarRate`（当前充电速率/h，0=休眠）、`IsSolarActive`。

### 2.8 微型核反应堆

```
槽位: 3×3 = 9个 FusionCoreSlot
仅在铀/爆发模式下工作（UsesUranium=false时不充电）
全天候充电（不受时间/天气影响）

聚变核心(小):
  燃耗时: 4h
  铀产出: 30/h
  
聚变核心(大):
  燃耗时: 12h
  铀产出: 45/h
```

- 每帧 `NuclearRecharge()`：遍历9个槽位，累加输出速率，扣减燃耗时间，燃尽槽位清零。
- 通过UI面板的"加燃料"按钮，从玩家背包装载聚变核心。
- 状态属性：`CurrentNuclearRate`（当前充电速率/h）、`IsNuclearActive`。

### 2.9 传送机制

- 冷却时间：30s（`TELEPORT_COOLDOWN`）。
- 触发条件：
  - 掉出NavMesh → 立即传送。
  - PathPartial持续5s → 传送。
  - 卡住5s（移动<0.1m + velocity<0.05m/s）→ 传送。
- `TeleportToPlayer()`：在玩家前方 `followDistance` 处采样NavMesh位置 → `Agent.Warp()`。

---

## 三、AIBotCombat — 战斗系统

### 3.1 武器系统总览

**内置激光（始终可用，节能模式除外）：**

| 模式 | 伤害 | 射程 | 冷却 |
|------|------|------|------|
| 电力/节能 | 150 | 15m | 1s |
| 铀模式 | 225 | 22.5m | 1s |
| 爆发模式 | 281.25 | 28.125m | 1s |

**右臂武器（4种，挂载式，需消耗对应弹药）：**

| 武器 | 伤害 | 射程 | 冷却 | 弹药 | 特殊 |
|------|------|------|------|------|------|
| 手枪 | 20 | 12m | 0.5s | 手枪子弹 | 单发 |
| 步枪 | 30 | 18m | 0.75s | 步枪子弹 | 单发 |
| 霰弹枪 | 25×3 | 8m | 1s | 霰弹 | 前方90°锥形散射，最多3目标 |
| 电磁步枪 | 40 | 20m | 1.5s | 电池组 | 穿透一排（RaycastAll） |

**左臂武器（3种）：**

| 武器 | 效果 | 射程 | 机制 |
|------|------|------|------|
| 盾牌 | 受伤-30%（被动`DamageMultiplier`） | - | 被动持续生效 |
| 电锯 | 15/秒AOE | 3m | 每秒Tick，`OverlapSphereNonAlloc`范围内所有僵尸 |
| 短刀 | 20 | 2m | 0.75s冷却，单发Tick |

### 3.2 武器装卸

```csharp
EquipRightArm(RightArmWeapon weapon) / UnequipRightArm()
EquipLeftArm(LeftArmWeapon weapon) / UnequipLeftArm()
```

武器名与物品名互转：
- `GetWeaponItemName(RightArmWeapon)` / `GetRightArmFromItemName(string)`
- `GetWeaponItemName(LeftArmWeapon)` / `GetLeftArmFromItemName(string)`
- `GetAmmoNameForWeapon(RightArmWeapon)` / `GetAmmoDisplayName(string)`

### 3.3 攻击优先级

三个攻击槽（`slot1` / `slot2` / `slot3`），每个可分配 `Laser` / `RightArm` / `LeftArm`。

- 高优先级武器分配到更近的僵尸。
- `CyclePriority()` 循环切换：slot1→slot2, slot2→slot3, slot3→slot1。
- 优先级决定多目标分配，各武器独立冷却不互等。
- 节能模式下AI辅助/AI接管均禁用（`IsAIAssistEnabled = false`）。

### 3.4 警觉距离与目标选择

- 默认 `alertRange = 15m`（范围 3m-30m）。
- 每0.5s（`COMBAT_TICK`）执行 `TickCombat()`：
  1. `Physics.OverlapSphereNonAlloc` 检测 `zombieLayer` 内所有碰撞体。
  2. 过滤：只保留有 `DamageableZombie` 且未死亡的目标。
  3. 排序：在玩家身边时按距玩家距离；否则按距机器人距离。
  4. ThreatSystem优先：如果 ThreatSystem 有最高威胁目标，交换到最前面。
  5. 优先级分配：按 slot1→slot2→slot3 顺序把最近僵尸分配给高优先级武器。
  6. 各武器独立开火（检查冷却 + 可用性 + 弹药）。

### 3.5 弹药系统（独立于背包）

- 内置弹药存储：4×4=16格（`AMMO_SLOT_COUNT=16`），每格上限999（`AMMO_MAX_PER_SLOT=999`）。
- `LoadAmmo(ammoName, count)` — 优先堆叠已有同类槽，再开空槽。
- `UnloadAmmo(ammoName, count)` — 从各槽扣减，空槽清零。
- `GetAmmoCount(ammoName)` — 统计某弹药总数。
- `ConsumeAmmoInternal(ammoName, count)` — 战斗内消耗。
- `AmmoConsumeCallback` 委托：可由 `AIBotInventory` 注册以从机器人背包消耗弹药。
- 弹药物品名常量：`AMMO_PISTOL="手枪子弹"`, `AMMO_RIFLE="步枪子弹"`, `AMMO_SHOTGUN="霰弹"`, `AMMO_EM_RIFLE="电池组"`。

### 3.6 驾驶模式手动开火

- `ManualFireLaser()` — 扫描激光射程内最近僵尸，自动锁敌发射。
- `ManualFireAimed(Vector3 aimPosition)` — Raycast从机器人位置(offset +0.5y)射向瞄准点。
  - 右臂模式：检查弹药→消耗→Raycast→命中僵尸。
  - 左臂模式：Raycast近战，短刀/电锯各自伤害。
- 手动开火不依赖0.5s Tick，即时响应左键。

---

## 四、AIBotInventory — 独立背包

### 4.1 规格

| 参数 | 值 |
|------|-----|
| 格数 | 6列 × 5行 = 30格 |
| 负重上限 | 200kg |
| 超重策略 | 软限制（允许超重，UI提示） |

### 4.2 核心API

```csharp
bool AddItem(ItemData item, int count)        // 优先堆叠→开新格
bool RemoveItem(ItemData item, int count)      // 从多格依次扣减
bool HasItem(ItemData item, int count=1)       // 检测拥有
int GetItemCount(ItemData item)                // 按引用统计
int GetItemCountByName(string itemName)        // 按名称统计
bool ConsumeItemByName(string itemName, int count=1)  // 按名称消耗（弹药回调用）
bool CanAddItem(ItemData item, int count=1)    // 负重预检查
List<AIBotInventorySlot> GetAllSlots()         // 全量快照
AIBotInventorySlot GetSlot(int index)          // 单格访问
```

### 4.3 数据类

```csharp
[System.Serializable]
public class AIBotInventorySlot
{
    public ItemData itemData;
    public int count;
}
```

### 4.4 事件

- `OnInventoryChanged` 委托：物品变动时触发，供UI刷新监听。
- `AmmoConsumeCallback` 注册到 `AIBotCombat`，使右臂武器弹药可从机器人背包消耗。

---

## 五、AIBotBuildable — 建造集成

- 继承 `PlacedStructure`，兼容建造系统。
- 覆盖交互逻辑：
  - `OnInteract(GameObject interactor)` — E键切换 `AIBotUI` 面板（而非拆除）。
  - `InteractionPrompt` — 根据是否死亡显示"管理 AI机器人"或"报废的AI机器人"。
  - `InteractionTime = 0f` — 瞬间打开面板。
- `CustomDeconstruct()` — 拆除时按 `deconstructReturnRate` 返还材料。
- 属性透传：`Bot`（`AIBot`引用）、`HasBot`。

---

## 六、驾驶系统（Pilot）

### 6.1 AIBotPilot（挂载机器人GameObject上）

**进入/退出：**
- `EnterPilot(GameObject pilot)` — 检查非驾驶中、非死亡 → 设 `IsPiloting=true`，`bot.IsPiloted=true`，保存 `_previousCommand`，切 `Pilot` 指令并 Stop Agent。发布 `AIBotPilotEnteredEvent`。
- `ExitPilot()` — 恢复 `_previousCommand`（死亡时不恢复），关闭AI武器接管，发布 `AIBotPilotExitedEvent`。
- 驾驶中自动退出条件：机器人死亡/停机、pilot为null、距离超过 `maxPilotDistance=100m`。

**操控（Update中，仅驾驶时）：**
- **WASD**：相对摄像机方向移动。速度 = `pilotBaseSpeed(8) × SpeedMultiplier × speedSliderValue`。使用 `Agent.Move()`（绕过寻路）。
- **Q**：`CycleManualWeapon()` 循环手动武器。节能模式跳过激光（只在右臂↔左臂间切换）。
- **鼠标**：非激光模式下计算地面射线交点作为瞄准点（`_aimPosition`），机器人朝向瞄准点。
- **左键**：`ManualFire()` — 激光自动锁敌 / 右臂Raycast / 左臂Raycast。
- **E**：`ToggleAIPanel()` 打开/关闭主面板。

### 6.2 AIBotPilotInputLock（挂载玩家GameObject上）

监听 `AIBotPilotEnteredEvent` / `AIBotPilotExitedEvent`，判断 `evt.Pilot == gameObject`。

**进入驾驶时禁用：**
- PlayerController, WeaponAiming, WeaponShooting, WeaponSwitcher, PlayerCombat, PlayerInteraction
- 隐藏所有 Renderer（玩家模型不可见）
- 禁用 Collider，Rigidbody 设为 Kinematic
- 玩家无敌（`DamageablePlayer.Invincible = true`）

**退出驾驶时全部恢复。**

---

## 七、UI系统

### 7.1 AIBotUI — 主控制面板

- UGUI，`ScreenSpaceOverlay`，sortingOrder=250。
- 单例模式（`static AIBotUI _instance`）。
- `Show(AIBot bot)` / `Hide()` 静态接口。
- 可拖动标题栏，点击面板外关闭（含子面板判断）。
- 30f间隔刷新（`_lastRefreshFrame`比较），`MarkDirty()` 强制刷新。

**面板布局（VerticalLayoutGroup + ScrollRect）：**

| 区域 | 控件 |
|------|------|
| HP | 彩色进度条 + "500/500"文本。重伤时显示"⚠ 重伤！"警告。 |
| 能量盾 | 蓝色进度条 + 状态文本。未开启显示"开启能量盾 (30铀)"按钮；已开启显示"关闭能量盾"按钮。启动中显示倒计时。 |
| 能量模式 | 当前模式标签（带颜色图标）+ 消耗/速度/冷却倍率文本。 |
| 电池 | 蓝色进度条 + "当前/最大"文本 + 太阳能充电状态。 |
| 铀燃料 | 橙色进度条 + "当前/最大"文本 + 核反应堆充电状态。 |
| 模式切换 | 节能按钮（绿/灰）+ 爆发按钮（红/灰）。节能中显示警告"激光/AI辅助/AI接管已禁用"。 |
| 移动速度 | 滑块（0.25-1.0）+ 实际速度文本。 |
| 警觉距离 | 滑块（3-30m）+ 数值文本。 |
| 攻击优先级 | 显示顺序 "[激光]→[右臂]→[左臂]" + "切换"按钮。 |
| 武器 | 右臂显示（装备名+⚙子面板）+ 左臂显示（装备名+⚙子面板）。 |
| 指令 | 跟随/驻守/巡逻按钮（当前指令高亮绿色）+ 各⚙子面板。 |
| 动作 | 打开背包 / 加燃料 / 修理 / 驾驶(进入/退出) / AI接管(开/关)。 |

**子面板（ToggleSubPanel）：**
- 跟随设置（距离滑块1-10m）
- 驻守设置（超距解除开关+距离滑块）
- 巡逻设置（围绕玩家/指定地点切换+半径滑块+超距解除）
- 右臂武器选择（手枪/步枪/霰弹枪/电磁步枪 + 装弹/卸弹）
- 左臂武器选择（盾牌/电锯/短刀）
- 反应堆管理（9槽位 + 装载核心 + 取出核心）

**加燃料逻辑：**
- 扫描玩家背包，优先消耗"电池组"（每5个+200电池），其次"浓缩铀"（1个直接充满）。

**修理逻辑：**
- 扫描玩家背包，优先消耗"高级零件"（+50HP），其次"铁锭"（+25HP）。

### 7.2 AIBotInventoryUI — 背包窗口

- UGUI，`ScreenSpaceOverlay`，sortingOrder=300。
- 单例模式，可拖动标题栏，点击窗口外关闭。
- 30f间隔刷新（`MarkDirty()` 强制立即刷新）。

**布局：**
- 标题栏 "AI机器人 背包"
- 负重文本 "负重: XX.Xkg / 200kg"
- 彩色负重条（绿<50%→黄<80%→红）
- 分隔线
- ScrollRect 内含 6×5 网格（48px + 4px gap）
- 物品格显示名称（截断4字）+ 数量。hover显示完整tooltip（名称+重量×数量）。
- 关闭按钮

---

## 八、存档系统

### 8.1 数据结构（AIBotSaveData）

实现 `ICloneable` 接口。

| 分类 | 字段 | 类型 |
|------|------|------|
| 标识 | `guid` | string（PersistentGUID） |
| 标识 | `buildableName` | string（BuildableData.displayName） |
| 位置 | `posX, posY, posZ` | float |
| 旋转 | `rotY` | float（eulerAngles.y） |
| 血量 | `hp` | float |
| 能量 | `battery, batteryMax, uranium, uraniumMax` | float |
| 模式 | `command` | string（枚举ToString） |
| 模式 | `energyMode` | string（枚举ToString） |
| 模式 | `ecoModeEnabled, burstModeEnabled` | bool |
| 护盾 | `shieldCurrentHP, shieldMaxHP, shieldStartupTimer` | float |
| 护盾 | `shieldActive` | bool |
| 跟随 | `followDistance, speedSliderValue` | float |
| 驻守 | `guardX, guardY, guardZ` | float |
| 驻守 | `hasGuardPosition` | bool |
| 巡逻 | `patrolRadius` | float |
| 巡逻 | `patrolAutoRecallEnabled, patrolAroundPlayer` | bool |
| 巡逻 | `patrolCenterX, patrolCenterY, patrolCenterZ` | float |
| 武器 | `rightArm, leftArm` | string（枚举ToString） |
| 核心 | `fusionCores` | List\<FusionCoreSaveData\> |
| 背包 | `inventorySlots` | List\<SlotSaveData\> |

### 8.2 FusionCoreSaveData

| 字段 | 类型 | 说明 |
|------|------|------|
| `itemName` | string | "聚变核心(小)" 或 "聚变核心(大)" |
| `burnTime` | float | 总燃耗时(h) |
| `burnRemaining` | float | 剩余燃耗时(h) |
| `outputRate` | float | 铀产出/h |

### 8.3 存档/读档流程

**存档：** `AIBot.GetSaveData()`
1. 获取 `PersistentGUID` → guid。
2. 获取 `AIBotBuildable` → buildableName。
3. 收集位置/旋转/血量/能量/模式/护盾/跟随/驻守/巡逻参数。
4. 收集武器（`AIBotCombat.CurrentRightArm.ToString()` / `CurrentLeftArm.ToString()`）。
5. 收集融合核心（遍历 `reactorSlots`，非空槽位转为 `FusionCoreSaveData`）。
6. 收集背包（`AIBotInventory.GetAllSlots()` → `SlotSaveData` 列表）。

**读档：** `AIBot.RestoreFromSave(AIBotSaveData, ItemCatalog)`
1. 恢复血量/能量/护盾/模式/开关。
2. `System.Enum.TryParse` 解析 command + energyMode 枚举。
3. `SetCommand(cmd)` 恢复指令 + 跟随/驻守/巡逻参数。
4. `System.Enum.TryParse` 解析武器枚举 → `EquipRightArm(raw)` / `EquipLeftArm(law)`。
5. 恢复融合核心（逐槽复制数据）。
6. 恢复背包（通过 `ItemCatalog.Find(itemName)` 解析物品引用，逐格填充）。
7. 同步 NavMeshAgent 速度。
8. 设置 `_restoredFromSave = true` 标记（防止 Start 覆盖 guardPosition）。

---

## 九、事件系统

| 事件 | 触发时机 | 数据 |
|------|----------|------|
| `AIBotDestroyedEvent` | 机器人死亡 | `Vector3 position` |
| `AIBotPilotEnteredEvent` | 玩家进入驾驶 | `GameObject bot, GameObject pilot` |
| `AIBotPilotExitedEvent` | 玩家退出驾驶 | `GameObject bot, GameObject pilot` |
| `EntityDeathEvent` | 死亡通知ThreatSystem | `int instanceID` |
| `ThreatReportEvent` | 每次攻击产生威胁值 | `int attackerId, int targetId, float damage` |

---

## 十、关键常量速查表

### AIBot 常量

| 常量 | 值 | 说明 |
|------|-----|------|
| `maxHP` | 500 | 最大血量 |
| `batteryMax` | 200 | 电池容量 |
| `uraniumMax` | 200 | 铀容量 |
| `solarRechargeRate` | 120/h | 太阳能充电速率 |
| `SHIELD_STARTUP_COST` | 30 | 启动护盾铀消耗 |
| `SHIELD_STARTUP_TIME` | 5s | 护盾启动时间 |
| `SHIELD_MAINTENANCE_FULL` | 0.1铀/s | 满盾维持 |
| `SHIELD_MAINTENANCE_RECHARGE` | 0.5铀/s | 未满维持 |
| `SHIELD_REGEN_PER_SEC` | 5/s | 护盾回复速度 |
| `SHIELD_MAX_HP` | 500 | 护盾HP上限 |
| `TELEPORT_COOLDOWN` | 30s | 传送冷却 |
| `MOVE_ENERGY_PER_H` | 5/h | 移动能耗 |
| `ENERGY_GUARD_PER_H` | 3/h | 驻守能耗 |
| `ENERGY_FOLLOW_PER_H` | 6/h | 跟随能耗 |
| `ENERGY_PATROL_PER_H` | 10/h | 巡逻能耗 |
| `ENERGY_LASER_PER_SHOT` | 2 | 激光每发 |
| `ENERGY_RIGHTARM_PER_SHOT` | 1 | 右臂每发 |
| `ENERGY_LEFTARM_PER_SWING` | 0.5 | 左臂每次 |
| `moveSpeed` | 5 | 基础移动速度 |
| `pilotBaseSpeed` | 8 | 驾驶基础速度 |
| `followDistance` | 3 (1-10) | 默认跟随距离 |
| `guardChaseRange` | 8 | 驻守追击范围 |
| `guardAutoRecallDistance` | 100 | 驻守超距解除 |
| `patrolRadius` | 30 (10-80) | 巡逻半径 |
| `patrolAutoRecallDistance` | 200 | 巡逻超距解除 |
| `STUCK_RESET_TIME` | 3s | 卡住重置间隔 |
| `STUCK_TELEPORT_TIME` | 5s | 卡住传送间隔 |

### AIBotCombat 常量

| 常量 | 值 | 说明 |
|------|-----|------|
| `COMBAT_TICK` | 0.5s | 战斗检测间隔 |
| `AMMO_SLOT_COUNT` | 16 | 弹药槽数(4×4) |
| `AMMO_MAX_PER_SLOT` | 999 | 单格弹药上限 |
| `alertRange` | 15 (3-30) | 默认警觉范围 |
| `laserDamageBattery` | 150 | 激光伤害(电池) |
| `laserDamageUranium` | 225 | 激光伤害(铀) |
| `laserRangeBattery` | 15m | 激光射程(电池) |
| `laserRangeUranium` | 22.5m | 激光射程(铀) |
| `laserCooldown` | 1s | 激光冷却 |
| `pistolDamage` | 20 | 手枪伤害 |
| `rifleDamage` | 30 | 步枪伤害 |
| `shotgunDamage` | 25 | 霰弹每目标伤害 |
| `shotgunTargets` | 3 | 霰弹最大目标数 |
| `emRifleDamage` | 40 | 电磁步枪伤害 |
| `chainsawDamagePerSec` | 15/s | 电锯持续伤害 |
| `knifeDamage` | 20 | 短刀伤害 |
| `shieldReducePercent` | 0.3 | 盾牌减伤比例 |

### AIBotInventory 常量

| 常量 | 值 | 说明 |
|------|-----|------|
| `GRID_WIDTH` | 6 | 列数 |
| `GRID_HEIGHT` | 5 | 行数 |
| `TOTAL_SLOTS` | 30 | 总格数 |
| `MAX_WEIGHT` | 200kg | 负重上限 |

### AIBotPilot 常量

| 常量 | 值 | 说明 |
|------|-----|------|
| `maxPilotDistance` | 100m | 驾驶最大距离 |
| `pilotBaseSpeed` | 8 | 驾驶基础速度 |

### 聚变核心

| 类型 | 燃耗时 | 铀产出 |
|------|--------|--------|
| 聚变核心(小) | 4h | 30/h |
| 聚变核心(大) | 12h | 45/h |
| 反应堆槽位 | 9 (3×3) | — |
