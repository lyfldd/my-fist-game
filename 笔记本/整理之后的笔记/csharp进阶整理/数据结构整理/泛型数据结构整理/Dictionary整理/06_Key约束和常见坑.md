Dictionary<TKey, TValue> —— Key 约束和常见坑
=============================================

一、Key 的三大约束

1. Key 不能为 null（泛型字典不允许）
   dict.Add(null, 1);  // ❌ 编译报错

2. Key 不能重复（重复则覆盖值）
   dict["name"] = "张三";
   dict["name"] = "李四";  // 自动覆盖，无报错

3. Key 的类型必须正确实现 GetHashCode() 和 Equals()
   → 内置类型（int、string）已实现，可直接用作 Key
   → 自定义类型作为 Key 时，需要重写这两个方法！


二、自定义类型作为 Key（必须重写）

class PersonKey
{
    public string Name;
    public int Age;

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Age);
    }

    public override bool Equals(object obj)
    {
        if (obj is PersonKey other)
            return Name == other.Name && Age == other.Age;
        return false;
    }
}

// 用法
Dictionary<PersonKey, int> scoreDict = new Dictionary<PersonKey, int>();
var p1 = new PersonKey { Name = "张三", Age = 20 };
var p2 = new PersonKey { Name = "张三", Age = 20 };
// p1 和 p2 的哈希码相同，Equals 也为 true，所以视为同一个 Key
scoreDict.Add(p1, 100);
scoreDict.Add(p2, 200);  // 覆盖 p1 的值


三、常见坑汇总

| 问题 | 说明 |
|---|---|
| 键不存在时用索引器取值 | 抛 KeyNotFoundException，用 TryGetValue 代替 |
| Key 为 null | 泛型字典不允许 null（Hashtable 可以） |
| 用可变类型作 Key | Key 的哈希码依赖内部字段，修改后哈希码变，字典状态混乱 |
| ContainsValue 性能差 | 时间复杂度 O(n)，数据量大时注意 |


四、应用场景

- 根据唯一标识（ID、名字）快速查找对象
- 缓存系统（缓存 key → value）
- 词频统计
- 配置字典
