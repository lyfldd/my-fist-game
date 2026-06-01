# 通用NPC寻路系统（AIAgent）

> **日期**: 2026-06-01 · **状态**: 📋 设计定稿 · **依赖**: ThreatSystem + FactionSystem + DecibelSystem + NavMesh
> **关联**: [[仇恨系统]] · [[僵尸AI设计]]

---

## 一、问题分析

### 现状

```
ZombieStateMachine:
  ├── FSM五状态 (Idle/Wander/Chase/Attack/Return)
  ├── NavMeshAgent 寻路
  ├── 视觉锥 + Linecast 检测
  ├── 听觉 OnSoundHeard → 走向声源
  └── 群体连锁扩散

AIBotCombat:
  ├── 距离优先选目标
  └── 没有"走向声源"的能力
```

僵尸和 AIBot 都有寻路攻击逻辑，但各写各的。未来加入黑恶势力、友好NPC、军方后，每个都复制一套 = 灾难。

### 目标

抽象一个 `AIAgent` 基类，提供：
- 统一的**寻路 + 目标选择 + 声音反应 + 状态机骨架**
- 子类只需配置差异化参数，不重写核心逻辑
- 像 `DecibelSystem` 一样：挂上组件 + 配SO → 就能用

---

## 二、架构

```
┌─────────────────────────────────────────────────┐
│                   AIAgent                         │
│              (抽象基类 · MonoBehaviour)            │
│                                                   │
│  内置能力（所有子类自动获得）：                      │
│  ├── NavMeshAgent 寻路                             │
│  ├── 从 ThreatSystem 取最高仇恨目标                 │
│  ├── 主动感知扫描循环（分帧）                       │
│  ├── 订阅 DecibelSystem → 听到声音加威胁           │
│  ├── 阵营归属（FactionType）                       │
│  ├── 活动范围约束（activityCenter + activityRadius）│
│  ├── 导航卡住检测 + 自动恢复                       │
│  └── 死亡/销毁自动注销                              │
│                                                   │
│  子类覆写（虚方法）：                               │
│  ├── CanSee(target) → bool     感知方式            │
│  ├── DoAttack(target)          攻击行为            │
│  ├── OnStateEnter/Exit(state)  状态钩子            │
│  └── GetHomePosition()         家园位置            │
│                                                   │
│  子类配置（AIAgentData SO）：                       │
│  ├── 身份/移动/感知/攻击/行为参数                  │
│  └── idleBehavior 枚举 + fleeThreshold            │
└─────────────────────────────────────────────────┘
```

---

## 三、完整状态机

### 3.1 六状态总览

```
                    ┌──────────────────────────────────┐
                    │              Idle                 │
                    │  Wander/StandBy/Patrol/Follow     │
                    └────┬──────────────┬───────────────┘
                         │              │
              威胁出现    │              │ 威胁出现
              +看不到     │              │ +直接看到
                         │              │
                         ▼              ▼
                    ┌─────────┐    ┌─────────┐
         ┌─────────│Investigate│   │ Combat  │──────────┐
         │         └────┬─────┘   └────┬─────┘          │
         │              │              │                │
         │   到达+没看到 │   丢失视线   │ 目标死亡/威胁清零
         │              │   超时       │                │
         │              ▼              ▼                │
         │         ┌──────────────────────┐             │
         │         │        Idle          │◄────────────┘
         │         └──────────────────────┘
         │
         │  ┌─────────────────────────────────────┐
         │  │        全局中断（事件驱动）            │
         │  │                                       │
         │  │  health < fleeThreshold → Flee       │
         │  │  被击晕/爆炸         → Stunned       │
         │  │  死亡                 → Dead         │
         │  │  distance > radius   → Return       │
         │  └─────────────────────────────────────┘
         │
         ▼
    ┌─────────┐     ┌─────────┐     ┌─────────┐
    │  Flee   │────→│  Idle   │     │  Dead   │
    │         │     │         │     │  (停)   │
    └────┬────┘     └─────────┘     └─────────┘
         │
         │ distance > activityRadius
         ▼
    ┌─────────┐     ┌─────────┐
    │ Return  │────→│  Idle   │
    │         │     │         │
    └────┬────┘     └─────────┘
         │
         │ 途中遇到敌人
         ▼
    ┌─────────┐
    │ Combat  │
    └─────────┘
```

### 3.2 状态与中断

**全局中断**（不是状态，从任何状态立刻触发，优先级最高）：

| 中断 | 触发 | 行为 | 恢复 |
|------|------|------|------|
| **Dead** | 死亡事件 | 停状态机 + 播死亡动画 + 不再恢复 | — |
| **Stunned** | 重击/爆炸/电击事件 | 硬直倒计时 + ResetPath + 不可动不可攻 | 计时结束 → Idle |

