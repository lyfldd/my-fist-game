using UnityEngine;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// Idle: 站立 1~4s，到时 → Wander。发现玩家 → Chase。
    /// </summary>
    public class ZombieState_Idle : ZombieState
    {
        public static readonly ZombieState_Idle Instance = new();

        public override void Enter(ZombieStateMachine ctx)
        {
            ctx.idleTimer = 0f;
            ctx.idleDuration = Random.Range(1f, 4f);
            ctx.SetSpeed(0f);
        }

        public override void Update(ZombieStateMachine ctx)
        {
            if (ctx.damageable != null && ctx.damageable.IsDead)
            {
                ctx.TransitionTo(ZombieState_Dead.Instance);
                return;
            }

            if (ctx.playerDetected && ctx.playerTarget != null)
            {
                ctx.TransitionTo(ZombieState_Chase.Instance);
                return;
            }

            ctx.idleTimer += UnityEngine.Time.deltaTime;
            if (ctx.idleTimer >= ctx.idleDuration)
                ctx.TransitionTo(ZombieState_Wander.Instance);
        }

        public override void Exit(ZombieStateMachine ctx) { }
    }
}
