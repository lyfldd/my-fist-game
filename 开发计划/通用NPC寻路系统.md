# 通用NPC寻路系统（AIAgent）

> **日期**: 2026-06-22 · **状态**: ✅ Phase 1 已实现 (Zombie) · **依赖**: ThreatSystem + FactionSystem + DecibelSystem + NavMesh
> 🔍 迭代追踪 | 第1轮: ✅ ZombieStateMachine继承 | 第2轮: ⬜ Bandit/Survivor/Military/AIBot | 第3轮: ⬜ Stunned中断

> **关联**: [[仇恨系统]] · [[僵尸AI设计]]

---

## 一、架构全景

```
┌──────────────────────────────────────────────────────────────┐
│                     AIAgent (抽象基类)                         │
│              _Game/Systems/AI/AIAgent.cs                      │
│                                                              │
│  ┌─ 组件依赖 ──────────────────────────────────────────────┐ │
│  │ NavMeshAgent _agent          ← 导航                      │ │
│  │ FactionComponent _factionComp ← 阵营归属                 │ │
│  │ AIAgentData _data            ← ScriptableObject 配置     │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌─ 核心循环 (每帧) ───────────────────────────────────────┐ │
│  │ Update() → PerceptionTick() + StateTick()                │ │
│  │ LateUpdate() → ExecuteTransition() (排队切换)            │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌─ 事件订阅 ──────────────────────────────────────────────┐ │
│  │ NoiseEvent    → OnHeardSound → ThreatSystem.AddThreat    │ │
│  │ AllyAlertEvent → OnAllyAlert  → ThreatSystem.AddThreat   │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌─ 子类覆写 (abstract/virtual) ──────────────────────────┐ │
│  │ DoAttack(GameObject)     abstract — 攻击行为             │ │
│  │ CanSee(Transform)        virtual  — 视觉检测             │ │
│  │ GetHomePosition()        virtual  — 据点位置             │ │
│  │ GetEyesPosition()        virtual  — 眼睛高度             │ │
│  │ OnStateEnter/Exit(state) virtual  — 状态钩子             │ │
│  │ OnDamaged()              public   — 受伤通知入口          │ │
│  └────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────┘
         ▲
         │ 继承
    ┌────┴────────────────────┐
    │  ZombieStateMachine     │  ← 唯一已实现子类
    │  + Die() / IsDead       │
    │  + ApplyFromZombieData()│
    │  + SampleRandomPoint()  │
    └─────────────────────────┘
```

### 当前实现状态

| 模块 | 状态 | 文件 |
|------|:--:|------|
| AIAgent 抽象基类 | ✅ 完整 | `_Game/Systems/AI/AIAgent.cs` (813行) |
| AIAgentData SO | ✅ 完整 | `_Game/Config/AIAgentData.cs` (78行) |
| ZombieStateMachine (子类) | ✅ 完整 | `_Game/Systems/Zombie/ZombieStateMachine.cs` (188行) |
| ZombieSpawner (两阶段) | ✅ 完整 | `_Game/Systems/Zombie/ZombieSpawner.cs` (315行) |
| ZombieData SO | ✅ 完整 | `_Game/Config/ZombieData.cs` (37行) |
| ThreatSystem | ✅ 完整 | `_Game/Systems/Threat/ThreatSystem.cs` (421行) |
| FactionSystem | ✅ 完整 | `_Game/Systems/Threat/FactionSystem.cs` (217行) |
| Bandit/Survivor/Military/AIBot 子类 | ⬜ 未实现 | — |
| Stunned 全局中断 | ⬜ 未实现 | — |

---

## 二、状态机

### 2.1 五状态枚举（已实现）

```csharp
// _Game/Systems/AI/AIAgent.cs
public enum AIState
{
    Idle,        // 待机：Wander / StandBy / Patrol / Follow
    Investigate, // 调查：走向威胁最后已知位置
    Combat,      // 战斗：追击 + 攻击循环
    Flee,        // 逃跑：远离威胁方向
    Return       // 返回：走回 activityCenter
}
```

**Stunned 和 Dead 尚未作为 AIState 实现。** 当前死_由 `ZombieStateMachine.Die()` 将 `enabled = false` 关闭整个组件并停止 NavMeshAgent。Stunned 在设计文档中有规划但未编码。

### 2.2 状态转移图（实际代码）

