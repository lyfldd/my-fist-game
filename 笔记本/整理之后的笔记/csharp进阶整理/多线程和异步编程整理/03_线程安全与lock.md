# 03_线程安全与 lock

## 一、什么是线程安全？

### 问题场景：多线程同时打怪扣血
```csharp
// 怪物血量
static int hp = 1000;

static void Attack()
{
    // 每次攻击 -1
    hp--;
}

static void Main(string[] args)
{
    Thread t1 = new Thread(Attack);
    Thread t2 = new Thread(Attack);

    t1.Start();
    t2.Start();

    t1.Join();
    t2.Join();

    Console.WriteLine("最终血量：" + hp);
}
```

**期望结果**：1000 - 1 - 1 = 998
**真实结果**：偶尔变成 999，甚至 1000！

### 为什么会有线程安全问题？

一句话讲透：
> **`hp--` 不是一步完成的！**

它在 CPU 里其实是**三步**：
| 步骤 | 操作 |
|---|---|
| 1. 读 | hp 现在是多少？ |
| 2. 改 | 减 1 |
| 3. 写 | 把新值存回去 |

**两个线程同时跑，就会出现这种情况**：
```
线程 A 读到 hp = 1000
线程 B 也读到 hp = 1000
线程 A 减 1 → 999，写回去
线程 B 减 1 → 999，也写回去
结果只减了一次！
```

这就叫：**竞态条件（Race Condition）**

---

## 二、竞态条件的本质

```csharp
static int hp = 100000;

static void Attack()
{
    for (int i = 0; i < 10000; i++)
    {
        hp--;  // 不加锁，必乱
    }
}
```

- 理想状态：80000
- 实际结果：**随机的**（每次运行都不一样）
- 原因：多个线程共享了同一个变量

---

## 三、lock 语法详解

### 核心原理
> **同一时间，只有一个线程能进入 lock 代码块**

### 使用步骤

**1. 定义一个锁对象**
```csharp
private static readonly object _lockObj = new object();
```

**2. 把会冲突的代码包进 lock**
```csharp
lock (_lockObj)
{
    // 这里面的代码，同一时间只会有一个线程执行
    hp--;
}
```

### 完整加锁示例
```csharp
private static readonly object _lockObj = new object();
private static int hp = 100000;

static void Attack()
{
    for (int i = 0; i < 10000; i++)
    {
        lock (_lockObj)  // ✅ 加锁
        {
            hp--;
        }
    }
}

static void Main(string[] args)
{
    Thread t1 = new Thread(Attack);
    Thread t2 = new Thread(Attack);

    t1.Start();
    t2.Start();

    t1.Join();
    t2.Join();

    Console.WriteLine(hp);  // 永远正确：80000
}
```

---

## 四、lock 的三条关键规则

### 规则 1：锁的对象必须是引用类型
| ✅ 可以锁 | ❌ 不能锁 |
|---|---|
| object | int |
| 实例对象 | float |
| 类实例 | bool |
| | enum |
| | string（虽然是引用类型，但不应该锁） |

### 规则 2：锁对象必须是同一个对象
```csharp
// ✅ 正确：所有线程锁同一个对象
private static readonly object _lock = new object();

// ❌ 错误：每次都 new 一个新锁，等于没锁
lock (new object())  // 每个线程锁不同的对象，互不影响
{
    hp--;
}
```

### 规则 3：锁对象尽量用 `private static readonly`
```csharp
private static readonly object _lock = new object();
```
| 关键字 | 作用 |
|---|---|
| `private` | 防止外面代码也乱锁 |
| `static` | 全局共用一把锁 |
| `readonly` | 防止中途被替换成别的对象 |

---

## 五、lock 粒度优化

### 两种写法对比
```csharp
// ❌ 锁整个循环，效率低
for (int i = 0; i < 10000; i++)
{
    lock (_lockObj)
    {
        hp--;
    }
}

// ✅ 只锁关键语句，效率高
for (int i = 0; i < 10000; i++)
{
    lock (_lockObj)
    {
        hp--;
    }
}
```

**性能区别不大**，关键是要**锁的范围尽量小，只锁关键那一句**。

---

## 六、补充知识点：Monitor（lock 的底层实现）

> **lock 其实是 Monitor 的语法糖**

```csharp
// lock 写法
lock (_lockObj)
{
    hp--;
}

// Monitor 等价写法
Monitor.Enter(_lockObj);
try
{
    hp--;
}
finally
{
    Monitor.Exit(_lockObj);  // 必须放 finally 里确保退出
}
```

**Monitor 高级特性**：TryEnter（带超时）
```csharp
if (Monitor.TryEnter(_lockObj, 10))  // 等10ms
{
    try
    {
        hp--;
    }
    finally
    {
        Monitor.Exit(_lockObj);
    }
}
else
{
    // 没拿到锁，直接放弃
}
```

---

## 七、补充知识点：Interlocked（更高效的原子操作）

> **Interlocked 对简单的加减/赋值操作，比 lock 更快**

### 常用方法
| 方法 | 说明 |
|---|---|
| `Interlocked.Increment(ref x)` | 原子加 1 |
| `Interlocked.Decrement(ref x)` | 原子减 1 |
| `Interlocked.Add(ref x, value)` | 原子加指定值 |
| `Interlocked.Exchange(ref x, value)` | 原子赋值 |
| `Interlocked.Read(ref x)` | 原子读取（64位系统） |

### 替代 lock 的简单操作
```csharp
static int hp = 100000;

static void Attack()
{
    for (int i = 0; i < 10000; i++)
    {
        Interlocked.Decrement(ref hp);  // ✅ 比 lock 更高效
    }
}
```

**什么时候用 Interlocked？**
- 只需要简单的 +1、-1、赋值操作 → ✅ 用 Interlocked
- 需要复杂逻辑（if/else/循环） → 用 lock

---

## 八、补充知识点：volatile 关键字

### 作用
> **volatile 告诉编译器：这个变量随时可能被多个线程修改，不要优化掉读取/写入**

```csharp
volatile bool isRunning = true;

static void Main()
{
    // 编译器可能会优化成：只读一次 isRunning
    // 加了 volatile，编译器就不会优化
    while (isRunning)
    {
        // ...
    }
}
```

### volatile 能解决什么问题？
| 问题 | 说明 |
|---|---|
| 编译器优化 | 防止编译器把变量缓存到寄存器 |
| CPU缓存 | 强制从主内存读写，不走 CPU 缓存 |

### volatile 不能替代 lock
> ⚠️ **volatile 只保证"读取/写入的原子性"，不保证复合操作的原子性！**

```csharp
volatile int hp = 1000;

// ❌ 这个操作仍然不是线程安全的
hp--;  // 读、改、写 三步，volatile 保证不了
```

**只能保护单个读/写操作的安全，复合操作（读-改-写）必须用 lock**

---

## 九、修正错误汇总表

| 错误认知 | 正确理解 |
|---|---|
| lock 能加快代码执行速度 | ❌ lock 会让代码变慢，但它保证了正确性 |
| volatile 能替代 lock | ❌ volatile 只保证单次读/写原子，复合操作必须用 lock |
| 所有代码都加 lock 就安全 | ❌ 加太多锁会降低性能，只锁必要的部分 |
| lock 里面 Sleep 没问题 | ❌ 锁里 Sleep 会长时间占用锁，严重影响性能 |
| 锁 int、float 没问题 | ❌ 基本值类型不是引用类型，不能作为 lock 对象 |
| lock 可以嵌套 | ❌ 同一个线程不能重复进入同一个锁，会死锁 |
