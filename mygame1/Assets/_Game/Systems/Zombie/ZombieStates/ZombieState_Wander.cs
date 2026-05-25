using UnityEngine;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// Wander: 随机 NavMesh 点，半速走过去。到达 → Idle。发现玩家 → Chase。
    /// </summary>
    public class ZombieState_Wander : ZombieState
    {
        public static readonly ZombieState_Wander Instance = new();

        public override void Enter(ZombieStateMachine ctx)
        {
            ctx.SetSpeed(ctx.moveSpeed * ctx.wanderSpeedMultiplier);

            if (ctx.SampleRandomPoint(5f, out Vector3 point))
            {
                ctx.wanderTarget = point;
                if (ctx.agent.enabled && ctx.agent.isOnNavMesh)
                    ctx.agent.SetDestination(point);
            }
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

            // 到达目标点 → 休息
            if (ctx.agent.enabled && ctx.agent.isOnNavMesh && !ctx.agent.pathPending)
            {
                if (ctx.agent.remainingDistance <= ctx.agent.stoppingDistance + 0.5f)
                {
                    ctx.TransitionTo(ZombieState_Idle.Instance);
                }
            }
        }

        public override void Exit(ZombieStateMachine ctx) { }
    }
}
