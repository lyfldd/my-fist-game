using UnityEngine;
using _Game.Systems.Combat;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// Attack: 停步，面朝玩家，每 1.5s 造成伤害。
    /// 玩家脱离攻击范围 → Chase。死亡 → Dead。
    /// </summary>
    public class ZombieState_Attack : ZombieState
    {
        public static readonly ZombieState_Attack Instance = new();

        public override void Enter(ZombieStateMachine ctx)
        {
            ctx.attackTimer = 0f;
            ctx.SetSpeed(0f);
        }

        public override void Update(ZombieStateMachine ctx)
        {
            if (ctx.damageable != null && ctx.damageable.IsDead)
            {
                ctx.TransitionTo(ZombieState_Dead.Instance);
                return;
            }

            if (ctx.playerTarget == null)
            {
                ctx.TransitionTo(ZombieState_Wander.Instance);
                return;
            }

            float dist = Vector3.Distance(ctx.transform.position, ctx.playerTarget.position);

            // 脱离攻击范围
            if (dist > ctx.attackRange)
            {
                ctx.TransitionTo(ZombieState_Chase.Instance);
                return;
            }

            // 面朝玩家
            Vector3 dir = ctx.playerTarget.position - ctx.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
                ctx.transform.rotation = Quaternion.LookRotation(dir.normalized);

            // 攻击冷却
            ctx.attackTimer -= UnityEngine.Time.deltaTime;
            if (ctx.attackTimer <= 0f)
            {
                ctx.attackTimer = ctx.attackCooldown;

                var playerDamageable = ctx.playerTarget.GetComponent<IDamageable>();
                if (playerDamageable != null && !playerDamageable.IsDead)
                    playerDamageable.TakeDamage(ctx.attackDamage);
            }
        }

        public override void Exit(ZombieStateMachine ctx) { }
    }
}
