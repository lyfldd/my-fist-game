namespace _Game.Config
{
    /// <summary>
    /// 装备位枚举
    /// None = 不是装备（普通物品）
    /// </summary>
    public enum EquipSlot
    {
        None,           // 不是装备
        Tops,           // 上衣（决定上衣口袋大小）
        Pants,          // 裤子（决定裤子口袋大小）
        Belt,           // 腰带（决定快速栏大小）
        Vest,           // 胸挂（决定胸挂格大小）
        Backpack,       // 背包（决定主存储格大小）
        Head,           // 头盔（无容器，仅护甲+显示）
        BodyArmor,      // 防弹衣（单独护甲层）
        RightHand,      // 主武器（始终可用）
        LeftHand,       // 副武器（始终可用）
        KnifeBelt,      // 小刀位（需腰带解锁）
        SidearmBelt     // 手枪位（需腰带解锁）
    }
}
