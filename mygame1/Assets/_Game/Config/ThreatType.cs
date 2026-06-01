using System.Collections.Generic;

namespace _Game.Config
{
    /// <summary>
    /// 威胁来源类型
    /// </summary>
    public enum ThreatType
    {
        Damage,        // 造成伤害 → 仇恨值 = 伤害量 × 阵营系数
        Visual,        // 视觉发现 → 仇恨值 = 50（恒定）
        Sound,         // 听到声音 → 仇恨值 = 分贝值
        AllyDamage,    // 友军被攻击 → 仇恨值 = 20
        Territory,     // 区域入侵 → 仇恨值 = 30（恒定）
        Aggression     // 主动挑衅 → 仇恨值 = 100
    }

    /// <summary>
    /// ThreatType 的权重和衰减配置
    /// </summary>
    public static class ThreatTypeConfig
    {
        /// <summary>威胁类型权重 — GetTopTarget 排序用</summary>
        public static readonly Dictionary<ThreatType, float> Weights = new()
        {
            { ThreatType.Damage,      1.5f },
            { ThreatType.Aggression,  1.3f },
            { ThreatType.AllyDamage,  1.0f },
            { ThreatType.Visual,      0.8f },
            { ThreatType.Territory,   0.6f },
            { ThreatType.Sound,       0.5f }
        };

        /// <summary>衰减速率（每秒）</summary>
        public static readonly Dictionary<ThreatType, float> DecayRates = new()
        {
            { ThreatType.Damage,      0.5f },  // 每10秒-5（记仇久）
            { ThreatType.Visual,      2.0f },  // 每5秒-10（离开视线就掉）
            { ThreatType.Sound,       5.0f },  // 每3秒-15（很快忘记）
            { ThreatType.AllyDamage,  0.3f },  // 衰减最慢（为同伴报仇）
            { ThreatType.Territory,   3.0f },  // 较快衰减
            { ThreatType.Aggression,  0.5f }   // 同伤害级
        };

        public static float GetWeight(ThreatType type) =>
            Weights.TryGetValue(type, out var w) ? w : 1f;

        public static float GetDecayRate(ThreatType type) =>
            DecayRates.TryGetValue(type, out var r) ? r : 1f;
    }
}