**六状态**（按优先级）：

| 优先级 | 状态 | 行为 |
|:--:|------|------|
| 1 | **Flee** | 远离威胁跑 → safeDistance → Idle |
| 2 | **Return** | 走回 activityCenter → Idle |
| 3 | **Combat** | 追击 + 攻击循环 |
| 4 | **Investigate** | 走向威胁来源位置 |
| 5 | **Idle** | Wander/StandBy/Patrol/Follow |

### 3.3 状态互斥规则

**全局中断（Dead/Stunned）**：从任何状态立刻覆盖，不受锁限制。

**普通状态切换**：每帧最多执行一次 `TransitionTo`，同一帧多个切换请求按优先级排队：

```csharp
AIState? _pendingTransition = null;

public void TransitionTo(AIState newState)
{
    if (currentState == AIState.Dead) return;  // Dead 不可恢复
    
    // 全局中断立即执行
    if (newState == AIState.Dead || newState == AIState.Stunned)
    {
        ExecuteTransition(newState);
        _pendingTransition = null;
        return;
    }
    
    // 普通状态：已有排队请求 → 取优先级更高的
    if (_pendingTransition.HasValue)
        _pendingTransition = PriorityHigher(_pendingTransition.Value, newState);
    else
        _pendingTransition = newState;
}

// Update 末尾统一执行
void LateUpdate()
{
    if (_pendingTransition.HasValue)
    {
        ExecuteTransition(_pendingTransition.Value);
        _pendingTransition = null;
    }
}

AIState PriorityHigher(AIState a, AIState b)
{
    int PriorityOf(AIState s) => s switch
    {
        AIState.Flee => 4,
        AIState.Return => 3,
        AIState.Combat => 2,
        AIState.Investigate => 1,
        _ => 0
    };
    return PriorityOf(a) >= PriorityOf(b) ? a : b;
}
```

**同一帧最多执行一次切换**，多个请求按优先级排队，全局中断（Dead/Stunned）不受限制。

---

## 四、完整触发条件

### 4.1 驱动模式：混合模式

| 触发类型 | 模式 | 说明 |
|------|------|------|
| 威胁出现 → 脱离 Idle | **近实时轮询** | PerceptionScan 每 0.5s 扫描，Sound 事件立即反应。最坏延迟 0.5s，单机游戏完全可接受 |
| 状态内部退出条件 | **每帧 Tick** | 丢失视线、到达目的地、超时 |
| 全局中断 | **事件驱动** | 受伤/死亡/击晕，EventBus 同步触发，从任何状态立刻切 |

### 4.2 Idle

```
进入条件：
  ├── Start() 默认状态
  ├── ThreatSystem 中没有任何威胁（主动或自然衰减归零）
  ├── Investigate → 到达位置 + investigateTime 耗尽 + 仍看不到 + 无其他威胁
  ├── Combat → 目标死亡 / 威胁清零
  ├── Flee → 跑到 safeDistance + 威胁消失
  ├── Return → 到达 activityCenter (distance < 1m)
  ├── Stunned → 计时结束
  └── Return 途中遇到敌人但打完 → 目标死亡/清零 → Idle

退出条件 → 跳转：
  ├── [→ Combat]      存在有 targetInstanceId 的威胁（真实目标）
  │                    AND CanSee(GetTopRealTarget()) == true
  │                    (Sound/AllyAlert 的 null 目标不触发 Combat，走 Investigate 靠近)
  ├── [→ Investigate] 存在任何威胁（含 Sound/AllyAlert）且不满足 Combat 条件
  │                    (声音威胁/友军受伤/远处发现/被伤害但看不到攻击者)
  ├── [→ Flee]        health < fleeThreshold AND ThreatSystem.HasThreat  ← 全局中断
  └── [→ Return]      distanceToActivityCenter > activityRadius AND hasActivityConstraint
```

```csharp
// ThreatSystem 新增查询
public ThreatEntry? GetTopRealTarget(int sourceId)
    => GetAllThreats(sourceId).FirstOrDefault(e => e.targetInstanceId.HasValue);
```

恢复机制：
  Wander: 打断时保存当前目标点 → 回 Idle 时先完成上次 Wander 目标，再随机新目标
  Patrol: 打断时保存当前巡逻索引 → 回 Idle 时从同一索引继续（不重头开始）
```

**Idle 打断恢复实现**：

```csharp
Vector3? _resumeWanderTarget;
int _resumePatrolIndex = -1;

void OnStateExit(AIState oldState)
{
    // 被打断时保存进度
    if (oldState == AIState.Idle && data.idleBehavior == IdleBehavior.Wander)
        _resumeWanderTarget = agent.destination;
    else if (oldState == AIState.Idle && data.idleBehavior == IdleBehavior.Patrol)
        _resumePatrolIndex = _currentPatrolIndex;
}

