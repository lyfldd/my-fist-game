LinkedList<T>（双向链表）—— 概念与创建
=============================================

一、概念
LinkedList<T> 是 C# 已封装好的泛型双向链表，位于 System.Collections.Generic 命名空间。

using System.Collections.Generic;

本质：每个节点都是 LinkedListNode<T> 对象，包含：
- Value：节点存储的值
- Previous：指向前一个节点（头节点的 Previous 为 null）
- Next：指向后一个节点（尾节点的 Next 为 null）

内存结构：
null ← [节点1] ⇆ [节点2] ⇆ [节点3] → null
       Head                    Last


二、创建

// 空链表
LinkedList<int> list = new LinkedList<int>();

// 从数组或集合创建（按集合顺序依次加入链表）
int[] arr = { 1, 2, 3 };
LinkedList<int> list2 = new LinkedList<int>(arr);
// 队头是 1，队尾是 3


三、核心属性

list.Count        // 元素数量
list.First        // 头节点（LinkedListNode<T>），链表为空时为 null
list.Last         // 尾节点（LinkedListNode<T>），链表为空时为 null
list.First.Value  // 头节点的值
list.Last.Value   // 尾节点的值
