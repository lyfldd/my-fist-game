using UnityEngine;
using _Game.Core;
using _Game.Systems.Audio;
using _Game.Systems.Building;

namespace _Game.UI
{
    /// <summary>
    /// 左上角噪声等级显示，放在时间下方。驾驶时自动下移避免与车速重叠。
    /// </summary>
    public class DecibelHUD : MonoBehaviour
    {
        private Transform _player;
        private float _noiseLevel;
        private bool _inVehicle;

        void Start()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;
            EventBus.Subscribe<VehicleEnteredEvent>(_ => _inVehicle = true);
            EventBus.Subscribe<VehicleExitedEvent>(_ => _inVehicle = false);
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
        }

        void OnGUI()
        {
            if (UIModeConfig.UseUGUI) return;
            if (BuildMenuUI.IsVisible) return;
            if (_player == null) return;

            float level = _noiseLevel;
            string label;
            Color color;

            // 6 级噪声：与游戏中声音半径一一对应
            // 走路3m(4%) 跑步8m(10%) 近战命中15m(19%) 建造20~25m(25~31%)
            // 车辆30m(37%) 手枪50m(63%) 步枪80m(100%)
            if (level < 0.05f)
            {
                label = "安静";
                color = new Color(0.3f, 0.8f, 0.3f);  // 绿
            }
            else if (level < 0.15f)
            {
                label = "细微声响";
                color = new Color(0.6f, 0.8f, 0.2f);  // 黄绿
            }
            else if (level < 0.35f)
            {
                label = "轻微声响";
                color = Color.yellow;
            }
            else if (level < 0.55f)
            {
                label = "嘈杂";
                color = new Color(1f, 0.5f, 0f);  // 橙
            }
            else if (level < 0.80f)
            {
                label = "喧闹";
                color = new Color(1f, 0.25f, 0f);  // 橙红
            }
            else
            {
                label = "震耳欲聋";
                color = Color.red;
            }

            float y = _inVehicle ? 75f : 45f;

            var oldColor = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(10, y, 300, 24), $"噪声: {label}  ({level * 100f:F0}%)");
            GUI.color = oldColor;
        }
    }
}
