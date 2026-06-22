# C# + Unity 八股复习指南 —— 用自己的项目代码学

> 你的项目 140+ 个 C# 脚本、19 个子系统，覆盖了 Unity 面试 90% 的知识点。
> 别去刷网上的 Demo，**拿自己的代码来背**——这比任何教程都实在。

---

## 📋 知识点总览图

```
┌───────────── C# 核心 ─────────────┐    ┌────── Unity 核心 ──────┐
│ ① 值类型 vs 引用类型               │    │ ① MonoBehaviour 生命周期│
│ ② 泛型 Generic                     │    │ ② ScriptableObject      │
│ ③ 接口 Interface                   │    │ ③ Coroutine 协程        │
│ ④ 委托 Delegate / Action           │    │ ④ Physics 物理系统      │
│ ⑤ 属性 Property vs 字段 Field      │    │ ⑤ NavMesh 导航寻路       │
│ ⑥ readonly struct                  │    │ ⑥ Camera / 输入         │
│ ⑦ 集合：Dictionary/List/Array      │    │ ⑦ UI: Canvas/UGUI       │
│ ⑧ switch 表达式 / 模式匹配         │    │ ⑧ 性能优化 / GC 规避     │
│ ⑨ null 检查：?? / ?. / ??=        │    │ ⑨ Resources.Load         │
│ ⑩ 异常处理 try-catch               │    │ ⑩ RequireComponent 属性  │
└────────────────────────────────────┘    └─────────────────────────┘

┌──────────── 设计模式 ────────────┐
│ ① 单例 Singleton                 │
│ ② 观察者 Observer (EventBus)     │
│ ③ 状态模式 State                 │
│ ④ 策略模式 Strategy (IGenStage)  │
│ ⑤ 组合模式 Composite            │
│ ⑥ 管线模式 Pipeline              │
└──────────────────────────────────┘
```

---

# 第一部分：C# 核心八股

## 1. 值类型 vs 引用类型 🏆 必考

**你的项目代码：** `GameEvents.cs` 所有事件都是 `struct`，`EventBus.cs` 约束 `where T : struct`

```csharp
// ✅ 你的项目 — 事件用 struct（值类型），分配在栈上，不产生 GC
public readonly struct PlayerDied
{
    public string KillerName { get; }
    public int KillCount { get; }
    public PlayerDied(string killerName, int killCount)
    {
        KillerName = killerName;
        KillCount = killCount;
    }
}

// EventBus 强制约束
public static void Publish<T>(T eventData) where T : struct { ... }
```

**面试背诵：**

| | struct（值类型） | class（引用类型） |
|---|---|---|
| 存储位置 | 栈（声明处） | 堆（new 出来的） |
| 传递方式 | 复制整个值 | 复制引用地址 |
| GC | 不产生 | 产生（需要回收） |
| 能否为 null | 默认不能（除非 `Nullable<T>`） | 可以 |
| 继承 | 只能实现接口 | 可以继承类 |
| Unity 中用在哪 | 事件数据、Vector3、Color | MonoBehaviour、ScriptableObject |

**人话：** struct 轻量、无 GC，适合"数据包"（事件）；class 需要继承/多态时用。

---

## 2. 泛型 Generic 🏆 必考

**你的项目代码：** `EventBus<T>`、`List<T>`、`Dictionary<K,V>`

```csharp
// ✅ 你的项目 — 泛型静态类，一个方法处理所有事件类型
public static void Subscribe<T>(Action<T> handler) where T : struct { ... }
public static void Publish<T>(T eventData) where T : struct { ... }

// 使用
EventBus.Subscribe<PlayerDied>(OnPlayerDied);
EventBus.Publish(new PlayerDied("zombie", 5));
```

**面试背诵：**

- **泛型是什么：** 用类型参数 `<T>` 写一套代码，适用所有类型。编译时确定类型，无装箱。
- **约束 `where T : struct`：** 限定 T 必须是值类型
- **常见约束：**
  - `where T : class` — 必须是引用类型
  - `where T : MonoBehaviour` — 必须是 MonoBehaviour 子类
  - `where T : new()` — 必须有无参构造函数
  - `where T : IInteractable` — 必须实现某接口

**人话：** 泛型 = 写一个模板，传什么类型就变什么类型，不用每种类型都写一遍。