```
                         ┌─────────────────────────┐
                         │          Idle             │
                         │  Wander/StandBy/Patrol/   │
                         │  Follow                   │
                         └──────┬──────────┬─────────┘
                                │          │
                 威胁出现        │          │ 威胁出现+能看到真实目标
                 (无真实目标     │          │ (type∈{Damage,Visual,
                 或看不到)       │          │  Aggression})
                                ▼          ▼
                         ┌──────────┐  ┌──────────┐  丢失视线超时
              ┌─────────│Investigate│  │  Combat  │──────────────┐
              │         └─────┬────┘  └────┬─────┘              │
              │               │            │                    │
              │   到达+观察超时│ 威胁清零   │ 目标死亡/威胁清零   │
              │               ▼            ▼                    │
              │         ┌──────────────────────┐                │
              │         │        Idle           │◄───────────────┘
              │         └──────────────────────┘
              │
              │─────────────────────────────────────────────────────┐
              │              全局中断 (在StateTick()中检查)           │
              │                                                     │
              │  距离 activityCenter > activityRadius → Return       │
              │  Flee状态: 威胁消失+距activityCenter近 → Idle        │
              │  Return状态: 途中看到敌人 → Combat                   │
              └─────────────────────────────────────────────────────┘

         ┌──────────┐      ┌──────────┐      ┌──────────┐
         │   Flee   │─────→│   Idle   │◄─────│  Return  │
         │          │      │          │      │          │
         └──────────┘      └──────────┘      └──────────┘
              │                                     ▲
              │                                     │
              └──── 威胁仍在+超出活动范围 ───────────→┘
```

### 2.3 状态切换优先级与排队

同一帧内多个 `TransitionTo` 调用不会立即执行，而是排队到 `LateUpdate`。多个请求按优先级取高者：

```csharp
// AIAgent.cs 第167-178行
static AIState PriorityHigher(AIState a, AIState b)
{
    int p(AIState s) => s switch
    {
        AIState.Flee        => 4,  // 最高
        AIState.Return      => 3,
        AIState.Combat      => 2,
        AIState.Investigate => 1,
        _                   => 0   // Idle = 最低
    };
    return p(a) >= p(b) ? a : b;
}
```

切换在 `LateUpdate` 中执行（第149-156行）：
```csharp
void LateUpdate()
{
    if (_pendingTransition.HasValue && !_transitionLock)
    {
        ExecuteTransition(_pendingTransition.Value);
        _pendingTransition = null;
    }
}
```

### 2.4 各状态详解

#### Idle
- **进入**：Start默认 / 威胁清零 / Investigate超时无发现 / Flee跑远安全 / Return到达
- **行为**：由 `AIAgentData.idleBehavior` 决定
  - `Wander`：在 `activityCenter` 周围 `wanderRadius` 内随机游荡，每 `wanderInterval` 秒换目标
  - `StandBy`：`ResetPath()` 原地不动
  - `Patrol`：沿 `patrolRoute` 列表循环走动
  - `FollowEntity`：预留（子类覆写 GetFollowTarget）
- **退出**：
  - 威胁出现 + 有真实目标 + CanSee → Combat
  - 威胁出现 + 无真实目标/看不到 → Investigate
  - 超出活动范围 → Return

#### Investigate
- **进入**：有威胁但无法进入Combat条件（Sound/AllyAlert/远处视觉/被伤害看不到攻击者）
- **行为**：NavMeshAgent走向 `ThreatSystem.GetLastKnownPosition()`，到达后停留 `investigateTime` 观察
- **门控**：只有威胁类型为 Damage/Visual/Aggression + CanSee + dist ≤ attackRange×2 才切 Combat
- **退出**：
  - 威胁消失 → Idle
  - 看清楚+距离近+直接威胁类型 → Combat
  - 到达观察超时 → Idle

#### Combat
- **进入**：CanSee + 直接威胁类型 + 距离合理
- **行为**：
  - 追击距离 > attackRange → NavMeshAgent追目标
  - 进入攻击范围 → ResetPath + FaceTarget + DoAttack冷却循环
  - 每 `targetReassessInterval` 秒重评估目标（威胁值×1.5才切换，需NavMesh可达+路径长度≤直线3倍）
  - 丢失视线计时
- **退出**：
  - 目标死亡/威胁清零 → Idle
  - 丢失视线超时 + 仍有威胁 → Investigate
  - 丢失视线超时 + 无威胁 → Idle
- **阵营扩散**：进入Combat时自动发射 `AllyAlertEvent`

#### Flee
- **进入**：当前未在代码中有自动触发（`canFlee` 和 `fleeThreshold` 字段存在但未连接到Health检查）
- **行为**：计算逃离方向 = `myPos - 最大威胁位置`，NavMeshAgent向逃离方向跑 `fleeDistance`
- **退出**：威胁消失 + 距activityCenter近 → Idle；超出活动范围 → Return

#### Return
- **进入**：`DistanceToActivityCenter() > activityRadius` 且 `hasActivityConstraint == true`
- **行为**：NavMeshAgent走向 `_activityCenter`
- **退出**：到达（dist < 1m） → Idle；途中CanSee敌人 → Combat

---

## 三、混合驱动模式

系统采用三种触发机制协同工作：

| 触发类型 | 机制 | 延迟 | 适用场景 |
|---------|------|:--:|---------|
| 威胁轮询 | `PerceptionTick()` 每10帧(≈0.16s)扫描周围实体 | ~0.16s | 视觉发现敌人/友军 |
| 事件驱动 | `NoiseEvent` / `AllyAlertEvent` 即时推送到 ThreatSystem | 0s | 听到声音/友军预警 |
| 每帧Tick | `StateTick()` 每帧检查状态退出条件 | 0s | 丢失视线/到达目的地 |

### 分帧分散

100个AIAgent若每帧全部扫描会性能爆炸。通过静态计数器分为10组：

