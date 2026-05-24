## 📋 4.0 前置初始计划（动工前定稿）

> **模块**: L4 组装 · **步骤**: 第1步 · **名称**: ChunkManager + ContainerRegistry + RefreshHub — 三级区块管理与通用世界刷新
> **日期**: 2026-05-19

### 一、本步骤核心开发目标

将游戏中所有场景对象纳入 **80m 区块管理**，实现三级区块体系（已加载 / 预加载 / 未加载），玩家周围按需激活、异步预热、远距离休眠。同时改造 WorldContainer 为两态懒加载，并建立可扩展的 RefreshHub 刷新中

---

### 二、关联联动模块

| 关联度 | 系统 | 数据互通方式 |
|--------|------|-------------|
| **强** | WorldContainer | 改造 Awake→懒加载，状态机 3态→2态，通过 ContainerRegistry 获取容器 |
| **强** | WorldGen/WorldData | 复用 40m 基础单元网格，Chunk 坐标 = WorldData 网格坐标 / 2 |
| **中** | 僵尸 AI | 僵尸按坐标分配到 Chunk，三级体系管理（未来） |
| **中** | WorldItem (地面物品) | 地面物品分配到 Chunk，远距离 SetActive(false) |
| **弱** | 建造系统 | 建筑分配到 Chunk，GameObject 持久化，不额外序列化 |
| **弱** | 交通工具 | 车辆不随 Chunk 休眠（数量少，全场景常驻 Active） |

**数据流向**:
```
ChunkManager → RefreshHub.OnChunkLoad(chunkId, day)
                 → ContainerRefreshHandler  → 容器刷新+重建
                 → TreeRefreshHandler       → 树木再生（未来）
                 → BerryRefreshHandler      → 浆果重生长（未来）
ChunkManager → RefreshHub.OnChunkUnload(chunkId)
                 → 倒序调用各 Handler 清理
ChunkManager → 僵尸/建筑: SetActive 控制
```

**EventBus 事件**:
```
ChunkEnterLoadEvent   { int chunkId, Vector2 center }   // 进入已加载层
ChunkExitLoadEvent    { int chunkId }                     // 退出已加载层
ChunkEnterPreloadEvent { int chunkId }                    // 进入预加载层
ChunkUnloadedEvent    { int chunkId }                     // 完全卸载
ContainerRefreshedEvent { int containerId }               // 容器批量刷新完成
```

---

### 三、整体实现流程拆解

#### 3.1 初始化流程

```
场景加载 / WorldGen 完成后:
  ChunkManager.Initialize()
    → 100 个 Chunk 父节点 (10×10 网格): 命名 Chunk_{x}_{z}
    → 遍历场景中所有标记了 ChunkManaged 的对象:
        WorldContainer / WorldItem / 僵尸 / 建筑 / 地面物品
    → 按坐标计算 chunkId = ((int)(x/80), (int)(z/80))
    → 将对象 SetParent 到对应 Chunk
    → 各系统建立索引:
        ContainerRegistry.Register(containerId, chunkId, profile)
        ZombieSystem 预留 Register(zombieId, chunkId)  // 未来
        WorldItemSystem 标记 isPlayerDropped 字段

  RefreshHub.Initialize()
    → Register(new ContainerRefreshHandler())
    → // 未来: Register(new TreeRefreshHandler())
```

#### 3.2 运行时三级切换

```
每帧或每2秒（玩家移动 ≥ 20m 或 2秒间隔）:

  计算玩家所在 Chunk
  计算动态预热带宽度 = BasePreloadChunks + GetDynamicExtra(speed)

  遍历 100 个 Chunk:

    距离 ≤ 加载半径:
      if 状态 != Loaded:
        → 状态切为 Loaded
        → SetActive(true)
        → RefreshHub.OnChunkLoad(chunkId, currentDay)
        → EventBus.Publish(ChunkEnterLoadEvent)
        → 如果预热未完成，强制完成剩余步骤（同步）

    加载半径 < 距离 ≤ 加载半径 + 预热带宽:
      if 状态 == Unloaded:
        → 状态切为 Preloaded
        → 加入预热队列（分帧处理）
        → EventBus.Publish(ChunkEnterPreloadEvent)
      if 状态 == Loaded:
        → 状态切为 Preloaded
        → 僵尸 SetActive(false) + 状态重置（保留死亡掉落容器 Record）
        → 容器: 释放 InventoryContainer，保留 ContainerRecord
        → 建筑: SetActive(false)

    距离 > 加载半径 + 预热带宽:
      if 状态 != Unloaded:
        → 状态切为 Unloaded
        → RefreshHub.OnChunkUnload(chunkId)
            ContainerRefreshHandler: 释放 InventoryContainer，保留 Record
            僵尸: 丢弃全部 Record（死亡掉落容器除外，转为独立 Record）
        → SetActive(false) 整个 Chunk
        → EventBus.Publish(ChunkUnloadedEvent)
```

