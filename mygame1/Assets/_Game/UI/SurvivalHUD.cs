using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Config;
using _Game.Systems.Survival;

namespace _Game.UI
{
    /// <summary>
    /// 生存数值 HUD
    /// 屏幕左下角显示 4 个属性条：健康/饥饿/口渴/体温
    /// 自动创建 UI，无需手动搭建。没有 SurvivalSystem 也能显示（值=0）
    /// </summary>
    public class SurvivalHUD : MonoBehaviour
    {
        [Header("显示设置")]
        [SerializeField] private float barWidth = 200f;
        [SerializeField] private float barHeight = 20f;
        [SerializeField] private float panelX = 20f;   // 距右边缘
        [SerializeField] private float panelY = 20f;   // 距上边缘
        [SerializeField] private Color bgColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        private SurvivalSystem survival;
        private GameObject canvasObject;
        private Slider healthBar, hungerBar, thirstBar, tempBar;
        private Text healthLabel, hungerLabel, thirstLabel, tempLabel;
        private bool initialized;

        private void Start()
        {
            survival = ServiceLocator.Get<SurvivalSystem>();
            CreateCanvas();
            CreateBars();
            UpdateAllBars();
            if (survival != null)
                EventBus.Subscribe<SurvivalStatChanged>(OnStatChanged);
            initialized = true;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SurvivalStatChanged>(OnStatChanged);
        }

        private void CreateCanvas()
        {
            canvasObject = new GameObject("SurvivalHUD_Canvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private void CreateBars()
        {
            float y = panelY;

            healthBar  = CreateBar("HealthBar",  ref healthLabel,  "生命", Color.red,               y); y += barHeight + 6;
            hungerBar  = CreateBar("HungerBar",  ref hungerLabel,  "饥饿", new Color(0.8f, 0.6f, 0f), y); y += barHeight + 6;
            thirstBar  = CreateBar("ThirstBar",  ref thirstLabel,  "口渴", new Color(0f, 0.5f, 1f),   y); y += barHeight + 6;
            tempBar    = CreateBar("TempBar",    ref tempLabel,    "体温", new Color(1f, 0.5f, 0f),    y);
        }

        private Font GetFont()
        {
            // Unity 2022+ 移除了内置的 Arial.ttf，改用 LegacyRuntime.ttf
            Font font;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { font = null; }
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont("Segoe UI", 14);
            return font;
        }

        private Slider CreateBar(string name, ref Text labelOut, string labelText, Color barColor, float y)
        {
            // 容器
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvasObject.transform, false);
            go.transform.localScale = Vector3.one;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(barWidth + 60, barHeight);
            rt.anchoredPosition = new Vector2(-panelX, -y);

            // 标签
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);

            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(0, 1);
            labelRt.pivot = new Vector2(0, 0.5f);
            labelRt.sizeDelta = new Vector2(55, barHeight);
            labelRt.anchoredPosition = new Vector2(2, 0);

            var label = labelGo.AddComponent<Text>();
            label.font = GetFont();
            label.fontSize = 14;
            label.color = Color.white;
            label.text = labelText;
            label.alignment = TextAnchor.MiddleLeft;
            labelOut = label;

            // 进度条背景
            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(go.transform, false);
            sliderGo.transform.localScale = Vector3.one;

            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0, 0);
            sliderRt.anchorMax = new Vector2(1, 1);
            sliderRt.pivot = new Vector2(0.5f, 0.5f);
            sliderRt.offsetMin = new Vector2(58, 2);
            sliderRt.offsetMax = new Vector2(-2, -2);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.interactable = false;

            // 背景图
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0);
            bgRt.anchorMax = new Vector2(1, 1);
            bgRt.sizeDelta = Vector2.zero;
            bgRt.anchoredPosition = Vector2.zero;

            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = bgColor;

            // 填充图
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(sliderGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(0, 1);
            fillRt.pivot = new Vector2(0, 0.5f);
            fillRt.sizeDelta = Vector2.zero;
            fillRt.anchoredPosition = Vector2.zero;

            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = barColor;

            slider.targetGraphic = bgImage;
            slider.fillRect = fillRt;

            return slider;
        }

        private void OnStatChanged(SurvivalStatChanged evt)
        {
            switch (evt.StatType)
            {
                case SurvivalStatType.Health:      UpdateBar(healthBar, healthLabel, "生命", evt.NewValue); break;
                case SurvivalStatType.Hunger:      UpdateBar(hungerBar, hungerLabel, "饥饿", evt.NewValue); break;
                case SurvivalStatType.Thirst:      UpdateBar(thirstBar, thirstLabel, "口渴", evt.NewValue); break;
                case SurvivalStatType.Temperature: UpdateBar(tempBar,   tempLabel,   "体温", evt.NewValue); break;
            }
        }

        private void UpdateAllBars()
        {
            float h = survival != null ? survival.Health : 100f;
            float hu = survival != null ? survival.Hunger : 100f;
            float th = survival != null ? survival.Thirst : 100f;
            float te = survival != null ? survival.Temperature : 36.5f;

            UpdateBar(healthBar, healthLabel, "生命", h);
            UpdateBar(hungerBar, hungerLabel, "饥饿", hu);
            UpdateBar(thirstBar, thirstLabel, "口渴", th);
            UpdateBar(tempBar,   tempLabel,   "体温", te);
        }

        private void UpdateBar(Slider bar, Text label, string name, float value)
        {
            if (bar == null || label == null) return;
            bar.value = Mathf.Clamp(value, 0, 100);
            label.text = $"{name} {value:F0}";
        }

        private void Update()
        {
            if (!initialized) return;

            bool buildVisible = _Game.Systems.Building.BuildMenuUI.IsVisible;
            if (canvasObject != null && canvasObject.activeSelf != !buildVisible)
                canvasObject.SetActive(!buildVisible);
            if (buildVisible) return;

            // 如果一开始没有 SurvivalSystem，持续尝试查找
            if (survival == null)
            {
                survival = ServiceLocator.Get<SurvivalSystem>();
                if (survival != null)
                {
                    EventBus.Subscribe<SurvivalStatChanged>(OnStatChanged);
                    UpdateAllBars();
                }
            }
        }
    }
}