```csharp
// AIAgent.cs 第72-74行
static int _tickGroupCounter;
int _tickGroup;
const int TICK_GROUP_COUNT = 10;

// Awake时分配组号
_tickGroup = _tickGroupCounter++ % TICK_GROUP_COUNT;

// Update中按组触发
if (UnityTime.frameCount % TICK_GROUP_COUNT == _tickGroup)
    PerceptionTick();
```

| 参数 | 值 |
|------|:--:|
| 分组数 | 10 |
| 每实体扫描间隔 | 10帧 ≈ 0.16s (60fps) |
| 100实体时每帧扫描数 | ~10 |

---

## 四、三档视觉系统

### 4.1 VisionLevel 枚举

```csharp
// AIAgent.cs 第23-29行
public enum VisionLevel
{
    Blind,              // 看不见——视野锥外或被遮挡
    PeripheralMotion,   // 余光——仅检测移动目标
    Peripheral,         // 周边视野——概率识别
    Central             // 中心视野——100%识别
}
```

### 4.2 基类 CanSee 实现

```csharp
// AIAgent.cs 第628-660行
protected virtual bool CanSee(Transform target)
{
    // 1. 距离过滤
    float dist = Vector3.Distance(eyes, targetEyes);
    if (dist > _data.perceptionRange) return false;

    // 2. 视线遮蔽 (Linecast against obstacleMask)
    if (Physics.Linecast(eyes, targetEyes, _data.obstacleMask))
        return false;

    // 3. 三档视觉角度判定
    float angle = Vector3.Angle(transform.forward, dirToTarget);

    if (angle <= _data.centralVisionAngle)       // 默认30°
        return true;  // 中心视野 100%

    if (angle <= _data.peripheralVisionAngle)     // 默认60°
        return Random.value < _data.peripheralRecognitionChance; // 概率

    if (angle <= _data.visionConeAngle * 0.5f)    // 余光区
    {
        var rb = target.GetComponent<Rigidbody>();
        return rb != null && rb.velocity.magnitude > _data.motionThreshold;
    }

    return false;  // 看不见
}
```

### 4.3 僵尸覆写版 CanSee

僵尸不使用三档视觉，改用近距离嗅觉 + 单层视觉锥：

```csharp
// ZombieStateMachine.cs 第105-138行
protected override bool CanSee(Transform target)
{
    // 距离+遮挡检测（同基类）

    // 3米内无视视野锥（听觉/嗅觉）
    const float meleeAwarenessRange = 3f;
    if (dist <= meleeAwarenessRange) return true;

    // 视觉锥（中远距离需要视野）
    float halfAngle = _data.visionConeAngle * 0.5f;
    float angle = Vector3.Angle(forward, toTarget);
    if (angle > halfAngle) return false;

    return true;
}
```

### 4.4 各类实体视觉配置（设计对照）

| 实体 | 中心视野 | 周边视野 | 余光运动检测 | 全锥角 | 特殊 |
|------|:--:|:--:|:--:|:--:|------|
| 僵尸 | 90° | — | — | 90° | 3m内无视视野锥（已实现） |
| 恶徒 (Bandit) | 30°/100% | 60°/80% | 90°/移动 | 90° | 掩体检测（未实现） |
| 幸存者 (Survivor) | 全向 | — | — | 360° | 未实现 |
| AIBot | 全向 | — | — | 360° | 未实现 |
| 军方 (Military) | 30°/100% | 60°/70% | 90°/移动 | 90° | 未实现 |
| 中立动物 | 45°/100% | 90°/40% | 180°/移动 | 180° | canFlee=true（未实现） |

---

## 五、感知扫描

### PerceptionTick 流程

```csharp
// AIAgent.cs 第580-620行
void PerceptionTick()
{
    // Physics.OverlapSphereNonAlloc 以 perceptionRange 为半径
    Collider[] hits = new Collider[16];
    int count = Physics.OverlapSphereNonAlloc(transform.position,
        _data.perceptionRange, hits, Physics.AllLayers);

    for (int i = 0; i < count; i++)
    {
        // 过滤：需有 FactionComponent + 非自身
        var otherFaction = hits[i].GetComponent<FactionComponent>();
        if (otherFaction == null) continue;

        // 只检测敌对阵营
        if (!FactionSystem.Instance.IsHostile(_data.factionType,
            otherFaction.Faction)) continue;

        // CanSee → 添加视觉威胁
        if (CanSee(otherFaction.transform))
        {
            ThreatSystem.Instance.AddThreat(myId,
                otherFaction.gameObject.GetInstanceID(),
                50f, ThreatType.Visual,
                otherFaction.transform.position);
        }
    }
}
```

---

## 六、导航卡住恢复 (NavMesh Recovery)

每2秒检测一次，三级渐进取恢复策略：

