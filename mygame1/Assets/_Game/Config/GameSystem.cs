namespace _Game.Config
{
    /// <summary>
    /// 游戏系统标识 — 用于 ItemBehaviourEntry 标记行为归属系统
    /// </summary>
    public enum GameSystem
    {
        Equipment,   // 装备系统（穿上生效：护甲、夜视、防毒面具）
        Combat,      // 战斗系统（武器、弹药特殊效果）
        Survival,    // 生存系统（食物、药品、饮品）
        Building,    // 建造系统（放置建筑/设备生效）
        Vehicle,     // 载具系统（配件、改装）
        Power,       // 电力系统（发电、储电、电网）
        Detection,   // 探测/传感器
        Environment, // 环境（辐射、毒气、温度）
        Hunting,     // 狩猎/陷阱
        Crafting,    // 合成系统
        AIBot,       // AI机器人
        Farming,     // 农耕
        Other        // 其他/未分类
    }
}
