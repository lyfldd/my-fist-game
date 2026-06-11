# 07_async/await 概念

## 一、async / await 是什么？

### 一句话理解
> **async / await = 让异步代码写得跟同步代码一样简单**

### 对比：以前 vs 现在

**以前**：
```csharp
// 开线程 → 等线程 → 继续 → 嵌套 → 乱
Thread t = new Thread(() => {
    DoWork(() => {
        DoMore(() -> {
            // 嵌套越来越深
        });
    });
});
t.Start();
```

**现在**：
```csharp
// 一步一步写，顺序自然，逻辑清晰
async Task DoAllAsync()
{
    await DoWorkAsync();
    await DoMoreAsync();
}
```

---

## 二、async / await 和 Task 的关系

| 概念 | 说明 |
|---|---|
| Task | "要做的事情"，是一张工单 |
| async/await | "优雅地做这件事的语法"，是自动驾驶 |

比喻：
- Task = **车**
- async/await = **自动驾驶**

---

## 三、两个关键字各自的作用

### async
```csharp
async Task 方法名()
{
    // 异步做事
}
```

| 特性 | 说明 |
|---|---|
| 写在方法前面 | 告诉编译器这个方法内部会使用 await |
| 不加 async | 就不能用 await |
| 它本身 | 不创建线程、不开启异步，只是一个"标记" |

### await
```csharp
await 某个Task();
```

| 特性 | 说明 |
|---|---|
| 写在哪里 | 写在一个可等待对象前面（绝大多数是 Task） |
| 作用 | 等待这个任务完成，但**不卡住当前线程** |
| 等待期间 | 线程可以去干别的，不会空转 |

---

## 四、最关键的一句话

> **await 不会阻塞线程，只会"暂停方法"，等任务完了再继续**

| 对比 | 行为 |
|---|---|
| `task.Wait()` | 阻塞线程（死等，啥也不干） |
| `await task` | 不阻塞线程（线程去忙别的，等好了再回来） |

**生活比喻**：
- 你（主线程）在煮开水
- `Wait()` → 死盯着水壶，啥也不干
- `await` → 把水壶放那，你去玩手机，水开了再回来

---

## 五、async 方法的基本写法

### 完整可运行示例
```csharp
using System;
using System.Threading.Tasks;

class Program
{
    // 入口 Main 也可以写成 async Task
    static async Task Main(string[] args)
    {
        Console.WriteLine("开始");

        await DoSomethingAsync();  // 等待完成，不卡死主线程

        Console.WriteLine("结束");
    }

    static async Task DoSomethingAsync()
    {
        await Task.Delay(1000);  // 模拟耗时操作
        Console.WriteLine("异步操作完成");
    }
}
```

### 带返回值的异步方法
```csharp
async Task<int> CalculateDamageAsync()
{
    await Task.Delay(500);
    return 500;
}

// 使用
int damage = await CalculateDamageAsync();  // ✅ 自动拆包
Console.WriteLine(damage);

// ❌ 错误：不要用 .Result
int damage2 = CalculateDamageAsync().Result;  // 会阻塞线程，可能死锁
```

### async 方法的命名规范
```csharp
// ✅ 推荐：以 Async 结尾
async Task LoadSceneAsync() { }
async Task<int> GetPlayerHpAsync() { }

// ❌ 不推荐：没有明确标识
async Task LoadScene() { }
```

---

## 六、await 的五大作用

### 作用 1：等待 Task 完成
```csharp
await task;  // 等待，不阻塞
```

### 作用 2：自动拆包 Task<T> → T
```csharp
int a = await CalcAsync();        // Task<int> → int
string s = await GetStrAsync();   // Task<string> → string
```

### 作用 3：让代码保持顺序，像同步一样流畅
```csharp
await A();
await B();
await C();
// 顺序执行：A → B → C
```
> 没有嵌套，没有回调地狱，结构清晰

### 作用 4：异常处理（await 天然支持）
```csharp
try
{
    await CastSkillAsync(player, enemy);
    Debug.Log("技能施放成功");
}
catch (InvalidOperationException ex)
{
    ShowTip(ex.Message);  // ✅ 直接捕获，不用拆包
}
```

