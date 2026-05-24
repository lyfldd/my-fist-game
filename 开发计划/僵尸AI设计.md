## 4.1 前置初始计划（Phase1 已完成）

> **模块**: L4 组装 · **步骤**: 第2步 · **名称**: 僵尸 AI — 有限状态机 + NavMesh + 集中式玩家感知
> **日期**: 2026-05-20

### 一、总体架构

```
┌─────────────────────────────────────────────────┐
│              ZombieAwarenessSystem               │
│           (单例，集中式玩家检测)                    │
│  每 0.2s 遍历活跃僵尸列表 → 距离判定 → 通知状态机    │
└────────────────────┬────────────────────────────┘
                     │ OnPlayerDetected / OnPlayerLost
     ┌───────────────┼───────────────┐
     ▼               ▼               ▼
  Zombie A       Zombie B       Zombie C
  (Chase)        (Wander)       (Idle)
     │               │               │
     └───────────────┴───────────────┘
                     │
              ZombieStateMachine (每只僵尸一个)
                     │
     ┌───────┬───────┼───────┬───────┐
     ▼       ▼       ▼       ▼       ▼
   Idle   Wander  Chase  Attack  Dead
  (5个全局单例，只存逻辑，零数据)
```

**三层分离设计**：

| 层 | 数量 | 职责 |
|----|------|------|
| 状态类 (ZombieState) | 5 个全局单例 | 只存逻辑，零数据。`Enter/Update/Exit` 的数据全从 ctx 读写 |
| 状态机 (ZombieStateMachine) | 每只僵尸 1 个 | 只存数据（timer / wanderTarget / playerDetected 等），零逻辑 |
| 感知系统 (AwarenessSystem) | 全局 1 个 | 集中距离检测，通知状态机，不分散到各僵尸 Update |

### 二、Chunk 绑定机制

```
僵尸出生 ──► 分配 chunkId ──► 加入 RuntimeChunk.zombieIds
                │
                ├─ Chunk 进入 Loaded ──► 状态机激活，Update 跑起来
                ├─ Chunk 退到 Preloaded ──► 状态冻结，NavMeshAgent 禁用
                └─ Chunk 退到 Unloaded ──► 完全不思考，SetActive(false)
```

任何时候 **只有 Loaded 层级的僵尸在跑 Update**。Preloaded/Unloaded 的僵尸零 CPU 消耗。

### 三、开发分两步

#### 第一步：核心骨架（今天做）

##### 3.1 `ZombieState` — 抽象基类

```csharp
public abstract class ZombieState
{
    public abstract void Enter(ZombieStateMachine ctx);
    public abstract void Update(ZombieStateMachine ctx);
    public abstract void Exit(ZombieStateMachine ctx);
}
```

##### 3.2 `ZombieStateMachine` — 状态机本体

挂僵尸 GameObject 上，职责：
- 持有 `NavMeshAgent`、`DamageableZombie`、当前状态引用
- `TransitionTo(ZombieState newState)` — Exit 旧状态 → Enter 新状态
- `Update()` 转发到当前状态
- 暴露 `Transform PlayerTarget`（由 AwarenessSystem 写入）
- 暴露 `bool PlayerDetected`（由 AwarenessSystem 设置）
- Chase 状态下每 0.5s 更新一次 `NavMeshAgent.destination`

**状态机上的数据字段**（状态类本身不存数据）：

```csharp
public class ZombieStateMachine : MonoBehaviour
{
    public ZombieState currentState;

    // 感知（由 AwarenessSystem 写入）
    public Transform playerTarget;
    public bool playerDetected;

    // Idle 用
    public float idleTimer;
    public float idleDuration;

    // Wander 用
    public Vector3 wanderTarget;

    // Attack 用
    public float attackTimer;

    // 组件引用
    public NavMeshAgent agent;
    public DamageableZombie damageable;
}
```

##### 3.3 五个状态实现

