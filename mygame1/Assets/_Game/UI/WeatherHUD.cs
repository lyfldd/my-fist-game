using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Systems.Building;
using _Game.Systems.Weather;

namespace _Game.UI
{
    /// <summary>
    /// 左上角天气 + 环境温度显示，挂在噪声下方。
    /// UGUI 模式下自动创建 Canvas Text 替代 OnGUI。
    /// </summary>
    public class WeatherHUD : MonoBehaviour
    {
        // --- UGUI ---
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

            if (UIModeConfig.UseUGUI)
                CreateUGUI();
        }

        void CreateUGUI()
        {
            _canvasGo = new GameObject("WeatherHUD_Canvas", typeof(Canvas), typeof(CanvasScaler));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            Font font = Font.CreateDynamicFontFromOSFont("Arial", 14);

            // 天气标签
            _weatherText = CreateText("WeatherLabel", font, out _weatherRect);
            // 温度标签
            _tempText = CreateText("TempLabel", font, out _tempRect);
        }

        Text CreateText(string name, Font font, out RectTransform rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvasGo.transform, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = 15;
            t.alignment = TextAnchor.UpperLeft;
            t.raycastTarget = false;
            rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(300, 22);
            return t;
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<VehicleEnteredEvent>(_ => _inVehicle = true);
            EventBus.Unsubscribe<VehicleExitedEvent>(_ => _inVehicle = false);
        }

        void Update()
        {
            if (UIModeConfig.UseUGUI && _weatherText != null)
                RefreshUGUI();
        }

        void RefreshUGUI()
        {
            if (_weather == null || _weather.CurrentData == null) return;

            var data = _weather.CurrentData;
            float baseY = _inVehicle ? -105f : -75f;

            // 天气
            Color labelColor = data.weatherType switch
            {
                Config.WeatherType.Sunny  => new Color(1f, 0.85f, 0.3f),
                Config.WeatherType.Cloudy => new Color(0.7f, 0.75f, 0.85f),
                Config.WeatherType.Rain   => new Color(0.35f, 0.6f, 0.9f),
                Config.WeatherType.Storm  => new Color(0.3f, 0.35f, 0.5f),
                _ => Color.white
            };
            string rainSuffix = data.rainIntensity > 0f
                ? $" (雨强 {(int)(data.rainIntensity * 100)}%)" : "";
            _weatherText.text = $"天气: {data.displayName}{rainSuffix}";
            _weatherText.color = labelColor;
            _weatherRect.anchoredPosition = new Vector2(10, baseY);

            // 环境温度
            float temp = _weather.AmbientTemperature;
            Color tempColor = temp < 5f ? new Color(0.4f, 0.7f, 1f) :
                              temp > 30f ? new Color(1f, 0.55f, 0.3f) :
                              Color.white;
            _tempText.text = $"环境温度: {temp:F1}°C";
            _tempText.color = tempColor;
            _tempRect.anchoredPosition = new Vector2(10, baseY - 20f);
        }

    }
}
