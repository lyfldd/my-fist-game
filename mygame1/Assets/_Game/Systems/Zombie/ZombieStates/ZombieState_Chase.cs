using UnityEngine;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// Chase: 全速追击。有 playerTarget → 追玩家；有 lastHeardPosition → 去调查。
    /// 贴脸 → Attack。到达声音原点 → Idle。完全无目标 → Wander。
    /// </summary>
    public class ZombieState_Chase : ZombieState
    {
        public static readonly ZombieState_Chase Instance = new();

        private const float InvestigateArriveDistance = 2f;

        public override void Enter(ZombieStateMachine ctx)
        {
            ctx.SetSpeed(ctx.moveSpeed);
        }

        public override void Update(ZombieStateMachine ctx)
        {
            if (ctx.damageable != null && ctx.damageable.IsDead)
            {
                ctx.TransitionTo(ZombieState_Dead.Instance);
                return;
            }

            // 有玩家目标 → 追击玩家
            if (ctx.playerDetected && ctx.playerTarget != null)
            {
                float dist = Vector3.Distance(ctx.transform.position, ctx.playerTarget.position);

                if (dist > ctx.loseRange)
                {
                    ctx.TransitionTo(ZombieState_Wander.Instance);
                    return;
                }

                if (dist <= ctx.attackRange)
                {
                    ctx.TransitionTo(ZombieState_Attack.Instance);
                    return;
                }

                ctx.UpdateDestination(ctx.playerTarget.position, 0.5f);
                return;
            }

            // 没有玩家目标，但有声音 → 去声音原点调查
            if (ctx.lastHeardPosition.sqrMagnitude > 0.1f)
            {
                float dist = Vector3.Distance(ctx.transform.position, ctx.lastHeardPosition);

                // 到达声音原点 → 没找到目标 → 休息（调查行为 Phase2 细化）
                if (dist <= InvestigateArriveDistance)
                {
                    ctx.TransitionTo(ZombieState_Idle.Instance);
                    return;
                }

                ctx.UpdateDestination(ctx.lastHeardPosition, 1f);
                return;
            }

            // 什么都没有 → 放弃
            ctx.TransitionTo(ZombieState_Wander.Instance);
        }

        public override void Exit(ZombieStateMachine ctx) { }
    }
}