void OnStateEnter(AIState newState)
{
    if (newState == AIState.Idle)
    {
        if (data.idleBehavior == IdleBehavior.Wander && _resumeWanderTarget.HasValue)
        {
            agent.SetDestination(_resumeWanderTarget.Value);
            _resumeWanderTarget = null;
            return;  // 先完成上次 Wander
        }
        if (data.idleBehavior == IdleBehavior.Patrol && _resumePatrolIndex >= 0)
        {
            _currentPatrolIndex = _resumePatrolIndex;
            _resumePatrolIndex = -1;
            // 继续从该索引巡逻
        }
    }
}
```

### 4.3 Investigate

```
进入条件：
  ├── 威胁出现但看不到目标
  └── Combat → lostSightTimer > targetLostTimeout AND 威胁未清零

内部 Tick:
  ├── NavMeshAgent 走向 ThreatSystem.GetLastKnownPosition(topThreat)
  ├── 每 0.5s 主动扫描 CanSee()
  └── 到达 lastKnownPosition → 停留 investigateTime 秒，加速扫描频率(每 0.2s)

退出条件 → 跳转：
  ├── [→ Combat]      CanSee(topThreat) == true
  │                    AND topThreat.type ∈ {Damage, Visual, Aggression}
  │                    AND distance ≤ attackRange × 2
  │                    (必须看到 + 是直接威胁类型 + 距离够近才打。
  │                     Sound/AllyDamage 即使看到了也不切 Combat——
  │                     继续在 Investigate 里靠近，直到距离够了再切)
  ├── [→ Idle]        到达 + investigateTime 耗尽 + 仍看不到 + 无其他威胁
  ├── [→ Flee]        health < fleeThreshold                            ← 全局中断
  └── [→ Return]      distanceToActivityCenter > activityRadius
```

### 4.4 Combat

```
进入条件：
  ├── CanSee(威胁目标) == true（从 Idle/Investigate 直接进入）
  └── Return 途中遇到敌人（先打）

内部 Tick:
  ├── 每 2s 重查 ThreatSystem.GetTopTarget():
  │     新目标威胁值 > currentTargetThreat × 1.3 → 切换目标
  ├── distance > attackRange → NavMeshAgent 追击 currentTarget
  ├── distance ≤ attackRange → agent.ResetPath() + 攻击循环
  │     Time.time - lastAttackTime ≥ attackCooldown → DoAttack(currentTarget)
  └── 每 0.5s CanSee(currentTarget):
       看不到 → lostSightTimer += 0.5

退出条件 → 跳转：
  ├── [→ Idle]        目标死亡 OR ThreatSystem 威胁清零
  ├── [→ Investigate]  lostSightTimer > targetLostTimeout AND 威胁未清零
  ├── [→ Flee]        health < fleeThreshold                            ← 全局中断
  └── [→ Return]      distanceToActivityCenter > activityRadius
```

### 4.5 Flee

```
进入条件：
  ├── health < fleeThreshold AND ThreatSystem.HasThreat（全局中断）
  └── 特定实体覆写（中立动物看到任何威胁就跑）

内部 Tick:
  ├── 计算逃离方向 = (myPos - 最大威胁来源位置).normalized
  └── NavMeshAgent 往逃离方向跑，目标距离 = fleeDistance

退出条件 → 跳转：
  ├── [→ Idle]    distanceToTopThreat > safeDistance AND 不再有威胁
  ├── [→ Idle]    health > fleeRecoveryThreshold（治疗恢复）
  └── [→ Return]  distanceToActivityCenter > activityRadius
```

### 4.6 Return

```
进入条件：
  ├── distanceToActivityCenter > activityRadius
      从 Idle/Investigate/Combat/Flee 都可能触发
  └── hasActivityConstraint == true 才触发

内部 Tick:
  └── NavMeshAgent 走向 activityCenter

退出条件 → 跳转：
  ├── [→ Idle]    distance < 1m（到达）
  └── [→ Combat]  途中 CanSee 敌人（先打完，但距离约束仍然检查→打完继续走）
```

### 4.7 Stunned（全局中断·非状态）

```
触发：
  └── 事件驱动：接收重击/爆炸/电击（全局中断）

内部：
  ├── agent.ResetPath() 停住
  ├── 不可移动、不可攻击
  └── 计时器倒计时

恢复：
  └── 计时结束 → Idle（重新评估局面）
```

### Dead（全局中断·非状态）

```
触发：
  └── 事件驱动：收到致命伤害

内部：
  ├── 状态机停掉，不再 Tick
  ├── 播死亡动画/物理
  └── 不再恢复到任何状态
```

### 4.8 转移优先级

当同时满足多个条件时：

```
全局中断（最高）：
  Dead > Stunned

