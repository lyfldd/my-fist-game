# Dictionary<TKey, TValue> 使用场景

---

## 一、核心本质

哈希表、键值对存储、O(1) 极速查询、键唯一。

---

## 二、核心特性

- 通过哈希函数计算键的存储位置，查询速度不受数据量影响（O(1)）
- 键（Key）必须唯一，值（Value）可重复
- 不保证存储顺序，不支持下标访问（不能用 [0] 取元素）
- 泛型类型安全，比非泛型 Hashtable 性能更高、更安全

---

## 三、使用场景

### 场景 1：高频键值对查询（核心场景）

```csharp
Dictionary<int, string> items = new Dictionary<int, string>();
items.Add(1001, "火球术");
string skill = items[1001]; // 按 ID 秒取，O(1)
```

### 场景 2：去重 + 快速映射

```csharp
// 背包物品数量统计
Dictionary<int, int> bagCount = new Dictionary<int, int>();
// Key = 物品ID，Value = 数量
```

### 场景 3：缓存场景

```csharp
// 资源缓存、热点数据缓存
Dictionary<string, object> cache = new Dictionary<string, object>();
if (!cache.ContainsKey("prefab_goblin"))
    cache["prefab_goblin"] = LoadPrefab("goblin");
```

### 场景 4：安全访问（TryGetValue）

```csharp
// 不确定 key 是否存在时，用 TryGetValue 避免 KeyNotFoundException
if (items.TryGetValue(1001, out string skillName))
    Console.WriteLine(skillName);
```

---

## 四、不适用场景

- 需要按顺序遍历、排序（可配合 ToList() 转换，但不如 List 原生）
- 键不唯一的场景（考虑用 List<T> + 自定义逻辑）
- 频繁增删且需要保持顺序（用 List<T> / LinkedList<T>）

---

## 五、时间复杂度速查

| 操作 | 时间复杂度 |
|---|---|
| 按键查询（[key]） | O(1) 均摊 |
| 添加（Add） | O(1) 均摊 |
| 删除（Remove） | O(1) 均摊 |
| 遍历全部 | O(n) |
| Contains Key | O(1) |