---

## 3. 接口 Interface 🏆 必考

**你的项目代码：** `IInteractable`（所有可交互物的统一入口）、`IDamageable`、`IGenStage`

```csharp
// ✅ 你的项目 — 接口定义契约
public interface IInteractable
{
    string InteractionPrompt { get; }    // 只读属性
    float InteractionTime { get; }
    bool IsInteractable { get; }
    void OnInteract(GameObject interactor);  // 方法
}

// VehicleInteraction 实现接口
public class VehicleInteraction : MonoBehaviour, IInteractable { ... }
// WorldContainer 实现接口
public class WorldContainer : MonoBehaviour, IInteractable { ... }
// PlacedStructure 实现接口
public class PlacedStructure : MonoBehaviour, IInteractable { ... }

// PlayerInteraction 只依赖接口，不依赖具体类
public class PlayerInteraction : MonoBehaviour
{
    // 用 OverlapSphere 找到所有 IInteractable，统一处理
    IInteractable currentTarget;
    if (currentTarget.IsInteractable)
        currentTarget.OnInteract(gameObject);
}
```

**面试背诵：**

| 接口 (interface) | 抽象类 (abstract class) |
|---|---|
| 只有方法签名，无实现 | 可有已实现的方法 |
| 一个类可实现多个接口 | 一个类只能继承一个抽象类 |
| 不能有字段 | 可以有字段 |
| 成员默认 public | 可以有访问修饰符 |
| 用于"能做什么"的契约 | 用于"是什么"的继承 |

**人话：** 接口 = 合同，签了就得按合同办事。`IInteractable` 的意思是"只要你能被交互，就必须有这 4 个东西"——至于你怎么实现，我不管。

---

## 4. 委托 Delegate / Action 🏆 必考

**你的项目代码：** `EventBus` 用 `Dictionary<Type, Delegate>` 存储事件处理器

```csharp
// ✅ 你的项目 — Delegate 是"函数变量"
private static readonly Dictionary<Type, Delegate> _handlers = new();

// Action<T> 是带一个参数的 void 委托
public static void Subscribe<T>(Action<T> handler) where T : struct
{
    // Delegate.Combine 把多个处理器串联
    _handlers[type] = Delegate.Combine(existing, handler);
}

// Delegate.Remove 移除某个处理器
Delegate newDelegate = Delegate.Remove(existing, handler);

// GetInvocationList 逐个调用（你的项目这样写是为了独立 try-catch）
foreach (Delegate single in handler.GetInvocationList())
    (single as Action<T>)?.Invoke(eventData);
```

**面试背诵：**

| 类型 | 说明 | 示例 |
|---|---|---|
| `delegate` | 自定义委托类型 | `delegate void MyDelegate(int x);` |
| `Action` | 无返回值的泛型委托 | `Action<int, string>` |
| `Action<T>` | 带一个参数，无返回 | `Action<PlayerDied>` |
| `Func<T, TResult>` | 有返回值的泛型委托 | `Func<int, bool>` 输入 int 返回 bool |
| `event` | 事件关键字，限制外部只能 +=/-= | `event Action OnDeath;` |

**人话：** 委托 = 把函数当参数传。`Action<T>` = "收到 T 类型数据后要做的事"。你的 EventBus 说白了就是"一个大字典，Key=事件类型，Value=要调用的函数列表"。

---

## 5. 属性 Property vs 字段 Field

**你的项目代码：** `GameEvents.cs` 的 `readonly struct` 全部用属性

```csharp
// ❌ 不用公共字段
public string KillerName;  // 外部可以直接改，不安全

// ✅ 你的项目 — 只读属性（构造函数设值后不可改）
public readonly struct PlayerDied
{
    public string KillerName { get; }   // 只有 get，没有 set
    public int KillCount { get; }
}
```

**面试背诵：**

```csharp
// 完整属性
private int _hp;
public int HP
{
    get { return _hp; }
    set { _hp = Mathf.Clamp(value, 0, 100); }  // 可以加逻辑
}

// 自动属性（编译器自动生成 _backingField）
public int HP { get; set; }

// 只读属性
public int HP { get; private set; }  // 外部只读，内部可写

// 表达式体属性
public float SpeedKmh => _rb.velocity.magnitude * 3.6f;  // 你的 VehicleController
```

