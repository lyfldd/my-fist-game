Dictionary<TKey, TValue>（泛型字典）—— 概念与创建
=============================================

一、概念
Dictionary<TKey, TValue> 是 Hashtable 的泛型升级版。
键值对存储，类型安全，无需装箱拆箱，性能更好。
基于哈希表实现，平均查找/插入/删除时间复杂度为 O(1)。

命名空间：using System.Collections.Generic;


二、Dictionary vs Hashtable 核心对比

  对比项         Hashtable                 Dictionary<K,V>
  类型安全       否（存 object）            是
  装箱拆箱       有                        无
  键为 null     不允许                    不允许
  常用性         历史遗留                   推荐使用

说明：
- Hashtable 存 object，值类型会触发装箱（性能损耗），取出来还要拆箱
- Dictionary<K,V> 是泛型，编译时就确定类型，类型安全，无额外开销


三、创建和初始化

// 推荐写法
Dictionary<string, int> dict = new Dictionary<string, int>();

// 集合初始化器（花括号语法）
Dictionary<int, string> dict2 = new Dictionary<int, string>
{
    { 1, "张三" },
    { 2, "李四" }
};

// 指定初始容量（减少扩容次数）
Dictionary<string, int> dict3 = new Dictionary<string, int>(100);
