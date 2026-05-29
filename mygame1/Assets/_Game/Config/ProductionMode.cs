namespace _Game.Config
{
    /// <summary>
    /// 生产模式 — 物品适合手工做还是工业量产
    /// </summary>
    public enum ProductionMode
    {
        Manual,      // 只能手工（高技能门槛，偶尔搓一件）
        Industrial,  // 只能工业量产（设备自动运转）
        Both         // 两种都行（手工入门 → 工业升级）
    }
}
