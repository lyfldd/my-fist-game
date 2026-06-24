namespace _Game.Core
{
    /// <summary>
    /// 耐久显示类型 — 决定 UI 标签文案和颜色方案。
    /// </summary>
    public enum DurabilityDisplayType
    {
        /// <summary> 血量 — 建造物/车辆/AIBot本体（红色系）</summary>
        Health,
        /// <summary> 耐久 — 武器/护甲/工具/电子设备（绿/黄/红色系）</summary>
        Durability,
        /// <summary> 磨损 — 生产设备（橙色系）</summary>
        Wear,
    }
}
