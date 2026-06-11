Hashtable（哈希表）
=============================================

一、概念
Hashtable 是一种键值对（Key-Value）存储结构。
通过 Key（键）直接计算出索引（哈希码），实现 O(1) 平均查找速度。
属于 System.Collections 命名空间，存储 object 类型。

核心规则：
  Key 不能重复（重复会覆盖）
  Key 不能为 null
  Value 可以为 null

二、Hashtable vs Dictionary<K,V> 的区别
  对比项           Hashtable                Dictionary<K,V>
  命名空间         System.Collections       System.Collections.Generic
  类型安全         否（存 object）         是
  装箱拆箱         有                      无
  线程安全         有 Synchronized()       无（需 ConcurrentDictionary）
  键可为 null     否                      否
  常用性           历史遗留                推荐使用

三、底层原理
底层是一个数组（桶），每个位置存一个键值对节点。

存数据过程：
  1. 计算 Key 的哈希码（GetHashCode）
  2. 通过算法转换为数组索引
  3. 把键值对存入对应位置

哈希冲突解决方式（C# Hashtable 用链地址法）：
  两个 Key 算到同一个索引 → 拉出一条链表挂在后面

四、常用方法

增加 / 修改：
  Add(key, value)     → 添加（key 重复抛异常）
  this[key] = value   → 添加或修改（不存在则添加，存在则覆盖）

删除：
  Remove(key)          → 删除指定键
  Clear()              → 清空所有

查找：
  this[key]            → 通过键取值（键不存在返回 null）
  ContainsKey(key)     → 是否包含某键（推荐，比 this[key] 安全）
  ContainsValue(value) → 是否包含某值（需遍历，慢）
  Contains(key)        → 与 ContainsKey 完全一样

遍历：
  foreach (var key in ht.Keys)  → 遍历所有键
  foreach (var value in ht.Values) → 遍历所有值
  foreach (DictionaryEntry item in ht) → 遍历键值对

五、代码示例
Hashtable ht = new Hashtable();
ht.Add("name", "张三");
ht.Add("age", 20);

ht["score"] = 95;  // 不存在则添加
ht["name"] = "李四"; // 存在则覆盖

if (ht.ContainsKey("name"))
{
    Console.WriteLine(ht["name"]);  // 李四
}

foreach (DictionaryEntry item in ht)
{
    Console.WriteLine($"Key: {item.Key}, Value: {item.Value}");
}