| 状态 | Enter | Update | Exit | 转换条件 |
|------|-------|--------|------|---------|
| **Idle** | `ctx.idleDuration = Random(1,4)`，站立 | 计时到 → Wander；PlayerDetected → Chase | — | timer 到 → Wander；发现玩家 → Chase |
| **Wander** | 以当前位置 5~10m 半径 NavMesh 随机采样，半速走过去 | 到达 → Idle；PlayerDetected → Chase | — | 到达 → Idle；发现玩家 → Chase |
| **Chase** | 全速，设 destination 为玩家位置 | 每 0.5s 更新 destination；距离 ≤ 1.5m → Attack；距离 > 30m → Wander | — | 贴脸 → Attack；丢失 → Wander |
| **Attack** | 停移动，重置计时器 | 每 1.5s 对玩家造成 10 伤害；面朝玩家；距离 > 1.5m → Chase；HP ≤ 0 → Dead | — | 脱离范围 → Chase；死亡 → Dead |
| **Dead** | 禁用 NavMeshAgent + 碰撞 | 等 Destroy（DamageableZombie 已有 1s 延迟） | — | 无 |

每个状态使用静态单例模式：

```csharp
public class ZombieState_Idle : ZombieState
{
    public static readonly ZombieState_Idle Instance = new();

    public override void Enter(ZombieStateMachine ctx)
    {
        ctx.idleTimer = 0f;
        ctx.idleDuration = Random.Range(1f, 4f);
    }

    public override void Update(ZombieStateMachine ctx)
    {
        ctx.idleTimer += Time.deltaTime;
        if (ctx.idleTimer >= ctx.idleDuration)
            ctx.TransitionTo(ZombieState_Wander.Instance);
        if (ctx.PlayerDetected)
            ctx.TransitionTo(ZombieState_Chase.Instance);
    }

    public override void Exit(ZombieStateMachine ctx) { }
}
```

##### 3.4 `ZombieAwarenessSystem` — 集中式玩家检测

方案 B。单例 MonoBehaviour，职责：
- `List<ZombieStateMachine> _activeZombies` — 僵尸 Awake 时注册，OnDestroy 时注销
- 每 0.2s 遍历一次 `_activeZombies`，算每个僵尸与玩家距离
- 距离 ≤ 10m 且之前未 detected → `stateMachine.OnPlayerDetected(playerTransform)`
- 距离 > 10m 且之前 detected → `stateMachine.OnPlayerLost()`
- 玩家 Transform 在 Start 时缓存一次（`GameObject.FindWithTag("Player")`）

```
每 0.2s 遍历:

  Zombie A (距离 8m)  → OnPlayerDetected()  // 首次发现
  Zombie B (距离 50m) → 无变化
  Zombie C (距离 3m)  → 已 detected，持续追击
  Zombie D (距离 12m) → OnPlayerLost()      // 刚超出范围
```

##### 3.5 改造 `ZombieController`

- 删除旧的 `Update()` 里直接移动逻辑（`transform.position +=`）
- 变成薄壳组件：挂 `NavMeshAgent`、`ZombieStateMachine`、`DamageableZombie`
- 初始化时创建状态机并注册到 AwarenessSystem

##### 3.6 改造 `DamageableZombie`

- `Die()` 方法中通知状态机进入 Dead 状态

##### 3.7 接入 ChunkManager

- `RuntimeChunk.zombieIds` 已预留，僵尸通过此字段归属到 Chunk
- ChunkManager 三级切换时遍历 `zombieIds`：
  - Preloaded → Loaded：`NavMeshAgent.enabled = true`，注册到 AwarenessSystem
  - Loaded → Preloaded/Unloaded：`NavMeshAgent.enabled = false`，从 AwarenessSystem 注销

##### 3.8 第一步文件清单

