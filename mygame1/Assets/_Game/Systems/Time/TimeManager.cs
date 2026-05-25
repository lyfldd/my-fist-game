using UnityEngine;
using _Game.Core;
using _Game.Systems.Character;

namespace _Game.Systems.Time
{
    /// <summary>
    /// 时间管理器
    /// 负责：游戏时间流逝、日夜循环、天数追踪、发布时间事件
    /// 所有参数在 Inspector 中调整，运行时修改 debugCurrentHour 可跳转时间
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        [Header("时间参数")]
        [Tooltip("现实 1 秒 = 游戏多少秒")]
        public float timeScale = 72f;
        public float startHour = 8f;
        public float startDay = 1f;

        [Header("灯光绑定")]
        public Light sunLight;
        public Light moonLight;

        [Header("时段颜色")]
        public Color dayColor = new Color(1f, 0.95f, 0.85f);
        public Color sunsetColor = new Color(1f, 0.5f, 0.2f);
        public Color nightColor = new Color(0.05f, 0.05f, 0.1f);
        public Color dawnColor = new Color(0.8f, 0.6f, 0.4f);
        public Color moonLightColor = new Color(0.1f, 0.12f, 0.2f);

        [Header("调试（运行时直接改这个值跳转时间）")]
        public float debugCurrentHour = 8f;

        public float CurrentHour => debugCurrentHour;
        public float CurrentMinute => (debugCurrentHour - Mathf.Floor(debugCurrentHour)) * 60f;

        /// <summary> 游戏总流逝天数（含小数），用于 ChunkManager 刷新冷却判定 </summary>
        public float CurrentDay => _totalGameDays;

        private float _totalGameDays;
        private float _previousHour24;
        private int _lastHourlyXP; // 已发放 XP 对应的游戏小时数（整数）

        void Start()
        {
            _previousHour24 = startHour;
            debugCurrentHour = startHour;
            _totalGameDays = startDay - 1f + startHour / 24f;
            _lastHourlyXP = Mathf.FloorToInt(startHour * startDay);

            if (sunLight == null)
                sunLight = FindObjectOfType<Light>();
        }

        void Update()
        {
            // 时间流逝
            float deltaGameHours = (timeScale / 3600f) * UnityEngine.Time.deltaTime;
            float newHour24 = _previousHour24 + deltaGameHours;

            // 检测跨天：累计小时每超过 24h 的整数倍就发 DayChanged
            int oldDay = Mathf.FloorToInt(_previousHour24 / 24f);
            int newDay = Mathf.FloorToInt(newHour24 / 24f);
            if (newDay > oldDay)
            {
                for (int d = oldDay + 1; d <= newDay; d++)
                {
                    EventBus.Publish(new DayChanged((int)startDay + d));
                    EventBus.Publish(new SurvivalXpGained(GameConstants.XP_PER_DAY, "day_survived"));
                }
            }

            _previousHour24 = newHour24;
            _totalGameDays = startDay + newHour24 / 24f;

            // 当天小时（0~24）
            debugCurrentHour = newHour24 % 24f;

            // 每小时生存经验
            int currentHourInt = Mathf.FloorToInt(_previousHour24);
            if (currentHourInt > _lastHourlyXP)
            {
                int hoursPassed = currentHourInt - _lastHourlyXP;
                for (int i = 0; i < hoursPassed; i++)
                {
                    int xp = Mathf.RoundToInt(GameConstants.XP_PER_HOUR * (IsNight() ? GameConstants.XP_NIGHT_MULTIPLIER : 1f));
                    EventBus.Publish(new SurvivalXpGained(xp, "hour_survived"));
                }
                _lastHourlyXP = currentHourInt;
            }

            UpdateSun();
            EventBus.Publish(new TimeOfDayChanged(debugCurrentHour));
        }

        void OnValidate()
        {
            if (Application.isPlaying)
            {
                // 调试跳转：同步累计小时和天数
                int dayPart = Mathf.FloorToInt(debugCurrentHour / 24f);
                float hourPart = debugCurrentHour % 24f;
                _previousHour24 = (_totalGameDays - startDay) * 24f + dayPart * 24f + hourPart;
                _totalGameDays = startDay + _previousHour24 / 24f;
                if (dayPart > 0)
                    EventBus.Publish(new DayChanged((int)_totalGameDays));
            }
        }

        void UpdateSun()
        {
            if (sunLight == null) return;

            float h = debugCurrentHour;
            float rad = Mathf.Deg2Rad;

            // 太阳仰角：-90°（午夜）→ 0°（日出）→ 90°（正午）→ 0°（日落）→ -90°
            float sunAngleDeg = (h / 24f) * 360f - 90f;
            float sunElevation = Mathf.Sin(sunAngleDeg * rad);  // -1 ~ 1
            float sunIntensity = Mathf.Max(0f, sunElevation);   // 0 ~ 1，平滑曲线

            sunLight.transform.rotation = Quaternion.Euler(sunAngleDeg, -30f, 0f);

            // 太阳颜色 + 强度：根据太阳高度平滑变化
            float dawnFactor = Mathf.Clamp01((sunElevation + 0.3f) / 0.6f);  // 日出日落过渡

            if (sunIntensity > 0.1f)
            {
                Color targetColor = Color.Lerp(dawnColor, dayColor, dawnFactor);
                sunLight.color = targetColor;
                sunLight.intensity = Mathf.Lerp(0.3f, 1.2f, sunIntensity);  // 强度平滑：日出0.3 → 正午1.2
                sunLight.shadowStrength = Mathf.Lerp(0.3f, 1f, sunIntensity);
                sunLight.enabled = true;
            }
            else
            {
                sunLight.enabled = false;
                sunLight.intensity = 0f;
            }

            // 月亮：相反方向 + 平滑月光
            float moonIntensity = 0f;
            if (moonLight != null)
            {
                float moonAngleDeg = sunAngleDeg + 180f;
                float moonElevation = Mathf.Sin(moonAngleDeg * rad);
                moonIntensity = Mathf.Max(0f, moonElevation);

                moonLight.transform.rotation = Quaternion.Euler(moonAngleDeg, -30f, 0f);

                if (moonIntensity > 0.05f)
                {
                    moonLight.enabled = true;
                    moonLight.color = moonLightColor;
                    moonLight.intensity = Mathf.Lerp(0.05f, 0.35f, moonIntensity);
                    moonLight.shadowStrength = Mathf.Lerp(0f, 0.3f, moonIntensity);
                }
                else
                {
                    moonLight.enabled = false;
                    moonLight.intensity = 0f;
                }
            }

            // 环境光：根据总亮度平滑过渡
            float ambient = Mathf.Max(sunIntensity * 0.8f, moonIntensity * 0.3f);
            RenderSettings.ambientIntensity = Mathf.Lerp(0.08f, 1f, ambient);
        }

        public string GetPeriodName()
        {
            float h = debugCurrentHour;
            if (h >= 5f && h < 8f) return "清晨";
            if (h >= 8f && h < 12f) return "上午";
            if (h >= 12f && h < 14f) return "中午";
            if (h >= 14f && h < 17f) return "下午";
            if (h >= 17f && h < 19f) return "黄昏";
            if (h >= 19f && h < 21f) return "傍晚";
            return "夜晚";
        }

        public bool IsNight()
        {
            float h = debugCurrentHour;
            return h >= 21f || h < 5f;
        }

    }
}