**人话：** 字段 = 裸数据，属性 = 带门卫的数据。面试官问"为什么要用属性不用 public 字段"——答"封装，可以在 get/set 里加逻辑，接口里只能定义属性不能定义字段"。

---

## 6. readonly struct

**你的项目代码：** `GameEvents.cs` 全部用 `readonly struct`

```csharp
public readonly struct ItemPickedUp
{
    public string ItemName { get; }
    public int Count { get; }
    public ItemPickedUp(string itemName, int count)
    {
        ItemName = itemName;
        Count = count;
    }
}
```

**面试背诵：**
- `readonly struct`：所有字段/属性只能在构造函数中赋值，之后不可改
- 好处：防止意外修改、编译器可以做更多优化、语义明确（"这是一个不可变的数据包"）
- Unity 中的 Vector3 不是 readonly struct（历史原因），但你的事件数据是——这是最佳实践

---

## 7. 集合类

**你的项目代码：** 到处在用

```csharp
// Dictionary — EventBus 的事件处理器字典
private static readonly Dictionary<Type, Delegate> _handlers = new();

// List — 合成系统的配方列表
var list = new List<RecipeData>();

// foreach — 遍历
foreach (var recipe in _catalog.GetByStation(ActiveStation)) { ... }

// 数组 — 配方材料
public RecipeMaterial[] materials;
```

**面试背诵：**

| 集合 | 特点 | 何时用 |
|---|---|---|
| `List<T>` | 动态数组，索引 O(1) | 需要按顺序/索引访问 |
| `Dictionary<K,V>` | 哈希表，Key查Value O(1) | 按类型/ID查找 |
| `T[]` | 定长数组，最快 | 固定数量的数据 |
| `Queue<T>` | 先进先出 | 命令队列 |
| `Stack<T>` | 后进先出 | 撤销操作 |
| `HashSet<T>` | 不重复集合，O(1) 查找 | 已访问集合 |

---

## 8. switch 表达式 / 模式匹配

**你的项目代码：** `CraftingSystem.ConsumeStamina()`

```csharp
// ✅ 你的项目 — switch 表达式（C# 8.0+）
float cost = recipe.requiredStation switch
{
    WorkstationTier.Hands or WorkstationTier.Campfire => 10f,
    WorkstationTier.SimpleBench => 5f,
    _ => 0f
};
```

**面试背诵：** 比传统 switch 更简洁，支持 `or` 组合、`_` 默认。Unity 2022+ 支持。

---

## 9. null 检查运算符

**你的项目代码：** 分散在各处

```csharp
// ??= — 如果为 null 才赋值（SurvivalSystem）
playerCharacter ??= GetComponent<PlayerCharacter>();

// ?. — 安全调用，为 null 就不执行（EventBus）
(single as Action<T>)?.Invoke(eventData);

// ?? — 如果为 null 就用备选值
float motorTorque = data?.motorTorque ?? 1500f;
```

---

## 10. nameof / typeof / GetType

```csharp
// EventBus 中获取类型
Type type = typeof(T);           // 编译时获取类型
handler.Method.DeclaringType?.Name  // 获取声明类的名字
```

---

# 第二部分：Unity 核心八股

## 1. MonoBehaviour 生命周期 🏆 必考

**你的项目代码：** 每个 MonoBehaviour 都在用

```csharp
// ✅ 你的 VehicleController — 完整生命周期示例
public class VehicleController : MonoBehaviour
{
    void Awake()         { _rb = GetComponent<Rigidbody>(); }  // 初始化自身
    void Start()         { ApplyConfig(); }                     // 依赖其他对象初始化
    void FixedUpdate()   { ApplyWheelPhysics(); }               // 物理更新（固定间隔）
    void OnEnable()      { InputRouter.BindKey(...); }          // 激活时绑定输入
    void OnDisable()     { InputRouter.UnbindAll(this); }       // 禁用时解绑
}

// ✅ 你的 SurvivalSystem
void Update() { TickSurvivalLogic(); }  // 每帧更新（间隔不定）
```

**面试背诵（执行顺序）：**

```
Awake → OnEnable → Start
         ↓
  FixedUpdate (每物理帧, 默认0.02s)
  Update      (每渲染帧, 间隔不定)
  LateUpdate  (Update之后, 摄像机跟随)
         ↓
  OnDisable → OnDestroy
```

