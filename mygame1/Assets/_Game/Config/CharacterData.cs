using System.Collections.Generic;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 身体属性（4个）。移除智力，归入心智技能。
    /// </summary>
    public enum AttributeType
    {
        力量,   // STR — 近战伤害、负重上限
        敏捷,   // AGI — 移动速度、闪避率、潜行效果
        体质,   // CON — 生命值上限、自然恢复速度
        耐力,   // END — 体力上限、体力恢复速度
    }

    /// <summary>
    /// 技能类型（10个，4组：战斗/生存/制造/心智）。
    /// </summary>
    public enum SkillType
    {
        // 战斗技能
        近战专精,   // 近战伤害加成 + 近战闪避
        枪械专精,   // 瞄准精度（散射锥收窄）
        防御专精,   // 减伤率（联动装备护甲）

        // 生存技能
        资源采集,   // 伐木/挖矿/搜刮速度 + 稀有爆率
        医疗生存,   // 医疗品效果加成 + debuff处理 + 草药制作
        野外求生,   // 追踪/狩猎/陷阱 + 环境适应

        // 制造工程
        工匠制作,   // 工具/武器制作品质 + 改造配件
        建造拆解,   // 建造耗时缩短 + 拆解返还提高
        汽车改造,   // 载具改装解锁

        // 心智
        智力,       // 配方解锁门槛 + 高阶制作品质 + NPC说服
    }

    /// <summary>
    /// 人物初始属性模板（ScriptableObject）
    /// 在编辑器中：右键 → Create → Game/Character Data 创建
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("基础属性")]
        public List<AttributeDef> attributes = new List<AttributeDef>
        {
            new AttributeDef { type = AttributeType.力量, defaultValue = 5 },
            new AttributeDef { type = AttributeType.敏捷, defaultValue = 5 },
            new AttributeDef { type = AttributeType.体质, defaultValue = 5 },
            new AttributeDef { type = AttributeType.耐力, defaultValue = 5 },
        };

        [Header("初始技能")]
        public List<SkillDef> skills = new List<SkillDef>
        {
            new SkillDef { type = SkillType.近战专精, level = 1 },
            new SkillDef { type = SkillType.枪械专精, level = 0 },
            new SkillDef { type = SkillType.防御专精, level = 0 },
            new SkillDef { type = SkillType.资源采集, level = 0 },
            new SkillDef { type = SkillType.医疗生存, level = 0 },
            new SkillDef { type = SkillType.野外求生, level = 0 },
            new SkillDef { type = SkillType.工匠制作, level = 0 },
            new SkillDef { type = SkillType.建造拆解, level = 0 },
            new SkillDef { type = SkillType.汽车改造, level = 0 },
            new SkillDef { type = SkillType.智力, level = 0 },
        };

        [Header("性别微调")]
        public bool isMale = true;
        public List<AttributeMod> genderMods = new List<AttributeMod>();

        [Header("职业模板")]
        public CharacterTemplate profession;
    }

    /// <summary> 单个属性定义 </summary>
    [System.Serializable]
    public class AttributeDef
    {
        public AttributeType type;
        public string shortName { get { return GetShortName(type); } }
        public int defaultValue = 5;
        public int min = 1;
        public int max = 10;
        public string description { get { return GetDescription(type); } }

        public static string GetShortName(AttributeType t)
        {
            switch (t)
            {
                case AttributeType.力量: return "STR";
                case AttributeType.敏捷: return "AGI";
                case AttributeType.体质: return "CON";
                case AttributeType.耐力: return "END";
                default: return "???";
            }
        }

        public static string GetDescription(AttributeType t)
        {
            switch (t)
            {
                case AttributeType.力量: return "近战伤害、负重上限";
                case AttributeType.敏捷: return "移速、闪避、潜行";
                case AttributeType.体质: return "生命值、自然恢复";
                case AttributeType.耐力: return "体力上限、体力恢复";
                default: return "";
            }
        }
    }

    /// <summary> 单个技能定义 </summary>
    [System.Serializable]
    public class SkillDef
    {
        public SkillType type;
        public int level;
    }

    /// <summary> 属性修正（用于职业/性别） </summary>
    [System.Serializable]
    public class AttributeMod
    {
        public AttributeType attributeType;
        public int mod;
    }
}
