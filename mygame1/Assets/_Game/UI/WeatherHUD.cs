using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Systems.Weather;

namespace _Game.UI
{
    /// <summary>
    /// 左上角天气 + 环境温度显示
    /// </summary>
    public class WeatherHUD : MonoBehaviour
    {
        private GameObject _canvasGo;
        private Text _weatherText, _tempText;
        private RectTransform _weatherRect, _tempRect;
        WeatherManager _weather;
        bool _inVehicle;

        void Start()
        {
            _weather = WeatherManager.Instance;
            EventBus.Subscribe<VehicleEnteredEvent>(_ => _inVehicle = true);
            EventBus.Subscribe<VehicleExitedEvent>(_ => _inVehicle = false);
            if (UIModeConfig.UseUGUI) CreateUI();
        }

        void CreateUI()
        {
            var existing = transform.Find("WeatherHUD_Canvas");
            if (existing != null) { _canvasGo = existing.gameObject; _weatherText = existing.Find("WeatherLabel")?.GetComponent<Text>(); _weatherRect = _weatherText?.rectTransform; _tempText = existing.Find("TempLabel")?.GetComponent<Text>(); _tempRect = _tempText?.rectTransform; return; }

            var canvas = UGUIBuilder.CreateCanvas("WeatherHUD_Canvas", 50);
            canvas.transform.SetParent(transform, false);
            _canvasGo = canvas.gameObject;

            _weatherText = MakeText("WeatherLabel", out _weatherRect);
            _tempText = MakeText("TempLabel", out _tempRect);
        }

        Text MakeText(string name, out RectTransform rect)
        {
            var t = UGUIBuilder.CreateTextAnchored(name, _canvasGo.transform,
                "", new Vector2(0, 1), Vector2.zero, 300, 22, 15,
                FontStyle.Normal, TextAnchor.UpperLeft);
            rect = t.rectTransform;
            return t;
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<VehicleEnteredEvent>(_ => _inVehicle = true);
            EventBus.Unsubscribe<VehicleExitedEvent>(_ => _inVehicle = false);
        }

        void Update()
        {
            if (UIModeConfig.UseUGUI && _weatherText != null) Refresh();
        }

        void Refresh()
        {
            if (_weather == null || _weather.CurrentData == null) return;
            var data = _weather.CurrentData;
            float baseY = _inVehicle ? -105f : -75f;

            Color labelColor = data.weatherType switch
            {
                Config.WeatherType.Sunny  => new Color(1f, 0.85f, 0.3f),
                Config.WeatherType.Cloudy => new Color(0.7f, 0.75f, 0.85f),
                Config.WeatherType.Rain   => new Color(0.35f, 0.6f, 0.9f),
                Config.WeatherType.Storm  => new Color(0.3f, 0.35f, 0.5f),
                _ => Color.white
            };
            string rainSuffix = data.rainIntensity > 0f ? $" (雨强 {(int)(data.rainIntensity * 100)}%)" : "";
            _weatherText.text = $"天气: {data.displayName}{rainSuffix}";
            _weatherText.color = labelColor;
            _weatherRect.anchoredPosition = new Vector2(10, baseY);

            float temp = _weather.AmbientTemperature;
            Color tempColor = temp < 5f ? new Color(0.4f, 0.7f, 1f)
                            : temp > 30f ? new Color(1f, 0.55f, 0.3f)
                            : Color.white;
            _tempText.text = $"环境温度: {temp:F1}°C";
            _tempText.color = tempColor;
            _tempRect.anchoredPosition = new Vector2(10, baseY - 20f);
        }
    }
}
