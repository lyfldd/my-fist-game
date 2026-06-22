# ChunkManager 与容器系统 — 实现文档

> **最后更新**: 2026-06-22
> **状态**: 已实现

---

## 一、系统概览

将游戏中所有场景对象纳入 **80m 区块管理**，实现三级区块体系（Loaded / Preloaded / Unloaded），玩家周围按需激活、异步预热、远距离休眠。同时容器系统采用**两态懒加载**设计，通过 RefreshHub 实现可扩展的世界刷新。

### 1.1 核心文件清单

| 文件 | 说明 |
|------|------|
| `Systems/WorldGen/ChunkManager.cs` | 三级区块管理器，驱动激活/休眠/预热队列 |
| `Systems/WorldGen/RuntimeChunk.cs` | Chunk 数据结构 + PreloadStage 枚举 |
| `Systems/WorldGen/ChunkState.cs` | `enum ChunkState { Unloaded, Preloaded, Loaded }` |
| `Systems/WorldGen/ChunkQuality.cs` | `enum ChunkQuality` + `ChunkQualityConfig` 半径计算 |
| `Systems/WorldGen/RefreshHub.cs` | 刷新调度中心：Handler 注册 + OnChunkLoad/Unload |
| `Systems/WorldGen/IRefreshHandler.cs` | 刷新处理器接口 |
| `Systems/WorldContainer/ContainerRegistry.cs` | 容器注册中心，全量字典 + 懒加载 |
| `Systems/WorldContainer/ContainerRecord.cs` | 容器记录纯数据类 |
| `Systems/WorldContainer/ContainerRefreshHandler.cs` | 实现 IRefreshHandler，容器冷却刷新 |
| `Systems/WorldContainer/WorldContainer.cs` | 世界容器组件，两态 + 搜索读条 + UI |
| `Systems/WorldContainer/ContainerWindowUI.cs` | 容器格子窗口 UI |
| `Systems/WorldContainer/WorldItem.cs` | 地面物品组件（Billboard Quad） |
| `Systems/WorldContainer/GroundItemInteract.cs` | 地面物品交互接口（E/F 拾取） |
| `Config/ContainerLootProfile.cs` | 容器 Loot 配置资产（ScriptableObject） |
| `Core/GameConstants.cs` | 常量：RUNTIME_CHUNK_SIZE=80, CHUNK_GRID_SIZE=10 等 |

---

## 二、Chunk 三级体系

### 2.1 世界空间划分

```
世界范围: 800m × 800m
区块大小: 80m × 80m (RUNTIME_CHUNK_SIZE)
网格布局: 10 × 10 = 100 个 Chunk (CHUNK_GRID_SIZE)

坐标映射:
  chunkId = z * GRID_SIZE + x
  x = Mathf.FloorToInt(worldPos.x / 80)
  z = Mathf.FloorToInt(worldPos.z / 80)
```

### 2.2 三级状态

```csharp
enum ChunkState {
    Unloaded,   // 完全休眠: chunkParent.SetActive(false)
    Preloaded,  // 预热中或预热完成: SetActive(true), 异步激活子内容
    Loaded      // 完全激活: 僵尸激活, 容器刷新完成
}
```

每个 Chunk 内部有一个分步预热状态机：

```csharp
enum PreloadStage {
    Stage0_ActivateGameObjects,  // 激活子 GameObject（已在 EnterPreloaded 处理）
    Stage1_RebuildContainers,    // 容器重建（RefreshHub.OnChunkLoad）
    Stage2_RespawnNPCs,          // 僵尸重生（ZombieSpawner.SpawnInitial + ActivateZombiesInChunk）
    Done
}
```

### 2.3 三级切换逻辑

**检查条件**：玩家移动超过 20m（CHUNK_CHECK_DISTANCE）或超过 2 秒（CHUNK_CHECK_INTERVAL）。

```
遍历 100 个 Chunk，计算到玩家 Chunk 的 Chebyshev 距离:

  距离 ≤ loadRadius        → EnterLoaded(chunkId)
  距离 ≤ preloadRadius     → EnterPreloaded(chunkId)
  距离 > preloadRadius     → EnterUnloaded(chunkId)
```

