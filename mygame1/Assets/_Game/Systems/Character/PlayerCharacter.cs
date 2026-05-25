using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Character
{
    /// <summary>
    /// 玩家角色组件。属性/技能委托给 SurvivalXPSystem 管理。
    /// </summary>
    public class PlayerCharacter : MonoBehaviour
    {
        [Header("初始模板")]
        public CharacterData characterData;

        [Header("移动速度修正")]
        [SerializeField] private float moveSpeedModifier = 1f;

        // 便捷属性访问（委托 SurvivalXPSystem）
        public int Strength => SurvivalXPSystem.Instance?.GetAttributeValue(AttributeType.力量) ?? 5;
        public int Agility => SurvivalXPSystem.Instance?.GetAttributeValue(AttributeType.敏捷) ?? 5;
        public int Constitution => SurvivalXPSystem.Instance?.GetAttributeValue(AttributeType.体质) ?? 5;
        public int Endurance => SurvivalXPSystem.Instance?.GetAttributeValue(AttributeType.耐力) ?? 5;

        void Awake()
        {
            // 自动挂载驾驶输入锁定组件
            var pilotLock = GetComponent<_Game.Systems.AIBot.AIBotPilotInputLock>();
            if (pilotLock == null)
                gameObject.AddComponent<_Game.Systems.AIBot.AIBotPilotInputLock>();

            if (characterData == null)
            {
                characterData = Resources.Load<CharacterData>("DefaultCharacter");
                if (characterData == null)
                {
                    characterData = ScriptableObject.CreateInstance<CharacterData>();
                    Debug.LogWarning("PlayerCharacter 未绑定 CharacterData，已使用默认值");
                }
            }
        }

        void Start()
        {
            // 确保 SurvivalXPSystem 存在
            if (SurvivalXPSystem.Instance == null)
            {
                var go = new GameObject("SurvivalXPSystem");
                go.AddComponent<SurvivalXPSystem>();
            }
        }

        public int GetAttributeValue(AttributeType type)
        {
            return SurvivalXPSystem.Instance?.GetAttributeValue(type) ?? 5;
        }

        public int GetSkillLevel(SkillType type)
        {
            return SurvivalXPSystem.Instance?.GetSkillLevel(type) ?? 0;
        }

        public void SetMoveSpeedModifier(float modifier)
        {
            moveSpeedModifier = Mathf.Clamp(modifier, GameConstants.PLAYER_MIN_MOVE_MODIFIER, GameConstants.PLAYER_MAX_MOVE_MODIFIER);
        }

        public float GetMoveSpeedModifier()
        {
            return moveSpeedModifier;
        }
    }
}
