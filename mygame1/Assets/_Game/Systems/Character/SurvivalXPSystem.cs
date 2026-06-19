using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Character
{
    /// <summary>
    /// 全局生存经验池单例。所有行为→AddXP→自动换算技能点→自由兑换升级。
    /// </summary>
    public class SurvivalXPSystem : MonoBehaviour
    {
        public static SurvivalXPSystem Instance { get; private set; }

        [Header("经验池")]
        [SerializeField] private int _totalXP;
        [SerializeField] private int _availablePoints;

        [Header("技能等级")]
        [SerializeField] private List<RuntimeSkill> _skills = new List<RuntimeSkill>();

        [Header("属性等级")]
        [SerializeField] private List<RuntimeAttribute> _attributes = new List<RuntimeAttribute>();

        public int TotalXP => _totalXP;
        public int AvailablePoints => _availablePoints;

        public delegate void SkillUpHandler(SkillType skill, int newLevel);
        public delegate void AttributeUpHandler(AttributeType attr, int newValue);
        public event SkillUpHandler OnSkillLevelUp;
        public event AttributeUpHandler OnAttributeUp;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            InitFromCharacterData();
            EventBus.Subscribe<SurvivalXpGained>(OnSurvivalXpGained);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<SurvivalXpGained>(OnSurvivalXpGained);
        }

        void OnSurvivalXpGained(SurvivalXpGained evt)
        {
            AddXP(evt.Amount);
        }

        // ============================================================
        // 初始化
        // ============================================================

        void InitFromCharacterData()
        {
            var pc = ServiceLocator.Get<PlayerCharacter>();
            CharacterData data = pc != null ? pc.characterData : null;

            // 属性
            _attributes.Clear();
            var attrTypes = new[] { AttributeType.力量, AttributeType.敏捷, AttributeType.体质, AttributeType.耐力 };
            foreach (var t in attrTypes)
            {
                int defVal = 5;
                if (data != null)
                {
                    var def = data.attributes.Find(a => a.type == t);
                    if (def != null) defVal = def.defaultValue;
                }
                _attributes.Add(new RuntimeAttribute { type = t, value = defVal, min = 1, max = 10 });
            }

            // 技能
            _skills.Clear();
            var skillTypes = new[] {
                SkillType.近战专精, SkillType.枪械专精, SkillType.防御专精,
                SkillType.资源采集, SkillType.医疗生存, SkillType.野外求生,
                SkillType.工匠制作, SkillType.建造拆解, SkillType.汽车改造,
                SkillType.智力
            };
            foreach (var t in skillTypes)
            {
                int initLevel = 0;
                if (data != null)
                {
                    var def = data.skills.Find(s => s.type == t);
                    if (def != null) initLevel = def.level;
                }
                _skills.Add(new RuntimeSkill { skillType = t, level = initLevel, xp = 0, xpToNext = 0 });
            }

            // 职业加成
            if (data != null && data.profession != null)
            {
                ApplyProfession(data.profession);

                // 投放初始装备和物品
                if (pc != null)
                {
                    var applier = pc.GetComponent<ProfessionApplier>();
                    if (applier == null)
                        applier = pc.gameObject.AddComponent<ProfessionApplier>();
                    applier.ApplyStartingGear(data.profession);
                }
            }
        }

        void ApplyProfession(CharacterTemplate template)
        {
            if (template.attributeMods != null)
            {
                foreach (var mod in template.attributeMods)
                {
                    var attr = _attributes.Find(a => a.type == mod.attributeType);
                    if (attr != null) attr.value = Mathf.Clamp(attr.value + mod.mod, attr.min, attr.max);
                }
            }
            if (template.skillBoosts != null)
            {
                foreach (var boost in template.skillBoosts)
                {
                    var skill = _skills.Find(s => s.skillType == boost.skillType);
                    if (skill != null) skill.level += boost.bonus;
                }
            }
        }

        // ============================================================
        // 公共 API
        // ============================================================

        /// <summary> 添加生存经验（仅累积，需手动调用 ConvertXPToPoints 兑换）。 </summary>
        public void AddXP(int amount)
        {
            if (amount <= 0) return;
            _totalXP += amount;
        }

        /// <summary> 手动将累积经验兑换为技能点。返回兑换到的点数。 </summary>
        public int ConvertXPToPoints()
        {
            int pointsEarned = _totalXP / GameConstants.XP_PER_SKILL_POINT;
            if (pointsEarned <= 0) return 0;
            _availablePoints += pointsEarned;
            _totalXP -= pointsEarned * GameConstants.XP_PER_SKILL_POINT;
            EventBus.Publish(new CharacterStatsChanged("xp_converted", "", pointsEarned));
            return pointsEarned;
        }

        /// <summary> 花 1 技能点升级技能。返回是否成功。 </summary>
        public bool SpendPoint(SkillType skill)
        {
            if (_availablePoints <= 0) return false;

            var rs = _skills.Find(s => s.skillType == skill);
            if (rs == null) return false;
            if (rs.level >= 10) return false;

            int cost = SkillCostTable.GetCost(skill, rs.level);
            if (_availablePoints < cost) return false;

            _availablePoints -= cost;
            rs.level++;
            OnSkillLevelUp?.Invoke(skill, rs.level);
            EventBus.Publish(new CharacterStatsChanged("skill_up", skill.ToString(), rs.level));
            return true;
        }

        /// <summary> 花点升级身体属性。 </summary>
        public bool SpendAttributePoint(AttributeType attr)
        {
            if (_availablePoints <= 0) return false;

            var ra = _attributes.Find(a => a.type == attr);
            if (ra == null) return false;
            if (ra.value >= ra.max) return false;

            int cost = SkillCostTable.GetAttributeCost(attr, ra.value);
            if (_availablePoints < cost) return false;

            _availablePoints -= cost;
            ra.value++;
            OnAttributeUp?.Invoke(attr, ra.value);
            EventBus.Publish(new CharacterStatsChanged("attr_up", attr.ToString(), ra.value));
            return true;
        }

        public int GetSkillLevel(SkillType skill)
        {
            var rs = _skills.Find(s => s.skillType == skill);
            return rs?.level ?? 0;
        }

        public int GetAttributeValue(AttributeType attr)
        {
            var ra = _attributes.Find(a => a.type == attr);
            return ra?.value ?? 5;
        }

        public int GetSkillUpgradeCost(SkillType skill)
        {
            var rs = _skills.Find(s => s.skillType == skill);
            if (rs == null || rs.level >= 10) return -1;
            return SkillCostTable.GetCost(skill, rs.level);
        }

        public int GetAttributeUpgradeCost(AttributeType attr)
        {
            var ra = _attributes.Find(a => a.type == attr);
            if (ra == null || ra.value >= ra.max) return -1;
            return SkillCostTable.GetAttributeCost(attr, ra.value);
        }

        public RuntimeSkill GetRuntimeSkill(SkillType skill) => _skills.Find(s => s.skillType == skill);
        public RuntimeAttribute GetRuntimeAttribute(AttributeType attr) => _attributes.Find(a => a.type == attr);

        /// <summary> 可升级的技能列表（等级<10且有足够点数）。 </summary>
        public List<SkillType> GetUpgradableSkills()
        {
            var list = new List<SkillType>();
            foreach (var rs in _skills)
            {
                if (rs.level >= 10) continue;
                if (SkillCostTable.GetCost(rs.skillType, rs.level) <= _availablePoints)
                    list.Add(rs.skillType);
            }
            return list;
        }
    }
}
