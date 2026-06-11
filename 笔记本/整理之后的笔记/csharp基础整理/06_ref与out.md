====================================================
  C# 基础笔记 · 06 ref 与 out
====================================================

【修订说明】
- 修正：ref 描述"可以把值类型转换为引用类型"说法不准确
         正确理解：ref 是按引用传递，让函数内的修改影响外部变量（不是"类型转换"）
- 补充：ref 和 out 完整规则对比
- 补充：实际使用场景举例

────────────────────────────────────────
一、为什么需要 ref 和 out？
────────────────────────────────────────

// 默认情况下，C# 函数的参数传的是"值的副本"（值类型）
// 或"地址的副本"（引用类型），函数内的修改不一定能影响到外部

// 例 1：值类型参数，函数内修改无效
static void Change(int a)
{
    a = 3;   // 改的是副本
}
int a = 1;
Change(a);
Console.WriteLine(a);   // 输出 1，没有变

// 例 2：引用类型修改元素，有效
static void ChangeElement(int[] arr)
{
    arr[0] = 5;   // 修改堆上的数据，有效
}
int[] nums = { 1, 2, 3 };
ChangeElement(nums);
Console.WriteLine(nums[0]);   // 输出 5，变了

// 例 3：引用类型重新赋值（new），无效
static void ReplaceArray(int[] arr)
{
    arr = new int[] { 10, 20, 30 };   // 函数内的 arr 指向了新内存
                                       // 但外部的 nums 还指向原来的内存
}
int[] nums2 = { 1, 2, 3 };
ReplaceArray(nums2);
Console.WriteLine(nums2[0]);   // 输出 1，没变

// 要解决以上问题，就需要 ref

────────────────────────────────────────
二、ref 关键字
────────────────────────────────────────

// ref：按引用传递，让函数内的修改直接作用于外部变量
// 函数声明和调用时都要加 ref

// 使用 ref 解决值类型传参问题
static void ChangeWithRef(ref int a)
{
    a = 3;   // 直接修改外部的 a
}
int a = 1;
ChangeWithRef(ref a);     // 调用时也要写 ref
Console.WriteLine(a);    // 输出 3（变了！）

// 使用 ref 解决引用类型重新赋值问题
static void ReplaceWithRef(ref int[] arr)
{
    arr = new int[] { 10, 20, 30 };   // 外部的 arr 也指向了新内存
}
int[] nums = { 1, 2, 3 };
ReplaceWithRef(ref nums);
Console.WriteLine(nums[0]);   // 输出 10（变了！）

⚠️ ref 使用规则：
  - 函数声明和调用处都要加 ref
  - 传入的变量必须已经初始化（有值），否则编译报错

────────────────────────────────────────
三、out 关键字
────────────────────────────────────────

// out：和 ref 类似，也是按引用传递，但有以下区别：
// 1. 传入的变量不需要初始化（可以是未赋值的变量）
// 2. 函数内部必须对 out 参数赋值（否则编译报错）
// 主要用途：让函数"输出"多个返回值

static void GetInfo(out int age, out string name)
{
    age = 18;         // 必须在函数内赋值
    name = "张三";    // 必须在函数内赋值
}

// 调用
int myAge;        // 不需要初始化
string myName;    // 不需要初始化
GetInfo(out myAge, out myName);
Console.WriteLine($"{myName}，{myAge}岁");   // 输出：张三，18岁

// C# 7.0+ 简写：可以在调用时直接声明变量
GetInfo(out int age, out string name);
Console.WriteLine($"{name}，{age}岁");

────────────────────────────────────────
四、ref vs out 对比
────────────────────────────────────────

特性                      ref                 out
-------------------------------------------------------
传入前是否需要初始化      ✅ 必须初始化        ❌ 不需要
函数内是否必须赋值        ❌ 不要求            ✅ 必须赋值
主要用途                  读写外部变量         输出多个返回值
函数声明/调用处需要写     ✅ 都要写 ref        ✅ 都要写 out

────────────────────────────────────────
五、实际使用场景
────────────────────────────────────────

// out 的典型场景：int.TryParse（内置函数就用了 out）
// TryParse：尝试转换，成功返回 true，结果通过 out 输出；失败返回 false
string input = Console.ReadLine();
if (int.TryParse(input, out int result))
{
    Console.WriteLine("转换成功：" + result);
}
else
{
    Console.WriteLine("输入的不是数字");
}
// 比 int.Parse 更安全，不会因为输入非法而抛出异常

// ref 的典型场景：交换两个变量的值
static void Swap(ref int a, ref int b)
{
    int temp = a;
    a = b;
    b = temp;
}
int x = 1, y = 2;
Swap(ref x, ref y);
Console.WriteLine($"x={x}, y={y}");   // 输出 x=2, y=1

====================================================