#### 3.3 异步预热队列

```
预热入口: Chunk 从 Unloaded 进入 Preloaded 层

预热队列:
  Queue<int> preloadQueue
  每帧处理 2 个预热步骤（preloadsPerFrame = 2）

状态机（每个 Chunk 内部）:
  Stage 0: SetActive(true) 建筑 + 容器 GameObject
  Stage 1: ContainerRefreshHandler 重建已搜过容器的 InventoryContainer
  Stage 2: 僵尸系统重生僵尸（未来）+ 掉落容器重建
  Stage 3: Done → 标记 ready，出队

  预热完成 → Chunk 在预加载层等待 → 玩家接近 → 已加载层瞬间完整

高速减载逻辑:
  玩家速度 > 20 m/s → 预热时跳过 Stage 2（僵尸+地面物品）
  玩家速度 > 40 m/s → 预热仅 Stage 0+1（建筑+容器）
  玩家急刹车（speed < 5 且上帧 > 20）→ 已加载层内补激活跳过的内容
```

#### 3.4 WorldContainer 交互流程（改造后）

```
WorldContainer.OnInteract(interactor):
  container = ContainerRegistry.GetOrCreate(containerId)

  if containerRecord.isOpened:
    → 直接打开容器窗口（瞬间，无读条）
  else:
    → 搜索读条协程
    → 进度条走完
    → 生成掉落（LootTable 随机）
    → ContainerRegistry.MarkOpened(containerId, currentDay)
    → 打开容器窗口

ContainerWindow关闭:
  → 不改变状态（不再有 Empty）
  → 空容器 = Opened + placedItems.Count == 0
  → 永远可交互
```

#### 3.5 容器刷新流程

```
RefreshHub.OnChunkLoad(chunkId, currentDay):
  ContainerRefreshHandler.OnChunkLoad(chunkId, day):
    遍历 ContainerRegistry 中该 Chunk 的所有 ContainerRecord:
      if isOpened && (currentDay - openedGameDay > profile.refreshCooldownDays)
      && profile.refreshEnabled:
        → 清空 InventoryContainer（重随机掉落）
        → 重置为 Unopened
        → 释放 InventoryContainer 引用
      elif isOpened:
        → 重建 InventoryContainer（保留玩家上次留下的物品）
      // else: Unopened → 什么都不做（保持懒加载）

规则:
  - 容器内所有物品（含玩家放入的）→ 全部刷新清空
  - 玩家自建存储容器 → 不刷新（未来处理，通过 refreshEnabled=false）
  - 尸体 → refreshEnabled=false，搜过永不重置
```

---

### 四、核心代码 / 架构设计思路

#### 4.1 新增文件

| 文件 | 说明 |
|------|------|
| `Systems/WorldGen/ChunkManager.cs` | MonoBehaviour 单例，每帧/定时检查玩家位置，驱动三级切换 + 预热队列 |
| `Systems/WorldGen/RuntimeChunk.cs` | `struct RuntimeChunk { int chunkId; Vector2Int gridPos; ChunkState state; PreloadStage preloadStage; List<int> containerIds; List<int> zombieIds; List<int> buildingIds; }` |
| `Systems/WorldGen/ChunkState.cs` | `enum ChunkState { Unloaded, Preloaded, Loaded }` |
| `Systems/WorldGen/ChunkQuality.cs` | `enum ChunkQuality { Low, Medium, High }` + 静态方法 `GetLoadRadius/GetPreloadRadius/GetUnloadRadius` |
| `Systems/WorldGen/RefreshHub.cs` | 刷新调度中心：`List<IRefreshHandler>` + `OnChunkLoad/OnChunkUnload` |
| `Systems/WorldGen/IRefreshHandler.cs` | 接口：`string handlerName` + `OnChunkLoad(chunkId, day)` + `OnChunkUnload(chunkId)` |
| `Systems/WorldContainer/ContainerRefreshHandler.cs` | 实现 IRefreshHandler，打理容器刷新+重建逻辑 |
| `Systems/WorldContainer/ContainerRegistry.cs` | MonoBehaviour 单例，`Dictionary<int, ContainerRecord>`，懒加载核心 |
| `Systems/WorldContainer/ContainerRecord.cs` | `class ContainerRecord { int containerId; bool isOpened; float openedGameDay; int chunkId; ContainerLootProfile profile; InventoryContainer container; }` |

