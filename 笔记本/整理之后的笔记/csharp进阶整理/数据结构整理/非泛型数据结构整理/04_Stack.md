Stack（栈，先进后出）
=============================================

一、概念
Stack（栈）是先进后出（LIFO）的集合。
一头进出：Push（压栈），Pop（弹栈）。
常用于撤销操作、递归模拟、括号匹配、表达式求值。

二、Stack vs Stack<T> 的区别
  对比项         Stack（System.Collections）   Stack<T>（推荐）
  命名空间       System.Collections            System.Collections.Generic
  类型安全       否（存 object）              是
  装箱拆箱       有                          无

三、核心方法

压栈（Push）—— 从栈顶放入
  stack.Push("apple");
  stack.Push("banana");
  stack.Push("cherry");
  // 栈顶是 cherry，最先弹出

弹栈（Pop）—— 取出并删除栈顶，栈为空抛异常
  if (stack.Count > 0)
      var item = stack.Pop();

查看（Peek）—— 只看不删
  var top = stack.Peek();

【原文错误纠正】
原文 `stack.peek()` 大小写错误，正确为 `stack.Peek()`

四、其他方法
  Contains(T item)  → 是否包含
  Clear()           → 清空
  ToArray()         → 转为数组
  Count             → 栈中元素数量

五、修改栈中的元素
Stack<T> 本身没有直接修改指定位置元素的方法。
变通方法（以修改引用类型属性为例）：

Stack<Person> personStack = new Stack<Person>();
personStack.Push(new Person("Alice", 30));
personStack.Push(new Person("Bob", 25));

// 取栈顶 → 修改属性 → 重新压回
var top = personStack.Pop();
top.Age = 26;
personStack.Push(top);

六、循环弹栈（游戏开发常用）
while (stack.Count > 0)
{
    var item = stack.Pop();
    Debug.Log("弹出：" + item);
}
// 典型应用：关闭所有UI面板、撤销所有操作、回溯路径

七、遍历
foreach (var item in stack) { Console.WriteLine(item); }
注意：foreach 遍历不会弹出元素，只是按栈的内部顺序读取。

【补充】Stack 的典型应用场景：
  1. 撤销/回退操作
  2. 递归改循环（手动模拟调用栈）
  3. 括号匹配检验
  4. 深度优先搜索（DFS）
