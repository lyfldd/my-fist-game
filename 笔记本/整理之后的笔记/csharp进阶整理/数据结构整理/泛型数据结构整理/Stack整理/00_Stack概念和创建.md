Stack<T>（泛型栈）—— 概念与创建
=============================================

一、概念
Stack<T> 是 C# 提供的泛型栈，后进先出（LIFO，Last In First Out）。

using System.Collections.Generic;

> 类比：叠盘子，最后放上去的那个最先被拿走。

栈结构示意：
栈顶  ←── 最先被取出
┌──────┐
│  5   │ ← Push 最后进来
│  4   │
│  3   │
│  2   │
│  1   │ ← 最早进来
└──────┘
栈底  ←── 最后被取出


二、泛型 vs 非泛型

// ❌ 不加泛型 → object 类型，存在装箱拆箱，不推荐
Stack stack = new Stack();

// ✅ 加泛型 → 类型安全，推荐
Stack<string> stack = new Stack<string>();

// 存不同类型时，先打包成类/结构体再装入
Stack<Animal> stack = new Stack<Animal>();


三、创建

// 空栈
Stack<string> stack = new Stack<string>();

// 从数组创建（注意：入栈顺序是集合末尾 → 栈顶）
int[] arr = { 1, 2, 3 };
Stack<int> stack2 = new Stack<int>(arr);
// 栈顶是 3，栈底是 1（第一个入栈的是 1，最后入栈的是 3）


四、核心属性

stack.Count    // 栈中的元素数量
