using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Combat;
using _Game.Systems.Threat;

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
                ApplyDefaults();
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
            var factionComp = GetComponent<FactionComponent>();

            // 设置阵营
            if (factionComp != null)
                factionComp.SetFaction(FactionType.Zombie);

            // 从 ZombieData 初始化 AIAgent 参数
            stateMachine.ApplyFromZombieData(data);
            damageable.maxHealth = data.maxHealth;
        }

        void ApplyDefaults()
        {
            var stateMachine = GetComponent<ZombieStateMachine>();
            var damageable = GetComponent<DamageableZombie>();
            var factionComp = GetComponent<FactionComponent>();

            if (factionComp != null)
                factionComp.SetFaction(FactionType.Zombie);

            // 使用默认 ZombieData（内联创建一个临时 SO）
            var defaultData = ScriptableObject.CreateInstance<ZombieData>();
            defaultData.zombieName = "普通僵尸";
            defaultData.maxHealth = GameConstants.ZOMBIE_MAX_HEALTH;
            defaultData.moveSpeed = GameConstants.ZOMBIE_MOVE_SPEED;
            defaultData.detectRange = GameConstants.ZOMBIE_DETECT_RANGE;
            defaultData.loseRange = 30f;
            defaultData.visionAngle = 90f;
            defaultData.attackRange = GameConstants.ZOMBIE_ATTACK_RANGE;
            defaultData.attackDamage = GameConstants.ZOMBIE_ATTACK_DAMAGE;
            defaultData.attackCooldown = GameConstants.ZOMBIE_ATTACK_COOLDOWN;

            stateMachine.ApplyFromZombieData(defaultData);
            damageable.maxHealth = defaultData.maxHealth;
        }
    }
}
