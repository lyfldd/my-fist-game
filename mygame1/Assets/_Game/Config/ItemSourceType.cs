namespace _Game.Config
{
    /// <summary> 物品获取来源 </summary>
    public enum ItemSourceType
    {
        None = 0,
        Tree,                // 树木（砍伐）
        Mine,                // 矿山（挖掘）
        Container_Medical,   // 医疗容器（搜索）
        Container_Kitchen,   // 厨房容器（搜索）
        Container_Garage,    // 车库容器（搜索）
        Container_Ammo,      // 弹药容器（搜索）
        Container_General,   // 通用容器（搜索）
        ZombieLoot,          // 僵尸掉落
        Scavenge,            // 世界搜刮
        Craft,               // 制作（工作台/工业）
        Trade,               // 交易（预留）
    }

    /// <summary> 物品获取动作 </summary>
    public enum ItemObtainAction
    {
        None = 0,
        Chop,       // 砍伐
        Mine,       // 挖掘
        Search,     // 搜索容器
        Craft,      // 制作
        Pickup,     // 拾取
        Loot,       // 搜刮/掉落
        Trade,      // 交易（预留）
    }
}