```csharp
// AIAgent.cs 第520-576行
void CheckStuck()
{
    _stuckCheckTimer += Time.deltaTime;
    if (_stuckCheckTimer < 2f) return;

    // 判定卡住：速度 < 0.1 且 有路径 且 剩余距离 > 1m
    if (_agent.velocity.magnitude < 0.1f && _agent.hasPath
        && _agent.remainingDistance > 1f)
    {
        _stuckCount++;

        if (_stuckCount == 1)
        {
            // 一级：尝试绕路45°/90°/135°找无遮挡NavMesh点
            Vector3 detour = FindDetour();
            if (detour != Vector3.zero)
                _agent.SetDestination(detour);
        }
        else if (_stuckCount == 2)
        {
            // 二级：退后1m重新寻路
            _agent.ResetPath();
            transform.position -= transform.forward * 1f;
            if (_currentDestination.HasValue)
                _agent.SetDestination(_currentDestination.Value);
        }
        else
        {
            // 三级：放弃，回Idle
            _agent.ResetPath();
            _stuckCount = 0;
            TransitionTo(AIState.Idle);
        }
    }
    else if (_agent.velocity.magnitude > 0.5f)
    {
        _stuckCount = 0;  // 正常移动中重置计数器
    }
}
```

`FindDetour()` 在45°至135°范围内以45°步进，左右对称探测NavMesh点并用 `Physics.Linecast` 排除障碍物遮挡。

---

## 七、Combat 目标切换

### 重评估逻辑

```csharp
// AIAgent.cs 第412-449行
void ReassessTarget()
{
    // 1. 取威胁表排序第一的真实目标
    int? newTopId = ThreatSystem.Instance.GetTopRealTarget(myId);
    if (newTopId == null || newTopId == currentTargetId) return;

    // 2. 阈值门槛：新威胁值 > 当前 × targetSwitchThreshold (默认1.5)
    float currentThreat = ThreatSystem.Instance.GetThreatValue(myId, currentTargetId);
    float newThreat = ThreatSystem.Instance.GetThreatValue(myId, newTopId.Value);
    if (newThreat <= currentThreat * _data.targetSwitchThreshold) return;

    // 3. NavMesh可达性检查
    NavMeshPath path = new NavMeshPath();
    if (!_agent.CalculatePath(newTarget.position, path)
        || path.status != NavMeshPathStatus.PathComplete)
        return;  // 完全不可达

    // 4. 路径合理性检查：路径长度 > 直线距离×3 → 视为墙后/T型障碍，不切换
    float pathLen = GetPathLength(path);
    float straightDist = Vector3.Distance(transform.position, newTarget.position);
    if (pathLen > straightDist * 3f) return;

    // 通过 → 切换目标
    _currentTargetId = newTopId.Value;
    _currentTarget = newTarget;
    _lostSightTimer = 0f;
}
```

### 攻击循环

```csharp
// CombatTick 核心 (第357-410行)
void CombatTick()
{
    if (_currentTarget == null) { TransitionTo(AIState.Idle); return; }

    float dist = Vector3.Distance(transform.position, _currentTarget.position);

    if (dist > _data.attackRange)
    {
        _agent.SetDestination(_currentTarget.position);  // 追击
        _agent.speed = _data.combatSpeed;
    }
    else
    {
        _agent.ResetPath();       // 停下
        FaceTarget(_currentTarget); // 面向目标
        if (Time.time - _lastAttackTime >= _data.attackCooldown)
        {
            DoAttack(_currentTarget.gameObject);
            _lastAttackTime = Time.time;
        }
    }
}
```

---

## 八、声音反应与友军预警

### 声音事件链

```
DecibelSystem → EventBus.Publish(NoiseEvent)
    → AIAgent.OnHeardSound(NoiseEvent e)
        → 检查距离半径 + 检查 reactsToSoundTags
        → ThreatSystem.AddThreat(sourceId, targetId, radius, ThreatType.Sound, position)
        → 威胁出现 → StateTick → Idle → Investigate
```

```csharp
// AIAgent.cs 第663-676行
void OnHeardSound(NoiseEvent e)
{
    float dist = Vector3.Distance(transform.position, e.Position);
    if (dist > e.Radius) return;
    if (_data.reactsToSoundTags == null
        || !_data.reactsToSoundTags.Contains(e.Tag)) return;

    ThreatSystem.Instance.AddThreat(
        gameObject.GetInstanceID(),
        e.SourceObject != null ? e.SourceObject.GetInstanceID() : (int?)null,
        e.Radius,              // 威胁值 = 声音半径（DecibelSystem已做距离衰减）
        ThreatType.Sound,
        e.Position
    );
}
```

僵尸的声音标签配置（ZombieStateMachine.ApplyFromZombieData）：
`Footstep | Combat | Gunshot | Building | Impact | Mechanical`

### AllyAlert 友军预警级联

```
任何 AIAgent 进入 Combat
    → EventBus.Publish(AllyAlertEvent(sourceId, position, allyAlertRange, faction))
    → 周围同阵营 AIAgent.OnAllyAlert()
        → 距离检查
        → ThreatSystem.AddThreat(sourceId, null, 20f, ThreatType.AllyDamage, position)
        → 威胁出现 → StateTick → Idle → Investigate
```

