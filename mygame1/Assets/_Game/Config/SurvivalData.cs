using UnityEngine;

namespace _Game.Config
{
    [CreateAssetMenu(fileName = "SurvivalData_Default", menuName = "Game/Survival Data")]
    public class SurvivalData : ScriptableObject
    {
        [Header("基础衰减速率（每游戏分钟）")]
        public float healthDecayRate = 0.1f;
        public float hungerDecayRate = 0.2f;
        public float thirstDecayRate = 0.3f;
        public float temperatureRegainRate = 0.1f;

        [Header("自然回血")]
        public float healthRegenRate = 0.5f;       // 每次Tick回血量
        public float regenAttrThreshold = 80f;      // 饥饿/口渴大于此值才回血
        public float regenAttrMultiplier = 3f;      // 回血时饥饿/口渴加速倍率

        [Header("危险阈值")]
        public float hungerDangerThreshold = 20f;
        public float thirstDangerThreshold = 15f;
        public float tempDangerMin = 30f;
        public float tempDangerMax = 40f;

        [Header("状态伤害值")]
        public float bleedingHealthLoss = 0.5f;
        public float infectedHealthLoss = 0.3f;
        public float fractureMoveSpeedPenalty = 0.5f;
        public float hypothermiaHealthLoss = 0.2f;
        public float overheatHealthLoss = 0.15f;
    }
}
