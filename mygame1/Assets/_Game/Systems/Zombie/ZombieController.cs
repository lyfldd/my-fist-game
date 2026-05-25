using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Systems.Combat;
using _Game.Core;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// 僵尸薄壳配置组件。挂载三件套，从 ZombieData SO 初始化参数。
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(ZombieStateMachine))]
    [RequireComponent(typeof(DamageableZombie))]
    public class ZombieController : MonoBehaviour
    {
        [Header("僵尸类型（可为空，使用默认值）")]
        public ZombieData zombieData;

        void Awake()
        {
            if (zombieData != null)
            {
                Apply(zombieData);
            }
            else
            {
                var stateMachine = GetComponent<ZombieStateMachine>();
                stateMachine.moveSpeed = GameConstants.ZOMBIE_MOVE_SPEED;
                stateMachine.detectRange = GameConstants.ZOMBIE_DETECT_RANGE;
                stateMachine.attackRange = GameConstants.ZOMBIE_ATTACK_RANGE;
                stateMachine.attackDamage = GameConstants.ZOMBIE_ATTACK_DAMAGE;
                stateMachine.attackCooldown = GameConstants.ZOMBIE_ATTACK_COOLDOWN;

                var agent = GetComponent<NavMeshAgent>();
                agent.speed = stateMachine.moveSpeed;
                agent.stoppingDistance = stateMachine.attackRange * 0.8f;
                agent.acceleration = 8f;
                agent.angularSpeed = 360f;
                agent.radius = 0.3f;
                agent.height = 1.8f;
            }
        }

        /// <summary>运行时初始化（代码 AddComponent 后调用，覆盖 Awake 中的默认值）。</summary>
        public void Initialize(ZombieData data)
        {
            zombieData = data;
            Apply(data);
        }

        void Apply(ZombieData data)
        {
            var stateMachine = GetComponent<ZombieStateMachine>();
            var damageable = GetComponent<DamageableZombie>();
            var agent = GetComponent<NavMeshAgent>();

            stateMachine.ApplyZombieData(data);
            damageable.maxHealth = data.maxHealth;
            agent.speed = data.moveSpeed;
            agent.stoppingDistance = data.attackRange * 0.8f;
            agent.acceleration = 8f;
            agent.angularSpeed = 360f;
            agent.radius = 0.3f;
            agent.height = 1.8f;
        }
    }
}
