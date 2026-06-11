Queue（队列，先进先出）
=============================================

一、概念
Queue（队列）是先进先出（FIFO）的集合。
一头进（Enqueue），一头出（Dequeue）。
常用于任务排队、消息队列、广度优先搜索（BFS）。

二、Queue vs Queue<T> 的区别
  对比项         Queue（System.Collections）   Queue<T>（推荐）
  命名空间       System.Collections            System.Collections.Generic
  类型安全       否（存 object）              是
  装箱拆箱       有                          无

三、创建和初始化
Queue<T> q = new Queue<T>();              // 默认容量 32
Queue<T> q = new Queue<T>(100);           // 指定初始容量
Queue<T> q = new Queue<T>(array);         // 从数组或 List 初始化

四、核心方法

入队（Enqueue）—— 加到队尾
  q.Enqueue("A");
  q.Enqueue(100);

出队（Dequeue）—— 取出并删除队头，队列为空抛异常
  var item = q.Dequeue();

查看（Peek）—— 只看不删，队列为空抛异常
  var item = q.Peek();

安全方法（推荐，不抛异常）：
  q.TryDequeue(out var item)  → 成功返回 true，失败返回 false
  q.TryPeek(out var item)    → 成功返回 true，失败返回 false

【原文错误纠正】
原文 Peek 示例中写了 int i=(int)queue.Peek();
这是非泛型的写法，泛型 Queue<T> 的 Peek 返回 T 类型，不需要强转！

五、其他方法
  Contains(T item)  → 是否包含
  Clear()           → 清空
  TrimExcess()      → 释放多余容量
  ToArray()         → 转为数组
  CopyTo(array, i)  → 复制到数组

六、属性
  Count  → 实际元素数量
  Capacity → 内部容量（自动管理，不需关心）

七、遍历
foreach (var item in q) { Console.WriteLine(item); }

【补充】Queue 的典型应用场景：
  1. 任务调度系统
  2. 打印队列
  3. BFS（广度优先搜索）