状态内部（按优先级）：
  Flee > Return > Combat > Investigate > Idle
```

高优先级可中断低优先级。Flee 不会被 Return 中断——先跑命再说。Stunned 和 Dead 不是状态，从任何状态立刻触发。

---

## 五、感知系统

### 5.1 主动扫描循环（替代被动 CanSee）

```
每 perceptionTickRate 秒（默认 0.5s）:
  ├── 1. Physics.OverlapSphereNonAlloc(pos, perceptionRange, entityLayer)
  ├── 2. 过滤：只处理有 IDamageable 或 FactionComponent 的实体
  ├── 3. 对每个实体调 CanSee(entity) ──→ 看得见 → ThreatSystem.AddThreat(Visual)
  ├── 4. 对每个实体调 CanHear(entity) ──→ 持续声音 → ThreatSystem.AddThreat(Sound)
  └── 5. 检查：ThreatSystem.GetTopTarget() 是否变了 → 触发状态评估
```

### 5.2 三档视觉

```csharp
protected virtual VisionLevel GetVisionLevel(Transform target)
{
    float angle = Vector3.Angle(transform.forward, dirToTarget);
    
    if (angle <= data.centralVisionAngle)      // 默认 30°
        return VisionLevel.Central;            // 100% 识别
    else if (angle <= data.peripheralVisionAngle) // 默认 60°
        return VisionLevel.Peripheral;         // 概率识别（僵尸50%，恶徒80%）
    else if (angle <= data.visionConeAngle * 0.5f) // 默认 90°（半锥角）
        return VisionLevel.PeripheralMotion;   // 仅检测移动目标
    else
        return VisionLevel.Blind;              // 看不见
}
```

| 实体 | 中心 | 周边 | 周边运动 | 全锥角 |
|------|:--:|:--:|:--:|:--:|
| 僵尸 | 30°/100% | 60°/50% | 120°/仅移动 | 120° |
| 恶徒 | 30°/100% | 60°/80% | 90°/仅移动 | 90° |
| 幸存者 | 全向/100% | — | — | 360° |
| AIBot | 全向/100% | — | — | 360° |

### 5.3 CanSee 虚方法

```csharp
protected virtual bool CanSee(Transform target)
{
    // 1. 距离
    if (Vector3.Distance(eyesPosition, target.position) > data.perceptionRange)
        return false;
    
    // 2. 视线遮蔽（Linecast）
    if (Physics.Linecast(eyesPosition, target.position, data.obstacleMask))
        return false;
    
    // 3. 视觉等级（子类可覆写 GetVisionLevel）
    VisionLevel level = GetVisionLevel(target);
    switch (level)
    {
        case VisionLevel.Central:
            return true;   // 100%
        case VisionLevel.Peripheral:
            return Random.value < data.peripheralRecognitionChance;  // 50%-80%
        case VisionLevel.PeripheralMotion:
            // 仅检测移动目标
            return target.GetComponent<Rigidbody>()?.velocity.magnitude > data.motionThreshold;
        default:
            return false;
    }
}
```

---

## 六、导航卡住恢复

```csharp
// Combat/Investigate 状态中每 2s 检测
void CheckStuck()
{
    if (agent.velocity.magnitude < 0.1f && agent.hasPath && agent.remainingDistance > 1f)
    {
        _stuckCount++;
        
        if (_stuckCount == 1)
        {
            // 第一次卡：左右各45°射线检测找通路
            Vector3 detour = FindDetour();
            if (detour != Vector3.zero)
                agent.SetDestination(detour);
        }
        else if (_stuckCount == 2)
        {
            // 第二次：退后1m重新寻路
            agent.ResetPath();
            transform.position -= transform.forward * 1f;
            agent.SetDestination(currentDestination);
        }
        else
        {
            // 连续3次：放弃，回 Idle
            agent.ResetPath();
            _stuckCount = 0;
            TransitionTo(AIState.Idle);
        }
    }
    else if (agent.velocity.magnitude > 0.5f)
    {
        _stuckCount = 0;  // 正常移动中，重置
    }
}
```

---

## 七、攻击系统

### 7.1 Combat 目标切换

```csharp
// Combat Tick 中每 2s 执行
void ReassessTarget()
{
    int? newTopId = ThreatSystem.Instance.GetTopTarget(GetInstanceID());
    if (newTopId == null || newTopId == currentTargetId) return;
    
    float currentThreat = ThreatSystem.Instance.GetThreatValue(GetInstanceID(), currentTargetId);
    float newThreat = ThreatSystem.Instance.GetThreatValue(GetInstanceID(), newTopId.Value);
    
    // 新目标威胁值至少高 30% 才切换
    if (newThreat <= currentThreat * data.targetSwitchThreshold) return;
    
    // ═══ 可达性检查 ═══
    Transform newTarget = InstanceRegistry.GetTransform(newTopId.Value);
    if (newTarget == null) return;
    
    NavMeshPath path = new NavMeshPath();
    if (agent.CalculatePath(newTarget.position, path) && path.status == NavMeshPathStatus.PathComplete)
    {
        float pathLength = GetPathLength(path);
        float straightDist = Vector3.Distance(transform.position, newTarget.position);
        
        // 路径长度超过直线 3 倍 → 视为不可达（墙后/T型障碍）
        if (pathLength > straightDist * 3f) return;
    }
    else
    {
        return;  // 完全不可达
    }
    
    // 通过所有检查 → 切换
    currentTargetId = newTopId.Value;
    currentTarget = newTarget;
    lostSightTimer = 0f;
}

