using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Systems.Audio;

namespace _Game.UI
{
    /// <summary>
    /// 左上角噪声等级显示
    /// </summary>
    public class DecibelHUD : MonoBehaviour
    {
        private GameObject _canvasGo;
        private Text _noiseText;
        private RectTransform _noiseRect;
        private Transform _player;
        private float _noiseLevel;
        private bool _inVehicle;

        void Start()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;
            EventBus.Subscribe<VehicleEnteredEvent>(_ => _inVehicle = true);
            EventBus.Subscribe<VehicleExitedEvent>(_ => _inVehicle = false);
            if (UIModeConfig.UseUGUI) CreateUI();
        }

        void CreateUI()
        {
            var existing = transform.Find("DecibelHUD_Canvas");
            if (existing != null) { _canvasGo = existing.gameObject; _noiseText = existing.Find("NoiseText")?.GetComponent<Text>(); _noiseRect = _noiseText?.rectTransform; _noiseText.text = "噪声: 安静  (0%)"; return; }

            var canvas = UGUIBuilder.CreateCanvas("DecibelHUD_Canvas", 50);
            canvas.transform.SetParent(transform, false);
            _canvasGo = canvas.gameObject;

            _noiseText = UGUIBuilder.CreateTextAnchored("NoiseText", _canvasGo.transform,
                "", new Vector2(0, 1), Vector2.zero, 300, 24, 16,
                FontStyle.Normal, TextAnchor.UpperLeft);
            _noiseRect = _noiseText.rectTransform;
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<VehicleEnteredEvent>(_ => _inVehicle = true);
            EventBus.Unsubscribe<VehicleExitedEvent>(_ => _inVehicle = false);
        }

        void Update()
        {
            if (_player == null || DecibelSystem.Instance == null) return;
            _noiseLevel = DecibelSystem.Instance.GetAmbientNoiseLevel(_player.position);
            if (UIModeConfig.UseUGUI && _noiseText != null) Refresh();
        }

        static void DecodeNoiseLevel(float level, out string label, out Color color)
        {
            if (level < 0.05f)       { label = "安静";      color = new Color(0.3f, 0.8f, 0.3f); }
            else if (level < 0.15f)  { label = "细微声响";  color = new Color(0.6f, 0.8f, 0.2f); }
            else if (level < 0.35f)  { label = "轻微声响";  color = Color.yellow; }
            else if (level < 0.55f)  { label = "嘈杂";      color = new Color(1f, 0.5f, 0f); }
            else if (level < 0.80f)  { label = "喧闹";      color = new Color(1f, 0.25f, 0f); }
            else                     { label = "震耳欲聋";  color = Color.red; }
        }

        void Refresh()
        {
            DecodeNoiseLevel(_noiseLevel, out var label, out var color);
            float y = _inVehicle ? 75f : 45f;
            _noiseText.text = $"噪声: {label}  ({_noiseLevel * 100f:F0}%)";
            _noiseText.color = color;
            _noiseRect.anchoredPosition = new Vector2(10, -y);
        }
    }
}
