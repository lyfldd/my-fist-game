using System;
using System.Collections.Generic;

namespace _Game.Systems.SaveLoad
{
    [Serializable]
    public class TimeWeatherSaveData : ICloneable
    {
        // TimeManager
        public float totalGameDays;

        // WeatherManager — 马尔可夫链状态机
        public string currentWeather;               // "Sunny"/"Cloudy"/"Rain"/"Storm"
        public int stormCooldownRemaining;
        public int consecutiveRainCount;

        // P1 预留：最近天气历史（马尔可夫链转移概率计算）
        public List<string> recentWeatherHistory;

        public object Clone()
        {
            return new TimeWeatherSaveData
            {
                totalGameDays = this.totalGameDays,
                currentWeather = this.currentWeather,
                stormCooldownRemaining = this.stormCooldownRemaining,
                consecutiveRainCount = this.consecutiveRainCount,
                recentWeatherHistory = this.recentWeatherHistory != null
                    ? new List<string>(this.recentWeatherHistory) : null,
            };
        }
    }
}
