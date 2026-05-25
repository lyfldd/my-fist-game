using UnityEngine;
using _Game.Core;
using _Game.Systems.Player;
using _Game.Systems.Weapon;
using _Game.Systems.Combat;
using _Game.Systems.Interaction;

namespace _Game.Systems.Vehicle
{
    /// <summary>
    /// 车辆输入锁定 — 驾驶时接管输入
    ///
    /// 订阅 VehicleEnteredEvent/VehicleExitedEvent 事件，在驾驶期间：
    /// - 禁用 PlayerController（WASD 不控制角色）
    /// - 禁用 WeaponAiming / WeaponShooting / PlayerCombat（鼠标左键无效）
    /// - 禁用 PlayerInteraction（E 键不检测交互）
    /// - WASD 重映射为油门/转向
    /// - E 键重映射为下车
    ///
    /// 保留的输入：
    /// - 摄像机跟随（CameraFollow 通过 SetTarget 切换到车辆）
    /// - 鼠标移动（不影响）
    ///
    /// 挂载：玩家 GameObject（和 PlayerController / BuildModeInputLock 同物体）
    /// </summary>
    public class VehicleInputLock : MonoBehaviour
    {
        // 被禁用的玩家组件
        private PlayerController _playerController;
        private WeaponAiming _weaponAiming;
        private WeaponShooting _weaponShooting;
        private PlayerCombat _playerCombat;
        private PlayerInteraction _playerInteraction;

        // 车辆引用
        private VehicleController _vehicleController;
        private VehicleInteraction _vehicleInteraction;

        private bool _isDriving;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _weaponAiming = GetComponent<WeaponAiming>();
            _weaponShooting = GetComponent<WeaponShooting>();
            _playerCombat = GetComponent<PlayerCombat>();
            _playerInteraction = GetComponent<PlayerInteraction>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<VehicleEnteredEvent>(OnVehicleEntered);
            EventBus.Subscribe<VehicleExitedEvent>(OnVehicleExited);
            InputRouter.BindKey(KeyCode.E, InputPriority.Action + 5, HandleExitVehicle, this);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<VehicleEnteredEvent>(OnVehicleEntered);
            EventBus.Unsubscribe<VehicleExitedEvent>(OnVehicleExited);
            InputRouter.UnbindAll(this);
        }

        bool HandleExitVehicle()
        {
            if (!_isDriving || _vehicleInteraction == null) return false;
            _vehicleInteraction.Exit();
            return true;
        }

        private void OnVehicleEntered(VehicleEnteredEvent evt)
        {
            if (evt.Driver != gameObject) return;

            _vehicleController = evt.Vehicle.GetComponent<VehicleController>();
            _vehicleInteraction = evt.Vehicle.GetComponent<VehicleInteraction>();
            _isDriving = true;

            BlockInput();
        }

        private void OnVehicleExited(VehicleExitedEvent evt)
        {
            if (evt.Driver != gameObject) return;

            _isDriving = false;
            RestoreInput();
            _vehicleController = null;
            _vehicleInteraction = null;
        }

        private void Update()
        {
            if (!_isDriving || _vehicleController == null || !_vehicleController.HasDriver) return;

            // === 车辆操作输入 ===
            float throttle = 0f;
            float steer = 0f;
            float brake = 0f;

            // W/S → 油门/倒车
            if (Input.GetKey(KeyCode.W))
                throttle = 1f;
            else if (Input.GetKey(KeyCode.S))
                throttle = -1f;

            // A/D → 转向
            if (Input.GetKey(KeyCode.A))
                steer = -1f;
            else if (Input.GetKey(KeyCode.D))
                steer = 1f;

            // Space → 手刹/刹车
            if (Input.GetKey(KeyCode.Space))
                brake = 1f;

            _vehicleController.SetThrottle(throttle);
            _vehicleController.SetSteer(steer);
            _vehicleController.SetBrake(brake);

            // Left Shift → 加速档
            _vehicleController.SetBoost(Input.GetKey(KeyCode.LeftShift));
        }

        private void BlockInput()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = new Vector3(0, rb.velocity.y, 0);
            }

            SetComponentsEnabled(false);
        }

        private void RestoreInput()
        {
            SetComponentsEnabled(true);
        }

        private void SetComponentsEnabled(bool enabled)
        {
            if (_playerController != null)
                _playerController.enabled = enabled;

            if (_weaponAiming != null)
                _weaponAiming.enabled = enabled;

            if (_weaponShooting != null)
                _weaponShooting.enabled = enabled;

            if (_playerCombat != null)
                _playerCombat.enabled = enabled;

            if (_playerInteraction != null)
                _playerInteraction.enabled = enabled;
        }
    }
}