```csharp
// AIAgent.cs 第190-199行 (ExecuteTransition中)
if (newState == AIState.Combat && _data.canAllyAlert)
{
    EventBus.Publish(new AllyAlertEvent(
        gameObject.GetInstanceID(),
        transform.position,
        _data.allyAlertRange,   // 僵尸默认15m
        _data.factionType
    ));
}

// 接收方 (第679-691行)
void OnAllyAlert(AllyAlertEvent e)
{
    if (_data.factionType != e.Faction) return;           // 仅同阵营
    if (Vector3.Distance(transform.position, e.Position) > e.Radius) return;

    ThreatSystem.Instance.AddThreat(
        gameObject.GetInstanceID(),
        null,           // 无具体目标——只知道出事了，走向位置调查
        20f,
        ThreatType.AllyDamage,
        e.Position
    );
}
```

级联效果：A发现玩家 → A进入Combat → 发射AllyAlert(15m) → 15m内同阵营B收到 → B进入Investigate走向A的位置 → B到达能看到玩家 → B进入Combat → B发射AllyAlert(15m) → ...

---

## 九、6类实体配置差异

### 9.1 阵营定义

```csharp
// _Game/Config/FactionType.cs
public enum FactionType
{
    Player,    // 玩家
    Survivor,  // 幸存者/好人帮
    Zombie,    // 僵尸 ✅ 已实现AI
    AIBot,     // 己方机器人
    Bandit,    // 黑恶势力
    Military,  // 军方
    Neutral    // 中立/动物
}
```

### 9.2 阵营关系矩阵（FactionSystem 硬编码默认值）

| | Player | Survivor | Zombie | AIBot | Bandit | Military | Neutral |
|------|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| Player | — | Ally | **Hostile** | Ally | **Hostile** | Ally | Neutral |
| Survivor | Ally | — | **Hostile** | Ally | **Hostile** | Neutral | Neutral |
| Zombie | **Hostile** | **Hostile** | — | **Hostile** | Neutral | **Hostile** | Neutral |
| AIBot | Ally | Ally | **Hostile** | — | **Hostile** | Ally | Neutral |
| Bandit | **Hostile** | **Hostile** | Neutral | **Hostile** | — | **Hostile** | Neutral |
| Military | Ally | Neutral | **Hostile** | Ally | **Hostile** | — | Neutral |
| Neutral | Neutral | Neutral | Neutral | Neutral | Neutral | Neutral | — |

### 9.3 规划配置对照表

仅有 Zombie 已通过 `ZombieStateMachine.ApplyFromZombieData()` 实现自动配置。其余实体类型尚未有子类，以下为设计规划值：

| 参数 | 僵尸 ✅ | 恶徒 | 幸存者 | AIBot | 军方 | 中立动物 |
|------|:--:|:--:|:--:|:--:|:--:|:--:|
| **factionType** | Zombie | Bandit | Survivor | AIBot | Military | Neutral |
| **idleSpeed** | moveSpeed×0.5 | 3 | 2.5 | 0 (StandBy) | 3 | 3 |
| **combatSpeed** | moveSpeed | 4.5 | 3.5 | 8 | 5 | 6 |
| **fleeSpeed** | moveSpeed | 6 | 5 | 10 | 7 | 8 |
| **hasActivityConstraint** | false | true | true | false | true | true |
| **activityRadius** | 100 | 100 | 80 | — | 120 | 60 |
| **perceptionRange** | detectRange | 40 | 25 | 50 | 45 | 25 |
| **visionConeAngle** | visionAngle | 90° | 360° | 360° | 90° | 180° |
| **centralVisionAngle** | visionAngle | 30° | 360° | 360° | 30° | 45° |
| **peripheralVisionAngle** | 0 (不用) | 60° | — | — | 60° | 90° |
| **peripheralRecogChance** | 0 (不用) | 0.8 | 1.0 | 1.0 | 0.7 | 0.4 |
| **motionThreshold** | 0.5 | 1.5 | — | — | 1.5 | 2.0 |
| **attackRange** | attackRange | 15 | 1.5 | 15 | 20 | 1.5 |
| **attackCooldown** | attackCooldown | 2s | 1.2s | 0.5s | 1s | 1.5s |
| **idleBehavior** | Wander | StandBy | Follow | StandBy | Patrol | Wander |
| **canFlee** | false | true | true | false | false | true |
| **fleeThreshold** | — | 0.3 | 0.4 | — | — | 0.6 |
| **canAllyAlert** | true | true | true | false | true | false |
| **allyAlertRange** | 15 | 20 | 25 | — | 30 | — |
| **reactsToSoundTags** | 6种全部 | 全部 | 全部 | Gunshot+Combat | Gunshot+Combat | Footstep+Impact |
| **obstacleMask** | Building | Building | Building | Building | Building | Building |
| **targetSwitchThreshold** | 1.5 | 1.5 | 1.5 | 1.5 | 1.5 | 1.5 |
| **targetLostTimeout** | 5s | 5s | 5s | 5s | 5s | 5s |
| **CanSee覆写** | 近距嗅觉+单锥 | 基类三档+掩体 | 全向 | 全向传感器 | 基类三档 | 基类三档 |

### 9.4 ZombieData → AIAgentData 映射