float GetPathLength(NavMeshPath path)
{
    float len = 0f;
    for (int i = 1; i < path.corners.Length; i++)
        len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
    return len;
}
```

### 7.2 攻击循环

```csharp
// Combat Tick 每帧
void CombatUpdate()
{
    if (currentTarget == null) { TransitionTo(AIState.Idle); return; }
    
    float dist = Vector3.Distance(transform.position, currentTarget.position);
    
    if (dist > data.attackRange)
    {
        // 追击
        agent.SetDestination(currentTarget.position);
        agent.speed = data.combatSpeed;
        CheckStuck();
    }
    else
    {
        // 进入攻击范围
        agent.ResetPath();  // 停下
        FaceTarget(currentTarget);
        
        if (Time.time - lastAttackTime >= data.attackCooldown)
        {
            DoAttack(currentTarget.gameObject);
            lastAttackTime = Time.time;
        }
    }
}
```

---

## 八、声音反应

> **注**：声音的距离衰减在 DecibelSystem（NoiseEvent 发射方）处理。`NoiseEvent.decibels` 已是衰减后的值（`sourceDecibels - attenuation × distance`），AIAgent 直接使用即可，不做二次衰减。

```csharp
// OnEnable 订阅
void OnEnable()
{
    EventBus.On<NoiseEvent>(OnHeardSound);
    EventBus.On<AllyAlertEvent>(OnAllyAlert);
    // 注：RegisterEntity 由 FactionComponent.OnEnable 负责，AIAgent 不重复注册（见 §十五）
}

void OnDisable()
{
    EventBus.Off<NoiseEvent>(OnHeardSound);
    EventBus.Off<AllyAlertEvent>(OnAllyAlert);
}

void OnHeardSound(NoiseEvent e)
{
    float dist = Vector3.Distance(transform.position, e.position);
    if (dist > e.radius) return;
    if (!data.reactsToSoundTags.Contains(e.tag)) return;
    
    ThreatSystem.Instance.AddThreat(
        GetInstanceID(),
        e.sourceInstanceId,
        e.decibels,
        ThreatType.Sound,
        e.position
    );
    // → 威胁出现 → OnThreatAppeared 事件 → Idle 立刻切到 Investigate/Combat
}
```

---

## 九、群体扩散（AllyAlert 事件通用化）

僵尸的群体连锁扩散现在是**所有阵营的通用能力**：

```csharp
// 任何 AIAgent 进入 Combat 时自动发射
void OnCombatEnter()
{
    EventBus.Emit(new AllyAlertEvent {
        sourceId = GetInstanceID(),
        position = transform.position,
        radius = data.allyAlertRange,      // 僵尸 15m，军方 30m，恶徒 20m
        faction = data.factionType
    });
}

// 同阵营 AIAgent 收到
void OnAllyAlert(AllyAlertEvent e)
{
    if (!FactionSystem.Instance.IsAlly(data.factionType, e.faction)) return;
    if (Vector3.Distance(transform.position, e.position) > e.radius) return;
    
    ThreatSystem.Instance.AddThreat(
        GetInstanceID(),
        null,  // 没有具体目标（ThreatEntry.targetInstanceId = null），只知道位置
        20f,
        ThreatType.AllyDamage,
        e.position
    );
    // → 进入 Investigate
    // AIAgent InvestigateTick: targetInstanceId 为 null → 走向 lastKnownPos 观察
}
```

---

## 十、AIAgentData (ScriptableObject) 完整版

```csharp
[CreateAssetMenu(menuName = "Game/AI Agent Data")]
public class AIAgentData : ScriptableObject
{
    [Header("身份")]
    public FactionType factionType;
    public string displayName;
    
    [Header("移动")]
    public float idleSpeed = 2f;
    public float combatSpeed = 4f;
    public float fleeSpeed = 6f;
    public float angularSpeed = 120f;
    
    [Header("活动范围")]
    public bool hasActivityConstraint = false;
    public float activityRadius = 50f;       // 超出→Return
    
