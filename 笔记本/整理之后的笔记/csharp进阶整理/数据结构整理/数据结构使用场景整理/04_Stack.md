# Stack<T> 使用场景

---

## 一、核心本质

后进先出（LIFO）、栈顶操作 O(1)、基于数组实现。

---

## 二、核心特性

- 只能操作栈顶：Push()（入栈）、Pop()（出栈取并删除）、Peek()（取栈顶不删除）
- 不支持随机访问，不支持中间元素操作
- 泛型类型安全，自动扩容

---

## 三、使用场景

### 场景 1：UI 层级/页面回退

```csharp
// 打开背包 → 打开物品栏，关闭时先关物品栏 → 再关背包
Stack<string> uiStack = new Stack<string>();
uiStack.Push("背包");
uiStack.Push("物品栏");
string current = uiStack.Pop(); // 先关物品栏
```

### 场景 2：撤销/回退操作

```csharp
// 玩家建造撤销、文本编辑撤销（Ctrl+Z）
Stack<ICommand> undoStack = new Stack<ICommand>();
undoStack.Push(buildAction);
undoStack.Pop().Undo(); // 撤销上一步
```

### 场景 3：深度优先搜索（DFS）

```csharp
// 迷宫寻路、地图探索、递归的迭代实现（避免栈溢出）
Stack<Node> dfsStack = new Stack<Node>();
dfsStack.Push(startNode);
while (dfsStack.Count > 0)
{
    Node node = dfsStack.Pop();
    // 处理节点，把相邻节点压栈
}
```

### 场景 4：括号/表达式合法性验证

```csharp
// 判断括号是否匹配
Stack<char> stack = new Stack<char>();
foreach (char c in expression)
{
    if (c == '(') stack.Push(c);
    else if (c == ')' && stack.Count > 0) stack.Pop();
}
bool isValid = stack.Count == 0;
```

---

## 四、不适用场景

- 需要随机访问、按顺序遍历 → 用 List<T>
- 先进先出的排队场景 → 用 Queue<T>

---

## 五、时间复杂度速查

| 操作 | 时间复杂度 |
|---|---|
| Push（入栈） | O(1) 均摊 |
| Pop（出栈） | O(1) |
| Peek（查栈顶） | O(1) |
| Contains（查找） | O(n) |
| 遍历全部 | O(n) |
