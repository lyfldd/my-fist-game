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

        private Inventory.Inventory _inventory;

        void Awake()
        {
            ServiceLocator.Register(this);

            var pilotLock = GetComponent<_Game.Systems.AIBot.AIBotPilotInputLock>();
            if (pilotLock == null)
                gameObject.AddComponent<_Game.Systems.AIBot.AIBotPilotInputLock>();

            // 尝试加载 CharacterData
            if (characterData == null)
            {
                characterData = Resources.Load<CharacterData>("DefaultCharacter");
#if UNITY_EDITOR
                if (characterData == null)
                    characterData = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterData>(
                        "Assets/_Game/Config/Character/DefaultCharacter.asset");
#endif
            }
            if (characterData == null)
            {
                characterData = ScriptableObject.CreateInstance<CharacterData>();
                Debug.LogWarning("[PlayerCharacter] CharacterData 未找到，已创建空模板");
            }

            _inventory = GetComponent<Inventory.Inventory>();
        }

        void OnDestroy()
        {
            ServiceLocator.Unregister<PlayerCharacter>();
        }

        void Start()
        {
            if (SurvivalXPSystem.Instance == null)
            {
                var go = new GameObject("SurvivalXPSystem");
                go.AddComponent<SurvivalXPSystem>();
            }
        }

        void OnEnable()
        {
            EventBus.Subscribe<InventoryChanged>(OnInventoryChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<InventoryChanged>(OnInventoryChanged);
        }

        void OnInventoryChanged(InventoryChanged evt)
        {
            UpdateWeightPenalty();
        }

        void UpdateWeightPenalty()
        {
            if (_inventory == null) return;
            float ratio = _inventory.EffectiveMaxWeight > 0f
                ? _inventory.CurrentWeight / _inventory.EffectiveMaxWeight
                : 0f;
            if (ratio > 0.7f)
            {
                float penalty = 1f - (ratio - 0.7f) * 1.5f;
                SetMoveSpeedModifier(Mathf.Max(0.3f, penalty));
            }
            else
            {
                SetMoveSpeedModifier(1f);
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
