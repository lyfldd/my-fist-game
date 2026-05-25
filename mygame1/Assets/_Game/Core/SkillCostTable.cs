using _Game.Config;

namespace _Game.Core
{
    /// <summary>
    /// 技能升级耗点静态查询表。同技能越高级越贵。
    /// </summary>
    public static class SkillCostTable
    {
        public static int GetCost(SkillType skill, int currentLevel)
        {
            if (currentLevel < 0) currentLevel = 0;
            int baseCost = GetBaseCost(skill);
            float increment = GetIncrement(skill);
            return UnityEngine.Mathf.RoundToInt(baseCost + currentLevel * increment);
        }

        public static int GetAttributeCost(AttributeType attr, int currentLevel)
        {
            if (currentLevel < 0) currentLevel = 0;
            // 身体属性: 5 + level*0.5, Lv0→1=5, Lv9→10=10
            return UnityEngine.Mathf.RoundToInt(5f + currentLevel * 0.5f);
        }

        static int GetBaseCost(SkillType skill)
        {
            switch (skill)
            {
                case SkillType.资源采集: return 10;
                case SkillType.近战专精: return 10;
                case SkillType.枪械专精: return 10;
                case SkillType.防御专精: return 10;
                case SkillType.野外求生: return 15;
                case SkillType.医疗生存: return 25;
                case SkillType.工匠制作: return 35;
                case SkillType.建造拆解: return 35;
                case SkillType.汽车改造: return 40;
                case SkillType.智力: return 50;
                default: return 10;
            }
        }

        static float GetIncrement(SkillType skill)
        {
            switch (skill)
            {
                case SkillType.资源采集: return 0.6f;
                case SkillType.近战专精: return 1.7f;
                case SkillType.枪械专精: return 1.7f;
                case SkillType.防御专精: return 1.7f;
                case SkillType.野外求生: return 1.7f;
                case SkillType.医疗生存: return 1.9f;
                case SkillType.工匠制作: return 1.9f;
                case SkillType.建造拆解: return 1.9f;
                case SkillType.汽车改造: return 1.9f;
                case SkillType.智力: return 3.3f;
                default: return 1.5f;
            }
        }

        public static string GetCategoryName(SkillType skill)
        {
            switch (skill)
            {
                case SkillType.近战专精:
                case SkillType.枪械专精:
                case SkillType.防御专精:
                    return "战斗技能";
                case SkillType.资源采集:
                case SkillType.医疗生存:
                case SkillType.野外求生:
                    return "生存技能";
                case SkillType.工匠制作:
                case SkillType.建造拆解:
                case SkillType.汽车改造:
                    return "制造工程";
                case SkillType.智力:
                    return "心智";
                default: return "";
            }
        }
    }
}