    [Header("感知")]
    public float perceptionRange = 30f;
    public float perceptionTickRate = 0.5f;   // 扫描间隔
    [Range(0, 360)] public float visionConeAngle = 120f;
    [Range(0, 90)] public float centralVisionAngle = 30f;
    [Range(0, 90)] public float peripheralVisionAngle = 60f;
    [Range(0, 1)] public float peripheralRecognitionChance = 0.5f;
    public float motionThreshold = 1.5f;      // 周边运动检测阈值(m/s)
    public LayerMask obstacleMask;
    public List<SoundTag> reactsToSoundTags;
    
    [Header("攻击")]
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    public float attackDamage = 10f;
    public float targetReassessInterval = 2f;  // 目标重评估间隔
    public float targetSwitchThreshold = 1.3f; // 新目标威胁×1.3才切换
    
    [Header("行为")]
    public IdleBehavior idleBehavior = IdleBehavior.Wander;
    public float wanderRadius = 10f;
    public float wanderInterval = 5f;          // 每N秒换一个游荡目标点
    public List<Vector3> patrolRoute;          // Patrol用
    public float investigateTime = 2f;
    public float targetLostTimeout = 5f;
    
    [Header("逃跑")]
    public bool canFlee = true;
    [Range(0, 1)] public float fleeThreshold = 0.3f;   // 血量低于30%逃跑
    [Range(0, 1)] public float fleeRecoveryThreshold = 0.5f;
    public float fleeDistance = 30f;
    public float safeDistance = 40f;
    
    [Header("阵营扩散")]
    public bool canAllyAlert = false;          // 发现敌人时通知友军
    public float allyAlertRange = 15f;         // 通知范围
    
    [Header("特殊")]
    public bool canOpenDoors = false;          // 预留
}

public enum IdleBehavior
{
    Wander,        // 随机游荡
    StandBy,       // 站定待命
    Patrol,        // 按 patrolRoute 巡逻
    FollowEntity   // 跟随某实体
}
```

---

## 十一、各实体差异化配置

| | 僵尸 | 恶徒 | 友好NPC | AIBot | 军方 | 中立动物 |
|------|:--:|:--:|:--:|:--:|:--:|:--:|
| factionType | Zombie | Bandit | Survivor | AIBot | Military | Neutral |
| idleSpeed | 1.5 | 3 | 2.5 | 0 | 3 | 3 |
| combatSpeed | 2.5 | 4.5 | 3.5 | 8 | 5 | 6 |
| fleeSpeed | 3 | 6 | 5 | 10 | 7 | 8 |
| activityRadius | 50 | 100 | 80 | 无约束 | 120 | 60 |
| perceptionRange | 30 | 40 | 25 | 50 | 45 | 25 |
| visionConeAngle | 120° | 90° | 360° | 360° | 90° | 180° |
| centralVision | 30° | 30° | 360° | 360° | 30° | 45° |
| peripheralRecog | 50% | 80% | 100% | 100% | 70% | 40% |
| attackRange | 1.5 | 15 | 1.5 | 15 | 20 | 1.5 |
| attackCooldown | 1s | 2s | 1.2s | 0.5s | 1s | 1.5s |
| idleBehavior | Wander | StandBy | Follow | StandBy | Patrol | Wander |
| canFlee | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| fleeThreshold | — | 0.3 | 0.4 | — | — | 0.6 |
| hasActivityConstr | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ |
| canAllyAlert | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ |
| allyAlertRange | 15 | 20 | 25 | — | 30 | — |

---

## 十二、ZombieStateMachine 改造方案

```
改前:
  ZombieStateMachine : MonoBehaviour
    ├── 自己维护状态机 (Idle/Wander/Chase/Attack/Return)
    ├── 自己选目标 (视觉锥+听觉+距离优先)
    ├── 自己处理声音
    └── 自己处理群体扩散

改后:
  ZombieStateMachine : AIAgent
    ├── 基类提供完整状态机（Idle/Investigate/Combat/Flee/Return/Stunned）
    ├── 基类提供目标选择（ThreatSystem.GetTopTarget）
    ├── 基类提供主动感知扫描（三档视觉+声音反应）
    ├── 基类提供导航卡住恢复
    ├── 基类提供 AllyAlert 扩散
    ├── 覆写 CanSee() → 默认三档视觉即可
    ├── 覆写 DoAttack() → 近战 OverlapSphere
    ├── 覆写 OnStateEnter/Exit → 群体扩散钩子（基类已处理，只需配置）
    └── 保留 ZombieData SO（外观/血量/速度——迁移到 AIAgentData）
```

**改造原则：不丢功能，只换实现方式。** 僵尸的最终行为和现在完全一致。

---

## 十三、使用示例

```csharp
// 创建新NPC类型极简
public class BanditAI : AIAgent
{
    // 几乎不需要写代码！大部分行为由 AIAgentData SO 决定
    
