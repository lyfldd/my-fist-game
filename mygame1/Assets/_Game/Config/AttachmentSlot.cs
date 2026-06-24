namespace _Game.Config
{
    /// <summary>
    /// 武器附件槽位枚举（前置D）。
    /// 附件挂载到武器/装备对应槽位，修改宿主属性。
    /// </summary>
    public enum AttachmentSlot
    {
        None = 0,
        Muzzle,       // 枪口（消音器）
        Scope,        // 瞄具（红点/全息）
        Underbarrel,  // 下挂（镭射/手电）
        Visor,        // 面部（夜视仪）
        Filter,       // 过滤（防毒面具）
    }
}
