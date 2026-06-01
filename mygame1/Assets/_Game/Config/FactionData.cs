using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 阵营数据 ScriptableObject — 定义阵营的静态关系
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Faction Data", fileName = "Faction_")]
    public class FactionData : ScriptableObject
    {
        public FactionType factionType;
        public string displayName;
        public Color factionColor = Color.white;
        public FactionType[] allies;      // 盟友
        public FactionType[] hostiles;    // 敌对
        // 未在两者中的 = 中立
    }
}