### 作用 5：避免死锁（UI / Unity 必备）
```csharp
// ❌ Wait() / .Result 极易死锁
void BadMethod()
{
    var task = DoAsync();
    task.Wait();  // 主线程死等
    // 如果 DoAsync 需要回到主线程，就会死锁
}

// ✅ await 不会死锁
async Task GoodMethodAsync()
{
    await DoAsync();  // 自动上下文调度，不会死锁
}
```

---

## 七、补充知识点：await 的底层原理

### await 实际上是状态机
```csharp
async Task<int> GetHpAsync()
{
    int hp = await LoadHpAsync();
    return hp;
}
```

编译器会自动生成一个**状态机类**，大致等价于：
```csharp
class GetHpAsyncStateMachine : IAsyncStateMachine
{
    public int MoveNext()
    {
        switch (state)
        {
            case 0:  // 第一次执行
                state = 1;
                var task = LoadHpAsync();
                task.ConfigureAwait(false).GetAwaiter().OnCompleted(...);
                break;
            case 1:  // LoadHpAsync 完成后
                hp = result;
                return;
        }
    }
}
```

### await 的等待机制
```
主线程执行到 await
    ↓
启动子线程做异步操作
    ↓
主线程返回（不阻塞）
    ↓
子线程完成，捕获 continuation（继续体）
    ↓
continuation 排队等待执行
    ↓
回到原上下文继续执行后续代码
```

---

## 八、补充知识点：同步上下文（SynchronizationContext）

### 什么是同步上下文？
> **控制 await 之后代码在哪里执行的东西**

### 不同环境的行为
| 环境 | await 之后在哪里执行 |
|---|---|
| UI 程序（WinForms/WPF） | **UI 线程**（可以安全更新界面） |
| ASP.NET Core | 线程池线程（不需要 UI 线程概念） |
| 控制台程序 | **线程池线程** |
| Unity | **主线程**（可以安全操作 Unity 对象） |

### ConfigureAwait（控制上下文切换）
```csharp
// 默认行为（true）
await Task.Delay(1000);  // 尝试回到原上下文

// 显式不需要回到原上下文（性能更好）
await Task.Delay(1000).ConfigureAwait(false);  // 不管原上下文，直接在线程池执行
```

**什么时候用 ConfigureAwait(false)？**
| 场景 | 是否用 |
|---|---|
| 库/工具类代码（不涉及 UI） | ✅ 用 |
| ASP.NET Core | ✅ 用 |
| UI 程序更新界面 | ❌ 不用 |
| Unity 更新游戏对象 | ❌ 不用 |

---

## 九、async void 的使用注意事项

> ⚠️ **只有这个地方允许 async void**

### 唯一允许的地方：UI 事件 / Unity 事件
```csharp
// 按钮点击事件（系统规定返回值必须是 void）
async void Button_Click(object sender, RoutedEventArgs e)
{
    await DoSomethingAsync();
}
```

### 为什么不能用 async void？
| 返回类型 | 能否捕获异常 | 能否被等待 | 是否安全 |
|---|---|---|---|
| async void | ❌ 异常会直接崩溃 | ❌ 不能被 await | ❌ 危险 |
| async Task | ✅ try/catch 正常 | ✅ 可以 | ✅ 安全 |

### async void 的坑
```csharp
// ❌ 危险：错误不会被 catch，会直接导致程序崩溃
async void RiskyMethod()
{
    throw new Exception("错误");  // 未捕获，程序崩溃
}

// ✅ 安全：错误可以被正常捕获
async Task SafeMethod()
{
    throw new Exception("错误");  // 可以被 catch
}
```

**规则**：**除非是事件处理程序，否则永远不要用 async void**

---

## 十、修正错误汇总表

| 错误认知 | 正确理解 |
|---|---|
| async 会自动开一个新线程 | ❌ async 只是语法糖，不一定开线程（可能是纯异步 IO） |
| await 和 .Result 效果一样 | ❌ .Result 会阻塞，await 不会 |
| async 方法可以没有 await | ❌ 编译会报警告，方法会同步执行 |
| async void 和 async Task 一样 | ❌ async void 危险，只能用于事件处理程序 |
| await 可以用在任何地方 | ❌ await 只能在 async 方法中使用 |
| Task.Run 和 async 是一回事 | ❌ Task.Run 开线程，async 只负责"等待"这件事 |
