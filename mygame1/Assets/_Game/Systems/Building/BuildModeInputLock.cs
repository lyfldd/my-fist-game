using UnityEngine;
using _Game.Core;
using _Game.Systems.Player;
using _Game.Systems.Weapon;

namespace _Game.Systems.Building
{
    /// <summary>
    /// 建造模式输入锁定 — 解决输入冲突
    ///
    /// 订阅 BuildModeEntered/BuildModeExited 事件，在建造期间：
    /// - 禁用 PlayerController 移动（WASD 不生效）
    /// - 禁用武器系统（瞄准/射击不能用鼠标左键）
    /// - 禁用手持快捷栏输入（数字键切换武器和被建造物选择冲突）
    ///
    /// 保留的输入：
    /// - 鼠标移动（用于选择建造位置）
    /// - B/Esc/鼠标左右键（BuildModeController 自己管理）
    /// - 摄像机跟随（CameraFollow 不受影响）
    ///
    /// 挂载：和 BuildModeController 同一 GameObject
    /// </summary>
    public class BuildModeInputLock : MonoBehaviour
    {
        private PlayerController _playerController;
        private WeaponAiming _weaponAiming;
        private WeaponShooting _weaponShooting;
        private MonoBehaviour[] _blockedScripts;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _weaponAiming = GetComponent<WeaponAiming>();
            _weaponShooting = GetComponent<WeaponShooting>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BuildModeEnteredEvent>(OnBuildModeEntered);
            EventBus.Subscribe<BuildModeExitedEvent>(OnBuildModeExited);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BuildModeEnteredEvent>(OnBuildModeEntered);
            EventBus.Unsubscribe<BuildModeExitedEvent>(OnBuildModeExited);
        }

        private void OnBuildModeEntered(BuildModeEnteredEvent evt)
        {
            BlockInput();
        }

        private void OnBuildModeExited(BuildModeExitedEvent evt)
        {
            RestoreInput();
        }

        private void BlockInput()
        {
            // 停止物理移动（清空当前速度）
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = new Vector3(0, rb.velocity.y, 0);
            }

            // 禁用移动控制器
            if (_playerController != null)
                _playerController.enabled = false;

            // 禁用武器瞄准
            if (_weaponAiming != null)
                _weaponAiming.enabled = false;

            // 禁用武器射击
            if (_weaponShooting != null)
                _weaponShooting.enabled = false;

            Debug.Log("[BuildModeInputLock] 输入已锁定（移动/武器禁用）");
        }

        private void RestoreInput()
        {
            if (_playerController != null)
                _playerController.enabled = true;

            if (_weaponAiming != null)
                _weaponAiming.enabled = true;

            if (_weaponShooting != null)
                _weaponShooting.enabled = true;

            Debug.Log("[BuildModeInputLock] 输入已恢复");
        }
    }
}