- `loadRadius` = ChunkQualityConfig 决定（Low=1, Medium=2, High=3）
- `preloadRadius` = loadRadius + 1 + GetDynamicPreloadExtra(speed)
- 距离使用 **Chebyshev 距离**（max(|dx|, |dz|)）

**EnterLoaded**:
- 状态切为 Loaded
- 如果预热未完成（如从 Unloaded 直接进入 Loaded 圈），同步 FinishPreload
- chunkParent.SetActive(true)
- ActivateZombiesInChunk（启用 NavMeshAgent）
- RefreshHub.OnChunkLoad → 刷新所有注册的 Handler

**EnterPreloaded**:
- 如果之前是 Unloaded：加入 `_preloadQueue` 队列，开始分帧预热
- 如果之前是 Loaded（降级）：DeactivateZombiesInChunk（禁用 Agent），保留容器状态

**EnterUnloaded**:
- DestroyZombiesInChunk（销毁 GameObject）
- ZombieSpawner.OnChunkUnloaded（重置刷怪预算）
- RefreshHub.OnChunkUnload（释放 InventoryContainer 内存）
- chunkParent.SetActive(false)

### 2.4 异步预热队列

预热队列 `Queue<int> _preloadQueue`，每帧处理最多 **2 个预热步骤**（PRELOAD_STEPS_PER_FRAME）。

核心方法 `ProcessOnePreloadStep(RuntimeChunk chunk)`：
1. **Stage0** → 直接跳过到 Stage1（SetActive 已在 EnterPreloaded 中处理）
2. **Stage1** → 调用 RefreshHub.OnChunkLoad(chunkId, currentDay)
3. **Stage2** → 高速减载检查：speed > 20 m/s 则跳过僵尸激活；否则 SpawnInitial + ActivateZombiesInChunk
4. **Done** → 出队

如果玩家直接从 Unloaded 区域移动到 Loaded 区域，调用 `FinishPreload` 同步完成所有剩余步骤。

### 2.5 RuntimeChunk 数据结构

```csharp
class RuntimeChunk {
    int chunkId;
    Vector2Int gridPos;
    ChunkState state;
    PreloadStage preloadStage;
    Transform chunkParent;

    List<int> containerIds;               // 容器 ID 列表
    List<ZombieStateMachine> zombieInstances;  // 僵尸实例引用
    List<int> buildingIds;                // 建筑 ID（预留）
    List<int> worldItemIds;              // 地面物品 ID（上限 400/Chunk）
}
```

---

## 三、性能分级体系

### 3.1 静态档位

```csharp
enum ChunkQuality { Low, Medium, High }

static class ChunkQualityConfig {
    GetLoadRadius:        Low=1, Medium=2, High=3   (Chunk 数)
    GetPreloadBaseRadius: GetLoadRadius + 1          (基准预热)
    GetUnloadRadius:      GetPreloadBaseRadius + 1   (卸载边界)
}
```

| 档位 | 加载半径 | 预热带基准 | 卸载半径 |
|------|:------:|:-------:|:------:|
| Low  | 80m (1) | 160m (2) | 240m (3) |
| Medium | 160m (2) | 240m (3) | 320m (4) |
| High | 240m (3) | 320m (4) | 400m (5) |

### 3.2 动态预热带

根据玩家移动速度扩展预热带宽度：

```csharp
int GetDynamicPreloadExtra(float speed) {
    speed < 10  → 0    // 步行
    speed < 20  → 1    // 普通车
    speed < 40  → 2    // 高速车
    speed ≥ 40  → 3    // 极速
}
```

### 3.3 高速减载

预热 Stage 2 时检查玩家速度：
- speed > 20 m/s → 跳过僵尸 NPC 激活
- 减速后进入 Loaded 层时，通过 FinishPreload 补激活

---

## 四、容器系统

### 4.1 WorldContainer 生命周期：两态设计

```
Unopened ──[搜索读条 + 生成 loot]──→ Opened
Opened   ──[冷却到期 + Chunk 重载]──→ Unopened (容器刷新)
```

**关键设计决策**：无 Empty 状态，容器永远可交互。

**状态判定**：
```csharp
bool IsOpened => ContainerRegistry.Instance?.IsOpened(containerId) ?? false;
```

### 4.2 搜索交互流程