#### 4.2 改造文件

| 文件 | 改造内容 |
|------|---------|
| `WorldContainer.cs` | 删除 Awake 中 `_container` 创建；删除 `ContainerState.Empty`；三态改两态（Unopened/Opened）；新增 `int containerId`；OnInteract 改为通过 `ContainerRegistry.GetOrCreate(containerId)` 获取容器；移除 `OnContainerWindowClosed` 中的 Empty 转换；移除 ContainerState 枚举（迁至 ContainerRecord） |
| `ContainerLootProfile.cs` | +`float refreshCooldownDays = 7` + `bool refreshEnabled = true` |
| `GameConstants.cs` | +`RUNTIME_CHUNK_SIZE = 80f` + `CHUNK_GRID_SIZE = 10` + `CHUNK_CHECK_DISTANCE = 20f` + `CHUNK_CHECK_INTERVAL = 2f` + `PRELOAD_STEPS_PER_FRAME = 2` + `PLAYER_DROP_PER_CHUNK_CAP = 400` + `SPEED_THRESHOLD_NORMAL = 10f` + `SPEED_THRESHOLD_FAST = 20f` + `SPEED_THRESHOLD_EXTREME = 40f` |

> **命名冲突处理**: GameConstants 已有 `CHUNK_SIZE = 32f`（旧 Mesh 用），新常量用 `RUNTIME_CHUNK_SIZE = 80f` 区分。旧常量保持不变。

#### 4.3 关键接口

```csharp
// ── 刷新处理器接口 ──
public interface IRefreshHandler {
    string handlerName { get; }
    void OnChunkLoad(int chunkId, float currentGameDay);
    void OnChunkUnload(int chunkId);
}

// ── 容器注册中心 ──
class ContainerRegistry : MonoBehaviour {
    Dictionary<int, ContainerRecord> _records; // 全量记录（初始化时创建）

    InventoryContainer GetOrCreate(int containerId);  // 懒加载
    void MarkOpened(int containerId, float gameDay);
    List<ContainerRecord> GetChunkRecords(int chunkId);
    bool IsOpened(int containerId);
}

// ── 刷新调度中心 ──
class RefreshHub : MonoBehaviour {
    List<IRefreshHandler> _handlers;

    public void Register(IRefreshHandler handler);
    public void OnChunkLoad(int chunkId, float currentDay);
    public void OnChunkUnload(int chunkId);
}

// ── 区块管理器 ──
class ChunkManager : MonoBehaviour {
    RuntimeChunk[] _chunks;          // 100 个
    Queue<int> _preloadQueue;
    ChunkQuality _quality;
    int _currentPlayerChunk;

    void CheckChunks();
    void LoadChunk(int chunkId);
    void PreloadChunk(int chunkId);  // 加入预热队列
    void UnloadChunk(int chunkId);
    bool ProcessOnePreloadStep(int chunkId);
    int GetDynamicPreloadExtra(float playerSpeed);

    // 静态辅助
    int GetChunkId(Vector3 worldPos);
    Vector2 GetChunkCenter(int chunkId);
}
```

---

### 五、解耦与可扩展预留设计

#### 5.1 分层架构

```
管理层: ChunkManager       — 坐标映射 + 激活控制 + 预热队列
调度层: RefreshHub         — 刷新 Handler 注册 + 遍历调度
数据层: ContainerRegistry  — 容器记录 + 懒加载 + 生命周期
        ZombieSystem       — 僵尸三级数据管理（未来）
        WorldItemSystem    — 地面物品持久化上限（未来）
业务层: WorldContainer     — 交互逻辑 + 搜索读条
        IRefreshHandler    — 各种刷新实现
```

#### 5.2 扩展预留

