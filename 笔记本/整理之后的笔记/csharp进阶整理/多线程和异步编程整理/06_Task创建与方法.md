# 06_Task 创建与方法

## 一、Task 创建方式

### 方式 1️⃣：Task.Run（最常用，推荐）
```csharp
Task task = Task.Run(() =>
{
    Console.WriteLine("Task 执行中");
});

// 如果主线程太快，可能不执行就略过，需要加：
task.Wait();
```

**自动特性**：
- 自动从线程池分配线程
- 自动启动，不需要 `task.Start()`
- 自动判断是否为长任务，必要时分配独立线程

---

### 方式 2️⃣：new Task + Start（完整控制）
```csharp
Task task = new Task(() =>
{
    Console.WriteLine("Task 执行中");
});
task.Start();  // 手动启动
```

---

## 二、最完整的 Task 构造函数（4个参数）

```csharp
public Task(
    Action<object?> action,      // 1. 要执行的方法（必须带一个 object 参数）
    object? state,               // 2. 传给上面方法的参数
    CancellationToken,           // 3. 取消令牌
    TaskCreationOptions          // 4. 创建选项
)
```

### 参数 1️⃣：Action<object?> action
**作用**：要执行的方法（必须带一个 object 参数）

```csharp
void Fun(object obj)
{
    for (int i = 0; i < (int)obj; i++)
    {
        Console.WriteLine($"线程执行第{i + 1}次");
    }
}

Task task = new Task(new Action<object?>(Fun), 100);
task.Start();
```

### 参数 2️⃣：object? state
**作用**：传给上面方法的参数

```csharp
Task task = new Task(Fun, 100);  // 100 传给 Fun 的 obj 参数
```

### 参数 3️⃣：CancellationToken cancellationToken
**作用**：取消令牌，用来中途取消任务

```csharp
CancellationTokenSource cts = new CancellationTokenSource();
CancellationToken token = cts.Token;

Task task = new Task(() =>
{
    for (int i = 0; i < 100; i++)
    {
        if (token.IsCancellationRequested)  // 检查取消信号
            return;
        Console.WriteLine($"执行第{i + 1}次");
        Thread.Sleep(100);
    }
}, token);

task.Start();

Console.ReadKey();
cts.Cancel();  // 发送取消信号
task.Wait();
```

### 参数 4️⃣：TaskCreationOptions creationOptions
**作用**：控制任务怎么运行

| 选项 | 说明 |
|---|---|
| `LongRunning` | 告诉线程池：这是长任务，单独开专用线程 |
| `PreferFairness` | 优先让等待久的任务先执行 |
| `AttachedToParent` | 附加到父任务（父子任务关联） |

```csharp
// 长任务：单独开线程，不放线程池
Task task = new Task(DoHeavyWork, 
    TaskCreationOptions.LongRunning);
```

---

## 三、Lambda 替代构造函数的优势

> 用 Lambda 替代构造函数，代码更简洁，类型更安全

### 对比：构造函数 vs Lambda

| 特性 | 构造函数 | Lambda |
|---|---|---|
| 参数传递 | object 需要装箱/拆箱 | 直接捕获变量，类型安全 |
| 取消令牌 | 需要单独传 | 直接捕获 |
| 代码量 | 多 | 少 |
| 可读性 | 较乱 | 清晰 |

### 完整对比示例
```csharp
// ❌ 构造函数写法（麻烦）
CancellationTokenSource cts = new CancellationTokenSource();
CancellationToken token = cts.Token;

void Fun(object obj)
{
    for (int i = 0; i < (int)obj; i++)
    {
        if (token.IsCancellationRequested) return;
        Console.WriteLine($"执行第{i + 1}次");
        Thread.Sleep(100);
    }
}
Task task1 = new Task(Fun, 100, token, TaskCreationOptions.LongRunning);
task1.Start();

// ✅ Lambda 写法（简洁）
int count = 100;
Task task2 = Task.Run(() =>
{
    for (int i = 0; i < count; i++)
    {
        if (token.IsCancellationRequested) return;
        Console.WriteLine($"执行第{i + 1}次");
        Thread.Sleep(100);
    }
}, token);

cts.Cancel();
task2.Wait();
```

---

## 四、Task 的静态方法

| 方法 | 说明 |
|---|---|
| `Task.Run()` | 开启异步任务 |
| `Task.Delay()` | 异步等待一段时间 |
| `Task.WhenAll()` | 等待所有任务完成 |
| `Task.WhenAny()` | 等待任意一个任务完成 |
| `Task.FromResult()` | 用结果创建已完成任务 |
| `Task.FromException()` | 创建带异常的已失败任务 |
| `Task.CompletedTask` | 获取一个已完成的任务 |

---

## 五、Task 的实例方法

| 方法 | 说明 |
|---|---|
| `task.Start()` | 启动任务（new Task 方式需要） |
| `task.Wait()` | 同步等待任务完成 |
| `task.WaitAll()` | 等待多个任务 |
| `task.WaitAny()` | 等待任一任务 |
| `task.ContinueWith()` | 任务完成后继续执行 |
| `task.GetAwaiter()` | 获取等待器 |

---

## 六、Task 的实例属性

| 属性 | 说明 |
|---|---|
| `IsCompleted` | 是否完成 |
| `IsFaulted` | 是否出错 |
| `IsCanceled` | 是否被取消 |
| `Status` | 任务状态（枚举值） |
| `Exception` | 任务异常 |