| 方法 | 调用时机 | 用途 |
|---|---|---|
| `Awake` | 对象创建时，最早 | 获取自身组件引用 |
| `OnEnable` | 对象激活时 | 订阅事件、绑定输入 |
| `Start` | 第一帧 Update 前 | 依赖其他对象的初始化 |
| `Update` | 每帧 | 游戏逻辑、输入检测 |
| `FixedUpdate` | 固定间隔（默认0.02s） | 物理相关（Rigidbody） |
| `LateUpdate` | Update 之后 | 摄像机跟随 |
| `OnDisable` | 对象禁用时 | 取消订阅、解绑输入 |
| `OnDestroy` | 对象销毁时 | 清理资源 |

**常见面试题：**
- Q: Awake 和 Start 的区别？
  - A: Awake 最早执行，不管 enabled 是 true/false 都会执行；Start 在第一帧 Update 前，且只在 enabled=true 时执行。
- Q: Update 和 FixedUpdate 的区别？
  - A: Update 每渲染帧执行，帧率不稳；FixedUpdate 固定时间间隔执行，物理计算放这里。
- Q: OnEnable 和 Awake 的执行顺序？
  - A: Awake → OnEnable → Start。对象池重复使用时 Awake 只执行一次，OnEnable 每次激活都执行。

---

## 2. ScriptableObject 🏆 必考

**你的项目代码：** 整个配置系统都基于 ScriptableObject

```
_Game/Config/
  ItemData.cs        → 物品定义（ScriptableObject）
  RecipeData.cs      → 配方定义
  BuildableData.cs   → 建造物定义
  VehicleData.cs     → 车辆参数
  SurvivalData.cs    → 生存配置
  ZombieData.cs      → 僵尸参数
  LootTable.cs       → 掉落表
  ...
```

```csharp
// ✅ 你的项目 — ScriptableObject 作为数据容器
[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public float weight;
    public Vector2Int gridSize;  // 占几格
    public int maxStack;
    // ...
}
```

**面试背诵：**

| ScriptableObject | 普通 MonoBehaviour |
|---|---|
| 数据资产，不挂场景 | 行为组件，挂在 GameObject 上 |
| 存在 Assets 文件夹中（.asset） | 存在场景/预设中 |
| 不随场景切换而销毁 | 随 GameObject 销毁 |
| 适合：物品配置、关卡数据 | 适合：移动、攻击、UI 逻辑 |

**优点：** 内存友好（多引用共享同一份数据）、编辑器和运行时分离、方便策划调整。

---

## 3. Coroutine 协程 🏆 必考

**你的项目代码：** 建造读条、容器搜索

```csharp
// ✅ 你的 BuildModeController — 建造进度条
private Coroutine _buildCoroutine;

void StartBuilding()
{
    if (_buildCoroutine != null) StopCoroutine(_buildCoroutine);
    _buildCoroutine = StartCoroutine(BuildRoutine());
}

IEnumerator BuildRoutine()
{
    float elapsed = 0f;
    float duration = activeBuildable.buildDuration;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        BuildProgress = elapsed / duration;  // 0 → 1
        yield return null;  // 等一帧
    }

    PlaceStructure();
    _buildCoroutine = null;
}
```

```csharp
// ✅ 你的 WorldContainer — 搜索读条
IEnumerator SearchRoutine()
{
    float elapsed = 0f;
    while (elapsed < profile.searchDuration)
    {
        elapsed += Time.deltaTime;
        // 更新进度条 UI
        yield return null;
    }
    GenerateLoot();
}
```

**面试背诵：**

| yield return 指令 | 含义 |
|---|---|
| `yield return null` | 等一帧（到下一帧 Update 后） |
| `yield return new WaitForSeconds(n)` | 等 n 秒 |
| `yield return new WaitForFixedUpdate()` | 等下一次物理更新 |
| `yield return new WaitForEndOfFrame()` | 等帧末尾 |
| `yield return StartCoroutine(Another())` | 等另一个协程结束 |
| `yield return new WaitUntil(() => x > 10)` | 等条件满足 |

