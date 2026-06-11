---
title: "MonoBehaviour 基类"
date: 2026-04-29
tags:
  - Unity
  - 团结引擎
aliases: ["monobehavior基类"]
source: Unity学习笔记本
---

# MonoBehaviour 基类
# 一、概念
MonoBehaviour 是团结引擎中最核心的脚本基类。
所有需要挂载到 GameObject 上的脚本，都必须继承它。

继承链：
  MonoBehaviour → Behaviour → Component → UnityEngine.Object

正是这条继承链，让脚本可以作为"组件"挂载在 GameObject 上，
并拥有访问场景、对象、组件的各种能力。


# 二、用法（继承 MonoBehaviour 的规则）
1. 继承了 MonoBehaviour 的类才可以挂载到 GameObject 上

2. 继承了 MonoBehaviour 的类「不可以在代码里 new」
   → 错误写法：MyScript obj = new MyScript();
   → MonoBehaviour 实例必须通过引擎的组件系统创建（挂载或 AddComponent）
   → 正确做法：
```csharp
```
 gameObject.AddComponent<MyScript>();  // 代码动态添加组件

3. 继承了 MonoBehaviour 的类「不要写构造函数」
   → 因为我们不会手动 new 它，构造函数没有意义
   → 初始化工作统一放在 Awake() 或 Start() 生命周期函数中

4. 一个 GameObject 上可以挂载「多个」同类脚本
   → 如果不想允许重复挂载，在类上加特性：[DisallowMultipleComponent]
   → 加了之后，再次添加该脚本时引擎会弹出提示阻止

5. 继承了 MonoBehaviour 的脚本「可以再次被继承」
   → 遵循 C# 面向对象的继承和多态规则
   → 可以把公共逻辑放在父类脚本里，子类脚本继承后扩展

   示例：
   public class BaseEnemy : MonoBehaviour
   {
```csharp
```
   protected virtual void Attack() { ... }
   }
   public class BossEnemy : BaseEnemy
   {
```csharp
```
   protected override void Attack() { ... }
   }


# 三、原理
MonoBehaviour 继承自 Component，而 Component 本身就是团结引擎对象系统的一部分。
GameObject 不是靠 C# 的继承来实现功能扩展的，
而是靠「挂载多个 Component 组件」来组合功能（组合模式）。

MonoBehaviour 作为 Component，它的实例生命周期由引擎对象系统管理，
不是由 C# 的垃圾回收（GC）直接管理。
这就是为什么销毁对象要用 Destroy()，而不是直接赋 null：
  Destroy(gameObject);     // 正确：通知引擎销毁对象
  gameObject = null;       // 错误（不会真正销毁，只是断开引用）


# 四、坑点
1. 手动 new MonoBehaviour 子类
   → 引擎会报警告："You are trying to create a MonoBehaviour using the new keyword."
   → new 出来的实例没有 GameObject 依附，生命周期函数不会被调用
   → 这种用法几乎总是错误的

2. 在构造函数里访问 Unity API
   → MonoBehaviour 的构造函数在特殊时机执行，此时运行环境可能未就绪
   → 访问 transform、gameObject 等 Unity API 可能会报错
   → 解决方案：所有初始化放 Awake()

3. 销毁时不要用 = null
   → Destroy(this) → 销毁这个组件
   → Destroy(gameObject) → 销毁整个对象
   → 把变量设为 null 不会销毁引擎对象，只是断开了 C# 的引用

4. 判断 Unity 对象是否为空的坑
   → 对象被 Destroy 后，用 == null 判断会返回 true（引擎重写了 == 运算符）
   → 但该对象的 C# 托管对象并没有立刻被 GC 回收
   → 不要在被销毁的对象上继续访问字段，会报 NullReferenceException


# 五、应用场景
- 玩家控制器、敌人 AI、道具拾取、UI 管理等挂载到 GameObject 的功能脚本
- 需要使用生命周期函数的所有脚本都需要继承 MonoBehaviour
- 需要访问 transform、gameObject、GetComponent 等 Unity API 的脚本


# 六、扩展
- MonoBehaviour 提供的常用 API（后续笔记专门整理）：
  GetComponent<T>()       → 获取同一对象上的其他组件
  Instantiate()           → 实例化 Prefab 或克隆对象
  Destroy()              → 销毁对象或组件
  StartCoroutine()        → 开启协程
  Invoke()                → 延迟调用方法
  InvokeRepeating()       → 重复调用方法

- [DisallowMultipleComponent] 特性：
  加在类定义上方，阻止同一 GameObject 重复挂载该脚本

- [RequireComponent(typeof(Rigidbody))] 特性：
  加在类定义上方，挂载该脚本时引擎会自动检查并添加所需的依赖组件