```
玩家对未搜容器按交互键:
  1. 启动 SearchRoutine 协程
  2. 显示世界空间进度条（Canvas + Slider，World Space 渲染模式）
  3. 经过 profile.searchTime 秒后完成
  4. ContainerRegistry.GetOrCreate(containerId) → 懒加载 InventoryContainer
  5. profile.lootTable.GenerateLoot(minLootTypes, maxLootTypes) → 生成掉落
  6. ContainerRegistry.MarkOpened(containerId, currentDay)
  7. EventBus.Publish(ContainerSearchedEvent)
  8. 打开 ContainerWindowUI

玩家对已搜容器按交互键:
  1. 直接打开 ContainerWindowUI（瞬间，无读条）
```

### 4.3 进度条 UI（动态构建）

在 `WorldContainer.BuildProgressUI()` 中通过代码创建：
- Canvas（World Space，2m×0.3m 面板，悬浮在容器上方 1.2m）
- Slider（不可交互，0→1 填充）
- 背景 Image（深灰半透明）+ Fill Image（金黄色）
- 默认隐藏，搜索时显示，完成后隐藏

### 4.4 ContainerWindowUI

**功能**：可拖拽容器格子窗口（UGUI）

- 窗口尺寸：320×260 像素
- 格子尺寸：50px + 4px 间隔
- 标题栏可拖拽（EventTrigger Drag）
- 关闭按钮（红色 X）
- 格子渲染：空位=深灰，占据=绿色
- 物品覆盖层：名称文字 + 数量 "xN"
- **双击拾取**：0.3 秒内双击同一格子 → 将物品移入玩家背包
- ESC 关闭（通过 InputRouter 绑定）
- 关闭不改变容器状态

### 4.5 容器 EventBus 事件

```csharp
// 搜索完成时触发（进度条结束，物品已生成但窗口未开）
struct ContainerSearchedEvent {
    GameObject Container;
    string DisplayName;
}

// 容器窗口打开时触发
struct ContainerOpenedEvent {
    GameObject Container;
    string DisplayName;
}
```

---

## 五、ContainerRegistry 懒加载

### 5.1 核心设计

```csharp
class ContainerRegistry : MonoBehaviour {
    static ContainerRegistry Instance;  // 场景单例

    Dictionary<int, ContainerRecord> _records;      // 全量记录
    Dictionary<int, List<ContainerRecord>> _chunkIndex;  // 按 Chunk 索引

    int _nextId;  // 自动分配容器 ID
}
```

### 5.2 关键方法

| 方法 | 说明 |
|------|------|
| `Register(chunkId, profile, existingId)` | 注册容器记录（初始化时调用），自动或手动分配 ID |
| `GetOrCreate(containerId)` | 懒加载：首次访问时创建 InventoryContainer，之后返回已有 |
| `MarkOpened(containerId, gameDay)` | 标记已搜，记录游戏天数 |
| `IsOpened(containerId)` | 查询是否已搜 |
| `GetChunkRecords(chunkId)` | 获取某 Chunk 的所有容器记录 |
| `ReleaseContainer(containerId)` | 释放 InventoryContainer 内存（保留 Record） |
| `GetAllRecords()` | 存档导出：返回所有 ContainerSaveRecord |
| `RestoreRecord(csr, itemCatalog)` | 存档恢复：从 SaveLoad 数据重建记录 |

### 5.3 内存模型

```
未搜容器: Record 存在 (isOpened=false, container=null) → ~100 bytes
已搜容器: Record + InventoryContainer + PlacedItems → 随物品数量增长
Chunk休眠: container 被 Release，=null → 回到未搜内存水平
```

### 5.4 ContainerRecord 数据结构

```csharp
class ContainerRecord {
    int containerId;
    bool isOpened;
    float openedGameDay;
    int chunkId;
    ContainerLootProfile profile;
    InventoryContainer container;  // 懒加载，可 null
}
```

---

## 六、容器刷新（ContainerRefreshHandler）

### 6.1 实现 IRefreshHandler

```csharp
class ContainerRefreshHandler : IRefreshHandler {
    string handlerName => "ContainerRefresh";

    void OnChunkLoad(int chunkId, float currentGameDay) { ... }
    void OnChunkUnload(int chunkId) { ... }
}
```

### 6.2 OnChunkLoad 刷新逻辑

遍历该 Chunk 所有 ContainerRecord：