**注意事项：**
- 协程依赖 MonoBehaviour，对象禁用/销毁时协程会停
- `StopCoroutine` 需要传启动时的返回值或方法名
- `WaitForSeconds` 受 `Time.timeScale` 影响

---

## 4. 物理系统 Physics

**你的项目代码：** `VehicleController`（Rigidbody + WheelCollider）

```csharp
// ✅ 你的 VehicleController — 车辆物理
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    public WheelCollider wheelFL, wheelFR, wheelRL, wheelRR;
    private Rigidbody _rb;

    void FixedUpdate()
    {
        // 油门/动力
        wheelFL.motorTorque = _currentThrottle * _motorTorque;
        wheelFR.motorTorque = _currentThrottle * _motorTorque;

        // 转向（前轮）
        wheelFL.steerAngle = _currentSteer * _maxSteerAngle;
        wheelFR.steerAngle = _currentSteer * _maxSteerAngle;

        // 刹车（后轮）
        wheelRL.brakeTorque = _currentBrake * _brakingForce;
        wheelRR.brakeTorque = _currentBrake * _brakingForce;

        // 限速
        if (CurrentSpeedKmh > EffectiveMaxSpeed)
            _rb.velocity = _rb.velocity.normalized * (EffectiveMaxSpeed / 3.6f);
    }

    // 当前速度 km/h
    public float CurrentSpeedKmh => _rb.velocity.magnitude * 3.6f;
}
```

```csharp
// ✅ 你的 PlayerInteraction — OverlapSphere 检测交互范围
Collider[] hits = Physics.OverlapSphere(transform.position, interactionRadius);
foreach (var hit in hits)
{
    var interactable = hit.GetComponent<IInteractable>();
    if (interactable != null && interactable.IsInteractable)
    { /* 处理交互 */ }
}
```

**面试背诵：**

| API | 用途 |
|---|---|
| `Rigidbody` | 受物理引擎控制（重力、碰撞、力） |
| `CharacterController` | 角色移动（不受物理力，自己写移动逻辑） |
| `Rigidbody.velocity` | 直接设速度（你的项目用的是这个，不是 linearVelocity） |
| `Collider` | 碰撞体积（检测用） |
| `Physics.OverlapSphere` | 球形范围检测（你用于交互检测） |
| `Physics.Raycast` | 射线检测（你用于武器瞄准） |
| `WheelCollider` | 车轮物理（悬挂+摩擦力+动力） |
| `FixedUpdate` | 物理相关代码放这里 |

**人话：** Rigidbody 是"受物理管的东西"，加力就动；CharacterController 是"我告诉你怎么走"。

---

## 5. NavMesh 导航寻路

**你的项目代码：** `ZombieSpawner` + 僵尸移动

```csharp
// NavMesh.SamplePosition — 在 NavMesh 上找最近点
NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, maxDistance, NavMesh.AllAreas);
```

**面试背诵：**
- `NavMesh.SamplePosition` — 找一个点在 NavMesh 上的最近有效位置
- `NavMeshAgent.SetDestination` — AI 自动寻路
- `NavMeshAgent.isStopped` — 暂停/恢复导航
- 需要先 bake NavMesh（Window → AI → Navigation）

---

## 6. Camera / 输入

**你的项目代码：** `CameraFollow.cs` + `PlayerController.cs`

```csharp
// 摄像机跟随 — LateUpdate 中执行
void LateUpdate()
{
    Vector3 targetPos = player.position + offset;
    transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
}
```

**面试背诵：**
- 摄像机放 `LateUpdate`：确保玩家位置先更新完再跟随
- `Vector3.Lerp` 线性插值做平滑跟随
- `Input.GetAxis("Horizontal")` 获取 WASD 输入
- `ScreenPointToRay` 屏幕坐标转射线（鼠标瞄准）

---

## 7. UI：Canvas / UGUI

**你的项目代码：** `_Game/UI/` 目录 14 个 UI 脚本

```csharp
// InventoryUI, SurvivalHUD, InteractionPromptUI, BuildMenuUI ...
```

**面试背诵：**

| 概念 | 说明 |
|---|---|
| Canvas | UI 的根，所有 UI 元素必须在 Canvas 下 |
| Canvas Scaler | 自适应不同分辨率 |
| RectTransform | UI 的 Transform，用锚点定位 |
| Image / Text / Button | 基础 UI 组件 |
| CanvasGroup | 控制一组 UI 的透明度/可交互性 |
| GraphicRaycaster | UI 射线检测（点击检测） |