    protected override bool CanSee(Transform target)
    {
        if (!base.CanSee(target)) return false;
        // 额外：恶徒检查掩体
        return !IsBehindCover(target);
    }
    
    protected override void DoAttack(GameObject target)
    {
        Shoot(target);  // 恶徒用枪
    }
    
    protected override Vector3? GetHomePosition()
    {
        return banditCampPosition;  // SO配置的营地位置
    }
}
```

---

## 十四、文件清单

### 新建

| 文件 | 职责 |
|------|------|
| `Config/AIAgentData.cs` | AIAgent 配置 SO |
| `Systems/AI/AIAgent.cs` | 抽象基类：六状态机+寻路+感知扫描+攻击虚方法+声音反应+卡住恢复+范围约束 |
| `Systems/AI/AIAgentPerception.cs` | 感知模块：三档视觉+主动扫描循环 |
| `Core/GameEvents.cs` | +AllyAlertEvent |
| `Editor/CreateAIAgentData.cs` | 一键生成6类AIAgentData资产 |

### 改造

| 文件 | 改动 |
|------|------|
| `ZombieStateMachine.cs` | 改为继承 AIAgent，删除通用逻辑，保留僵尸特有逻辑 |
| `ZombieAwarenessSystem.cs` | 废弃（功能合并到 AIAgent 基类） |
| `ZombieSpawner.cs` | 刷怪时预制体挂 ZombieStateMachine（继承AIAgent） |

---

## 十五、初始化顺序

```
Bootstrap.Awake():
  1. FactionSystem 实例化 + LoadDefaults(factionData[])
  2. ThreatSystem 实例化 (Awake中订阅 ThreatReportEvent + EntityDeathEvent)
  
  ── 以上完成后才创建实体 ──
  
  3. 实体 Instantiate (Prefab 已挂 FactionComponent + AIAgent子类)
     ├─ FactionComponent.OnEnable:
     │    ├─ ThreatSystem.Instance.RegisterEntity(id, faction)
     │    └─ InstanceRegistry.Register(id, transform)
     │
     └─ AIAgent.OnEnable (确保在 FactionComponent 之后):
          ├─ agent = GetComponent<NavMeshAgent>()
          ├─ 读 data (AIAgentData SO)
          ├─ 订阅 NoiseEvent / AllyAlertEvent
          ├─ activityCenter = transform.position  (默认出生点)
          └─ TransitionTo(AIState.Idle)
```

**关键原则**：
- AIAgent **不调** RegisterEntity（FactionComponent 已做），只读 ThreatSystem
- 如果 FactionComponent 和 AIAgent 在同一 Prefab 上，利用 Script Execution Order 确保 FactionComponent 先 Awake
- Bootstrap 负责保证 ThreatSystem 在实体之前就绪

**启动防御**：AIAgent.Start 检查必要条件，任一失败 → 报错并禁用 AI（不会静默失效）：

```csharp
void Start()
{
    var factionComp = GetComponent<FactionComponent>();
    if (factionComp == null)
    {
        Debug.LogError($"[AIAgent] {name} 缺少 FactionComponent！AI 已禁用。", this);
        enabled = false;
        return;
    }
    
    if (ThreatSystem.Instance == null)
    {
        Debug.LogError($"[AIAgent] {name} ThreatSystem 未就绪！AI 已禁用。", this);
        enabled = false;
        return;
    }
    
    if (!ThreatSystem.Instance.IsEntityRegistered(GetInstanceID()))
    {
        Debug.LogWarning($"[AIAgent] {name} 未注册，下一帧重试。", this);
        StartCoroutine(RetryRegistration());
    }
}

IEnumerator RetryRegistration()
{
    yield return null;  // 等一帧让 FactionComponent.OnEnable 先跑完
    if (!ThreatSystem.Instance.IsEntityRegistered(GetInstanceID()))
    {
        Debug.LogError($"[AIAgent] {name} 注册失败，AI 已禁用。", this);
        enabled = false;
    }
}
```

100 个 AIAgent 每 0.5s OverlapSphere + Linecast = 性能炸弹。

**分帧分散**：

```csharp
// AIAgent 基类自动管理
static int _tickGroupCounter = 0;
static int _tickGroupCount = 10;  // 分10组

void OnEnable()
{
    _tickGroup = _tickGroupCounter % _tickGroupCount;
    _tickGroupCounter++;
}

