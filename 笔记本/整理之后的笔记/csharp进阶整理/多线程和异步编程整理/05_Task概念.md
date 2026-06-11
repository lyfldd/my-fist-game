# 05_Task 概念

## 一、Task 到底是什么？

### 一句话理解
> **Task = 一个"要完成的工作"，它不是线程，而是一张工单**

| 概念 | 比喻 |
|---|---|
| Thread | 是真正干活的**工人** |
| Task | 是一张**工单**（记录要做什么） |

### Thread 和 Task 的核心区别

| 特性 | Thread（老版本） | Task（新版本） |
|---|---|---|
| 本质 | 真正干活的工人 | 一张工单 |
| 控制 | 你完全自己控制 | 系统自动分配 |
| 开销 | 大（每次新建/销毁） | 小（线程池复用） |
| 返回值 | 很难返回 | 直接支持 Task<T> |
| 等待/组合 | 需要自己实现 | Wait、WhenAll、ContinueWith |
| 适用场景 | 长任务、独立任务 | 短小、频繁的任务 |

### Task 的三个最重要特点

| 特点 | 说明 |
|---|---|
| 基于线程池 | 线程自动复用，不用你创建/销毁 |
| 支持返回值 | Task<T> 可以直接 return 结果 |
| 方便的等待和组合 | Wait、WhenAll、WhenAny、ContinueWith |

---

## 二、Task vs Thread 选择决策表

| 场景 | 推荐 | 原因 |
|---|---|---|
| 耗时短、数量多的小任务 | ✅ Task | 线程池复用，效率极高 |
| 需要返回值的场景 | ✅ Task | Task<T> 天生支持，Thread 很难返回 |
| 需要组合、顺序、等待多个任务 | ✅ Task | WaitAll、WhenAny、ContinueWith 方便 |
| 异步操作（加载、网络、文件） | ✅ Task + async/await | 天生最优解 |
| 长时间后台运行的线程 | ✅ Thread | 不适合放线程池 |
| 需要完全独立、不被线程池干扰 | ✅ Thread | Thread 是完全独立内核线程 |
| 深度递归、大量栈内存 | ✅ Thread | 线程池线程默认栈较小 |
| 需要精确控制线程生命周期 | ✅ Thread | 手动 Abort、Interrupt、设置优先级 |

> **总结**：Task 是默认首选，Thread 是特殊场景才用

---

## 三、适合用 Task 的场景详解

### 场景 1：耗时短、数量多的小任务
```csharp
// 比如：计算伤害、计算路径点、解析数据、临时计算
Task.Run(() => CalculateDamage(baseDamage, critRate));
Task.Run(() => ParseJson(data));
```
> 这些活很快就干完，用 Thread 太浪费，Task 用线程池复用，效率极高

### 场景 2：需要返回值的场景
```csharp
// Task 直接返回结果
Task<int> calculateTask = Task.Run(() => {
    Thread.Sleep(500);
    return 325;
});

int damage = await calculateTask;  // 直接拿到结果
```
> Thread 想返回值要自己封装，非常麻烦

### 场景 3：需要组合、顺序、等待多个任务
```csharp
// 等 A、B 都做完再执行 C
Task.WhenAll(taskA, taskB).ContinueWith(_ => taskC());
```
> 比 Thread 自己实现方便太多

### 场景 4：异步操作（加载资源、网络请求、读写文件）
```csharp
// 加载图片
Task<byte[]> LoadImageAsync(string path);

// 网络请求
Task<string> FetchDataAsync(string url);

// 读写文件
Task WriteFileAsync(string content);
```
> 这些不是很耗 CPU，但要等时间，Task + async/await 是天生最优解

---

## 四、补充知识点：Task 底层原理

### Task 的内部状态机
```
Created → WaitingForActivation → Running → RanToCompletion
                ↓                              ↓
           WaitingToRun                    Faulted
                ↓                              ↓
             Running                       Canceled
```

### Task 状态枚举
| 状态 | 说明 |
|---|---|
| Created | 已创建，未启动 |
| WaitingToRun | 等待被调度 |
| Running | 正在执行 |
| RanToCompletion | 成功完成 |
| Faulted | 出错（抛异常） |
| Canceled | 被取消 |

### Task 和线程池的关系
```
Task.Run() 
    ↓
线程池中有空闲线程？ 
    ↓ 是
    → 分配一个线程执行
    ↓ 否
    → 创建新线程 或 等待
    ↓
Task 完成
    ↓
线程归还到线程池
```

---

## 五、补充知识点：ValueTask（.NET Core 2.0+）

> **ValueTask = Task 的高性能版本，专门解决异步代码中的 GC 问题**

### 什么时候用 ValueTask？
| 情况 | 推荐 |
|---|---|
| 异步方法几乎总是异步执行 | 用 Task |
| 异步方法有时同步完成（缓存命中等） | 用 ValueTask |

### 使用示例
```csharp
// Task 传统写法（总是分配）
async Task<int> GetValueAsync()
{
    return await _cache.GetOrCreateAsync(key);
}

// ValueTask 高性能写法（减少 GC）
async ValueTask<int> GetValueAsync()
{
    if (_cache.TryGet(key, out int value))
        return value;  // 同步返回，不分配
    return await _cache.GetOrCreateAsync(key);
}
```

> ⚠️ 注意：ValueTask 不要重复 await 两次，因为它可能不支持

---

## 六、补充知识点：TaskScheduler

### 什么是 TaskScheduler？
> **TaskScheduler 决定 Task 在哪个线程上执行**

### 内置的 TaskScheduler
| TaskScheduler | 说明 |
|---|---|
| TaskScheduler.Default | 默认线程池调度器 |
| TaskScheduler.FromCurrentSynchronizationContext() | UI 线程调度器 |

### 游戏/Unity 中的使用
```csharp
// 在 Unity 中把 Task 调度到主线程执行
TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

Task.Run(() => {
    // 子线程做计算
    return HeavyCalculation();
}).ContinueWith(result => {
    // 自动回到主线程更新 UI
    text.text = result.ToString();
}, uiScheduler);
```

---

## 七、修正错误汇总表

| 错误认知 | 正确理解 |
|---|---|
| Task 是一个新线程 | ❌ Task 只是一张工单，是否开线程由系统决定 |
| Task.Run 会立刻开一个新线程 | ❌ 线程池会决定是复用还是新建线程 |
| Task 一定比 Thread 好 | ❌ 长任务、独立任务用 Thread 更合适 |
| Task 会自动释放线程 | ❌ Task 完成的是"工作"，线程归还线程池 |
| 可以无限制创建 Task | ❌ 线程池有上限，太多 Task 会排队等待 |
| ValueTask 可以完全替代 Task | ❌ ValueTask 有特殊用途，异步场景 Task 仍然是默认选择 |
