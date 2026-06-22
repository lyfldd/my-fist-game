using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Character;

namespace _Game.Systems.Survival
{
    /// <summary>
    /// 生存数值系统
    /// 管理 4 大属性（健康/饥饿/口渴/体温）+ 生存状态（出血/感染/骨折等）
    /// 与时间系统、物品系统、角色系统通过 EventBus 解耦联动
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public class SurvivalSystem : MonoBehaviour
    {
        [Header("系统配置")]
        [SerializeField] private SurvivalData survivalData;
        [SerializeField] private PlayerCharacter playerCharacter;
        [SerializeField] private _Game.Systems.Time.TimeManager timeManager;

        [Header("当前属性")]
        [SerializeField] private float health = GameConstants.SURVIVAL_HEALTH_MAX;
        [SerializeField] private float hunger = GameConstants.SURVIVAL_HUNGER_MAX;
        [SerializeField] private float thirst = GameConstants.SURVIVAL_THIRST_MAX;
        [SerializeField] private float temperature = GameConstants.SURVIVAL_TEMP_INITIAL;

        private float currentEnvTemperature = GameConstants.SURVIVAL_ENV_TEMP_DEFAULT;
        private Dictionary<SurvivalStateType, bool> survivalStates = new();
        private Dictionary<ItemEffectType, float> temporaryEffects = new();

        // 食物类型 + 药品冷却
        private float _lastEatTime;
        private string _lastFoodType;
        private Dictionary<string, float> _medCooldowns = new();

        private float lastTickTime;
        private const float TICK_INTERVAL = GameConstants.SURVIVAL_TICK_INTERVAL;

        // 缓存（由事件驱动更新）
        private float _cachedArmor = 0;
        private float _cachedWarmth = 0;
        private bool _isRegenActive;

        // ===== 外部只读接口 =====
        public float Health => health;
        public float Hunger => hunger;
        public float Thirst => thirst;
        public float Temperature => temperature;
        public bool HasState(SurvivalStateType state) => survivalStates.ContainsKey(state) && survivalStates[state];
        public float EnvTemperature => currentEnvTemperature;

        private void Awake() { ServiceLocator.Register(this); }

        private void Start()
        {
            AutoInitComponents();
            InitStates();
            SubscribeEvents();
        }

        private void Update()
        {
            TickSurvivalLogic();
            UpdateTemporaryEffects();
        }

        #region 初始化

        private void AutoInitComponents()
        {
            playerCharacter ??= GetComponent<PlayerCharacter>();
            timeManager ??= ServiceLocator.Get<_Game.Systems.Time.TimeManager>();

            if (survivalData == null)
            {
                survivalData = Resources.Load<SurvivalData>("SurvivalData_Default");
                if (survivalData == null) CreateDefaultConfig();
            }

            // 新字段防呆（旧资产可能未序列化）
            if (survivalData.healthRegenRate <= 0) survivalData.healthRegenRate = 0.5f;
            if (survivalData.regenAttrThreshold <= 0) survivalData.regenAttrThreshold = 80f;
            if (survivalData.regenAttrMultiplier <= 0) survivalData.regenAttrMultiplier = 3f;
            if (survivalData.overheatHealthLoss <= 0) survivalData.overheatHealthLoss = 0.15f;
        }

        private void InitStates()
        {
            foreach (SurvivalStateType state in Enum.GetValues(typeof(SurvivalStateType)))
                survivalStates[state] = false;
        }

        private void SubscribeEvents()
        {
            if (timeManager != null)
                EventBus.Subscribe<TimeOfDayChanged>(OnTimeChanged);
            EventBus.Subscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            EventBus.Subscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Subscribe<ItemUsedEvent>(OnItemUsed);
        }

        private void OnPlayerDamaged(PlayerDamaged evt)
        {
            TakeDamage(evt.Damage, evt.Source);
        }

        #endregion

        #region 核心逻辑

        private void TickSurvivalLogic()
        {
            if (timeManager == null) return;
            if (UnityEngine.Time.time - lastTickTime < TICK_INTERVAL / timeManager.timeScale) return;

            lastTickTime = UnityEngine.Time.time;

            UpdateTemperature();
            ApplyAttrDecay();
            ApplyNaturalRegen();
            ApplyDangerDamage();
            ApplyTemperatureDamage();
            ApplyStateEffects();
        }

        private void ApplyAttrDecay()
        {
            float hungerMul = 1f;
            float thirstMul = 1f;

            // 自然回血激活时加速消耗饥饿/口渴
            bool canRegen = hunger > survivalData.regenAttrThreshold
                && thirst > survivalData.regenAttrThreshold
                && temperature >= survivalData.tempDangerMin
                && temperature <= survivalData.tempDangerMax
                && !GetState(SurvivalStateType.Bleeding)
                && !GetState(SurvivalStateType.Infected);
            _isRegenActive = canRegen;
            // 只有血量不满时才加速消耗（满血不需要回血）
            if (canRegen && health < GameConstants.SURVIVAL_HEALTH_MAX)
            {
                hungerMul = survivalData.regenAttrMultiplier;
                thirstMul = survivalData.regenAttrMultiplier;
            }

            // 饥饿归零加速口渴消耗
            if (hunger <= 0) thirstMul *= 1.5f;
            // 口渴归零加速饥饿消耗
            if (thirst <= 0) hungerMul *= 1.5f;

            float oldHunger = hunger;
            float oldThirst = thirst;
            hunger = Mathf.Max(0, hunger - survivalData.hungerDecayRate * hungerMul);
            thirst = Mathf.Max(0, thirst - survivalData.thirstDecayRate * thirstMul);

            EventBus.Publish(new SurvivalStatChanged(SurvivalStatType.Hunger, oldHunger, hunger, gameObject));
            EventBus.Publish(new SurvivalStatChanged(SurvivalStatType.Thirst, oldThirst, thirst, gameObject));
        }

        private void ApplyNaturalRegen()
        {
            if (!_isRegenActive) return;
            ModifyHealth(survivalData.healthRegenRate, "自然恢复");
        }

        private void ApplyTemperatureDamage()
        {
            // 过冷
            if (GetState(SurvivalStateType.Hypothermia))
                ModifyHealth(-survivalData.hypothermiaHealthLoss, "失温");
            // 过热
            if (GetState(SurvivalStateType.Overheated))
                ModifyHealth(-survivalData.overheatHealthLoss, "过热");
        }

        bool GetState(SurvivalStateType type) => survivalStates.ContainsKey(type) && survivalStates[type];

        private void ApplyDangerDamage()
        {
            float damage = 0;
            string reason = "";

            if (hunger < survivalData.hungerDangerThreshold) { damage += 0.4f; reason = "饥饿"; }
            if (thirst < survivalData.thirstDangerThreshold) { damage += 0.6f; reason = string.IsNullOrEmpty(reason) ? "口渴" : "饥饿+口渴"; }

            if (damage > 0) ModifyHealth(-damage, reason);
        }

        private void ApplyStateEffects()
        {
            if (GetState(SurvivalStateType.Bleeding))    ModifyHealth(-survivalData.bleedingHealthLoss, "出血");
            if (GetState(SurvivalStateType.Infected))    ModifyHealth(-survivalData.infectedHealthLoss, "感染");
        }

        private void UpdateTemperature()
        {
            float targetTemp = currentEnvTemperature;
            if (temporaryEffects.ContainsKey(ItemEffectType.TemporaryWarmth)) targetTemp += 2f;

            // 装备保暖：每1点warmth抵消约0.75度环境温差
            targetTemp += _cachedWarmth * 0.75f;

            float old = temperature;
            temperature = Mathf.MoveTowards(temperature, targetTemp, survivalData.temperatureRegainRate);
            if (Mathf.Abs(temperature - old) > 0.01f)
                EventBus.Publish(new SurvivalStatChanged(SurvivalStatType.Temperature, old, temperature, gameObject));
            CheckTempStates();
        }

        private void CheckTempStates()
        {
            SetSurvivalState(SurvivalStateType.Hypothermia, temperature < survivalData.tempDangerMin);
            SetSurvivalState(SurvivalStateType.Overheated, temperature > survivalData.tempDangerMax);
        }

        private void UpdateTemporaryEffects()
        {
            List<ItemEffectType> expired = new();
            foreach (var kv in temporaryEffects)
            {
                temporaryEffects[kv.Key] -= UnityEngine.Time.deltaTime;
                if (temporaryEffects[kv.Key] <= 0) expired.Add(kv.Key);
            }
            foreach (var t in expired) temporaryEffects.Remove(t);
        }

        #endregion

        #region 公共接口

        /// <summary> 修改健康值（正数回血，负数扣血）</summary>
        public void ModifyHealth(float amount, string reason)
        {
            float old = health;
            health = Mathf.Clamp(health + amount, 0, GameConstants.SURVIVAL_HEALTH_MAX);

            EventBus.Publish(new SurvivalStatChanged(SurvivalStatType.Health, old, health, gameObject));
            if (amount < 0) EventBus.Publish(new HealthDamaged(-amount, reason, gameObject));
            if (health <= 0)
            {
                EventBus.Publish(new CharacterDeath(gameObject));
                EventBus.Publish(new EntityDeathEvent(gameObject.GetInstanceID()));
            }
        }

        /// <summary> 物品使用（直接订阅 ItemUsedEvent，不再经过 ItemUsageSystem 中转）</summary>
        void OnItemUsed(ItemUsedEvent evt)
        {
            if (evt.ItemData == null || evt.ItemData.itemEffects == null || evt.ItemData.itemEffects.Count == 0)
                return;

            var item = evt.ItemData;

            // 药品冷却检查
            if (item.foodType == "Medicine" && _medCooldowns.TryGetValue(item.itemName, out var cdEnd)
                && UnityEngine.Time.time < cdEnd)
                return;

            // 食物类型连续递减
            float foodMultiplier = 1f;
            if (!string.IsNullOrEmpty(item.foodType) && item.foodType != "Medicine")
            {
                if (item.foodType == _lastFoodType && UnityEngine.Time.time - _lastEatTime < 300f)
                    foodMultiplier = 0.5f; // 5分钟内同类食物效果减半
                _lastFoodType = item.foodType;
                _lastEatTime = UnityEngine.Time.time;
            }

            // 药品冷却记录
            if (item.foodType == "Medicine")
                _medCooldowns[item.itemName] = UnityEngine.Time.time + 30f;

            foreach (var eff in item.itemEffects)
                ApplyItemEffect(eff, foodMultiplier);
        }

        /// <summary> 应用物品效果（吃食物/用药）</summary>
        public void ApplyItemEffect(ItemEffect effect, float multiplier = 1f)
        {
            float v = effect.value * multiplier;
            switch (effect.effectType)
            {
                case ItemEffectType.RestoreHealth:      ModifyHealth(v, "物品"); break;
                case ItemEffectType.RestoreHunger:      hunger = Mathf.Min(GameConstants.SURVIVAL_HUNGER_MAX, hunger + v); break;
                case ItemEffectType.RestoreThirst:      thirst = Mathf.Min(GameConstants.SURVIVAL_THIRST_MAX, thirst + v); break;
                case ItemEffectType.RestoreTemperature: temperature = Mathf.Clamp(temperature + v, GameConstants.SURVIVAL_TEMP_MIN, GameConstants.SURVIVAL_TEMP_MAX); break;
                case ItemEffectType.CureBleeding:       SetSurvivalState(SurvivalStateType.Bleeding, false); break;
                case ItemEffectType.CureInfected:       SetSurvivalState(SurvivalStateType.Infected, false); break;
                case ItemEffectType.FixFracture:        SetSurvivalState(SurvivalStateType.Fracture, false); break;
                case ItemEffectType.TemporaryWarmth:    temporaryEffects[effect.effectType] = effect.duration; break;
            }
        }

        /// <summary> 设置/取消生存状态</summary>
        public void SetSurvivalState(SurvivalStateType type, bool active)
        {
            if (!survivalStates.ContainsKey(type) || survivalStates[type] == active) return;
            survivalStates[type] = active;

            EventBus.Publish(new SurvivalStateChanged(type, active, gameObject));

            // 骨折 → 移速减半
            if (type == SurvivalStateType.Fracture && playerCharacter != null)
                playerCharacter.SetMoveSpeedModifier(active ? survivalData.fractureMoveSpeedPenalty : 1f);
        }

        /// <summary> 设置环境温度（外部调用，如天气系统/室内外判断）</summary>
        public void SetEnvironmentTemperature(float temp)
        {
            currentEnvTemperature = temp;
        }

        /// <summary> 承受外部伤害（经过护甲减免）</summary>
        public void TakeDamage(float rawDamage, string reason)
        {
            // 使用缓存护甲，不再 Find Inventory
            float multiplier = 100f / (100f + _cachedArmor);
            float actualDamage = rawDamage * multiplier;

            ModifyHealth(-actualDamage, reason);
        }

        /// <summary> 装备变化时更新护甲缓存 </summary>
        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            _cachedArmor = evt.TotalArmor;
            _cachedWarmth = evt.TotalWarmth;
        }