1. **未搜过 (isOpened=false)** → 什么都不做（保持懒加载）
2. **已搜过 + 不允许刷新 (refreshEnabled=false)** → 什么都不做（保留玩家物品）
3. **已搜过 + 允许刷新 + 冷却已过**：
   - `record.container = null`（释放内存）
   - `record.isOpened = false`（重置为未搜）
   - `record.openedGameDay = 0`
4. **已搜过 + 允许刷新 + 冷却未到** → 保留 Record，不重建容器（懒加载在交互时触发）

### 6.3 OnChunkUnload 逻辑

遍历该 Chunk 所有 ContainerRecord，将 `record.container = null`——释放 InventoryContainer 内存，保留 ContainerRecord 供后续重载。

### 6.4 刷新规则总结

| 条件 | 行为 |
|------|------|
| 未搜 + 从未加载 | 零内存，等待交互 |
| 已搜 + 冷却未到 + Chunk 加载 | 保留物品，等待交互时重建容器 |
| 已搜 + 冷却已到 + Chunk 加载 | 清空重置为 Unopened，下次再搜重随机 |
| 已搜 + refreshEnabled=false | 永不刷新（玩家自建存储） |

---

## 七、Loot 配置（ContainerLootProfile）

### 7.1 ScriptableObject 字段

```csharp
[CreateAssetMenu(menuName = "Game/ContainerLootProfile")]
class ContainerLootProfile : ScriptableObject {
    string displayName;         // 显示名称
    LootTable lootTable;        // 权重随机掉落表
    float emptyChance;          // 空容器概率 (0-1)
    int minLootTypes;           // 最少掉落种类 (0-10)
    int maxLootTypes;           // 最多掉落种类 (1-10)
    int gridWidth;              // 容器格子列数 (1-10)
    int gridHeight;             // 容器格子行数 (1-10)
    float searchTime;           // 搜索耗时秒 (0.1-10)
    string containerTag;        // 类型标签（L4 区域系统用）
    float refreshCooldownDays;  // 冷却天数 (0-30)
    bool refreshEnabled;        // 是否开启刷新
}
```

### 7.2 使用方式

编辑器中将 ContainerLootProfile 拖到场景中 WorldContainer 组件的 `profile` 字段上。每个容器类型（橱柜、冰箱、箱子等）可创建独立的 Profile 资产。

---

## 八、RefreshHub — 刷新调度中心

### 8.1 设计

```csharp
class RefreshHub : MonoBehaviour {
    static RefreshHub Instance;
    List<IRefreshHandler> _handlers;

    void Register(IRefreshHandler handler);    // 注册处理器
    void OnChunkLoad(int chunkId, float day);  // 正向遍历通知加载
    void OnChunkUnload(int chunkId);           // 反向遍历通知卸载
}
```

### 8.2 IRefreshHandler 接口

```csharp
interface IRefreshHandler {
    string handlerName { get; }
    void OnChunkLoad(int chunkId, float currentGameDay);
    void OnChunkUnload(int chunkId);
}
```

### 8.3 注册时机

在 `ChunkManager.Start()` 中：
```csharp
RefreshHub.Instance.Register(new ContainerRefreshHandler());
```

ChunkManager 仅负责调用 `RefreshHub.OnChunkLoad` 和 `RefreshHub.OnChunkUnload`，不关心有哪些 Handler。新增刷新类型只需实现接口并注册，不修改 ChunkManager。

### 8.4 调度顺序

- **OnChunkLoad**: 正向遍历 `_handlers`（注册顺序）
- **OnChunkUnload**: 反向遍历 `_handlers`（逆注册顺序，后注册的先清理）

---

## 九、地面物品系统（WorldItem）

### 9.1 WorldItem 组件

创建 Billboard Quad 显示物品图标：
- PrimitiveType.Quad 作为子物体
- Unlit/Transparent 材质 + 物品图标纹理
- LateUpdate 中始终面向摄像机（Billboard）
- 父级 BoxCollider（Trigger）作为交互触发器
- 自动添加 GroundItemInteract 组件

### 9.2 GroundItemInteract

实现 IInteractable：
- InteractionTime = 0（瞬间交互）
- OnInteract → 将物品添加到玩家背包
- 全部拾取完 → Destroy(gameObject)
- 部分拾取 → 更新 count

### 9.3 每 Chunk 上限

