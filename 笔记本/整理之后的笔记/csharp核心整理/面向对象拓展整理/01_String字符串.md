String 字符串
=============================================

一、string 的本质
string 本质上是一个 char 数组。

string str = "lyf";
Console.WriteLine(str[0]);  // l（按数组下标访问）

// 转为 char 数组
char[] chars = str.ToCharArray();  // ['l', 'y', 'f']

二、字符串拼接
string str = string.Format("{0}{1}", 1123, 214);
// 输出：1123214
// 推荐使用字符串插值：$"{1123}{214}"

三、查找相关方法
IndexOf(char/string)     → 首次出现位置，未找到返回 -1
LastIndexOf(char/string)  → 最后一次出现位置

string s = "lyfyf";
int i = s.LastIndexOf("f");  // 4

四、修改相关方法
Remove(int start, int count)    → 删除指定位置后的字符
Replace(string old, string new)→ 替换字符串
Insert(int index, string)      → 在指定位置插入（补充）
Trim() / TrimStart() / TrimEnd()→ 去除首尾空白

string str = "hello world";
Console.WriteLine(str.Remove(5, 6));    // hello
Console.WriteLine(str.Replace("world", "C#")); // hello C#

五、大小写转换
ToUpper()  → 转大写
ToLower()  → 转小写

六、截取和切割
Substring(int start, int count)  → 从 start 开始截取 count 个字符
Split(params char[] separator)   → 按分隔符切割为字符串数组

string s = "lyfyf";
Console.WriteLine(s.Substring(3, 2));  // yf

string csv = "1,2,3,4,5";
string[] parts = csv.Split(',');  // ["1","2","3","4","5"]

七、判断相关方法（C# 入门遗漏补充）
Contains(string)      → 是否包含子串
StartsWith(string)    → 是否以指定字符串开头
EndsWith(string)      → 是否以指定字符串结尾
IsNullOrEmpty(string) → 是否为 null 或空字符串
IsNullOrWhiteSpace(string) → 是否为 null、空或只含空白

八、string 的特点（重要！）
string 是引用类型，但行为像值类型（不可变性）。
每次对 string 的"修改"实际上会创建新字符串，旧的被丢弃。
频繁拼接字符串会产生大量垃圾，推荐使用 StringBuilder。

string s = "hello";
s = s + " world";  // 创建了新的字符串，s 指向新字符串
