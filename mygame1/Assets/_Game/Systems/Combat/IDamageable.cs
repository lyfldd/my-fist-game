namespace _Game.Systems.Combat
{
    /// <summary>
    /// 任何能受伤的东西实现此接口
    /// 玩家 / 僵尸 / 门 / 车窗 ...
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage);
        bool IsDead { get; }
    }
}
