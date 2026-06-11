# Mono 重要内容补充知识点

> 为已有的延迟函数、协同程序笔记补充遗漏的重要概念

---

## 一、协程补充

### 1. 自定义 YieldInstruction

原笔记讲了 Unity 内置的 `yield return` 类型，但没讲如何自定义：

```csharp
// 自定义等待条件 —— 继承 CustomYieldInstruction
public class WaitForCondition : CustomYieldInstruction
{
    private System.Func<bool> _condition;
    
    public WaitForCondition(System.Func<bool> condition)
    {
        _condition = condition;
    }
    
    public override bool keepWaiting => !_condition();
}

// 使用
yield return new WaitForCondition(() => isReady);
```

### 2. 协程嵌套模式

```csharp
// 1. 顺序执行协程链
IEnumerator AttackSequence()
{
    yield return StartCoroutine(WindUp());     // 先蓄力
    yield return StartCoroutine(SwingWeapon()); // 再挥砍
    yield return StartCoroutine(Recover());     // 最后恢复
}

// 2. 并行协程启动（同时执行多个协程）
IEnumerator AttackWithEffects()
{
    // 用同一个 StartCoroutine 启动多个协程
    Coroutine move = StartCoroutine(MoveToTarget());
    Coroutine effect = StartCoroutine(PlayEffects());
    Coroutine sound = StartCoroutine(PlaySound());
    
    // 等待所有完成
    yield return move;
    yield return effect;
    yield return sound;
}
```

### 3. 协程的销毁和资源清理陷阱

```csharp
// 陷阱：物体被 Destroy 后，协程中的代码依然可能执行到 yield return 之前的部分
IEnumerator DangerousCoroutine()
{
    Debug.Log("开始");  // 即使物体即将销毁，这行也会执行
    yield return new WaitForSeconds(1f);
    Debug.Log("1秒后"); // 如果物体在等待期间被销毁，这行会抛出 MissingReferenceException
}

// 安全做法：每次 yield 后检查
IEnumerator SafeCoroutine()
{
    yield return new WaitForSeconds(1f);
    if (this == null) yield break;  // 检查是否还被销毁
    // 继续执行...
}
```

### 4. 协程的执行时机细节

| yield return 类型 | 执行时机 | 说明 |
|------------------|---------|------|
| `null` | `Update` 之后，下一帧开始前 | 最常用的等待一帧 |
| `new WaitForSeconds(n)` | 经过 n 秒后的 Update | 受 Time.timeScale 影响 |
| `new WaitForSecondsRealtime(n)` | 经过 n 秒后的 Update | 不受 Time.timeScale 影响 |
| `new WaitForFixedUpdate()` | FixedUpdate 之后 | 用于物理相关操作 |
| `new WaitForEndOfFrame()` | 所有渲染完成之后 | 用于屏幕截图、读像素 |
| `new WaitForSeconds(n)` 传 0 | 等价于 `null` | 等待一帧 |

### 5. 协程替代方案（现代 Unity 开发趋势）

Unity 工程越来越大，协程的弱项逐渐暴露：

| 方案 | 优点 | 缺点 | 适合场景 |
|------|------|------|---------|
| 协程 Coroutine | 简单易用、兼容性好 | 无法返回值、无法处理异常、与MonoBehaviour耦合 | 简单时间延迟、异步加载 |
| 异步方法 async/await | 可返回值、可 try-catch、可取消 | 需要 .NET 4.x+、线程安全问题 | 网络请求、文件IO |
| UniTask | 零分配、高性能、支持CancellationToken | 第三方库、需学习 | 大型项目首选 |
| 状态机 | 完全可控、可调试 | 代码量大 | 角色状态、AI行为 |

```csharp
// 使用 async/await 替代协程（需要 .NET 4.x）
async void Start()
{
    await Task.Delay(1000);
    Debug.Log("1秒后执行");
    // 注意：await Task.Delay 不依赖于 MonoBehaviour，物体销毁后依然可能执行
}

// 推荐：使用 UniTask（第三方库）
// 零GC分配、支持 CancellationToken、可返回结果
// async UniTask<int> LoadAsync(CancellationToken token) { ... }
```

### 6. 协程性能优化

- **避免在 Update 中频繁 StartCoroutine**：协程的启动有开销
- **协程中不要执行耗时的循环**：协程分时执行，但每帧执行的时间片段仍应小于 16.6ms
- **尽量复用单个协程**：而不是反复创建和销毁
- **用 `WaitForSeconds` 对象的缓存**：避免每帧创建新对象导致 GC

```csharp
// 坏做法：每帧创建新对象
IEnumerator Bad()
{
    while (true)
    {
        yield return new WaitForSeconds(1f);  // 每帧分配新对象
    }
}

// 好做法：缓存对象
WaitForSeconds _wait = new WaitForSeconds(1f);
IEnumerator Good()
{
    while (true)
    {
        yield return _wait;  // 复用同一对象
    }
}
```

---