| 预留点 | 方式 | 用途 |
|--------|------|------|
| `IRefreshHandler` 接口 | 新增类型 = 实现接口 + RefreshHub.Register | 树木/浆果/野外物资/矿物等 |
| `RuntimeChunk.zombieIds` | `List<int>` | 僵尸系统管理自己的 Record |
| `RuntimeChunk.buildingIds` | `List<int>` | 建造系统管理，预留 |
| `RuntimeChunk.worldItemIds` | `List<int>` | 地表物品系统管理，预留 |
| `ContainerRecord.customData` | `Dictionary<string, object>` | 后续扩展字段，不影响现有序列化 |
| `ChunkQuality` 枚举 | 新增档位 = 加枚举值 + 对应半径 | 未来可加 Ultra 档 |
| `RefreshHub 独立于 ChunkManager` | Handler 不依赖 ChunkManager 实现细节 | 未来可单独测试刷新逻辑 |
| 新增刷新类型流程 | 3 步：Profile SO → Handler 实现 → Register | 不改 ChunkManager/ContainerRegistry |

---

### 六、玩家产出世界物品的持久化方案

#### 6.1 各类对象处理策略

| 对象类型 | 持久化方式 | 原因 |
|----------|-----------|------|
| **建造物 (PlacedStructure)** | GameObject 持久化，SetActive(false) 不销毁 | 本身有组件+Collider，Transform 保留 |
| **玩家丢弃物品 (WorldItem)** | GameObject 持久化 + 单 Chunk 400 上限 | 正常游玩 ~50 件，500 件才 ~1MB |
| **容器内物品** | 区块刷新时全部清空重随机 | 玩家自建存储容器除外（future） |
| **车辆** | 不随 Chunk 休眠，全场景常驻 | 数量少（5-10 辆），无性能影响 |

#### 6.2 地面物品上限兜底

```
单 Chunk 内 WorldItem 上限: 400 件
超出处理: FIFO，最旧的玩家丢弃物品 Destroy
天然生成的不计入上限（WorldGen 固定数量）
isPlayerDropped=true 的物品参与计数，天然生成的跳过
```

---

### 七、性能分级体系

#### 7.1 档位定义

```csharp
enum ChunkQuality { Low, Medium, High }

// 加载半径（Chunk 数）
Low    → 1 chunk  (80m)   // ~9 个活跃 Chunk
Medium → 2 chunks (160m)  // ~25 个活跃 Chunk ← 默认
High   → 3 chunks (240m)  // ~49 个活跃 Chunk

// 预热带基准 = 加载半径 + 1
// 卸载半径 = 预热带基准 + 1
```

| 档位 | 加载 | 预热带基准 | 卸载 |
|------|:---:|:--------:|:---:|
| 低 | 80m (1) | 160m (2) | 240m (3) |
| 中 | 160m (2) | 240m (3) | 320m (4) |
| 高 | 240m (3) | 320m (4) | 400m (5) |

#### 7.2 动态预热带（根据速度扩展）

```csharp
int GetDynamicPreloadExtra(float speed) {
    if (speed < 10f)  return 0;  // 步行
    if (speed < 20f)  return 1;  // 普通车
    if (speed < 40f)  return 2;  // 高速车
    return 3;                     // 极速
}
```

| 场景 | 预热带 | 卸载 |
|------|:-----:|:---:|
| 低配步行 | 160m | 240m |
| 中配步行 | 240m | 320m |
| 中配普通车 | 320m | 400m |
| 中配极速车 | 400m | 480m |
| 高配极速车 | 480m | 560m |

#### 7.3 高速减载策略

| 速度 | 预热内容 | 原因 |
|------|---------|------|
| 步行 (≤10 m/s) | 全量：建筑+容器+僵尸+地面物品 | 时间充裕 |
| 普通车 (≤20 m/s) | 全量 | 预热带够宽 |
| 高速车 (≤40 m/s) | 精简：建筑+容器，跳过僵尸+地面物品 | 飙车不停车打怪 |
| 极速 (>40 m/s) | 最简：仅建筑+容器 | 预热带窄，优先保建筑可见 |

> 高速减载内容在玩家减速时补激活：`speed < 5 && prevSpeed > 20` → 已加载层内补全跳过内容。

---

### 八、预期功能边界