---

## 8. 性能优化 / GC 规避 🏆 必考

**你的项目代码：** EventBus 设计本身就是性能优化的案例

```csharp
// ✅ 为什么用 struct 事件？—— 避免 GC
// ❌ 如果用 class：
EventBus.Publish(new PlayerDied("zombie", 5));  // 每次都在堆上分配 → GC
// ✅ 用 struct：
EventBus.Publish(new PlayerDied("zombie", 5));  // 栈上分配 → 无 GC
```

**面试背诵（Unity 性能优化清单）：**

| 优化手段 | 说明 |
|---|---|
| 避免 Update 中 GetComponent | 在 Awake 缓存 |
| 避免字符串拼接 | 用 StringBuilder（你的 EventBus 用了） |
| 事件用 struct 不用 class | 你的 EventBus 就是这样设计的 |
| 对象池 | 子弹/特效/敌人 重复利用 |
| 避免 Update 中 GameObject.Find | 太慢 |
| Camera culling | 摄像机剔除不可见物体 |
| LOD | 远处用低精度模型 |
| 合批 (Batching) | 同材质合并 Draw Call |

---

## 9. `RequireComponent` / `[SerializeField]` / `[Header]`

```csharp
// ✅ 你的 VehicleController
[RequireComponent(typeof(Rigidbody))]    // 添加此脚本时自动添加 Rigidbody
public class VehicleController : MonoBehaviour
{
    [Header("配置")]                    // Inspector 中显示分组标题
    public VehicleData vehicleData;

    [Tooltip("燃料物品名")]             // 鼠标悬停提示
    public string fuelItemName;

    [SerializeField]                    // 私有字段也显示在 Inspector
    private SurvivalData survivalData;
}
```

---

# 第三部分：设计模式八股 🏆 必考

## 1. 单例模式 Singleton

**你的项目代码：**

```csharp
// ✅ 你的 CraftingSystem — 标准 MonoBehaviour 单例
public class CraftingSystem : MonoBehaviour
{
    public static CraftingSystem Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);  // 防止重复
            return;
        }
        Instance = this;
    }
}
```

**面试背诵：** 确保一个类只有一个实例，提供全局访问点。Unity 中实现：静态属性 + Awake 中赋值 + 重复检测销毁。

---

## 2. 观察者模式 Observer（事件总线）

**你的项目代码：** `EventBus` 就是完整的观察者模式实现

```csharp
// 发布者（不知道谁在听）
EventBus.Publish(new CraftingCompletedEvent(name, item, count, xp));

// 订阅者（不知道谁会发）
EventBus.Subscribe<CraftingCompletedEvent>(OnCraftingCompleted);
```

**面试背诵：** 发布-订阅模式，解耦发送者和接收者。你的 EventBus 三个核心方法：`Subscribe`（订阅）、`Publish`（发布）、`Unsubscribe`（取消订阅）。

---

## 3. 状态模式 State

**你的项目代码：** `BuildModeController` 的状态机

```csharp
// ✅ 你的建造系统 — 4 状态枚举
private BuildModeState _state = BuildModeState.Inactive;

// Inactive → MenuOnly → Preview → Building → Preview → MenuOnly → Inactive

public enum BuildModeState
{
    Inactive,   // 未激活
    MenuOnly,   // 菜单可见，未选中
    Preview,    // 虚影预览中
    Building    // 正在建造（读条）
}
```

**面试背诵：** 把对象行为按状态拆分，每个状态有自己的逻辑。好处：消除大量 if-else，新增状态不影响已有代码。

---

## 4. 策略模式 Strategy + 管线模式 Pipeline

**你的项目代码：** WorldGen 的 `IGenStage` + `WorldGenerator`

```csharp
// ✅ 统一的生成阶段接口
public interface IGenStage
{
    int Order { get; }       // 执行顺序
    bool Enabled { get; }
    void Execute(WorldData worldData);
}

// 18 个实现类：SeedStage, HeightStage, RoadStage, BuildingStage...
// WorldGenerator 收集所有 IGenStage，按 Order 排序依次执行
```

