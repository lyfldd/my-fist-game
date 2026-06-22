using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Config;
using _Game.Systems.Survival;

namespace _Game.UI
{
    /// <summary>
    /// 生存数值 HUD — 屏幕右上角显示健康/饥饿/口渴/体温。
    /// v2: 使用 UGUIBuilder，代码量减少 ~40%。
    /// </summary>
    public class SurvivalHUD : MonoBehaviour
    {
        [Header("显示设置")]
        [SerializeField] private float barWidth = 200f;
        [SerializeField] private float barHeight = 20f;
        [SerializeField] private float panelX = 20f;
        [SerializeField] private float panelY = 20f;
        [SerializeField] private Color bgColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        private SurvivalSystem _survival;
        private GameObject _canvasGo;
        private Slider _healthBar, _hungerBar, _thirstBar, _tempBar;
        private Text _healthLabel, _hungerLabel, _thirstLabel, _tempLabel;
        private bool _initialized;

        private void Start()
        {
            if (!UIModeConfig.UseUGUI) { enabled = false; return; }
            _survival = ServiceLocator.Get<SurvivalSystem>();
            BuildUI();
            RefreshAll();
            if (_survival != null) EventBus.Subscribe<SurvivalStatChanged>(OnStatChanged);
            _initialized = true;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SurvivalStatChanged>(OnStatChanged);
        }

        private void BuildUI()
        {
            var existing = transform.Find("SurvivalHUD_Canvas");
            if (existing != null)
            {
                _canvasGo = existing.gameObject;
                FindBars(existing);
                RefreshAll();
                return;
            }

            var canvas = UGUIBuilder.CreateCanvas("SurvivalHUD_Canvas", 100);
            canvas.transform.SetParent(transform, false);
            _canvasGo = canvas.gameObject;

            float y = panelY;
            (_healthBar, _healthLabel) = CreateBar("HealthBar", "生命", Color.red,                  y += barHeight + 6);
            (_hungerBar, _hungerLabel) = CreateBar("HungerBar", "饥饿", new Color(0.8f,0.6f,0f),    y += barHeight + 6);
            (_thirstBar, _thirstLabel) = CreateBar("ThirstBar", "口渴", new Color(0f,0.5f,1f),      y += barHeight + 6);
            (_tempBar,   _tempLabel)   = CreateBar("TempBar",   "体温", new Color(1f,0.5f,0f),      y += barHeight + 6);
        }

        void FindBars(Transform canvasRoot)
        {
            (_healthBar, _healthLabel) = FindBar(canvasRoot, "HealthBar");
            (_hungerBar, _hungerLabel) = FindBar(canvasRoot, "HungerBar");
            (_thirstBar, _thirstLabel) = FindBar(canvasRoot, "ThirstBar");
            (_tempBar,   _tempLabel)   = FindBar(canvasRoot, "TempBar");
        }

        (Slider, Text) FindBar(Transform parent, string name)
        {
            var go = parent.Find(name);
            if (go == null) return (null, null);
            var s = go.Find("Slider")?.GetComponent<Slider>();
            var l = go.Find("Label")?.GetComponent<Text>();
            return (s, l);
        }

        private (Slider, Text) CreateBar(string name, string labelText, Color barColor, float y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(barWidth + 60, barHeight);
            rt.anchoredPosition = new Vector2(-panelX, -y);

            var label = UGUIBuilder.CreateTextAnchored("Label", go.transform, labelText,
                new Vector2(0, 0.5f), new Vector2(2, 0), 55, barHeight, 14,
                FontStyle.Normal, TextAnchor.MiddleLeft);

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(go.transform, false);
            var srt = sliderGo.GetComponent<RectTransform>();
            UGUIBuilder.Stretch(srt);
            srt.offsetMin = new Vector2(58, 2);
            srt.offsetMax = new Vector2(-2, -2);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 100; slider.interactable = false;

            var fill = UGUIBuilder.CreateProgressBar("Fill", sliderGo.transform, barWidth, barHeight,
                bgColor, barColor, out _, out var bgImg);
            slider.targetGraphic = bgImg;
            slider.fillRect = fill.rectTransform;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.zero;
            fill.rectTransform.pivot = new Vector2(0, 0.5f);
            slider.fillRect = fill.rectTransform;

            return (slider, label);
        }

        private void OnStatChanged(SurvivalStatChanged evt)
        {
            switch (evt.StatType)
            {
                case SurvivalStatType.Health:      SetBar(_healthBar, _healthLabel, "生命", evt.NewValue); break;
                case SurvivalStatType.Hunger:      SetBar(_hungerBar, _hungerLabel, "饥饿", evt.NewValue); break;
                case SurvivalStatType.Thirst:      SetBar(_thirstBar, _thirstLabel, "口渴", evt.NewValue); break;
                case SurvivalStatType.Temperature: SetBar(_tempBar,   _tempLabel,   "体温", evt.NewValue); break;
            }
        }

        private void RefreshAll()
        {
            SetBar(_healthBar, _healthLabel, "生命", _survival != null ? _survival.Health : 100f);
            SetBar(_hungerBar, _hungerLabel, "饥饿", _survival != null ? _survival.Hunger : 100f);
            SetBar(_thirstBar, _thirstLabel, "口渴", _survival != null ? _survival.Thirst : 100f);
            SetBar(_tempBar,   _tempLabel,   "体温", _survival != null ? _survival.Temperature : 36.5f);
        }

        private void SetBar(Slider bar, Text label, string name, float value)
        {
            if (bar == null || label == null) return;
            bar.value = Mathf.Clamp(value, 0, 100);
            label.text = $"{name} {value:F0}";
        }

        private void Update()
        {
            if (!_initialized) return;
            bool buildVisible = _Game.Systems.Building.BuildMenuUI.IsVisible;
            if (_canvasGo != null && _canvasGo.activeSelf != !buildVisible)
                _canvasGo.SetActive(!buildVisible);
            if (buildVisible) return;

            if (_survival == null)
            {
                _survival = ServiceLocator.Get<SurvivalSystem>();
                if (_survival != null) { EventBus.Subscribe<SurvivalStatChanged>(OnStatChanged); RefreshAll(); }
            }
        }
    }
}
