namespace _Game.Config
{
    /// <summary>
    /// 电子设备类型（前置C）— 决定运行时行为。
    /// </summary>
    public enum ElectronicDeviceType
    {
        Flashlight = 0,  // 手电筒 — SpotLight开关
        Lighter    = 1,  // 打火机 — 短暂点火
        Compass    = 2,  // 指南针 — HUD方向
        Watch      = 3,  // 手表 — HUD时间
    }
}