僵尸通过 `ZombieStateMachine.ApplyFromZombieData(ZombieData data)` 在运行时从 ZombieData 创建 AIAgentData：

```csharp
// ZombieStateMachine.cs 第28-82行
public void ApplyFromZombieData(ZombieData data)
{
    if (_data == null)
        _data = ScriptableObject.CreateInstance<AIAgentData>();

    _data.displayName       = data.zombieName;
    _data.factionType       = FactionType.Zombie;
    _data.idleSpeed         = data.moveSpeed * 0.5f;
    _data.combatSpeed       = data.moveSpeed;
    _data.perceptionRange   = data.detectRange;
    _data.visionConeAngle   = data.visionAngle;
    _data.attackRange       = data.attackRange;
    _data.attackCooldown    = data.attackCooldown;
    _data.attackDamage      = data.attackDamage;
    _data.idleBehavior      = IdleBehavior.Wander;
    _data.canFlee           = false;
    _data.canAllyAlert      = true;
    _data.allyAlertRange    = 15f;
    // ...
}
```

---

## 十、ZombieSpawner — 两阶段刷新系统

### 架构

```
ZombieSpawner (单例)
├── Phase A: SpawnInitial(chunkId, isNight)
│      Chunk 首次预热时调用（ChunkManager.Stage2）
│      按 ZoneSpawnProfile 随机刷 initialMin~initialMax 只
│      权重随机选择僵尸类型
│
├── Phase B: TryRespawn(chunkId, isNight)
│      每 spawnCheckInterval(10s) 检查 Loaded 状态 Chunk
│      当前存活 < maxPerChunk 时补刷
│      每轮最多补 maxPerRespawnBatch 只
│
├── 死亡回收: OnZombieDied → 对应Chunk Alive-1 → 解锁预算
│
└── 卸载重置: OnChunkUnloaded → 清除该Chunk所有数据

昼夜倍率: nightMultiplier=2x (18:00-6:00 为夜间)
```

### 刷新点选取策略

```csharp
// ZombieSpawner.cs 第186-224行
bool TryGetSpawnPoint(int chunkId, float minDist, out Vector3 result)
{
    // 1. 在Chunk内随机取点
    // 2. NavMesh.SamplePosition 保证可导航
    // 3. 距离玩家 ≥ minSpawnDistFromPlayer (默认25m)
    // 4. 不在玩家视野前方 (dot(playerForward, toSpawn) ≤ 0.3)
    // 5. 最多尝试10次
}
```

### ZoneSpawnProfile 配置

```csharp
// _Game/Config/ZoneSpawnProfile.cs
public class ZoneSpawnProfile : ScriptableObject
{
    public string zoneName;
    public int initialMin = 2;         // 初始最少
    public int initialMax = 5;         // 初始最多
    public int maxPerChunk = 10;       // Chunk上限

    public float respawnInterval = 120f;     // 补刷冷却
    public float minSpawnDistFromPlayer = 25f; // 距玩家最小距离
    public int maxPerRespawnBatch = 2;       // 每轮补刷上限

    public ZombieTypeWeight[] typeWeights;   // 类型权重
}
```

### 僵尸生成方法

```csharp
// ZombieSpawner.cs 第259-276行
GameObject SpawnZombie(Vector3 pos, ZombieData data, int chunkId)
{
    var go = new GameObject($"Zombie_{data.zombieName}");
    go.transform.position = pos;
    go.AddComponent<NavMeshAgent>();
    go.AddComponent<ZombieStateMachine>();  // AIAgent子类
    go.AddComponent<DamageableZombie>();
    go.AddComponent<ZombieController>().Initialize(data);
    BuildBody(go, data);  // 圆柱体身体
    ChunkManager.Instance.RegisterZombie(stateMachine, chunkId);
    return go;
}
```

---

## 十一、ThreatSystem 威胁系统

AIAgent 不直接选择目标。所有目标选择由 ThreatSystem 集中管理：

### 数据结构

```
_table: Dictionary<sourceId, Dictionary<targetId, ThreatEntry>>
  sourceId = 谁持有仇恨
  targetId = 恨谁（0 = sentinel，用于Sound/AllyAlert无具体目标）
  
ThreatEntry {
    sourceInstanceId,      // 谁恨
    targetInstanceId,      // 恨谁 (nullable)
    value,                 // 裸威胁值
    timestamp,             // 最近更新时间
    type,                  // Damage/Visual/Sound/AllyDamage/Territory/Aggression
    lastKnownPos,          // 目标最后已知位置
    WeightedValue          // = value × ThreatTypeConfig.GetWeight(type)
}
```

### 威胁类型权重与衰减

| ThreatType | 权重 | 衰减速率/秒 | 半衰期 | 来源 |
|-----------|:--:|:--:|:--:|------|
| Damage | 1.5 | 0.5 | ~20s | `ThreatReportEvent`（伤害→反击方） |
| Aggression | 1.3 | 0.5 | ~20s | 主动挑衅 |
| AllyDamage | 1.0 | 0.3 | ~33s | `AllyAlertEvent` |
| Visual | 0.8 | 2.0 | ~5s | `PerceptionTick.CanSee` |
| Territory | 0.6 | 3.0 | ~3s | 区域入侵 |
| Sound | 0.5 | 5.0 | ~2s | `NoiseEvent` |

