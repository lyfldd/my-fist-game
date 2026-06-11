# Queue<T> 使用场景

---

## 一、核心本质

先进先出（FIFO）、队尾入队/队头出队 O(1)、基于数组实现。

---

## 二、核心特性

- 只能操作队头/队尾：Enqueue()（入队）、Dequeue()（出队取并删除）、Peek()（取队头不删除）
- 不支持随机访问，不支持中间元素操作
- 泛型类型安全，自动扩容

---

## 三、使用场景

### 场景 1：顺序执行/排队

```csharp
// 技能连击队列、任务执行队列、怪物波次刷新
Queue<string> skillQueue = new Queue<string>();
skillQueue.Enqueue("攻击");
skillQueue.Enqueue("跳跃");
skillQueue.Enqueue("防御");
string next = skillQueue.Dequeue(); // 按顺序执行：攻击
```

### 场景 2：广度优先搜索（BFS）

```csharp
// 最短路径寻路、幽灵追踪玩家、地图层序遍历
Queue<Node> bfsQueue = new Queue<Node>();
bfsQueue.Enqueue(startNode);
while (bfsQueue.Count > 0)
{
    Node node = bfsQueue.Dequeue();
    // 把相邻节点加入队列
}
```

### 场景 3：异步/多线程任务队列

```csharp
// 网络消息队列、UI 事件队列
Queue<Action> taskQueue = new Queue<Action>();
taskQueue.Enqueue(() => Console.WriteLine("处理任务1"));
taskQueue.Dequeue()?.Invoke();
```

### 场景 4：安全出队（TryDequeue）

```csharp
// 不确定队列是否为空时，用 TryDequeue 避免 InvalidOperationException
if (skillQueue.TryDequeue(out string skill))
    Console.WriteLine($"执行技能：{skill}");
```

---

## 四、不适用场景

- 需要后进先出的回退场景 → 用 Stack<T>
- 需要随机访问、中间操作 → 用 List<T>

---

## 五、时间复杂度速查

| 操作 | 时间复杂度 |
|---|---|
| Enqueue（入队） | O(1) 均摊 |
| Dequeue（出队） | O(1) |
| Peek（查队头） | O(1) |
| Contains（查找） | O(n) |
| 遍历全部 | O(n) |