**做什么**:
- ChunkManager 三级区块体系，100 Chunk 覆盖 800m×800m
- 异步预热队列，分帧激活，不卡主线程
- ContainerRegistry 懒加载：未搜过 = 零 InventoryContainer 内存
- 区块重载时按冷却天数批量刷新容器
- WorldContainer 两态：Unopened / Opened，永可交互
- RefreshHub 可扩展刷新接口
- 性能三档 + 动态预热带 + 高速减载

**不做什么**:
- 不做异步/协程加载（一期同步 SetActive + 预热队列已满足）
- 不做 LOD/渲染剔除（阶段4）
- 不做对象池（阶段3）
- 不做存档序列化（仅内存，存档延后）
- 不处理僵尸 AI 的 Chunk 归属（仅预留 zombieIds 列表）
- 不做地面物品刷新（仅保留一次，不重新生成）
- 不做车辆 Chunk 管理（全场景常驻）

---

### 九、调试与测试验收标准

| 验收项 | 标准 |
|--------|------|
| 玩家移动 | 移动到新 Chunk → 新区块内对象可见，离开 → 超出卸载半径后隐藏 |
| 三级切换 | Hierarchy 中可见 Chunk 父节点 on/off 跟随玩家位置 |
| 容器搜索 | 第一次打开 → 读条 → 生成掉落；第二次打开 → 瞬间显示 |
| 容器刷新 | 搜过容器 → 手动设 openedGameDay 为 7 天前 → Chunk 卸载再加载 → 容器回到 Unopened |
| 容器存物 | Opened 容器内放物品 → 卸载 → 未过冷却时加载 → 物品还在 |
| 容器全刷新 | 上述条件下过冷却 → 玩家物品也被清空，全家随机重来 |
| 内存 | 200 个容器场景：活跃 Chunk 内 ≤ 30 个有 InventoryContainer，其他未搜过零开销 |
| 预热不卡顿 | Hierarchy Profiler 确认 Awake/OnEnable 分散在多帧，单帧 ≤ 2 个预热步骤 |
| 高速减载 | 60 m/s 移动时主线程不卡，减速后僵尸/地面物品补激活 |
| 倒退兼容 | 旧 WorldContainer 场景对象改造后仍正常交互 |
| 性能分档 | 切换 Low/Med/High → 加载半径变化 → Hierarchy 中活跃 Chunk 数量对应变化 |
| 动态预热带 | 上车后预热带自动扩展 → 下车后恢复基准 |

---

### 十、新增刷新类型的未来流程

以后加"浆果刷新"只需 3 步，不动底层：

```
1. 创建 BerryProfile SO（含 refreshCooldownDays 等）
2. 创建 BerryRefreshHandler : IRefreshHandler
3. RefreshHub.Instance.Register(new BerryRefreshHandler())
→ 完成
```

---

### 十一、文件清单（更新后）

| 文件 | 状态 | 说明 |
|------|:----:|------|
| `Systems/WorldGen/ChunkManager.cs` | 新建 | 区块加载/卸载、坐标→Chunk映射、玩家位置追踪、预热队列 |
| `Systems/WorldGen/RuntimeChunk.cs` | 新建 | Chunk 数据结构（容器/僵尸/建筑 ID 列表） |
| `Systems/WorldGen/ChunkState.cs` | 新建 | `enum ChunkState { Unloaded, Preloaded, Loaded }` |
| `Systems/WorldGen/ChunkQuality.cs` | 新建 | `enum ChunkQuality` + 半径计算静态方法 |
| `Systems/WorldGen/RefreshHub.cs` | 新建 | 刷新调度中心：Handler 注册 + OnChunkLoad/Unload 遍历 |
| `Systems/WorldGen/IRefreshHandler.cs` | 新建 | 刷新处理器接口 |
| `Systems/WorldContainer/ContainerRefreshHandler.cs` | 新建 | 实现 IRefreshHandler，打理容器刷新+重建逻辑 |
| `Systems/WorldContainer/ContainerRegistry.cs` | 新建 | 容器注册中心、懒加载、GetOrCreate |
| `Systems/WorldContainer/ContainerRecord.cs` | 新建 | 容器记录（纯数据 class） |
| `Systems/WorldContainer/WorldContainer.cs` | 改造 | 三态→两态、Awake 移除容器创建、懒加载调用 Registry |
| `Config/ContainerLootProfile.cs` | 改造 | +`refreshCooldownDays`、+`refreshEnabled` |
| `Core/GameConstants.cs` | 改造 | +9 常量（RUNTIME_CHUNK_SIZE / CHUNK_GRID_SIZE / ...） |

