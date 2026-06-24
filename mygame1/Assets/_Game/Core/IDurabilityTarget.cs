namespace _Game.Core
{
    /// <summary>
    /// 耐久目标统一接口。
    /// 所有有耐久的实体实现此接口，DurabilityHUD 通过准星检测统一显示。
    /// </summary>
    public interface IDurabilityTarget
    {
        /// <summary> 当前耐久比 0~1 </summary>
        float DurabilityRatio { get; }
        /// <summary> 显示名称（准星浮条上显示） </summary>
        string DisplayName { get; }
        /// <summary> 耐久类型 — UI 据此选择标签和颜色 </summary>
        DurabilityDisplayType DisplayType { get; }
    }
}