`PLAYER_DROP_PER_CHUNK_CAP = 400`，超出时 FIFO 淘汰最旧物品。

---

## 十、初始化与自动注册流程

### 10.1 初始化顺序

```
Awake:
  ChunkManager.Awake → Initialize()
    → 创建 100 个 RuntimeChunk
    → 为每个 Chunk 创建 "Chunk_{x}_{z}" 父节点（初始 SetActive(false)）
  ContainerRegistry.Awake → 单例就绪
  RefreshHub.Awake → 单例就绪

Start:
  ChunkManager.Start
    → 获取 playerTransform
    → 获取 TimeManager
    → RefreshHub.Register(new ContainerRefreshHandler())
    → AutoRegisterContainers()
        → 遍历 ServiceLocator.GetAll<WorldContainer>()
        → 计算每个容器的 chunkId
        → ContainerRegistry.Register(chunkId, profile, containerId)
        → 将容器 GameObject SetParent 到对应 Chunk
        → ChunkManager.RegisterContainer(containerId, chunkId)
```

### 10.2 对象注册入口

```csharp
// 各系统在初始化时调用
ChunkManager.Instance.RegisterContainer(containerId, chunkId);
ChunkManager.Instance.RegisterBuilding(buildingId, chunkId);
ChunkManager.Instance.RegisterWorldItem(itemId, chunkId);
ChunkManager.Instance.RegisterZombie(zombie, chunkId);
```

---

## 十一、存档系统集成

### 11.1 导出

`ContainerRegistry.GetAllRecords()` → 返回 `List<ContainerSaveRecord>`，包含：
- 每个容器的 isOpened / openedGameDay / profileName
- 已搜容器的物品列表（SlotSaveData 数组）
- 未搜容器 items=null（读档后由 profile 重新生成）

### 11.2 恢复

`ContainerRegistry.RestoreRecord(csr, itemCatalog)` → 根据存档数据重建：
- 恢复 isOpened / openedGameDay 状态
- 重建 InventoryContainer + PlacedItems
- 通过 ItemCatalog 按名称查找 ItemData

---

## 十二、GameConstants 相关常量

```csharp
RUNTIME_CHUNK_SIZE        = 80f    // Chunk 边长（米）
CHUNK_GRID_SIZE           = 10     // 网格维度（10×10=100 Chunks）
CHUNK_CHECK_DISTANCE      = 20f    // 重新检查的最小移动距离
CHUNK_CHECK_INTERVAL      = 2f     // 重新检查的最小时间间隔
PRELOAD_STEPS_PER_FRAME   = 2      // 每帧预热步骤数
PLAYER_DROP_PER_CHUNK_CAP = 400    // 每 Chunk 地面物品上限
SPEED_THRESHOLD_NORMAL    = 10f    // 步行速度阈值
SPEED_THRESHOLD_FAST      = 20f    // 快速移动阈值
SPEED_THRESHOLD_EXTREME   = 40f    // 极速移动阈值
```

---

## 十三、架构分层

```
管理层: ChunkManager       — 坐标映射 + 三级激活 + 预热队列
调度层: RefreshHub         — Handler 注册 + OnChunkLoad/Unload 遍历
数据层: ContainerRegistry  — Record 字典 + 懒加载 + 生命周期
        ZombieSpawner      — 僵尸分配与重生
业务层: WorldContainer     — 交互逻辑 + 搜索读条 + 进度条
        IRefreshHandler    — 各种刷新实现（可扩展）
UI层:   ContainerWindowUI  — 格子窗口 + 双击拾取
        WorldItem          — 地面物品 Billboard
```

---

## 十四、扩展预留

| 预留点 | 方式 | 当前状态 |
|--------|------|:------:|
| 新刷新类型 | 实现 IRefreshHandler + RefreshHub.Register | 已用（ContainerRefreshHandler） |
| 树木/浆果刷新 | 同上模式 | ⬜ 未来 |
| 建筑 Chunk 归属 | RuntimeChunk.buildingIds | 预留列表 |
| 地面物品管理 | RuntimeChunk.worldItemIds | 预留列表 |
| 新性能档位 | ChunkQuality 枚举 + ChunkQualityConfig | 可扩展 |
| 存档完整恢复 | ContainerRegistry.GetAllRecords / RestoreRecord | ✅ 已实现 |
