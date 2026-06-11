StringBuilder
=============================================

一、为什么需要 StringBuilder
string 每次修改都会创建新对象（不可变性），造成内存浪费。
StringBuilder 是可变字符串，在原对象上修改，不产生垃圾。

二、使用方法
using System.Text;  // 需要引用命名空间

StringBuilder sb = new StringBuilder("lyflyf", 50);
// 第一个参数：初始内容
// 第二个参数：初始容量

三、StringBuilder 的容量
- 默认初始容量为 16 个字符
- 当容量不够时，自动扩容（容量翻倍，不会产生垃圾）
- 可以通过 Capacity 属性查看和设置容量

四、常用方法
Append(string)       → 追加
AppendFormat(...)    → 追加格式化字符串
Insert(int, string)  → 在指定位置插入
Remove(int, int)     → 删除
Clear()              → 清空
Replace(string, string) → 替换

StringBuilder sb = new StringBuilder();
sb.Append("hello");
sb.AppendLine(" world");        // 追加并换行
sb.AppendFormat("{0} + {1} = {2}", 1, 2, 3);
sb.Insert(0, "start:");         // 在开头插入
sb.Remove(0, 7);                 // 删除前7个字符
sb.Replace("world", "C#");       // 替换
sb.Clear();                      // 清空

五、StringBuilder 的局限性
StringBuilder 没有 string 那么多的内置方法（如 IndexOf、Split 等）。
如果需要这些方法，最终还是要调用 sb.ToString() 转为 string。

六、使用场景
- 循环中拼接字符串
- 动态构建长字符串（如日志、HTML/XML 生成）
- 不确定长度的字符串构建