---

## 七、核心方法详解

### 1. `Wait()` —— 等待单个任务
```csharp
Task task = Task.Run(() =>
{
    Thread.Sleep(1000);
    Console.WriteLine("怪物攻击完成");
});

Console.WriteLine("等待攻击结束...");
task.Wait();  // 主线程卡住，等子线程跑完
Console.WriteLine("攻击结束，主线程继续");
```
> 对应 `Thread.Join()`

---

### 2. `Task<T> + Result` —— 获取返回值
```csharp
Task<int> calculateTask = Task.Run(() =>
{
    Thread.Sleep(500);
    return 325;
});

Console.WriteLine("正在计算伤害...");
int damage = calculateTask.Result;  // 自动等待 + 获取结果
Console.WriteLine($"最终伤害：{damage}");
```

> ⚠️ `.Result` 会阻塞当前线程，用 `await` 更安全

---

### 3. `WaitAll()` —— 等待所有任务完成
```csharp
Task t1 = Task.Run(() => { Thread.Sleep(1000); Console.WriteLine("战士攻击完成"); });
Task t2 = Task.Run(() => { Thread.Sleep(800); Console.WriteLine("射手攻击完成"); });
Task t3 = Task.Run(() => { Thread.Sleep(1200); Console.WriteLine("法师攻击完成"); });

Task.WaitAll(t1, t2, t3);  // 等全部结束
Console.WriteLine("所有角色攻击完毕");
```
> **总时间 ≈ 最慢的那个任务**

---

### 4. `WaitAny()` —— 等待任意一个完成
```csharp
Task t1 = Task.Run(() => { Thread.Sleep(1000); Console.WriteLine("战士击杀BOSS"); });
Task t2 = Task.Run(() => { Thread.Sleep(800); Console.WriteLine("射手击杀BOSS"); });
Task t3 = Task.Run(() => { Thread.Sleep(1200); Console.WriteLine("法师击杀BOSS"); });

int index = Task.WaitAny(t1, t2, t3);  // 任意一个完成就继续
Console.WriteLine($"第 {index + 1} 个角色完成击杀");
```
> **场景**：谁先击杀 BOSS，谁拿奖励

---

### 5. CancellationToken —— 取消任务
```csharp
CancellationTokenSource cts = new CancellationTokenSource();
CancellationToken token = cts.Token;

Task task = Task.Run(() =>
{
    for (int i = 0; i < 100; i++)
    {
        if (token.IsCancellationRequested)  // 检查取消信号
        {
            Console.WriteLine("任务被取消");
            return;
        }
        Console.WriteLine($"技能施放进度：{i + 1}%");
        Thread.Sleep(100);
    }
}, token);

Console.ReadKey();
cts.Cancel();  // 发送取消信号
task.Wait();
```

---

### 6. `ContinueWith()` —— 任务接力
```csharp
Task.Run(() =>
{
    Console.WriteLine("第一步：加载角色模型");
    Thread.Sleep(1000);
})
.ContinueWith((prevTask) =>  // 前一个任务完成后执行
{
    Console.WriteLine("第二步：加载装备");
    Thread.Sleep(800);
})
.ContinueWith((prevTask) =>
{
    Console.WriteLine("第三步：进入游戏");
});
```

---

### 7. 查看任务状态
```csharp
Task task = Task.Run(() =>
{
    Thread.Sleep(1000);
    // throw new Exception("出错了");
});

while (!task.IsCompleted)
{
    Console.WriteLine("任务运行中...");
    Thread.Sleep(200);
}

if (task.IsCompletedSuccessfully)
    Console.WriteLine("任务成功完成");

if (task.IsFaulted)
    Console.WriteLine($"任务出错：{task.Exception}");

if (task.IsCanceled)
    Console.WriteLine("任务被取消");
```

---

### 8. LongRunning —— 长任务
```csharp
// 告诉线程池这是长任务，单独开线程
Task.Factory.StartNew(() =>
{
    while (true)
    {
        Console.WriteLine("后台日志监控运行中...");
        Thread.Sleep(1000);
    }
}, TaskCreationOptions.LongRunning);
```

---

### 9. 线程安全（Task 和 Thread 一样）
```csharp
static int hp = 1000;
static readonly object _lockObj = new object();

Task t1 = Task.Run(() => {
    for (int i = 0; i < 500; i++)
        lock (_lockObj) hp--;
});

Task t2 = Task.Run(() => {
    for (int i = 0; i < 500; i++)
        lock (_lockObj) hp--;
});

Task.WaitAll(t1, t2);
Console.WriteLine($"最终血量：{hp}");  // 一定是 0
```

---

## 八、修正错误汇总表

| 错误认知 | 正确理解 |
|---|---|
| Task.Run 不需要 Start | ✅ 对的，Task.Run 自动启动 |
| .Result 和 await 一样 | ❌ .Result 会阻塞线程，await 不会 |
| WaitAll 等所有完成再执行 | ✅ 正确 |
| Cancel() 会立刻停止任务 | ❌ Cancel 只设置标志，任务内部必须主动检查 IsCancellationRequested |
| ContinueWith 可以链式调用 | ✅ 可以无限链下去 |
| Task 自动处理线程安全 | ❌ 多线程改共享变量必须自己 lock |
