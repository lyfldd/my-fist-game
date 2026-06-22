using System;
using System.Collections.Generic;
using System.Linq;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 玩家状态存档数据。对照 SurvivalSystem / SurvivalXPSystem / StaminaSystem。
    /// </summary>
    [Serializable]
    public class PlayerSaveData : ICloneable
    {
        // Transform
        public float posX, posY, posZ;
        public float rotY;

        // SurvivalSystem
        public float health, hunger, thirst, temperature;
        public List<string> activeStates;                // SurvivalStateType 枚举名
        public string lastFoodType;
        public float lastEatGameTime;
        public Dictionary<string, float> medCooldownsRemaining; // 药品名→剩余冷却游戏秒

        // SurvivalXPSystem
        public int totalXP;
        public int availablePoints;
        public List<AttributeSaveData> attributes;       // 力量/敏捷/体质/耐力
        public List<SkillSaveData> skills;               // 10 技能

        // StaminaSystem
        public float currentStamina;

        public object Clone()
        {
            return new PlayerSaveData
            {
                posX = this.posX, posY = this.posY, posZ = this.posZ,
                rotY = this.rotY,
                health = this.health, hunger = this.hunger, thirst = this.thirst, temperature = this.temperature,
                activeStates = this.activeStates != null ? new List<string>(this.activeStates) : null,
                lastFoodType = this.lastFoodType,
                lastEatGameTime = this.lastEatGameTime,
                medCooldownsRemaining = this.medCooldownsRemaining != null
                    ? new Dictionary<string, float>(this.medCooldownsRemaining) : null,
                totalXP = this.totalXP,
                availablePoints = this.availablePoints,
                attributes = this.attributes?.Select(a => a.Clone() as AttributeSaveData).ToList(),
                skills = this.skills?.Select(s => s.Clone() as SkillSaveData).ToList(),
                currentStamina = this.currentStamina,
            };
        }
    }

    [Serializable]
    public class AttributeSaveData : ICloneable
    {
        public string type;    // "力量"/"敏捷"/"体质"/"耐力"
        public int value;      // 1~10

        public object Clone()
        {
            return new AttributeSaveData { type = this.type, value = this.value };
        }
    }

    [Serializable]
    public class SkillSaveData : ICloneable
    {
        public string skillType; // "近战专精"/"枪械专精"/...
        public int level;
        public int xp;
        public int xpToNext;

        public object Clone()
        {
            return new SkillSaveData { skillType = this.skillType, level = this.level, xp = this.xp, xpToNext = this.xpToNext };
        }
    }
}
