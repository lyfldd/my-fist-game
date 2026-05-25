using System.Collections.Generic;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 职业模板（ScriptableObject）
    /// 右键 → Create → Game/Character Template 创建
    /// 阶段 3 直接新增 SO 即可扩展新职业
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Character Template")]
    public class CharacterTemplate : ScriptableObject
    {
        public string templateName;
        [TextArea(2, 4)]
        public string description;

        [Header("属性/技能修正")]
        public List<AttributeMod> attributeMods = new List<AttributeMod>();
        public List<SkillBoost> skillBoosts = new List<SkillBoost>();

        [Header("初始装备（开局自动穿上，扩容容器）")]
        public ItemData[] startingEquipment;

        [Header("初始物品（装备扩容后放入背包）")]
        public ItemRequirement[] startingItems;

        [Header("特殊开局")]
        public bool startWithAIBot;
        public BuildableData startingAIBotBuildable;
    }

    [System.Serializable]
    public class SkillBoost
    {
        public SkillType skillType;   // 下拉选技能
        public int bonus;             // +1 / +2 等
    }
}