| 文件 | 动作 | 说明 |
|------|:----:|------|
| `Systems/Zombie/ZombieState.cs` | 新建 | 抽象基类 |
| `Systems/Zombie/ZombieStateMachine.cs` | 新建 | 状态机上下文 + 状态切换 |
| `Systems/Zombie/ZombieStates/ZombieState_Idle.cs` | 新建 | 待机（静态 Instance） |
| `Systems/Zombie/ZombieStates/ZombieState_Wander.cs` | 新建 | 漫游（静态 Instance） |
| `Systems/Zombie/ZombieStates/ZombieState_Chase.cs` | 新建 | 追击（静态 Instance） |
| `Systems/Zombie/ZombieStates/ZombieState_Attack.cs` | 新建 | 攻击（静态 Instance） |
| `Systems/Zombie/ZombieStates/ZombieState_Dead.cs` | 新建 | 死亡（静态 Instance） |
| `Systems/Zombie/ZombieAwarenessSystem.cs` | 新建 | 集中式玩家检测 |
| `Systems/Zombie/ZombieController.cs` | 改造 | 删除旧移动逻辑，挂载新组件 |
| `Systems/Combat/DamageableZombie.cs` | 改造 | 死亡时通知状态机进入 Dead |
| `Systems/WorldGen/ChunkManager.cs` | 改造 | 三级切换时激活/休眠僵尸 |
| `Systems/WorldGen/RuntimeChunk.cs` | 无改动 | zombieIds 已预留 |
| `Systems/WorldGen/Stages/SpawnStage.cs` | 无改动 | Enabled 保持 false，第二步再用 |

##### 3.9 第一步验收标准

| 验收项 | 标准 |
|--------|------|
| 僵尸漫游 | 僵尸在 NavMesh 上随机走动，到达后停顿再走 |
| 发现玩家 | 玩家进入 10m → 僵尸切 Chase，全速追来 |
| 追击+攻击 | 追到 1.5m 停步，每 1.5s 造成一次伤害 |
| 失去目标 | 玩家跑出 30m → 僵尸切回 Wander |
| 僵尸死亡 | HP ≤ 0 → Dead → 1s 后 Destroy |
| 集中检测 | AwarenessSystem 统一管理，不分散到各 Update |
| Chunk 联动 | Unloaded Chunk 中僵尸休眠（NavMeshAgent 禁用） |
| 建造阻挡 | 放置建筑自动阻挡僵尸路径（blocksNavMesh 生效） |

##### 3.10 性能分析（以 250 只活跃僵尸为例）

| 开销项 | 频率 | 每帧成本 | 说明 |
|--------|------|---------|------|
| `Update()` 虚方法调用 | 每帧 ×250 | 极轻 | Idle/Wander/Dead 几乎什么都不做 |
| `SetDestination` | 每 0.5s ×追击中僵尸 | 假设 20 只追击 | **节流后开销可忽略** |
| `AwarenessSystem` 遍历 | 每 0.2s ×活跃数 | 1250 次距离/秒 | 比每僵尸自行检测少 12 倍 |
| Chunk 外僵尸 | 0 | 0 | **最大收益：75% 僵尸处于休眠** |

---

#### 第二步：扩展（后续做）

##### 4.1 `ZombieData` ScriptableObject

定义僵尸类型模板：血量/速度/伤害、检测范围、感知权重、体型(半径+高度)、外观索引、Loot 组。

##### 4.2 `ZombieSpawner` + `SpawnStage` 实现

- 按 Chunk 区域类型决定刷什么僵尸
- 按时间：白天少，夜晚多
- 按密度上限：单 Chunk 僵尸数量上限
- 写入 `RuntimeChunk.zombieIds`

##### 4.3 高级感知系统

- 视觉锥：`Vector3.Angle` 判定是否在僵尸前方锥形内，白天远（15m）、夜晚近（5m）
- 听觉事件：新增 `NoiseEvent`，玩家跑步/开枪/敲击 → AwarenessSystem 广播
- 环境遮蔽：`Physics.Linecast` 检测视线是否被墙壁阻挡

##### 4.4 群体行为

- 僵尸发现玩家时"传染"给 10m 内其他僵尸（Wander 状态的跟着切 Chase）
- 小型尸群自然形成

---

### 五、依赖确认

```
ZombieState（无依赖）
  └─ ZombieStateMachine（依赖 ZombieState）
       ├─ 5 个状态实现（依赖 ZombieStateMachine）
       ├─ ZombieAwarenessSystem（依赖 ZombieStateMachine 列表）
       ├─ ZombieController 改造（依赖 ZombieStateMachine + AwarenessSystem）
       └─ ChunkManager 集成（依赖 ZombieStateMachine + AwarenessSystem）

外部依赖（全部已就绪）:
  ✅ ChunkManager Phase1（三级区块 + RuntimeChunk.zombieIds）
  ✅ NavMeshAreas.asset（Walkable / Not Walkable / Jump）
  ✅ BuildableData.blocksNavMesh（7 个建造资产全部 true）
  ✅ GameConstants 僵尸参数（速度/范围/伤害/血量）
  ✅ GameEvents.ZombieDied
  ✅ DamageableZombie（仅需小改造通知状态机）
  ✅ ZombieStates/ 目录已创建
```

