using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Systems.AIBot;
using _Game.Systems.Building;
using _Game.Systems.Vehicle;

namespace _Game.UI
{
    /// <summary>
    /// 左上角上下文 HUD：非驾驶时显示时间/天数，载具驾驶时显示车速/油量，AI机器人驾驶时显示武器名。
    /// 挂载到场景任意 GameObject，Start 时自动注册事件订阅。
    /// UGUI 模式下自动创建 Canvas Text 替代 OnGUI。
    /// </summary>
    public class TopLeftHUD : MonoBehaviour
    {
        // --- UGUI ---
        private GameObject _canvasGo;
        private Text _mainText;
        private Text _weaponText, _hpText;
        private RectTransform _mainRect, _weaponRect, _hpRect;

        private float _currentHour;
        private int _currentDay = 1;
        private string _periodName = "";

        private VehicleController _vehicle;
        private float _speedKmh;
        private float _fuel;
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

            if (UIModeConfig.UseUGUI)
                CreateUGUI();
        }

        void CreateUGUI()
        {
            // 检查是否已有 TopLeftHUD_Canvas（避免重复创建）
            _canvasGo = GameObject.Find("TopLeftHUD_Canvas");
            if (_canvasGo == null)
            {
                _canvasGo = new GameObject("TopLeftHUD_Canvas", typeof(Canvas), typeof(CanvasScaler));
                _canvasGo.transform.SetParent(transform, false);
                var canvas = _canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
            }
            else
            {
                // 已存在：清理旧的文本子对象，避免重复叠加
                foreach (Transform child in _canvasGo.transform)
                    Destroy(child.gameObject);
                _canvasGo.transform.SetParent(transform, false);
                var canvas = _canvasGo.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 50;
                }
            }

            Font font = Font.CreateDynamicFontFromOSFont("Arial", 14);

            // 主文本（时间 或 车速/油量）
            var mainGo = new GameObject("MainText");
            mainGo.transform.SetParent(_canvasGo.transform, false);
            _mainText = mainGo.AddComponent<Text>();
            _mainText.font = font;
            _mainText.fontSize = 18;
            _mainText.alignment = TextAnchor.UpperLeft;
            _mainText.raycastTarget = false;
            _mainText.supportRichText = true;
            _mainRect = mainGo.GetComponent<RectTransform>();
            _mainRect.anchorMin = new Vector2(0, 1);
            _mainRect.anchorMax = new Vector2(0, 1);
            _mainRect.pivot = new Vector2(0, 1);
            _mainRect.anchoredPosition = new Vector2(10, -10);
            _mainRect.sizeDelta = new Vector2(350, 60);

