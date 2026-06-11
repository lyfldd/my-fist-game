# 02_Thread 语法详解

## 一、引入命名空间
```csharp
using System.Threading;
```

---

## 二、创建并启动线程（四种重载）

### 重载 1️⃣：`Thread(ThreadStart start)`
- 传一个**无参**的委托方法
- 最简单、最常用

```csharp
// 定义线程要执行的方法
static void NewThread()
{
    Console.WriteLine("新线程执行中...");
}

// 创建并启动
Thread thread = new Thread(NewThread);
thread.Start();
```

**游戏场景**：
- 开启一个后台线程去加载 AssetBundle
- 开启一个线程专门监听网络消息
- 开启一个线程去计算怪物 AI 路径

### 重载 2️⃣：`Thread(ParameterizedThreadStart start)`
- 传一个**带 object 参数**的方法
- 参数只能是 object 类型

```csharp
// 定义带参数的方法
static void Print(object obj)
{
    string msg = obj as string;
    Console.WriteLine($"线程收到参数：{msg}");
}

// 启动
Thread thread1 = new Thread(Print);
thread1.Start("hello");
```

**游戏场景**：
- 线程加载指定 ID 的资源
- 线程处理某个 UID 的玩家逻辑

> ⚠️ 因为是 object，所以需要装箱/拆箱，类型强转时有风险

### 重载 3️⃣：`Thread(ParameterizedThreadStart start, int maxStackSize)`
- 带参数 + **自定义栈大小**
- 参数 maxStackSize：线程栈的最大内存大小（单位：字节）

```csharp
// 申请 10MB 的栈空间
Thread t = new Thread(HeavyWork, 10 * 1024 * 1024);
t.Start(data);
```

**场景**：如果在线程里写了**极深的递归**，或者局部变量特别大（比如定义了一个超大的 struct 栈变量），默认的 1MB 栈可能会溢出（StackOverflowException）。

> ⚠️ **慎用！** 栈空间是独占的，开 100 个线程每个 10MB，就是 1GB 内存！

### 重载 4️⃣：`Thread(ThreadStart start, int maxStackSize)`
- 无参 + **自定义栈大小**，和第3个几乎一样

```csharp
Thread t = new Thread(MyWork, 4 * 1024 * 1024);
t.Start();
```

---

## 三、线程的四个核心方法

### 1. `Start()` —— 启动线程
```csharp
Thread t = new Thread(DoWork);
t.Start();  // 真正启动线程
```

**注意**：不是调用方法，是启动线程。启动后和主线程同时跑。

### 2. `Sleep()` —— 让线程睡觉
```csharp
Thread.Sleep(1000);  // 睡1秒（单位：毫秒）
```

```csharp
static void DoWork()
{
    Console.WriteLine("开始睡觉 1 秒");
    Thread.Sleep(1000);  // 睡1秒
    Console.WriteLine("睡醒了");
}
```

**游戏里的用途**：
| 场景 | 说明 |
|---|---|
| 技能冷却 | 技能释放后等待一段时间 |
| 怪物刷新延迟 | 怪物死亡后延迟刷新 |
| 倒计时 | 显示倒计时 |
| 延迟加载 | 延迟一段时间后加载资源 |

> **重点**：
> - Sleep 是**静态方法**，只能让**当前线程**睡觉
> - 睡觉期间**不占 CPU**
> - **不会释放锁**（后面讲线程安全会用到）

### 3. `Join()` —— 让线程排队
```csharp
t.Start();
t.Join();  // 主线程卡住，等 t 结束再继续
```

**让多个子线程有先后顺序**：
```csharp
t1.Start();
t1.Join();  // 等t1跑完

t2.Start();
t2.Join();  // 等t2跑完

t3.Start();  // t3最后执行
```

**对应关系**：`Thread.Join()` ≈ `Task.Wait()`

### 4. `Interrupt()` —— 强行叫醒睡觉的线程
```csharp
Thread t = new Thread(() =>
{
    try
    {
        Thread.Sleep(5000);  // 睡5秒
    }
    catch (ThreadInterruptedException)
    {
        Console.WriteLine("我被叫醒了！");
    }
});

t.Start();
t.Interrupt();  // 立刻叫醒
```

---

## 四、线程的两个关键属性

### 1. `IsBackground` —— 后台线程
| 类型 | 说明 |
|---|---|
| 前台线程（默认） | 主线程关闭，进程会等它跑完 |
| 后台线程 | 主线程关闭，它直接被杀死 |

```csharp
thread.IsBackground = true;  // 设为后台
```

### 2. `Name` —— 给线程起名字
```csharp
thread.Name = "怪物加载线程";
```

> ✅ 调试时给线程命名非常有用，可以快速定位是哪个线程在执行

---

## 五、完整示例代码
```csharp
static void DoWork()
{
    Console.WriteLine("子线程开始");

    // 睡觉1秒
    Thread.Sleep(1000);

    Console.WriteLine("子线程结束");
}

static void Main(string[] args)
{
    Thread t = new Thread(DoWork);
    t.Name = "工作线程";
    t.IsBackground = true;

    t.Start();       // 启动
    t.Join();        // 主线程等待

    Console.WriteLine("主线程最后执行");
}
```

---

## 六、补充知识点：线程池（ThreadPool）

### 什么是线程池？
> 预先创建一批线程，随时拿来用，用完放回池子，而不是每次都新建/销毁

### 手动创建线程 vs 线程池
| 特性 | 手动创建 Thread | 线程池 |
|---|---|---|
| 创建开销 | 大（每次新建内核对象） | 小（复用已有线程） |
| 销毁开销 | 大 | 无 |
| 管理 | 自己管理生命周期 | 系统自动管理 |
| 数量控制 | 自己控制 | 系统自动控制 |
| 适合场景 | 长生命周期、独立任务 | 短小、频繁的任务 |

### C# 使用线程池
```csharp
// 方式一：ThreadPool.QueueUserWorkItem
ThreadPool.QueueUserWorkItem(_ =>
{
    Console.WriteLine("线程池线程执行中...");
});

// 方式二：Task（推荐，基于线程池）
Task.Run(() => Console.WriteLine("Task 使用线程池"));
```

---

## 七、修正错误汇总表

| 错误认知 | 正确理解 |
|---|---|
| Thread.Sleep() 会释放锁 | ❌ Sleep 不会释放锁，只有 lock 块结束才会 |
| `thread.Start()` 就是调用方法 | ⚠️ 是启动线程，线程和主方法同时跑 |
| 每个任务都 new 一个 Thread 最好 | ❌ 频繁创建/销毁线程开销大，应该用线程池或 Task |
| Interrupt() 可以叫醒任何线程 | ❌ 只能叫醒正在 Sleep/Wait 的线程，对正在执行的无效 |
