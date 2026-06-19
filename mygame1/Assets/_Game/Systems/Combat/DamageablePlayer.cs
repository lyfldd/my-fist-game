using UnityEngine;
using _Game.Core;

namespace _Game.Systems.Combat
{
    /// <summary>
    /// 玩家受伤组件（Phase 1）
    /// 实现 IDamageable，把伤害转发给 SurvivalSystem
    /// </summary>
    public class DamageablePlayer : MonoBehaviour, IDamageable
    {
        public bool IsDead { get; private set; }
        public bool Invincible { get; set; }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;
            if (Invincible) return;

            // 转发伤害给生存系统
            EventBus.Publish(new PlayerDamaged(Mathf.RoundToInt(damage), "僵尸"));
        }
    }
}
