List<T>（泛型动态数组）—— 概念与创建
=============================================

一、概念
List<T> 是 ArrayList 的泛型升级版，属于 System.Collections.Generic 命名空间。
无需装箱拆箱，类型安全，性能更好，是 C# 中最常用的集合。

命名空间：using System.Collections.Generic;


二、ArrayList vs List<T> 核心对比

  对比项         ArrayList                List<T>
  类型           object（不安全）         T（类型安全）
  装箱拆箱       有                       无
  推荐程度       历史遗留，不推荐         最推荐

说明：
- ArrayList 存 object，值类型会触发装箱（性能损耗），取出来还要拆箱
- List<T> 是泛型，编译时就确定类型，类型安全，无额外开销


三、创建和初始化

// 推荐写法
List<int> list = new List<int>();

// 用 var 推断（类型由右边决定）
var list2 = new List<int>();

// 指定初始容量（减少扩容次数，适合大数据量预估）
List<int> list3 = new List<int>(10);

// 从数组初始化
int[] arr = { 1, 2, 3 };
List<int> list4 = new List<int>(arr);

// 从另一个集合初始化（只要实现了 IEnumerable<T>）
List<int> list5 = new List<int>(list4);


四、Count vs Capacity（重要概念）

  Count     → 实际元素数量
  Capacity  → 内部数组容量（可以手动设置，但不建议）

规则：
- 元素数量超过 Capacity 时，自动扩容（通常是翻倍）
- TrimExcess() 可以将 Capacity 压缩为 Count，节省内存
- 小列表不必手动调 TrimExcess，意义不大
