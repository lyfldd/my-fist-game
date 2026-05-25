using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Survival;
using _Game.Systems.Time;

namespace _Game.Systems.Weather
{
    public enum WeatherDifficulty { Easy, Normal, Hard }

    /// <summary>
    /// 天气系统单例。马尔可夫链状态机 + 硬约束 + 场景光照控制。
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("配置")]
        [SerializeField] WeatherData[] _weatherData = new WeatherData[4];
        WeatherData[] _sorted = new WeatherData[4]; // 按 WeatherType 索引排序后的缓存

        [Header("难度")]
        [SerializeField] WeatherDifficulty _difficulty = WeatherDifficulty.Normal;

        [Header("光照")]
        [SerializeField] Light _sunLight;

        [Header("检测间隔")]
        [SerializeField] float _checkIntervalHours = 2f;

        // 状态
        WeatherType _currentWeather;
        int _lastCheckedHour = -1;
        int _stormCooldownRemaining;
        int _consecutiveRainCount;
        float _dailyRainTotal;
        int _lastDay;

        // 光照平滑
        float _targetSunIntensity;
        Color _targetSunColor;

        // DevTools 强制覆盖
        bool _forceWeather;
        WeatherType _forcedType;

        // 公开
        public WeatherType CurrentWeather => _currentWeather;
        public WeatherData CurrentData => GetData(_currentWeather);
        public float RainIntensity => CurrentData != null ? CurrentData.rainIntensity : 0f;
        public float AmbientTemperature { get; private set; } = 15f;
        public WeatherDifficulty Difficulty => _difficulty;
        public int StormCooldownRemaining => _stormCooldownRemaining;
        public int ConsecutiveRainCount => _consecutiveRainCount;

        public bool IsForceOverride => _forceWeather;
        public WeatherType ForcedType => _forcedType;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (_sunLight == null)
            {
                var suns = FindObjectsOfType<Light>();
                foreach (var l in suns)
                    if (l.type == LightType.Directional) { _sunLight = l; break; }
            }

            EnsureDefaultData();
            ApplyWeather(WeatherType.Sunny);
        }

        void Update()
        {
            LerpSunLight();

            var tm = FindObjectOfType<TimeManager>();
            if (tm == null) return;

            int currentHour = Mathf.FloorToInt(tm.CurrentHour);
            int currentDay = Mathf.FloorToInt(tm.CurrentDay);

            if (currentDay != _lastDay)
            {
                _dailyRainTotal = 0f;
                _lastDay = currentDay;
            }

            if (currentHour != _lastCheckedHour && currentHour % (int)_checkIntervalHours == 0)
            {
                _lastCheckedHour = currentHour;
                RollWeather();
            }
        }

        // ============================================================
        // 核心：天气切换
        // ============================================================

        void RollWeather()
        {
            if (_forceWeather) { ApplyWeather(_forcedType); return; }

            float[] weights = BuildWeights();
            WeatherType next = WeightedPick(weights);
            ApplyWeather(next);
        }

        float[] BuildWeights()
        {
            int from = (int)_currentWeather;
            float[] w = new float[4];
            for (int i = 0; i < 4; i++)
                w[i] = GameConstants.WEATHER_TRANSITION_WEIGHTS[from, i];

            // 难度修正
            float rainMul = 1f, stormMul = 1f;
            switch (_difficulty)
            {
                case WeatherDifficulty.Easy:
                    rainMul = GameConstants.WEATHER_DIFF_EASY_RAIN_MULT;
                    stormMul = GameConstants.WEATHER_DIFF_EASY_STORM_MULT;
                    break;
                case WeatherDifficulty.Hard:
                    rainMul = GameConstants.WEATHER_DIFF_HARD_RAIN_MULT;
                    stormMul = GameConstants.WEATHER_DIFF_HARD_STORM_MULT;
                    break;
            }
            w[2] *= rainMul;
            w[3] *= stormMul;

            // 地形修正（TODO Phase2: 查 WorldData.moduleGrid → ModuleType）
            // var biomeMod = GetBiomeModifier(); w[2] *= biomeMod[0]; ...

            // 硬约束
            if (_stormCooldownRemaining > 0) { w[2] = 0f; w[3] = 0f; }
            if (_consecutiveRainCount >= GameConstants.WEATHER_MAX_CONSECUTIVE_RAIN) { w[2] = 0f; w[3] = 0f; }
            if (from == 0) w[3] = 0f; // Sunny 不直接跳 Storm

            return w;
        }

        WeatherType WeightedPick(float[] w)
        {
            float total = 0f;
            for (int i = 0; i < 4; i++) total += w[i];
            if (total <= 0f) return _currentWeather;

            float roll = Random.Range(0f, total);
            float accum = 0f;
            for (int i = 0; i < 4; i++)
            {
                accum += w[i];
                if (roll <= accum) return (WeatherType)i;
            }
            return _currentWeather;
        }

        void ApplyWeather(WeatherType type)
        {
            var prev = _currentWeather;
            _currentWeather = type;

            // 追踪变量
            if (type == WeatherType.Storm)
            {
                float cdMult = _difficulty switch
                {
                    WeatherDifficulty.Easy => GameConstants.WEATHER_DIFF_EASY_COOLDOWN,
                    WeatherDifficulty.Hard => GameConstants.WEATHER_DIFF_HARD_COOLDOWN,
                    _ => 1f
                };
                _stormCooldownRemaining = Mathf.RoundToInt(GameConstants.WEATHER_STORM_COOLDOWN * cdMult);
            }
            else if (_stormCooldownRemaining > 0)
            {
                _stormCooldownRemaining--;
            }

            if (type == WeatherType.Rain || type == WeatherType.Storm)
                _consecutiveRainCount++;
            else
                _consecutiveRainCount = 0;

            // 光照
            var data = GetData(type);
            if (data != null)
            {
                _targetSunIntensity = data.sunIntensity;
                _targetSunColor = data.sunColor;
            }

            // 环境温度
            float tempMod = (int)type < GameConstants.WEATHER_TEMP_MODS.Length
                ? GameConstants.WEATHER_TEMP_MODS[(int)type] : 0f;
            AmbientTemperature = GameConstants.WEATHER_BASE_TEMP + tempMod;
            var survival = FindObjectOfType<SurvivalSystem>();
            survival?.SetEnvironmentTemperature(AmbientTemperature);

            // 事件
            float rain = data != null ? data.rainIntensity : 0f;
            EventBus.Publish(new WeatherChangedEvent(prev, type, rain, AmbientTemperature));
        }

        // ============================================================
        // 光照平滑
        // ============================================================

        void LerpSunLight()
        {
            if (_sunLight == null) return;
            float t = GameConstants.WEATHER_LIGHT_LERP_SPEED * UnityEngine.Time.deltaTime;
            _sunLight.intensity = Mathf.Lerp(_sunLight.intensity, _targetSunIntensity, t);
            _sunLight.color = Color.Lerp(_sunLight.color, _targetSunColor, t);
        }

        // ============================================================
        // DevTools API
        // ============================================================

        public void ForceWeather(WeatherType type)
        {
            _forceWeather = true;
            _forcedType = type;
            ApplyWeather(type);
        }

        public void ReleaseOverride()
        {
            _forceWeather = false;
        }

        public void SetDifficulty(WeatherDifficulty diff) => _difficulty = diff;

        // ============================================================
        // 工具
        // ============================================================

        void EnsureDefaultData()
        {
            if (_weatherData == null) _weatherData = new WeatherData[4];
            for (int i = 0; i < 4; i++)
            {
                if (_weatherData[i] == null)
                    _weatherData[i] = WeatherData.CreateDefault((WeatherType)i);
            }
            SortData();
        }

        void SortData()
        {
            for (int i = 0; i < 4; i++) _sorted[i] = null;
            if (_weatherData == null) return;
            foreach (var d in _weatherData)
            {
                if (d != null && (int)d.weatherType < 4)
                    _sorted[(int)d.weatherType] = d;
            }
            // 兜底：仍未分配的用默认值
            for (int i = 0; i < 4; i++)
                if (_sorted[i] == null)
                    _sorted[i] = WeatherData.CreateDefault((WeatherType)i);
        }

        WeatherData GetData(WeatherType type)
        {
            int idx = (int)type;
            if (idx < 0 || idx >= 4) return WeatherData.CreateDefault(WeatherType.Sunny);
            return _sorted[idx] ?? WeatherData.CreateDefault(type);
        }
    }
}
