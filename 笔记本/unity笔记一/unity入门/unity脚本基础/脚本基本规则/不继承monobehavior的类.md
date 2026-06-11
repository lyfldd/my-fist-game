---
title: "不继承 MonoBehaviour 的类"
date: 2026-04-29
tags:
  - Unity
  - 团结引擎
aliases: ["不继承monobehavior的类"]
source: Unity学习笔记本
---

# 不继承 MonoBehaviour 的类
# 一、概念
团结引擎脚本不是只能继承 MonoBehaviour。
有一类 C# 类，不继承任何 Unity 基类，就是普通的 C# 类。
这类类在团结引擎中有自己的使用场景和规则。


# 二、用法
1. 不继承 MonoBehaviour 的类「不能」挂载在 GameObject 上
   → 只有继承了 MonoBehaviour 的类才能被引擎识别为组件并挂载
   → 把一个普通类直接拖到 GameObject 上不会有任何效果

2. 使用方式：需要自己 new 出来
   → 就像普通 C# 一样，在需要的地方手动 new 一个实例
   → 可以在其他脚本（MonoBehaviour 脚本）内部使用它

   示例：
   // 普通数据类
   public class PlayerData
   {
```csharp
```
   public string name;
   public int level;
   public float hp;
   }

   // 在 MonoBehaviour 脚本中使用
   public class PlayerManager : MonoBehaviour
   {
```csharp
```
   private PlayerData data = new PlayerData();
   }

3. 不需要保留默认的 Start / Update 函数
   → 这两个函数是 MonoBehaviour 的生命周期函数，普通类里写了也没用
   → 普通类就是标准 C#，想怎么写就怎么写

4. 可以正常使用 C# 的一切特性
   → 构造函数、属性、继承、接口、泛型、委托……全部可用
   → 不受 MonoBehaviour 的限制


# 三、原理
MonoBehaviour 之所以特殊，是因为它继承自 Unity 的 Component 类，
而 Component 需要依附在 GameObject 上才能存在。

普通 C# 类不继承 Component，所以引擎的对象系统不认识它，
无法把它当组件挂载，也不会给它调用任何生命周期函数。

普通类的实例生命周期完全由 C# 的垃圾回收（GC）管理，
而不像 MonoBehaviour 那样由引擎的对象系统管理。


# 四、坑点
1. 误在普通类里写 Start / Update
   → 引擎不会主动调用，白写了
   → 如果想要类似的初始化/更新行为，需要自己在构造函数或其他方法中处理

2. 在普通类里调用 Unity API 要注意上下文
   → 部分 Unity API 只能在主线程中调用（比如 GameObject、Transform 操作）
   → 普通类里调用这些 API，要确保是在主线程中执行

3. 普通类无法直接在 Inspector 中显示（默认情况下）
   → 需要加 [System.Serializable] 特性才能被序列化显示在 Inspector 里
   → 但只有当它作为 MonoBehaviour 脚本的成员变量时才会显示


# 五、应用场景
1. 单例管理类
   用于全局管理某个系统（游戏管理器、音频管理器、存档管理器等）
   不需要挂载到 GameObject，全局只需要一个实例

   示例：
   public class GameManager
   {
```csharp
```
   private static GameManager instance;
   public static GameManager Instance
   {
```csharp
```
   get
   {
```csharp
```
if (instance == null)
instance = new GameManager();
return instance;
   }
   }
   }

2. 数据结构类
   用于存储游戏数据，比如角色属性、道具信息、关卡配置等
   类似 C# 里的 DTO（数据传输对象）

   示例：
   public class ItemData
   {
```csharp
```
   public int id;
   public string itemName;
   public int price;
   }

3. 工具类 / 算法类
   提供纯逻辑功能，比如数学计算、路径算法、字符串处理等
   不需要依附于任何游戏对象


# 六、扩展
- ScriptableObject：
  团结引擎提供的另一种数据容器类，继承自 ScriptableObject 而非 MonoBehaviour，
  可以在 Inspector 中编辑，并作为独立的资源文件保存在 Assets 中
  比普通 C# 类更适合做配置数据（技能数据、武器参数等）

- 纯 C# 类 vs ScriptableObject vs MonoBehaviour 的选择：
  纯 C# 类        → 运行时逻辑/数据，不需要 Inspector 可视化
  ScriptableObject → 配置数据，需要在编辑器中编辑和保存
  MonoBehaviour   → 需要挂载到场景对象上、使用生命周期函数的脚本