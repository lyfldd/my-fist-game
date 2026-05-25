namespace _Game.Systems.Power
{
    /// <summary>
    /// 发电端类型
    /// </summary>
    public enum PowerSourceType
    {
        Human,       // 脚踏发电机 — 消耗体力
        Solar,       // 太阳能板 — 仅白天
        Wind,        // 风车 — 高地间歇
        Water,       // 水车 — 水边持续
        Combustion,  // 简易发电机 — 烧汽油
        Thermal,     // 火力发电站 — 烧煤
        Nuclear      // 核电站 — 烧浓缩铀
    }
}
