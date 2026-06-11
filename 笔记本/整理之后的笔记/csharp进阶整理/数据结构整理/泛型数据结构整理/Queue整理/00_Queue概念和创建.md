Queue<T>（泛型队列）—— 概念与创建
=============================================

一、概念
Queue<T> 是 C# 提供的泛型队列，先进先出（FIFO，First In First Out）。

using System.Collections.Generic;

> 类比：排队买票，先来的人先买到，后来的只能排后面。

队列结构示意：
入队方向 →
┌────┬────┬────┬────┐
│ 1  │ 2  │ 3  │ 4  │  → 出队方向
└────┴────┴────┴────┘
  队头（先出）         队尾（后入）


二、创建（3 种重载）

// 1. 无参构造，创建空队列
Queue<int> queue = new Queue<int>();

// 2. 指定初始容量（提前预留内存，减少扩容次数）
Queue<int> queue2 = new Queue<int>(10);

// 3. 从集合/数组创建（保持原有顺序，第一个元素在队头）
int[] arr = { 1, 2, 3 };
Queue<int> queue3 = new Queue<int>(arr);
// 队头是 1，队尾是 3


三、核心属性

queue.Count    // 队列中的元素数量
