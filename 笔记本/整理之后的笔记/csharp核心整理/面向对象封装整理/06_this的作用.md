this 的作用
=============================================

this 关键字代表当前类的实例对象本身。
主要用在构造函数和成员方法内部。

一、解决成员变量与参数的命名冲突
class Person
{
    private string name;
    private int age;

    public Person(string name, int age)
    {
        this.name = name;   // this.name 指成员变量，name 是参数
        this.age = age;     // this.age   指成员变量，age   是参数
    }
}

二、返回自身（用于链式调用）
class Person
{
    private string name;

    public Person SetName(string name)
    {
        this.name = name;
        return this;  // 返回自身，实现链式调用
    }

    public void Introduce()
    {
        Console.WriteLine("我是" + name);
    }
}

使用：
new Person().SetName("张三").SetName("李四").Introduce();

三、在构造函数中调用另一个构造函数（见"构造函数"章节的 :this() 部分）

四、索引器中使用 this（见"索引器"章节）

五、注意事项
1. this 不能在静态方法中使用（静态方法不属于实例）
2. 局部变量会遮蔽同名的成员变量，此时需要用 this 区分
