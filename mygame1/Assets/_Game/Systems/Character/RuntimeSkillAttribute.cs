using System;
using _Game.Config;

namespace _Game.Systems.Character
{
    /// <summary>
    /// 技能运行时数据（序列化，Inspector 可见）
    /// </summary>
    [Serializable]
    public class RuntimeSkill
    {
        public SkillType skillType;
        public int level;
        public int xp;
        public int xpToNext;
    }

    /// <summary>
    /// 属性运行时数据（序列化，Inspector 可见）
    /// </summary>
    [Serializable]
    public class RuntimeAttribute
    {
        public AttributeType type;
        public int value;
        public int min = 1;
        public int max = 10;
    }
}
