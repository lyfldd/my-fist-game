namespace _Game.Config
{
    public enum WorkstationTier
    {
        Hands,         // 徒手
        Campfire,      // 篝火
        SimpleBench,   // 简易工作台
        Furnace,       // 熔炉
        MediumBench,   // 中级工作台
        AdvancedBench, // 高级工作台（精密制造：武器/弹药/护甲/防御/通讯）
        Chemistry,           // 研究中心（工业设备配方解锁）
        Machining,           // 机械加工台（工业设施/电力/车辆/动力）
        ElectronicsAssembly, // 电子装配台（芯片/电路/传感器/精密电子）
        ElementFurnace       // 元素合成炉（终局：聚变核心/高级能源）
    }
}
