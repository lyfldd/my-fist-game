using UnityEngine;

namespace _Game.Config
{
    public enum WeatherType
    {
        Sunny,
        Cloudy,
        Rain,
        Storm
    }

    [CreateAssetMenu(menuName = "Game/Weather Data")]
    public class WeatherData : ScriptableObject
    {
        public WeatherType weatherType;
        public string displayName = "晴";
        [Range(0f, 1f)] public float rainIntensity;
        public float tempModifier;
        [Range(0.5f, 2f)] public float thirstRateMult = 1f;
        [Range(0f, 1f)] public float ambientDarkness;
        public Color sunColor = Color.white;
        [Range(0f, 2f)] public float sunIntensity = 1f;

        public static WeatherData CreateDefault(WeatherType type)
        {
            return type switch
            {
                WeatherType.Sunny => new WeatherData
                {
                    weatherType = WeatherType.Sunny,
                    displayName = "晴",
                    rainIntensity = 0f,
                    tempModifier = 0f,
                    thirstRateMult = 1.0f,
                    ambientDarkness = 0f,
                    sunColor = new Color(1f, 0.95f, 0.85f),
                    sunIntensity = 1.2f
                },
                WeatherType.Cloudy => new WeatherData
                {
                    weatherType = WeatherType.Cloudy,
                    displayName = "多云",
                    rainIntensity = 0f,
                    tempModifier = -2f,
                    thirstRateMult = 1.0f,
                    ambientDarkness = 0.3f,
                    sunColor = new Color(0.85f, 0.88f, 0.95f),
                    sunIntensity = 0.8f
                },
                WeatherType.Rain => new WeatherData
                {
                    weatherType = WeatherType.Rain,
                    displayName = "雨",
                    rainIntensity = 0.5f,
                    tempModifier = -5f,
                    thirstRateMult = 0.8f,
                    ambientDarkness = 0.55f,
                    sunColor = new Color(0.55f, 0.6f, 0.7f),
                    sunIntensity = 0.5f
                },
                WeatherType.Storm => new WeatherData
                {
                    weatherType = WeatherType.Storm,
                    displayName = "暴雨",
                    rainIntensity = 1f,
                    tempModifier = -8f,
                    thirstRateMult = 0.6f,
                    ambientDarkness = 0.75f,
                    sunColor = new Color(0.35f, 0.38f, 0.45f),
                    sunIntensity = 0.25f
                },
                _ => null
            };
        }
    }
}