        #endregion

        #region 工具方法

        private void OnTimeChanged(TimeOfDayChanged evt)
        {
            // 根据游戏时间计算环境温度基准：夜晚低白天高
            currentEnvTemperature = Mathf.Sin(evt.CurrentHour / 24f * Mathf.PI * 2) * 10 + 15;
        }

        private void CreateDefaultConfig()
        {
            survivalData = ScriptableObject.CreateInstance<SurvivalData>();
            survivalData.name = "SurvivalData_Default";
            Debug.LogWarning("SurvivalSystem: 未找到 SurvivalData 配置，已使用默认值");
        }

        private void OnDestroy()
        {
            if (timeManager != null)
                EventBus.Unsubscribe<TimeOfDayChanged>(OnTimeChanged);
            EventBus.Unsubscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            EventBus.Unsubscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Unsubscribe<ItemUsedEvent>(OnItemUsed);
        }

        #endregion

        #region 存档系统接口

        /// <summary> 导出存档数据 </summary>
        public SaveLoad.PlayerSaveData GetSaveData()
        {
            var pd = new SaveLoad.PlayerSaveData
            {
                health = health,
                hunger = hunger,
                thirst = thirst,
                temperature = temperature,
                currentStamina = 0f, // 由 StaminaSystem 填充
            };

            // 活跃的生存状态
            pd.activeStates = new System.Collections.Generic.List<string>();
            foreach (var kv in survivalStates)
                if (kv.Value) pd.activeStates.Add(kv.Key.ToString());

            // 食物类型（转换为游戏时间）
            pd.lastFoodType = _lastFoodType;
            pd.lastEatGameTime = timeManager != null
                ? (UnityEngine.Time.time - _lastEatTime) / timeManager.timeScale
                : 0f;

            // 药品冷却（实时时间 → 游戏剩余秒数）
            pd.medCooldownsRemaining = new System.Collections.Generic.Dictionary<string, float>();
            float now = UnityEngine.Time.time;
            float scale = timeManager != null ? timeManager.timeScale : 1f;
            foreach (var kv in _medCooldowns)
            {
                float remaining = kv.Value - now;
                if (remaining > 0)
                    pd.medCooldownsRemaining[kv.Key] = remaining / scale;
            }

            return pd;
        }