> **Git 提交**: `ChunkManager(L4): Phase1 完成 — 三级区块+预热队列+RefreshHub+容器两态+性能分档+引导启动+鼠标朝向+角色模型目录`

---

### 4.0 后置定稿总结（开发完成后回顾）

#### 计划 vs 实际

| 对比维度 | 前置计划 | 实际完成 | 差异 |
|----------|---------|---------|:--:|
| ChunkManager | 三级区块 + 预热队列 + 性能分档 | 完全按计划实现 | ✅ |
| ContainerRegistry | 懒加载 + GetOrCreate + 两态追踪 | 完全按计划实现 | ✅ |
| RefreshHub | Handler 注册 + OnChunkLoad/Unload | 完全按计划实现 | ✅ |
| ContainerRefreshHandler | 容器冷却刷新 + 重建 | 完全按计划实现 | ✅ |
| WorldContainer 改造 | 三态→两态 | 完全按计划实现 | ✅ |
| 鼠标朝向 | 未在 4.0 初始计划中 | 额外完成：MouseGroundProjector + PlayerController 平滑旋转 | ➕ |
| UI 解耦 | 未在 4.0 初始计划中 | 额外完成：TopLeftHUD 上下文切换 | ➕ |
| 角色模型目录 | 未在 4.0 初始计划中 | 额外完成：Characters 目录结构 | ➕ |
| 僵尸 AI 集成 | 仅预留 zombieIds | RuntimeChunk + zombieInstances 列表，实际联动已在 4.1 完成 | ➕ |
| 高速减载 | 3 档速度阈值 | 按计划实现 | ✅ |
| 引导启动 | AutoRegisterContainers | 按计划实现 | ✅ |

#### 新建文件（实际）

| 文件 | 说明 |
|------|------|
| `Systems/WorldGen/ChunkManager.cs` | 三级区块管理器（单例，Awake 初始化保证在 Start 前就绪） |
| `Systems/WorldGen/ChunkState.cs` | `enum ChunkState { Unloaded, Preloaded, Loaded }` |
| `Systems/WorldGen/ChunkQuality.cs` | `enum ChunkQuality` + `ChunkQualityConfig` 半径计算 |
| `Systems/WorldGen/RuntimeChunk.cs` | Chunk 数据结构 + `PreloadStage` 枚举 + `zombieInstances` 列表 |
| `Systems/WorldGen/IRefreshHandler.cs` | 刷新处理器接口 |
| `Systems/WorldGen/RefreshHub.cs` | 刷新调度中心（单例） |
| `Systems/WorldContainer/ContainerRegistry.cs` | 容器注册中心（单例，懒加载 GetOrCreate） |
| `Systems/WorldContainer/ContainerRecord.cs` | 容器记录纯数据类 |
| `Systems/WorldContainer/ContainerRefreshHandler.cs` | 容器刷新处理器（实现 IRefreshHandler） |
| `Systems/PlayerInput/MouseGroundProjector.cs` | 鼠标→地面投影共享组件 |

#### 改造文件（实际）

| 文件 | 改造内容 |
|------|---------|
| `GameConstants.cs` | +9 Chunk常量 (RUNTIME_CHUNK_SIZE=80, CHUNK_GRID_SIZE=10, 预热/速度阈值) |
| `ContainerLootProfile.cs` | +refreshCooldownDays, +refreshEnabled |
| `WorldContainer.cs` | 三态→两态, Awake移除容器创建, 通过 ContainerRegistry 懒加载 |
| `PlayerController.cs` | +鼠标朝向旋转 |
| `WeaponAiming.cs` / `GhostPreview.cs` | 改用 MouseGroundProjector |

#### 经验教训

- **Awake vs Start 顺序问题**：ChunkManager.Initialize() 最初在 Start()，导致其他系统的 Start() 可能在 _chunks 就绪前调用 RegisterZombie。移到 Awake() 解决了所有依赖此数组的系统初始化顺序。
- **预留接口设计有效**：`IRefreshHandler` + `RefreshHub.Register` 模式让容器刷新零耦合接入，未来新刷新类型只需实现接口即可。

---

