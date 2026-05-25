using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 僵尸类型模板 ScriptableObject。定义僵尸的所有核心属性。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/ZombieData")]
    public class ZombieData : ScriptableObject
    {
        [Header("基础")]
        public string zombieName = "普通僵尸";

        [Header("战斗")]
        public float maxHealth = 100f;
        public float moveSpeed = 3f;
        public int attackDamage = 10;
        public float attackRange = 1.5f;
        public float attackCooldown = 1.5f;

        [Header("感知")]
        public float detectRange = 10f;
        public float loseRange = 30f;
        [Tooltip("视野锥角度（全角），0 = 关闭视觉锥，退化为圆形检测")]
        [Range(0f, 180f)]
        public float visionAngle = 90f;

        [Header("体型")]
        [Tooltip("圆柱体半径（米）")]
        public float bodyRadius = 0.35f;
        [Tooltip("圆柱体高度（米）")]
        public float bodyHeight = 1.8f;

        [Header("掉落")]
        public string lootGroup = "common";
    }
}
