# List<T> 使用场景

---

## 一、核心本质

动态数组、自动扩容、连续内存、下标 O(1) 随机访问。

---

## 二、核心特性

- 底层是数组，自动 2 倍扩容，减少内存拷贝次数
- 支持下标访问，尾部增删 O(1)，中间增删 O(n)
- 泛型类型安全，可存储任意类型

---

## 三、使用场景

### 场景 1：动态列表、频繁遍历

```csharp
List<string> items = new List<string>();
items.Add("物品1");
items.Add("物品2");
foreach (var item in items)
    Console.WriteLine(item);
```

### 场景 2：需要随机访问、按索引操作

```csharp
// 背包分页、列表滚动、物品拖拽排序
string item = items[3]; // O(1) 随机访问
```

### 场景 3：数量不确定的动态集合

```csharp
// 玩家身上的 Buff 列表、地图上的敌人列表
List<Enemy> enemies = new List<Enemy>();
enemies.Add(new Enemy("哥布林"));
enemies.Remove(deadEnemy); // 尾部或少量删除可以接受
```

---

## 四、不适用场景

- 频繁在中间插入/删除 → 用 LinkedList<T> 代替
- 超高频键值对查询 → 用 Dictionary<K,V> 代替
- 栈/队列式操作 → 用 Stack<T> / Queue<T>（语义更清晰）

---

## 五、时间复杂度速查

| 操作 | 时间复杂度 |
|---|---|
| 按下标访问 | O(1) |
| 遍历全部 | O(n) |
| 尾部添加（Add） | O(1) 均摊（偶发扩容为 O(n)） |
| 中间插入/删除 | O(n) |
| 查找（Contains） | O(n) |