            // 武器文本
            _weaponText = CreateAuxText("WeaponText", font, out _weaponRect);
            // HP 文本
            _hpText = CreateAuxText("HPText", font, out _hpRect);
        }

        Text CreateAuxText(string name, Font font, out RectTransform rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvasGo.transform, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = 15;
            t.alignment = TextAnchor.UpperLeft;
            t.raycastTarget = false;
            t.supportRichText = true;
            rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(350, 24);
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
            if (_vehicle != null)
            {
                _speedKmh = _vehicle.CurrentSpeedKmh;
                _fuel = _vehicle.CurrentFuel;
                _isBoosting = _vehicle.IsBoosting;
            }

            if (UIModeConfig.UseUGUI && _mainText != null)
                RefreshUGUI();
        }

        void RefreshUGUI()
        {
            if (_vehicle != null)
                RefreshVehicleInfoUGUI();
            else
                RefreshTimeInfoUGUI();

            if (_pilot != null)
                RefreshPilotWeaponUGUI();
            else
            {
                _weaponText.text = "";
                _hpText.text = "";
            }
        }

        void RefreshTimeInfoUGUI()
        {
            int hour = Mathf.FloorToInt(_currentHour);
            int minute = Mathf.FloorToInt((_currentHour - hour) * 60f);
            _mainText.text = $"<color=white>第{_currentDay}天  {hour:00}:{minute:00}  {_periodName}</color>";
        }

        void RefreshVehicleInfoUGUI()
        {
            string boostLabel = _isBoosting ? " [SHIFT]" : "";
            _mainText.text = $"<color=white>车速: {_speedKmh:F0} km/h{boostLabel}\n" +
                             $"油量: {_fuel:F1} L</color>";
        }

        void RefreshPilotWeaponUGUI()
        {
            if (_pilot == null) return;
            string weaponName = _pilot.GetManualWeaponName();
            float y = _vehicle != null ? -155f : -125f;

            _weaponText.text = $"<color=#FFD700>当前武器: {weaponName}</color>";
            _weaponRect.anchoredPosition = new Vector2(10, y);

            if (_pilotedBot != null)
            {
                _hpText.text = $"<color=white>HP: {_pilotedBot.HP:F0}/{_pilotedBot.MaxHP:F0}</color>";
                _hpRect.anchoredPosition = new Vector2(10, y - 22f);
            }
            else
            {
                _hpText.text = "";
            }
        }

        // ===== 事件回调 =====

        void OnTimeChanged(TimeOfDayChanged evt)
        {
            _currentHour = evt.CurrentHour;
            _periodName = GetPeriodName(evt.CurrentHour);
        }

        void OnDayChanged(DayChanged evt)
        {
            _currentDay = evt.Day;
        }

        void OnVehicleEnter(VehicleEnteredEvent evt)
        {
            _vehicle = evt.Vehicle != null ? evt.Vehicle.GetComponent<VehicleController>() : null;
        }

        void OnVehicleExit(VehicleExitedEvent evt)
        {
            _vehicle = null;
        }

        void OnPilotEnter(AIBotPilotEnteredEvent evt)
        {
            _pilot = evt.Bot != null ? evt.Bot.GetComponent<AIBotPilot>() : null;
            _pilotedBot = evt.Bot != null ? evt.Bot.GetComponent<AIBot>() : null;
            Debug.Log($"[TopLeftHUD] 进入驾驶, pilot={_pilot != null}, weapon={_pilot?.GetManualWeaponName()}");
        }

        void OnPilotExit(AIBotPilotExitedEvent evt)
        {
            _pilot = null;
            _pilotedBot = null;
        }

        // ===== 渲染 (IMGUI) =====

        void OnGUI()
        {
            if (UIModeConfig.UseUGUI) return;
            if (BuildMenuUI.IsVisible) return;
            if (_vehicle != null)
                DrawVehicleInfo();
            else
                DrawTimeInfo();

            if (_pilot != null)
                DrawPilotWeapon();
        }

        void DrawTimeInfo()
        {
            int hour = Mathf.FloorToInt(_currentHour);
            int minute = Mathf.FloorToInt((_currentHour - hour) * 60f);
            GUI.Label(new Rect(10, 10, 300, 30),
                $"<color=white>第{_currentDay}天  {hour:00}:{minute:00}  {_periodName}</color>");
        }

        void DrawVehicleInfo()
        {
            string boostLabel = _isBoosting ? " [SHIFT]" : "";
            GUI.Label(new Rect(10, 10, 300, 60),
                $"<color=white>车速: {_speedKmh:F0} km/h{boostLabel}\n" +
                $"油量: {_fuel:F1} L</color>");
        }

        void DrawPilotWeapon()
        {
            if (_pilot == null) return;
            string weaponName = _pilot.GetManualWeaponName();
            float y = _vehicle != null ? 155f : 125f;

            GUI.Label(new Rect(10, y, 300, 24),
                $"<color=#FFD700>当前武器: {weaponName}</color>");
            if (_pilotedBot != null)
                GUI.Label(new Rect(10, y + 22, 300, 24),
                    $"<color=white>HP: {_pilotedBot.HP:F0}/{_pilotedBot.MaxHP:F0}</color>");
        }

        string GetPeriodName(float hour)
        {
            if (hour >= 5f && hour < 8f) return "清晨";
            if (hour >= 8f && hour < 12f) return "上午";
            if (hour >= 12f && hour < 14f) return "中午";
            if (hour >= 14f && hour < 17f) return "下午";
            if (hour >= 17f && hour < 19f) return "黄昏";
            if (hour >= 19f && hour < 21f) return "傍晚";
            return "夜晚";
        }
    }
}
