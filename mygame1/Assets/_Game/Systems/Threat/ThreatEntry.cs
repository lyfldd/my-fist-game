using _Game.Config;
using UnityEngine;

namespace _Game.Systems.Threat
{
    /// <summary>
    /// 威胁条目 — 记录"谁恨谁"以及仇恨值
    /// </summary>
    public struct ThreatEntry
    {
        public int sourceInstanceId;          // 谁恨
        public int? targetInstanceId;         // 恨谁（null = 只知道位置不知道谁，如AllyAlert）
        public float value;                   // 当前仇恨值（裸值）
        public float timestamp;               // 最近威胁时间
        public ThreatType type;               // 威胁类型
        public Vector3 lastKnownPos;          // 目标最后已知位置
        public FactionType? sourceFactionType; // 威胁来源阵营（Sound/AllyAlert时用于来源死亡后批量清理）

        public float WeightedValue => value * ThreatTypeConfig.GetWeight(type);
    }
}
