using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Character
{
    /// <summary>
    /// 体力系统。跑步/近战/劳作消耗，静止/走路恢复，耐力属性加成。
    /// 体力=0 时移速×0.6，无法跑步和攻击。
    /// </summary>
    public class StaminaSystem : MonoBehaviour
    {
        [SerializeField] private float _currentStamina;
        [SerializeField] private float _maxStamina = 100f;

        public float CurrentStamina => _currentStamina;
        public float MaxStamina => _maxStamina;
        public float Ratio => _maxStamina > 0 ? _currentStamina / _maxStamina : 1f;
        public bool IsExhausted => _currentStamina <= 0f;

        private PlayerCharacter _player;
        private bool _isRunning;
        private bool _isWalking;
        private float _overloadBonusDrain;

        void Start()
        {
            _player = GetComponent<PlayerCharacter>();
            RecalculateMax();
            _currentStamina = _maxStamina;
        }

        public void RecalculateMax()
        {
            int endurance = _player != null ? _player.Endurance : 5;
            _maxStamina = GameConstants.STAMINA_BASE_MAX + endurance * GameConstants.STAMINA_PER_ENDURANCE;
        }

        void Update()
        {
            float dt = UnityEngine.Time.deltaTime;
            float endurance = _player != null ? _player.Endurance : 5;

            // 消耗
            float drain = 0f;
            if (_isRunning) drain += GameConstants.STAMINA_DRAIN_RUN;
            if (_overloadBonusDrain > 0) drain *= GameConstants.STAMINA_OVERLOAD_MULT;

            if (drain > 0)
            {
                _currentStamina = Mathf.Max(0, _currentStamina - drain * dt);
            }

            // 恢复
            if (drain == 0)
            {
                float regen = _isWalking ? GameConstants.STAMINA_REGEN_WALK : GameConstants.STAMINA_REGEN_IDLE;
                regen += endurance * GameConstants.STAMINA_REGEN_PER_ENDURANCE;
                _currentStamina = Mathf.Min(_maxStamina, _currentStamina + regen * dt);
            }
        }

        // ============================================================
        // 公共 API
        // ============================================================

        public bool CanPerform(float cost) => _currentStamina >= cost;

        public void Consume(float amount)
        {
            _currentStamina = Mathf.Max(0, _currentStamina - amount);
        }

        /// <summary> 近战攻击消费。 </summary>
        public void ConsumeMelee()
        {
            Consume(GameConstants.STAMINA_DRAIN_MELEE);
        }

        /// <summary> 劳作（伐木/挖矿）消费。 </summary>
        public void ConsumeLabor()
        {
            Consume(GameConstants.STAMINA_DRAIN_LABOR);
        }

        public void SetRunning(bool running) => _isRunning = running;
        public void SetWalking(bool walking) => _isWalking = walking;
        public void SetOverloaded(bool overloaded)
        {
            _overloadBonusDrain = overloaded ? 1f : 0f;
        }

        /// <summary> 完全恢复体力（睡觉/使用物品等）。 </summary>
        public void FullRestore()
        {
            _currentStamina = _maxStamina;
        }

        // ============================================================
        // 存档系统接口
        // ============================================================

        /// <summary> 填充存档数据中的体力字段 </summary>
        public void PopulateSaveData(SaveLoad.PlayerSaveData pd)
        {
            if (pd == null) return;
            pd.currentStamina = _currentStamina;
        }

        /// <summary> 从存档恢复体力 </summary>
        public void RestoreFromSave(SaveLoad.PlayerSaveData pd)
        {
            if (pd == null) return;
            _currentStamina = pd.currentStamina;
        }
    }
}
