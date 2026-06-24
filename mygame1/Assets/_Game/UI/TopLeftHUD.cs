using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Systems.AIBot;
using _Game.Systems.Vehicle;

namespace _Game.UI
{
    /// <summary>
    /// 左上角上下文 HUD：非驾驶时时间/天数，载具驾驶时车速/油量，AI驾驶时武器名
    /// </summary>
    public class TopLeftHUD : MonoBehaviour
    {
        private GameObject _canvasGo;
        private Text _mainText;
        private Text _weaponText, _hpText;
        private RectTransform _mainRect, _weaponRect, _hpRect;

        private float _currentHour;
        private int _currentDay = 1;
        private string _periodName = "";

        private VehicleController _vehicle;
        private float _speedKmh, _fuel;
        private bool _isBoosting;

        private AIBotPilot _pilot;
        private AIBot _pilotedBot;

        void Start()
        {
            EventBus.Subscribe<TimeOfDayChanged>(OnTimeChanged);
            EventBus.Subscribe<DayChanged>(OnDayChanged);
            EventBus.Subscribe<VehicleEnteredEvent>(OnVehicleEnter);
            EventBus.Subscribe<VehicleExitedEvent>(OnVehicleExit);
            EventBus.Subscribe<AIBotPilotEnteredEvent>(OnPilotEnter);
            EventBus.Subscribe<AIBotPilotExitedEvent>(OnPilotExit);
            if (UIModeConfig.UseUGUI) CreateUI();
        }

        void CreateUI()
        {
            var existing = transform.Find("TopLeftHUD_Canvas");
            if (existing != null)
            {
                _canvasGo = existing.gameObject;
                _mainText = existing.Find("MainText")?.GetComponent<Text>();
                _mainRect = _mainText?.rectTransform;
                _weaponText = existing.Find("WeaponText")?.GetComponent<Text>();
                _weaponRect = _weaponText?.rectTransform;
                _hpText = existing.Find("HPText")?.GetComponent<Text>();
                _hpRect = _hpText?.rectTransform;
                return;
            }

            _canvasGo = UGUIBuilder.CreateCanvas("TopLeftHUD_Canvas", 50).gameObject;
            _canvasGo.transform.SetParent(transform, false);

            _mainText = UGUIBuilder.CreateTextAnchored("MainText", _canvasGo.transform,
                "", new Vector2(0, 1), new Vector2(10, -10), 350, 60, 18,
                FontStyle.Normal, TextAnchor.UpperLeft);
            _mainText.supportRichText = true;
            _mainRect = _mainText.rectTransform;

            _weaponText = MakeAux("WeaponText", out _weaponRect);
            _hpText = MakeAux("HPText", out _hpRect);
        }

        Text MakeAux(string name, out RectTransform rect)
        {
            var t = UGUIBuilder.CreateTextAnchored(name, _canvasGo.transform,
                "", new Vector2(0, 1), Vector2.zero, 350, 24, 15,
                FontStyle.Normal, TextAnchor.UpperLeft);
            t.supportRichText = true;
            rect = t.rectTransform;
            return t;
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<TimeOfDayChanged>(OnTimeChanged);
            EventBus.Unsubscribe<DayChanged>(OnDayChanged);
            EventBus.Unsubscribe<VehicleEnteredEvent>(OnVehicleEnter);
            EventBus.Unsubscribe<VehicleExitedEvent>(OnVehicleExit);
            EventBus.Unsubscribe<AIBotPilotEnteredEvent>(OnPilotEnter);
            EventBus.Unsubscribe<AIBotPilotExitedEvent>(OnPilotExit);
        }

        void Update()
        {
            if (_vehicle != null) { _speedKmh = _vehicle.CurrentSpeedKmh; _fuel = _vehicle.CurrentFuel; _isBoosting = _vehicle.IsBoosting; }
            if (UIModeConfig.UseUGUI && _mainText != null) Refresh();
        }

        void Refresh()
        {
            if (_vehicle != null) RefreshVehicle(); else RefreshTime();
            if (_pilot != null) RefreshPilot(); else { _weaponText.text = ""; _hpText.text = ""; }
        }

        void RefreshTime()
        {
            int h = Mathf.FloorToInt(_currentHour);
            int m = Mathf.FloorToInt((_currentHour - h) * 60f);
            _mainText.text = $"<color=white>第{_currentDay}天  {h:00}:{m:00}  {_periodName}</color>";
        }

        void RefreshVehicle()
        {
            float hpPct = _vehicle.HealthPercent;
            float cur = _vehicle.CurrentHealth;
            float max = _vehicle.vehicleData != null ? _vehicle.vehicleData.maxHealth : 0f;
            string hpColor = hpPct > 0.5f ? "green" : hpPct > 0.25f ? "yellow" : "red";
            _mainText.text = $"<color=white>车速: {_speedKmh:F0} km/h{(_isBoosting ? " [SHIFT]" : "")}\n油量: {_fuel:F1} L\n车况: <color={hpColor}>{hpPct*100:F0}%  {cur:F0}/{max:F0}</color></color>";
        }

        void RefreshPilot()
        {
            string wn = _pilot.GetManualWeaponName();
            float y = _vehicle != null ? -155f : -125f;
            _weaponText.text = $"<color=#FFD700>当前武器: {wn}</color>";
            _weaponRect.anchoredPosition = new Vector2(10, y);
            if (_pilotedBot != null)
            {
                _hpText.text = $"<color=white>HP: {_pilotedBot.HP:F0}/{_pilotedBot.MaxHP:F0}</color>";
                _hpRect.anchoredPosition = new Vector2(10, y - 22f);
            }
            else _hpText.text = "";
        }

        void OnTimeChanged(TimeOfDayChanged e) { _currentHour = e.CurrentHour; _periodName = GetPeriodName(e.CurrentHour); }
        void OnDayChanged(DayChanged e) => _currentDay = e.Day;
        void OnVehicleEnter(VehicleEnteredEvent e) => _vehicle = e.Vehicle?.GetComponent<VehicleController>();
        void OnVehicleExit(VehicleExitedEvent e) => _vehicle = null;
        void OnPilotEnter(AIBotPilotEnteredEvent e) { _pilot = e.Bot?.GetComponent<AIBotPilot>(); _pilotedBot = e.Bot?.GetComponent<AIBot>(); }
        void OnPilotExit(AIBotPilotExitedEvent e) { _pilot = null; _pilotedBot = null; }

        string GetPeriodName(float h) => h >= 5 && h < 8 ? "清晨" : h < 12 ? "上午" : h < 14 ? "中午" : h < 17 ? "下午" : h < 19 ? "黄昏" : h < 21 ? "傍晚" : "夜晚";
    }
}