### 关键查询 API

```csharp
GetTopTarget(sourceId)       → 威胁值最高的 targetId（含sentinel=0的声音）
GetTopRealTarget(sourceId)   → 威胁值最高的真实 targetId（≠0，即有明确对象）
GetAllThreats(sourceId)      → 所有威胁条目，按 WeightedValue 降序
HasThreat(sourceId)          → 是否有任何威胁
GetLastKnownPosition(s,t)    → 目标最后已知位置
GetThreatValue(s,t)          → 查询特定目标的裸威胁值
```

---

## 十二、AIAgentData 完整字段

```csharp
// _Game/Config/AIAgentData.cs
public class AIAgentData : ScriptableObject
{
    [Header("身份")]
    public FactionType factionType;          // 阵营
    public string displayName;               // 显示名

    [Header("移动")]
    public float idleSpeed = 2f;             // Idle/Investigate/Return速度
    public float combatSpeed = 4f;           // Combat追击速度
    public float fleeSpeed = 6f;             // Flee逃跑速度
    public float angularSpeed = 120f;        // 旋转角速度

    [Header("活动范围")]
    public bool hasActivityConstraint = false; // 是否有活动范围约束
    public float activityRadius = 50f;        // 允许活动半径

    [Header("感知")]
    public float perceptionRange = 30f;       // 感知扫描半径
    public float perceptionTickRate = 0.5f;   // 扫描间隔(秒) (预留字段)
    [Range(0,360)] public float visionConeAngle = 120f;       // 全视野锥角
    [Range(0,90)] public float centralVisionAngle = 30f;      // 中心视野(100%)
    [Range(0,90)] public float peripheralVisionAngle = 60f;   // 周边视野(概率)
    [Range(0,1)] public float peripheralRecognitionChance = 0.5f; // 周边识别率
    public float motionThreshold = 1.5f;       // 运动检测阈值(m/s)
    public LayerMask obstacleMask;             // 视线遮挡层
    public List<SoundTag> reactsToSoundTags;   // 响应的声音标签

    [Header("攻击")]
    public float attackRange = 1.5f;           // 攻击范围
    public float attackCooldown = 1f;          // 攻击冷却
    public float attackDamage = 10f;           // 攻击伤害
    public float targetReassessInterval = 2f;  // 目标重评估间隔
    [Range(1f,3f)] public float targetSwitchThreshold = 1.3f; // 切换门槛

    [Header("行为")]
    public IdleBehavior idleBehavior = Wander;  // 待机行为
    public float wanderRadius = 10f;            // 游荡半径
    public float wanderInterval = 5f;           // 游荡换目标间隔
    public List<Vector3> patrolRoute;           // 巡逻路径
    public float investigateTime = 2f;          // 调查停留时间
    public float targetLostTimeout = 5f;        // 丢失目标超时

    [Header("逃跑")]
    public bool canFlee = true;                 // 是否可逃跑
    [Range(0,1)] public float fleeThreshold = 0.3f;         // 血量逃跑阈值
    [Range(0,1)] public float fleeRecoveryThreshold = 0.5f; // 恢复阈值
    public float fleeDistance = 30f;            // 逃跑距离
    public float safeDistance = 40f;            // 安全距离

    [Header("阵营扩散")]
    public bool canAllyAlert = false;           // 发现敌人时通知友军
    public float allyAlertRange = 15f;          // 通知范围半径

    [Header("特殊")]
    public bool canOpenDoors = false;           // 可开门（预留）
    public bool canHordeSpread = false;         // 尸潮扩散（预留）
    public float hordeSpreadRange = 15f;        // 尸潮扩散范围（预留）
}
```

---

## 十三、初始化顺序与防御

### 启动链

```
Bootstrap.Awake()
  → FactionSystem 实例化 + LoadDefaults
  → ThreatSystem 实例化 (订阅 ThreatReportEvent + EntityDeathEvent)

实体 Instantiate:
  → FactionComponent.OnEnable
      → ThreatSystem.Instance.RegisterEntity(id, faction)
      → InstanceRegistry.Register(id, transform)
  → AIAgent.Awake
      → GetComponent<NavMeshAgent>()
      → GetComponent<FactionComponent>()
      → _tickGroup = 分帧组号
  → AIAgent.OnEnable
      → NavMeshAgent 存在性检查（无则禁用+报错）
      → 订阅 NoiseEvent + AllyAlertEvent
      → TransitionTo(Idle)
  → AIAgent.Start (一帧延迟)
      → FactionComponent 存在性检查
      → ThreatSystem.Instance 存在性检查
      → IsEntityRegistered 检查（重试一次）
      → 任一失败 → enabled = false + 报错
```

### 启动防御代码

