namespace _Game.Config
{
    /// <summary>
    /// 物品材质标签（前置B）— 影响耐久消耗系数。
    /// 不是完整材质系统，仅一个枚举供 ItemData 选择。
    /// </summary>
    public enum ItemMaterial
    {
        Default = 0,   // 未指定 / 混合 ×1.0
        Cloth   = 1,   // 布料 ×1.5（易损耗）
        Wood    = 2,   // 木质 ×1.3（一般易损）
        Iron    = 3,   // 铁质 ×0.9（略耐用）
        Steel   = 4,   // 钢质 ×0.6（较耐用）
        Carbon  = 5,   // 碳纤维 ×0.4（极耐用）
    }
}
