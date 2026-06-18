using UnityEngine;
using _Game.Core;
using _Game.Systems.Building;
using _Game.Systems.Weather;

namespace _Game.UI
{
    /// <summary>
    /// 左上角天气 + 环境温度显示，挂在噪声下方。
    /// </summary>
    public class WeatherHUD : MonoBehaviour
    {
        WeatherManager _weather;
        bool _inVehicle;

        void Start()
        {
            _weather = WeatherManager.Instance;
            EventBus.Subscribe<VehicleEnteredEvent>(_ => _inVehicle = true);
            EventBus.Subscribe<VehicleExitedEvent>(_ => _inVehicle = false);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<VehicleEnteredEvent>(_ => _inVehicle = true);
            EventBus.Unsubscribe<VehicleExitedEvent>(_ => _inVehicle = false);
        }

        void OnGUI()
        {
            if (UIModeConfig.UseUGUI) return;
            if (BuildMenuUI.IsVisible) return;
            if (_weather == null || _weather.CurrentData == null) return;

            var data = _weather.CurrentData;
            float baseY = _inVehicle ? 105f : 75f;

            // 天气名
            string weatherLabel = data.displayName;
            Color labelColor = data.weatherType switch
            {
                Config.WeatherType.Sunny  => new Color(1f, 0.85f, 0.3f),
                Config.WeatherType.Cloudy => new Color(0.7f, 0.75f, 0.85f),
                Config.WeatherType.Rain   => new Color(0.35f, 0.6f, 0.9f),
                Config.WeatherType.Storm  => new Color(0.3f, 0.35f, 0.5f),
                _ => Color.white
            };

            GUI.color = labelColor;
            string rainSuffix = data.rainIntensity > 0f
                ? $" (雨强 {(int)(data.rainIntensity * 100)}%)" : "";
            GUI.Label(new Rect(10, baseY, 300, 22),
                $"天气: {weatherLabel}{rainSuffix}");
            GUI.color = Color.white;

            // 环境温度
            float temp = _weather.AmbientTemperature;
            Color tempColor = temp < 5f ? new Color(0.4f, 0.7f, 1f) :
                              temp > 30f ? new Color(1f, 0.55f, 0.3f) :
                              Color.white;

            GUI.color = tempColor;
            GUI.Label(new Rect(10, baseY + 20f, 300, 22),
                $"环境温度: {temp:F1}°C");
            GUI.color = Color.white;
        }
    }
}
