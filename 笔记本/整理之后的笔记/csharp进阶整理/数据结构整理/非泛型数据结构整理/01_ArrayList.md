ArrayList（非泛型动态数组）
=============================================

一、概念
ArrayList 是非泛型集合（System.Collections 命名空间）。
可以存储任意类型的对象，内部用 object[] 实现。
存储值类型会发生装箱，取出需要拆箱（存在性能损耗）。

命名空间：using System.Collections;

二、ArrayList vs List<T> 的区别
  对比项          ArrayList              List<T>
  命名空间        System.Collections     System.Collections.Generic
  类型安全        否（存 object）        是（类型确定）
  装箱拆箱        有（性能损耗）         无
  常用性          历史遗留，不推荐       最常用
  API             基本一致              基本一致

三、创建和初始化
ArrayList list1 = new ArrayList();           // 默认初始容量 0
ArrayList list2 = new ArrayList(10);        // 指定初始容量 10
int[] nums = { 1, 2, 3 };
ArrayList list3 = new ArrayList(nums);      // 从数组初始化

四、增加元素
Add(T item)         → 末尾添加，返回插入索引
AddRange(collection)→ 批量添加（数组/List/实现了 IEnumerable 的集合）

ArrayList list = new ArrayList();
list.Add("apple");
list.Add(100);          // 值类型装箱
list.AddRange(new[] { "banana", "cherry" });

五、删除元素
Remove(object item)   → 删除第一个匹配项（成功返回 true）
RemoveAt(int index)   → 删除指定索引
RemoveRange(i, count) → 删除从索引 i 开始的 count 个元素
Clear()               → 清空所有

六、查找元素
IndexOf(object)       → 第一次出现的索引，未找到返回 -1
LastIndexOf(object)   → 最后一次出现的索引
Contains(object)      → 是否包含指定元素

// 三个重载
list.IndexOf("apple");                       // 整个集合查找
list.IndexOf("apple", 2);                   // 从索引2开始找
list.IndexOf("apple", 1, 3);                // 从索引1开始，找3个元素

七、访问和修改
通过索引器：list[0] = 100;

八、排序和反转
Sort()                 → 排序（元素类型需实现 IComparable）
Sort(IComparer)       → 自定义排序
Reverse()              → 反转全部顺序
Reverse(int, int)      → 反转指定区间

九、容量管理
Capacity 属性 → 获取/设置容量
TrimToSize()  → 将容量压缩为实际元素数量

十、类型安全（重要！）
ArrayList 存的是 object，访问时必须判断类型：

ArrayList list = new ArrayList();
list.Add(10);
object item = list[0];

// 用 is 判断类型（推荐）
if (item is int num)        // C# 7.0+ 模式匹配
{
    Console.WriteLine(num);
}

// 用 as 转换（仅限引用类型）
string s = item as string;
if (s != null) Console.WriteLine(s);