```csharp
// AIAgent.cs 第87-136行
protected virtual void OnEnable()
{
    if (_agent == null)
    {
        Debug.LogError($"[AIAgent] {name} 缺少 NavMeshAgent！AI 已禁用。", this);
        enabled = false;
        return;
    }
    EventBus.Subscribe<NoiseEvent>(OnHeardSound);
    EventBus.Subscribe<AllyAlertEvent>(OnAllyAlert);
    _activityCenter = transform.position;
    TransitionTo(AIState.Idle);
}

IEnumerator Start()
{
    yield return null; // 等一帧让FactionComponent.OnEnable先跑完

    if (_factionComp == null)
    {
        Debug.LogError($"[AIAgent] {name} 缺少 FactionComponent！AI 已禁用。", this);
        enabled = false;
        yield break;
    }

    if (ThreatSystem.Instance == null)
    {
        Debug.LogError($"[AIAgent] {name} ThreatSystem 未就绪！AI 已禁用。", this);
        enabled = false;
        yield break;
    }

    if (!ThreatSystem.Instance.IsEntityRegistered(gameObject.GetInstanceID()))
    {
        yield return null;  // 再等一帧
        if (!ThreatSystem.Instance.IsEntityRegistered(gameObject.GetInstanceID()))
        {
            Debug.LogError($"[AIAgent] {name} 注册失败，AI 已禁用。", this);
            enabled = false;
        }
    }
}
```

---

## 十四、ZombieStateMachine 死亡处理

```csharp
// ZombieStateMachine.cs 第141-158行
public void Die()
{
    if (_isDead) return;
    _isDead = true;

    enabled = false;  // 触发 OnDisable() → 自动取消事件订阅

    if (_agent != null && _agent.enabled)
    {
        _agent.isStopped = true;
        _agent.enabled = false;
    }

    var col = GetComponent<Collider>();
    if (col != null) col.enabled = false;
}
```

设为 `enabled = false` 后，Unity 不再调用 Update/LateUpdate，状态机完全停止。`OnDisable()` 自动取消 `NoiseEvent` 和 `AllyAlertEvent` 订阅。

---

## 十五、Gizmos 调试

选中 AIAgent 时 Scene 视图显示（仅在 `UNITY_EDITOR`）：

- **青色圆环**：活动范围 `activityRadius`
- **黄色线**：视觉锥边界 (全锥半角)
- **绿色线**：中心视野区 (centralVisionAngle半角)
- **红色线+球**：当前目标连线
- **绿色线**：NavMesh 导航路径
- **红色半透明圆环**：攻击范围
- **头顶标签**：`displayName [CurrentState]` + 威胁计数

```csharp
// AIAgent.cs 第761-811行 OnDrawGizmosSelected
```

---

## 十六、文件清单

| 文件 | 行数 | 职责 |
|------|:--:|------|
| `_Game/Systems/AI/AIAgent.cs` | 813 | AIAgent 抽象基类：五状态机+寻路+感知扫描+声音反应+卡住恢复+Combat目标切换+AllyAlert |
| `_Game/Config/AIAgentData.cs` | 78 | AIAgent 配置 ScriptableObject |
| `_Game/Systems/Zombie/ZombieStateMachine.cs` | 188 | 僵尸AI：继承AIAgent，覆写CanSee/DoAttack/Die |
| `_Game/Systems/Zombie/ZombieSpawner.cs` | 315 | 两阶段刷新：Phase A初始+Phase B补刷，昼夜倍率，Chunk管理 |
| `_Game/Config/ZombieData.cs` | 37 | 僵尸类型模板SO |
| `_Game/Config/ZoneSpawnProfile.cs` | ~35 | 地段刷怪配置SO |
| `_Game/Config/FactionType.cs` | 16 | 7类阵营枚举 |
| `_Game/Systems/Threat/ThreatSystem.cs` | 421 | 威胁仇恨表：AddThreat/GetTopTarget/衰减/GC |
| `_Game/Systems/Threat/ThreatEntry.cs` | 22 | 威胁条目struct |
| `_Game/Systems/Threat/FactionSystem.cs` | 217 | 阵营关系管理：Ally/Hostile/Neutral + 运行时变更 + 存档 |
| `_Game/Systems/Threat/FactionComponent.cs` | — | 实体阵营标记组件 |
| `_Game/Systems/Threat/InstanceRegistry.cs` | — | InstanceID→Transform 全局注册表 |
| `_Game/Config/ThreatType.cs` | ~50 | ThreatType枚举 + ThreatTypeConfig权重/衰减 |
| `_Game/Core/GameEvents.cs` | (片段) | NoiseEvent + AllyAlertEvent + ThreatReportEvent + EntityDeathEvent 等 |

---

## 十七、版本历史

| 日期 | 内容 |
|------|------|
| 2026-06-01 | v1.0 — 初始设计 |
| 2026-06-01 | v2.0 — 完整修订：六状态机+混合驱动+三档视觉+导航恢复+Combat目标切换+Idle行为枚举+AllyAlert通用化+感知分帧 |
| 2026-06-22 | v3.0 — **实现文档**：基于实际代码重写，反映当前已实现模块（AIAgent基类/ZombieStateMachine/ZombieSpawner/ThreatSystem/FactionSystem），标注未实现模块（Stunned/Bandit/Survivor/Military/AIBot），添加代码行号引用和文件清单 |
