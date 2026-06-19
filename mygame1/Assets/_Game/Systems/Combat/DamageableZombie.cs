using System.Collections;
using UnityEngine;
using _Game.Core;
using _Game.Config;
using _Game.Systems.Zombie;

namespace _Game.Systems.Combat
{
    /// <summary>
    /// 僵尸受伤组件。接伤害 → 闪红 → 死亡 → 通知状态机 → 发事件 → 销毁。
    /// </summary>
    public class DamageableZombie : MonoBehaviour, IDamageable
    {
        [Header("僵尸血量")]
        public float maxHealth = GameConstants.ZOMBIE_MAX_HEALTH;
        [SerializeField] private float _currentHealth;

        private Renderer _bodyRenderer;
        private Color _originalColor;

        public bool IsDead { get; private set; }

        void Start()
        {
            _currentHealth = maxHealth;
            _bodyRenderer = GetComponentInChildren<Renderer>();
            if (_bodyRenderer != null)
                _originalColor = _bodyRenderer.material.color;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;

            _currentHealth -= damage;
            FlashRed();

            if (_currentHealth <= 0)
                Die();
        }

        void FlashRed()
        {
            if (_bodyRenderer == null) return;
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            _bodyRenderer.material.color = Color.red;
            yield return new WaitForSeconds(GameConstants.ZOMBIE_FLASH_DURATION);
            if (_bodyRenderer != null && !IsDead)
                _bodyRenderer.material.color = _originalColor;
        }

        void Die()
        {
            IsDead = true;

            // 通知 ThreatSystem 清理
            EventBus.Publish(new EntityDeathEvent(gameObject.GetInstanceID()));

            // 通知 AI 进入死亡
            var stateMachine = GetComponent<ZombieStateMachine>();
            if (stateMachine != null)
                stateMachine.Die();

            EventBus.Publish(new ZombieDied(
                transform.position.x,
                transform.position.z,
                "zombie_default"
            ));

            EventBus.Publish(new SurvivalXpGained(GameConstants.XP_KILL_NORMAL_ZOMBIE, "zombie_kill"));

            Destroy(gameObject, GameConstants.ZOMBIE_DESTROY_DELAY);
        }
    }
}
