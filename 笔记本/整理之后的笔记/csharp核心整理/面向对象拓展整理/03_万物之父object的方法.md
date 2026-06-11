万物之父 object 的方法
=============================================

object 是所有类型的基类，以下是 object 的主要方法：

一、静态方法

1. Equals(object a, object b)
   比较两个对象是否相等。判断权在左边对象。
   - 值类型：比较值是否相等
   - 引用类型：比较是否引用同一个对象

2. ReferenceEquals(object a, object b)
   比较两个引用是否指向同一内存地址。
   注意：值类型参数传入时会发生装箱，总返回 false。

二、实例方法

1. Equals(object obj)
   判断当前对象与参数对象是否相等。
   object 基类中比较引用，派生类通常重写此方法。
   派生类重写 Equals 时，应同时重写 GetHashCode。

2. GetHashCode()
   返回对象的哈希码，用于哈希表（Dictionary、HashSet 等）存储。
   object 基类中基于内存地址生成哈希码。
   重写 Equals 时必须重写 GetHashCode，保证相等的对象返回相同哈希码。

3. ToString()
   返回对象的字符串表示。
   object 基类返回"命名空间.类名"。
   大多数类重写了此方法（如 int.ToString() 返回数字字符串）。

4. GetType()
   返回对象的运行时类型（Type 对象）。
   与反射相关。

5. MemberwiseClone()
   创建当前对象的浅表副本。
   - 值类型字段：逐位复制（副本独立）
   - 引用类型字段：只复制引用（副本和原对象指向同一引用对象）

【补充】Equals 的正确重写示例：
class Person
{
    public string Name { get; set; }
    public int Age { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is Person p)
            return Name == p.Name && Age == p.Age;
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Age);
    }
}
