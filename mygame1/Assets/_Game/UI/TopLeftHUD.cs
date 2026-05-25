using UnityEngine;
using _Game.Core;
using _Game.Systems.AIBot;
using _Game.Systems.Building;
using _Game.Systems.Vehicle;

namespace _Game.UI
{
    /// <summary>
    /// 左上角上下文 HUD：非驾驶时显示时间/天数，载具驾驶时显示车速/油量，AI机器人驾驶时显示武器名。
    /// 挂载到场景任意 GameObject，Start 时自动注册事件订阅。
    /// </summary>
    public class TopLeftHUD : MonoBehaviour
    {
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
        }

        void OnGUI()
        {
            if (BuildMenuUI.IsVisible) return;
            if (_vehicle != null)
                DrawVehicleInfo();
            else
                DrawTimeInfo();

            if (_pilot != null)
                DrawPilotWeapon();
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

        // ===== 渲染 =====

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
            // 放在天气HUD下方（天气占 y=75~117 或载具时 y=105~147）
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