---

> **Git 提交**: `僵尸AI(L4): Phase1 完成 — FSM五状态+NavMeshAgent+集中感知+Chunk联动+静态单例共享`

---

### 4.1 后置定稿总结（开发完成后回顾）

#### 计划 vs 实际

| 对比维度 | 前置计划 | 实际完成 | 差异 |
|----------|---------|---------|:--:|
| ZombieState 基类 | 抽象类 Enter/Update/Exit | 完全按计划实现 | ✅ |
| 五个状态 | Idle/Wander/Chase/Attack/Dead | 完全按计划实现，全部静态单例 | ✅ |
| 状态机 ZombieStateMachine | ctx 存数据，状态存逻辑 | 完全按计划实现 | ✅ |
| 集中玩家检测 | ZombieAwarenessSystem 每 0.2s 遍历 | 完全按计划实现 | ✅ |
| NavMeshAgent 迁移 | 替换旧直移逻辑 | 完全按计划实现 | ✅ |
| ZombieController 改造 | 薄壳配置组件 | RequireComponent 三件套自动挂载 | ✅ |
| SetDestination 节流 | 每 0.5s 更新 | 完全按计划实现 | ✅ |
| Chunk 绑定 | RuntimeChunk.zombieInstances | 直接存引用而非 ID，运行时更高效 | 🔄 细节调整 |
| ChunkManager 集成 | Activate/Deactivate + 三级切换联动 | 完全按计划实现，Initialize() 因顺序问题移到 Awake | 🔄 细节调整 |
| DamageableZombie 联动 | Die() 通知状态机 | 完全按计划实现 | ✅ |

#### 新建文件（实际）

| 文件 | 说明 |
|------|------|
| `Systems/Zombie/ZombieState.cs` | 状态抽象基类 |
| `Systems/Zombie/ZombieStateMachine.cs` | 状态机组件，存运行时数据，管理状态切换 |
| `Systems/Zombie/ZombieAwarenessSystem.cs` | 集中式玩家检测单例 |
| `Systems/Zombie/ZombieStates/ZombieState_Idle.cs` | Idle: 站1~4s → Wander / 发现玩家 → Chase |
| `Systems/Zombie/ZombieStates/ZombieState_Wander.cs` | Wander: NavMesh随机点半速漫游 → Idle / 发现玩家 → Chase |
| `Systems/Zombie/ZombieStates/ZombieState_Chase.cs` | Chase: 全速追，每0.5s更新destination → Attack / 丢失 → Wander |
| `Systems/Zombie/ZombieStates/ZombieState_Attack.cs` | Attack: 停步+面朝玩家+cooldown伤害循环 → Chase / 死亡 → Dead |
| `Systems/Zombie/ZombieStates/ZombieState_Dead.cs` | Dead: 禁用agent+碰撞，等Destroy |

#### 改造文件（实际）

| 文件 | 改造内容 |
|------|---------|
| `ZombieController.cs` | 删除旧直接移动逻辑，RequireComponent 自动挂载三件套 |
| `DamageableZombie.cs` | Die() 中新增 `stateMachine.TransitionTo(ZombieState_Dead.Instance)` |
| `ChunkManager.cs` | +RegisterZombie / ActivateZombiesInChunk / DeactivateZombiesInChunk；Initialize() 移到 Awake |
| `RuntimeChunk.cs` | +`zombieInstances: List<ZombieStateMachine>` |

#### 经验教训

