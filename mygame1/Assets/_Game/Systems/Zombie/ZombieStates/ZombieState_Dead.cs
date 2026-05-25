namespace _Game.Systems.Zombie
{
    /// <summary>
    /// Dead: 禁用 AI + 碰撞，等待 Destroy 或 Chunk 回收。
    /// </summary>
    public class ZombieState_Dead : ZombieState
    {
        public static readonly ZombieState_Dead Instance = new();

        public override void Enter(ZombieStateMachine ctx)
        {
            if (ctx.agent != null && ctx.agent.enabled)
            {
                ctx.agent.isStopped = true;
                ctx.agent.enabled = false;
            }

            var col = ctx.GetComponent<UnityEngine.Collider>();
            if (col != null)
                col.enabled = false;
        }

        public override void Update(ZombieStateMachine ctx)
        {
            // 死亡后无行为，等 DamageableZombie 触发 Destroy
        }

        public override void Exit(ZombieStateMachine ctx) { }
    }
}
