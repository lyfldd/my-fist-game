using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Audio;
using _Game.Systems.Character;
using _Game.Systems.Weapon;

namespace _Game.Systems.Combat
{
    public class PlayerCombat : MonoBehaviour
    {
        [Header("空手攻击参数")]
        public float emptyHandRange = GameConstants.MELEE_ATTACK_RANGE;
        public float emptyHandRadius = GameConstants.MELEE_ATTACK_RADIUS;
        public float emptyHandDamage = GameConstants.MELEE_ATTACK_DAMAGE;
        public float emptyHandCooldown = GameConstants.MELEE_ATTACK_COOLDOWN;

        private float _cooldownTimer;
        private PlayerCharacter _player;
        private StaminaSystem _stamina;
        private WeaponSwitcher _switcher;

        void Start()
        {
            _player = GetComponent<PlayerCharacter>();
            _stamina = GetComponent<StaminaSystem>();
            _switcher = GetComponent<WeaponSwitcher>();
        }

        void OnEnable() { InputRouter.BindMouse(0, InputPriority.Gameplay, TryMeleeAttack, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }

        void Update()
        {
            _cooldownTimer -= UnityEngine.Time.deltaTime;
        }

        bool TryMeleeAttack()
        {
            if (_cooldownTimer > 0f) return false;
            if (_stamina != null && !_stamina.CanPerform(GameConstants.STAMINA_DRAIN_MELEE))
                return false;

            PerformAttack();
            return true;
        }

        void PerformAttack()
        {
            var weapon = _switcher != null ? _switcher.ActiveWeapon : null;
            bool hasMeleeWeapon = weapon != null && !weapon.isFirearm;

            float range = hasMeleeWeapon ? weapon.weaponRange : emptyHandRange;
            float radius = hasMeleeWeapon ? emptyHandRadius * 1.2f : emptyHandRadius;
            float baseDamage = hasMeleeWeapon ? weapon.weaponDamage : emptyHandDamage;
            float cooldown = hasMeleeWeapon ? weapon.fireRate : emptyHandCooldown;

            _cooldownTimer = cooldown;

            int str = _player != null ? _player.Strength : 5;
            int meleeLevel = _player != null ? _player.GetSkillLevel(SkillType.近战专精) : 0;
            float finalDamage = baseDamage * (1f + str * GameConstants.STRENGTH_DAMAGE_MULT + meleeLevel * GameConstants.MELEE_SKILL_DAMAGE_MULT);

            Vector3 origin = transform.position + Vector3.up * GameConstants.PLAYER_MELEE_ORIGIN_Y;
            Vector3 forward = transform.forward;
            Vector3 hitPoint = origin + forward * range;

            var hits = Physics.OverlapSphere(origin + forward * range * 0.5f, radius);

            bool hitSomething = false;
            foreach (var hit in hits)
            {
                // 跳过AI机器人（友军）
                if (hit.GetComponentInParent<_Game.Systems.AIBot.AIBot>() != null) continue;

                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(finalDamage);
                    // ThreatSystem 旁路通知
                    EventBus.Publish(new ThreatReportEvent(
                        gameObject.GetInstanceID(),
                        hit.GetComponent<Collider>().gameObject.GetInstanceID(),
                        finalDamage));
                    hitSomething = true;
                    hitPoint = hit.ClosestPoint(origin);
                    break;
                }
            }

            if (hitSomething)
            {
                SoundEmitter.EmitMeleeHit(hitPoint);
                EventBus.Publish(new SurvivalXpGained(GameConstants.XP_DODGE_SUCCESS, "melee_hit"));
            }
            else
            {
                SoundEmitter.EmitMeleeSwing(hitPoint);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var sw = GetComponent<WeaponSwitcher>();
            var weapon = sw != null ? sw.ActiveWeapon : null;
            float range = (weapon != null && !weapon.isFirearm) ? weapon.weaponRange : emptyHandRange;
            float radius = (weapon != null && !weapon.isFirearm) ? emptyHandRadius * 1.2f : emptyHandRadius;

            Gizmos.color = Color.red;
            Vector3 origin = transform.position + Vector3.up * GameConstants.PLAYER_MELEE_ORIGIN_Y;
            Vector3 forward = transform.forward;
            Gizmos.DrawWireSphere(origin + forward * range * 0.5f, radius);
        }
#endif
    }
}