## 二、延迟函数补充

### 1. Invoke 的反射性能代价

原笔记提到 Invoke 底层通过反射实现，这里补充具体影响：

```csharp
// Invoke 的反射调用比直接调用慢约 10-100 倍
// 每帧调用 Invoke 会触发反射查找
void Update()
{
    if (condition)
        Invoke("MyMethod", 0f);  // 不好：频繁触发反射
}
```

**替代方案**：
- 高频判断场景：用协程代替
- 简单的延迟执行：用 `StartCoroutine` + `yield return new WaitForSeconds`
- 或者自定义计时器系统

### 2. 更灵活的计时器模式

Invoke 只能调用无参函数，这里补充更灵活的替代方案：

```csharp
// 方案一：用协程实现带参延迟
IEnumerator DelayedAction(float delay, System.Action action)
{
    yield return new WaitForSeconds(delay);
    action?.Invoke();
}
// 使用：
StartCoroutine(DelayedAction(2f, () => Debug.Log("2秒后执行")));

// 方案二：自定义计时器（适合需要暂停/加速/减速的场景）
public class Timer
{
    public float remainingTime;
    public System.Action onComplete;
    public bool isPaused;
    
    public void Tick(float deltaTime)
    {
        if (isPaused) return;
        remainingTime -= deltaTime;
        if (remainingTime <= 0)
        {
            remainingTime = 0;
            onComplete?.Invoke();
        }
    }
}
```

### 3. InvokeRepeating 的注意事项

```csharp
// 问题：InvokeRepeating 的首次延迟和后续间隔都是 float 类型
// 如果间隔设为 0，会导致每帧调用，性能问题
InvokeRepeating("MyMethod", 1f, 0f); // ⚠️ 每帧调用！不是每帧！

// 安全替代：用协程实现重复执行
IEnumerator RepeatAction(float interval, System.Action action)
{
    while (true)
    {
        action?.Invoke();
        yield return new WaitForSeconds(interval);
    }
}
```

### 4. 协程 vs Invoke 选择指南

| 场景 | 推荐 | 原因 |
|------|------|------|
| 一次性延迟 | Invoke | 简单，代码少 |
| 定时重复 | 协程 | 更灵活，可控制 |
| 需要传参 | 协程 | Invoke 不支持 |
| 需要取消 | 都可以 | 各有对应方法 |
| 需要暂停/恢复 | 协程 | 配合 Time.timeScale 或自定义 |
| 大量对象 | 协程 | 避免反射开销 |
| UnityEvent 回调 | Invoke | 可直接绑定 Inspector |

---

## 三、MonoBehaviour 生命周期补充

### 1. 完整的生命周期执行顺序

```
场景加载
  → Awake()          // 无论脚本是否启用都会执行，适合初始化引用
  → OnEnable()       // 每次启用时执行，适合注册事件
  → Start()          // 第一次 Update 前执行，适合初始化数据
  → FixedUpdate()    // 固定时间步长（默认 0.02s），物理更新
  → Update()         // 每帧执行，逻辑更新
  → LateUpdate()     // Update 之后执行，摄像机跟随等
  → OnDisable()      // 组件/对象失活时执行
  → OnDestroy()      // 组件/对象销毁时执行

应用程序退出时:
  → OnApplicationQuit()
  → OnDisable()
  → OnDestroy()
```

### 2. 生命周期常见陷阱

```csharp
// 陷阱1：在 Awake 中不能假设其他对象的 Awake 已执行
void Awake()
{
    // 不要在这里用 FindObjectOfType 访问其他脚本
    // 其他对象的 Awake 可能还没执行！
}

// 陷阱2：OnEnable/OnDisable 的配对问题
void OnEnable()
{
    // 每次 SetActive(true) 都会执行
    // 如果引用已被销毁，需要判空
    if (eventManager != null)
        eventManager.Register(this);
}

void OnDisable()
{
    // 每次 SetActive(false) 或对象销毁都会执行
    if (eventManager != null)
        eventManager.Unregister(this);
    // 注意：OnDestroy 在 OnDisable 之后调用
}

// 陷阱3：StopAllCoroutines 在 OnDisable 中不会自动调用
// 协程脚本失活时，协程还在运行！
// 对象销毁时协程才会自动终止
```

### 3. Time.timeScale 对各项功能的影响

| 功能 | 受 timeScale 影响 | 不受 timeScale 影响 |
|------|-----------------|-------------------|
| `Update` | 调用频率不变 | - |
| `FixedUpdate` | 调用次数会变 | - |
| `WaitForSeconds` | ✅ 是 | ❌ |
| `WaitForSecondsRealtime` | ❌ | ✅ 否 |
| `Time.deltaTime` | ✅ 是 | ❌ |
| `Time.unscaledDeltaTime` | ❌ | ✅ 否 |
| `Invoke` | ✅ 是 | ❌ |
| 动画播放 | ✅ 是 | ❌ |
| 物理模拟 | ✅ 是 | ❌ |
