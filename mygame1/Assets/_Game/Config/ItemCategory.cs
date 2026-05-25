namespace _Game.Config
{
    public enum ItemCategory
    {
        RawMaterial,   // 原材料（木材/石料/草药/矿石/兽皮/碎布料/废金属...）
        SemiFinished,  // 半成品（铁锭/铜锭/高级零件/弹壳/黑火药...）
        Consumable,    // 消耗品（食物/饮水/药品/投掷物）
        Equipment,     // 装备（武器/护甲/头盔/工具）
        Ammo,          // 弹药
        Buildable,     // 建筑（墙体/地板/门窗/陷阱/工业设施）
        Workstation,   // 工作台（可放置物件）
        Functional     // 功能道具（图纸/技能书/种子/钥匙/任务物品）
    }
}
