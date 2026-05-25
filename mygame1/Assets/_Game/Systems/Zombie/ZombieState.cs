namespace _Game.Systems.Zombie
{
    /// <summary>
    /// 僵尸状态抽象基类。所有状态为全局单例，数据存在 ZombieStateMachine 上。
    /// </summary>
    public abstract class ZombieState
    {
        public abstract void Enter(ZombieStateMachine ctx);
        public abstract void Update(ZombieStateMachine ctx);
        public abstract void Exit(ZombieStateMachine ctx);
    }
}