        /// <summary> 从存档恢复 </summary>
        public void RestoreFromSave(SaveLoad.PlayerSaveData pd)
        {
            if (pd == null) return;

            health = pd.health;
            hunger = pd.hunger;
            thirst = pd.thirst;
            temperature = pd.temperature;

            // 恢复生存状态
            foreach (SurvivalStateType state in System.Enum.GetValues(typeof(SurvivalStateType)))
                survivalStates[state] = false;
            if (pd.activeStates != null)
            {
                foreach (var stateName in pd.activeStates)
                {
                    if (System.Enum.TryParse<SurvivalStateType>(stateName, out var st))
                        survivalStates[st] = true;
                }
            }

            // 食物类型
            _lastFoodType = pd.lastFoodType;
            float scale = timeManager != null ? timeManager.timeScale : 1f;
            _lastEatTime = UnityEngine.Time.time - pd.lastEatGameTime * scale;

            // 药品冷却（游戏剩余秒数 → 实时时间）
            _medCooldowns.Clear();
            float now = UnityEngine.Time.time;
            if (pd.medCooldownsRemaining != null)
            {
                foreach (var kv in pd.medCooldownsRemaining)
                    _medCooldowns[kv.Key] = now + kv.Value * scale;
            }
        }

        #endregion
    }
}