- **命名空间冲突**：`_Game.Systems.Time` 命名空间遮蔽了 `UnityEngine.Time`，导致 `Time.deltaTime` 编译报错 CS0234。在 `_Game.Systems.*` 下的文件必须用全限定 `UnityEngine.Time.deltaTime`。
- **执行顺序陷阱**：ChunkManager.Initialize() 最初在 Start()，但其他系统的 Start() 可能先跑，导致 `_chunks` 为 null 引发 NullReferenceException。移到 Awake() 解决了所有初始化顺序问题。
- **状态单例模式收益**：5 个全局 `Instance` 替代每僵尸 5 个 new 对象。100 只僵尸从 500 个状态对象降为 5 个，零 GC 分配。
- **Chunk 休眠才是最大的性能优化**：250 只僵尸中约 75% 处于 Preloaded/Unloaded 状态完全不跑 Update，比所有 Update 内部的微观优化加起来还重要。

---


---

### 4.1 第二步计划（Phase2 · ✅ 已完成）

> **日期**: 2026-05-20 · **状态**: ✅ Phase2 完成 · **Git**: d06f988 · **依赖**: ChunkManager + 声音系统 Phase1（全部已就绪）

---

### 4.1 Phase2 后置定稿总结（开发完成后回顾）

#### 计划 vs 实际

| 对比维度 | 前置计划 | 实际完成 | 差异 |
|----------|---------|---------|:--:|
| ZombieData SO | 僵尸类型模板，3 默认资产 | 完全按计划实现 | ✅ |
| ZombieStateMachine 改造 | ZombieData 替代硬编码 | +ApplyZombieData()，+防重复注册 | ✅ |
| ZombieController 改造 | 薄壳引用 ZombieData | 同步设置 DamageableZombie.maxHealth | ➕ |
| ZombieSpawner | 按区域+时间+密度刷怪 | 两阶段预算制（初始+持续补刷）+ 隐身刷新 + 权重随机 | 🔄 升级 |
| ZoneSpawnProfile | 未在原始计划中 | 新增 SO：地段参数集中管理，后续多地段差异化零代码 | ➕ |
| SpawnStage | 填充 Execute 挂入管线 | 保持 Enabled=false，运行时由 ChunkManager.Stage2 驱动 | 🔄 架构调整 |
| 视觉锥 | ZombieData.visionAngle 锥形检测 | 完全按计划实现，visionAngle=0 回退圆形 | ✅ |
| 听觉联动 | 已在声音系统 Phase1 完成 | 零改动，直接生效 | ✅ |
| 视线遮蔽 | Physics.Linecast 阻挡判定 | 完全按计划实现 | ✅ |
| 群体行为 | 15m 内连锁唤醒 | 完全按计划实现（逐帧级联，涟漪扩散） | ✅ |
| Chunk 卸载 | 僵尸休眠保留 | 改为销毁僵尸 + 重置预算，下次加载全新刷新 | 🔄 架构调整 |
| 昼夜倍率 | 夜晚密度 ×2 | 完全按计划实现（Spawner.nightMultiplier + IsNight 判定） | ✅ |
| 地段区分 | 按区域类型差异配置 | ZoneSpawnProfile + 伪随机哈希分配，后续替换为真实区域数据 | ⏳ 基础就绪 |

#### 新建文件（实际）

| 文件 | 说明 |
|------|------|
| `Config/ZombieData.cs` | 僵尸类型 SO 模板：血量/速度/伤害/感知/视野锥/体型(半径+高度)/掉落 |
| `Config/ZoneSpawnProfile.cs` | 地段刷怪配置 SO：数量/间隔/隐身距离/类型权重 |
| `Systems/Zombie/ZombieSpawner.cs` | 两阶段刷怪系统：PhaseA 初始刷怪 + PhaseB 持续补刷（预算制+隐身刷新+权重随机+昼夜倍率+BuildBody圆柱体） |
| `Editor/CreateDefaultZombieData.cs` | 一键创建 3 种 ZombieData(含体型) + 默认 ZoneSpawnProfile + Spawner 场景对象 |
| `Editor/SetupZombieSpawnDebug.cs` | 一键创建 F1 调试面板 GameObject |
| `UI/ZombieSpawnDebugWindow.cs` | F1 开发者刷怪面板：距离/类型/数量可调，圆柱体可视化 |

#### 改造文件（实际）