**面试背诵：** 策略模式——定义一组算法，让它们可以互相替换。管线模式——多个处理步骤按顺序串联，每个步骤完成一个独立任务。你的 WorldGen 把两个模式结合了。

---

# 第四部分：学习路线图

## 第一周：C# 核心（每天 1-2 小时）

| 天 | 主题 | 看哪段代码 |
|---|---|---|
| 1 | 值类型/引用类型、struct vs class | `GameEvents.cs` 全部 |
| 2 | 泛型、where 约束 | `EventBus.cs` 全部 |
| 3 | 接口、抽象类 | `IInteractable.cs` + 3 个实现类 |
| 4 | 委托、Action、Func、event | `EventBus.cs` 订阅/发布部分 |
| 5 | 属性、索引器、readonly | `GameEvents.cs` + `VehicleController.cs` |
| 6 | 集合：List/Dict/Queue | grep 搜你项目里的 `new List` / `new Dictionary` |
| 7 | 运算符重载、switch表达式、null检查 | `CraftingSystem.cs` |

**方法：** 每天看 2-3 段自己的代码，能讲清楚"这段代码用了什么 C# 特性，为什么要这样写"。

---

## 第二周：Unity 核心（每天 1-2 小时）

| 天 | 主题 | 看哪段代码 |
|---|---|---|
| 1 | MonoBehaviour 生命周期 | `VehicleController.cs` + `BuildModeController.cs` |
| 2 | ScriptableObject 数据驱动 | `_Game/Config/` 下任意 3 个文件 |
| 3 | Coroutine 协程 | `BuildModeController.cs` + `WorldContainer.cs` |
| 4 | 物理：Rigidbody/Collider/Raycast | `VehicleController.cs` + `WeaponAiming.cs` |
| 5 | NavMesh | `ZombieSpawner.cs` |
| 6 | UI：Canvas/UGUI | `_Game/UI/` 下 3 个文件 |
| 7 | 性能优化：GC/ObjectPool | `EventBus.cs` 为什么用 struct |

**方法：** 每天打开 Unity，运行场景，打断点看生命周期调用顺序。

---

## 第三周：设计模式 + 综合复盘

| 天 | 主题 | 看哪段代码 |
|---|---|---|
| 1 | 单例模式 | `CraftingSystem.cs` + `ZombieSpawner.cs` |
| 2 | 观察者/事件总线 | `EventBus.cs` + `GameEvents.cs` |
| 3 | 状态模式 | `BuildModeController.cs` 状态机部分 |
| 4 | 策略/管线模式 | `IGenStage.cs` + `WorldGenerator.cs` |
| 5 | 综合：完整走一遍合成流程 | 从 E 键交互 → 合成面板 → Craft → 事件 |
| 6 | 综合：完整走一遍建造流程 | BuildModeController 全流程 |
| 7 | 综合：完整走一遍驾驶流程 | Vehicle 上下车 + 驾驶 + 事件 |

**方法：** 画流程图 + 写注释，把关键类的调用链串起来。

---

## 每日学习模板

```
第 X 天：主题 _____

1. 看代码（20分钟）
   文件：_____
   关键行：_____

2. 自己写一遍（30分钟）
   不看原代码，自己手写核心片段

3. 背八股（10分钟）
   面试如果问"_____"怎么答：
   - 定义：_____
   - 你的代码例子：_____
   - 为什么这样设计：_____
```

---

## 复习检查清单

- [ ] 能说出 struct 和 class 的区别，为什么事件用 struct
- [ ] 能写出泛型方法的签名，解释 where 约束
- [ ] 能写出 IInteractable 接口，说出 3 个实现类
- [ ] 能解释 EventBus 的工作原理（Dictionary + Delegate）
- [ ] 能背出 MonoBehaviour 生命周期顺序
- [ ] 能解释 ScriptableObject 和 MonoBehaviour 的区别
- [ ] 能写出一个协程，说出 yield return null 和 WaitForSeconds 的区别
- [ ] 能解释 Rigidbody.velocity 和 transform.position 移动的区别
- [ ] 能说出 3 个性能优化手段
- [ ] 能解释单例/观察者/状态/策略 4 个模式

---

*最后：面试官最怕的不是你不会，而是你"用过但说不清楚"。你的项目 140+ 个脚本就是最好的证明——关键是能讲出来每一段的为什么。*