void Update()
{
    if (Time.frameCount % _tickGroupCount == _tickGroup)
        PerceptionScan();  // 每10帧执行一次（分帧）
    StateTick();           // 状态机每帧都跑（不受分帧影响）
}
```

| 参数 | 值 |
|------|:--:|
| 分组数 | 10 |
| 每帧 Tick 数 | 10 个 (100÷10) |
| 每实体扫描间隔 | 0.16s (10帧×16ms) |
| 每帧开销 | 10×OverlapSphere |

---

## 十六、Gizmos 调试可视化

```csharp
// AIAgent 基类
void OnDrawGizmosSelected()
{
    if (data == null) return;
    
    Vector3 pos = transform.position;
    Vector3 eyes = eyesPosition;  // 子类覆写，默认 transform.position + Vector3.up * 1.5f
    
    // ═══ 活动范围圈 ═══
    Gizmos.color = new Color(0, 1, 1, 0.15f);
    DrawWireCircle(activityCenter, data.activityRadius, 32);
    
    // ═══ 视觉锥 ═══
    Gizmos.color = new Color(1, 1, 0, 0.3f);
    Vector3 forward = transform.forward * data.perceptionRange;
    float halfAngle = data.visionConeAngle * 0.5f;
    // 左边界
    Gizmos.DrawLine(eyes, eyes + Quaternion.Euler(0, -halfAngle, 0) * forward);
    // 右边界
    Gizmos.DrawLine(eyes, eyes + Quaternion.Euler(0, halfAngle, 0) * forward);
    // 弧线
    DrawWireArc(eyes, transform.forward, halfAngle, data.perceptionRange, 16);
    
    // 中心视觉区 (30°)
    Gizmos.color = new Color(0, 1, 0, 0.2f);
    float centralHalf = data.centralVisionAngle * 0.5f;
    Gizmos.DrawLine(eyes, eyes + Quaternion.Euler(0, -centralHalf, 0) * forward);
    Gizmos.DrawLine(eyes, eyes + Quaternion.Euler(0, centralHalf, 0) * forward);
    
    // ═══ 当前目标连线 ═══
    if (currentTarget != null)
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(eyes, currentTarget.position);
        Gizmos.DrawWireSphere(currentTarget.position, 0.3f);
    }
    
    // ═══ 导航路径 ═══
    if (agent != null && agent.hasPath && agent.path.corners.Length > 1)
    {
        Gizmos.color = Color.green;
        for (int i = 0; i < agent.path.corners.Length - 1; i++)
            Gizmos.DrawLine(agent.path.corners[i], agent.path.corners[i + 1]);
        
        // 路径点小球
        Gizmos.color = Color.white;
        foreach (var corner in agent.path.corners)
            Gizmos.DrawWireSphere(corner, 0.1f);
    }
    
    // ═══ 攻击范围 ═══
    Gizmos.color = new Color(1, 0, 0, 0.2f);
    DrawWireCircle(pos, data.attackRange, 16);
}

// ═══ Scene 视图标签 ═══
void OnDrawGizmos()
{
    if (data == null) return;
    
    // 头顶状态标签
    Vector3 labelPos = transform.position + Vector3.up * 2.5f;
    string label = $"{data.displayName}\n[{currentState}]\nThreats: {ThreatSystem.Instance?.GetAllThreats(GetInstanceID()).Count ?? 0}";
    
    #if UNITY_EDITOR
    UnityEditor.Handles.Label(labelPos, label, new GUIStyle 
    { 
        normal = new GUIStyleState { textColor = GetStateColor(currentState) },
        fontSize = 11,
        alignment = TextAnchor.MiddleCenter
    });
    #endif
}

Color GetStateColor(AIState state) => state switch
{
    AIState.Idle        => Color.green,
    AIState.Investigate => Color.yellow,
    AIState.Combat      => Color.red,
    AIState.Flee        => Color.magenta,
    AIState.Return      => Color.cyan,
    _                   => Color.white
};
```

---

## 十七、版本历史

| 日期 | 内容 |
|------|------|
| 2026-06-01 | v1.0 — 初始设计 |
| 2026-06-01 | v2.0 — 完整修订：六状态机+完整触发条件+混合驱动+三档视觉+导航恢复+Combat目标切换+Idle行为枚举+活动范围约束+AllyAlert通用化+感知分帧 |
| 2026-06-01 | v2.1 — 审查修正：Investigate→Combat加门控(类型+距离)；Dead/Stunned改为全局中断非状态；初始化顺序文档化；AllyAlert用int?替代magic number；Gizmos调试可视化 |
| 2026-06-01 | v2.2 — 补充修正：状态机互斥锁+同帧排队；AIAgent启动校验；Combat目标切换NavMesh可达性；Wander/Patrol打断恢复；声音衰减职责加注 |
| 2026-06-01 | v2.3 — Bug修复：分帧return改为if不跳过StateTick；AIAgent OnEnable删重复RegisterEntity+补OnDisable；Idle区分真实目标(GetTopRealTarget)防Sound死锁；措辞"事件驱动→近实时轮询" |