| 文件 | 改造内容 |
|------|---------|
| `ZombieStateMachine.cs` | +ApplyZombieData() 从 SO 批量应用参数；Start() 防重复注册（Contains 检查） |
| `ZombieController.cs` | +zombieData 引用替代硬编码字段；+Apply() 提取共用逻辑；+Initialize() 修复 AddComponent 后数据覆盖（Spawner 刷怪此前一直用默认值） |
| `ZombieSpawner.cs` | SpawnZombie 改调 Initialize()；+BuildBody() 静态工具方法创建圆柱体+缩放 Collider |
| `ZombieAwarenessSystem.cs` | 完全重写：+CanSeePlayer（视觉锥+Linecast遮蔽）+CascadeToNearby（逐帧级联扩散）+_newlyDetected 列表 |
| `ChunkManager.cs` | Stage2 调 SpawnInitial；EnterUnloaded 调 DestroyZombiesInChunk + OnChunkUnloaded；+IsNightTime()；+DestroyZombiesInChunk() |
| `SpawnStage.cs` | 更新注释（运行时由 ChunkManager 驱动） |
| `UI/DebugPanel.cs` | +ZombieSpawner 当前 Chunk 存活/预算显示 |
| `UI/ZombieSpawnDebugWindow.cs` | 新建：F1 调试面板，距离/类型/数量可调配刷怪 |
| `Editor/SetupZombieSpawnDebug.cs` | 新建：一键创建 F1 面板 GameObject |
| `Core/GameEvents.cs` | 无需改动（NoiseEvent 已在声音系统 Phase1 完成） |

#### 刷怪机制详解

```
┌─ Phase A（初始）──────────────────────────────────────┐
│ ChunkManager.Stage2_RespawnNPCs                       │
│   → ZombieSpawner.SpawnInitial(chunkId, isNight)      │
│   → 按 ZoneSpawnProfile 刷 initialMin~initialMax 只    │
│   → 权重随机选 ZombieData 类型                         │
│   → 预算 = maxPerChunk - 实际刷出                      │
└──────────────────────────────────────────────────────┘
                         │
                         ▼
┌─ Phase B（持续补刷）──────────────────────────────────┐
│ ZombieSpawner.Update() 每 10s                         │
│   → 遍历 _alivePerChunk，找 Loaded 状态的 Chunk        │
│   → 冷却检查（respawnInterval 每 Chunk）               │
│   → 当前存活 < maxPerChunk？补刷 1~2 只                │
│   → 隐身刷新：NavMesh找点 + 距玩家 > 25m               │
│               + dot(player.forward, toSpawn) < 0.3     │
└──────────────────────────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         ▼               ▼               ▼
    僵尸死亡          Chunk 卸载       玩家远离
  预算 +1          销毁全部僵尸      不再补刷
  下次补刷填补      预算全清重置      Chunk休眠
```

#### 3 种默认僵尸

| 类型 | 血量 | 速度 | 伤害 | 视野锥 | 权重 |
|------|:---:|:---:|:---:|:-----:|:---:|
| 普通僵尸 | 100 | 3 | 10 | 90° | 60 |
| 快速僵尸 | 50 | 6 | 8 | 120° | 25 |
| 胖僵尸 | 300 | 1.5 | 20 | 60° | 15 |

#### 经验教训

- **预算制优于存活计数**：不直接统计每 Chunk 当前僵尸数，改用 `maxPerChunk - 已刷 + 死亡返还`。避免僵尸跨 Chunk 移动导致的计数错乱。
- **Chunk 卸载必须销毁僵尸**：旧设计让僵尸休眠保留（SetActive=false），但刷怪预算重置后会重复刷。改为卸载时 Destroy + Clear，下次加载全新初始化，逻辑更清晰。
- **僵尸类型配置移到 Profile**：Spawner 本身不直接持有 ZombieData 列表，而是通过 ZoneSpawnProfile.typeWeights 间接引用。加新地段只需创建新 Profile，不碰 Spawner 代码。
- **隐身刷新的三个条件**：距离(25m) + 视野(dot<0.3) + NavMesh 有效点，三重保障玩家不会看到僵尸凭空出现。实践中 10 次寻点重试足够找到合法位置。

---
