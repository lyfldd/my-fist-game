# LinkedList<T> 使用场景

---

## 一、核心本质

双向链表、非连续内存、任意位置增删 O(1)、不支持下标访问。

---

## 二、核心特性

- 每个节点包含 Value、Previous、Next，节点间通过指针连接
- 任意位置增删 O(1)（只需修改指针，无需移动元素）
- 不支持下标访问，查找元素必须遍历（O(n)）
- 泛型类型安全，无扩容开销

---

## 三、使用场景

### 场景 1：频繁增删，尤其是中间增删

```csharp
// 背包物品频繁在任意位置插入/删除
LinkedList<string> bag = new LinkedList<string>();
bag.AddLast("剑");
var node = bag.Find("剑");
bag.AddAfter(node, "盾"); // O(1) 插入，无需移动其他元素
bag.Remove(node);         // O(1) 删除
```

### 场景 2：双向遍历

```csharp
// 浏览器历史记录前进/后退、文本编辑器光标移动
var cur = bag.First;
while (cur != null) { cur = cur.Next; }  // 正向

var cur2 = bag.Last;
while (cur2 != null) { cur2 = cur2.Previous; } // 反向
```

### 场景 3：LRU 缓存（淘汰最久未使用）

```csharp
// LinkedList 配合 Dictionary 实现 LRU Cache
// 访问时把对应节点移到链表头，淘汰时删除链表尾节点
// Dictionary<key, LinkedListNode<T>> 用于 O(1) 定位节点
LinkedList<int> lruList = new LinkedList<int>();
Dictionary<int, LinkedListNode<int>> map = new Dictionary<int, LinkedListNode<int>>();
```

### 场景 4：行为队列（结合委托）

```csharp
// 将委托方法存入链表，可以灵活在任意位置插入/删除行为
LinkedList<Action> actionChain = new LinkedList<Action>();
actionChain.AddLast(() => Console.WriteLine("移动"));
actionChain.AddLast(() => Console.WriteLine("攻击"));
foreach (var action in actionChain)
    action?.Invoke();
```

---

## 四、不适用场景

- 需要随机访问、按索引操作 → 用 List<T>
- 高频查询场景 → 用 Dictionary<K,V>
- 数据量极大且以遍历为主 → 不如 List<T> 缓存命中率高

---

## 五、时间复杂度速查

| 操作 | 时间复杂度 |
|---|---|
| 头/尾增删（AddFirst/AddLast/RemoveFirst/RemoveLast） | O(1) |
| 已知节点前后插入/删除 | O(1) |
| 按值查找（Find） | O(n) |
| 遍历全部 | O(n) |
| 按下标访问 | 不支持 |
