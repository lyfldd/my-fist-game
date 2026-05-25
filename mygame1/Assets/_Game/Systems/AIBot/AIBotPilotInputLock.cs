using UnityEngine;
using _Game.Core;
using _Game.Systems.Combat;
using _Game.Systems.Interaction;
using _Game.Systems.Player;
using _Game.Systems.Weapon;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人驾驶输入锁定。驾驶时禁用玩家组件、隐藏模型、绑定驾驶按键。
    /// 挂载在玩家GameObject上。
    /// </summary>
    public class AIBotPilotInputLock : MonoBehaviour
    {
        private bool _isPiloting;
        private AIBotPilot _currentPilot;

        // 缓存的玩家组件
        private MonoBehaviour _playerController;
        private MonoBehaviour _weaponAiming;
        private MonoBehaviour _weaponShooting;
        private MonoBehaviour _weaponSwitcher;
        private MonoBehaviour _playerCombat;
        private MonoBehaviour _playerInteraction;
        private DamageablePlayer _damageablePlayer;
        private Renderer[] _playerRenderers;
        private Collider _playerCollider;
        private Rigidbody _playerRb;

        void Awake()
        {
            CachePlayerComponents();
        }

        void OnEnable()
        {
            EventBus.Subscribe<AIBotPilotEnteredEvent>(OnPilotEntered);
            EventBus.Subscribe<AIBotPilotExitedEvent>(OnPilotExited);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<AIBotPilotEnteredEvent>(OnPilotEntered);
            EventBus.Unsubscribe<AIBotPilotExitedEvent>(OnPilotExited);
        }

        void CachePlayerComponents()
        {
            _playerController = GetComponent<PlayerController>();
            if (_playerController == null) _playerController = GetComponentInChildren<PlayerController>();
            _weaponAiming = GetComponent<WeaponAiming>();
            if (_weaponAiming == null) _weaponAiming = GetComponentInChildren<WeaponAiming>();
            _weaponShooting = GetComponent<WeaponShooting>();
            if (_weaponShooting == null) _weaponShooting = GetComponentInChildren<WeaponShooting>();
            _weaponSwitcher = GetComponent<WeaponSwitcher>();
            if (_weaponSwitcher == null) _weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
            _playerCombat = GetComponent<PlayerCombat>();
            if (_playerCombat == null) _playerCombat = GetComponentInChildren<PlayerCombat>();
            _playerInteraction = GetComponent<_Game.Systems.Interaction.PlayerInteraction>();
            if (_playerInteraction == null) _playerInteraction = GetComponentInChildren<_Game.Systems.Interaction.PlayerInteraction>();

            _damageablePlayer = GetComponent<DamageablePlayer>();
            if (_damageablePlayer == null) _damageablePlayer = GetComponentInChildren<DamageablePlayer>();

            _playerRenderers = GetComponentsInChildren<Renderer>();
            _playerCollider = GetComponent<Collider>();
            _playerRb = GetComponent<Rigidbody>();
        }

        // ============================================================
        // 事件处理
        // ============================================================

        void OnPilotEntered(AIBotPilotEnteredEvent evt)
        {
            if (evt.Pilot != gameObject) return;

            _isPiloting = true;
            _currentPilot = evt.Bot.GetComponent<AIBotPilot>();

            // 禁用玩家组件
            SetComponentEnabled(_playerController, false);
            SetComponentEnabled(_weaponAiming, false);
            SetComponentEnabled(_weaponShooting, false);
            SetComponentEnabled(_weaponSwitcher, false);
            SetComponentEnabled(_playerCombat, false);
            SetComponentEnabled(_playerInteraction, false);

            // 隐藏玩家模型
            if (_playerRenderers != null)
                foreach (var r in _playerRenderers)
                    if (r != null) r.enabled = false;

            // 禁用物理
            if (_playerCollider != null) _playerCollider.enabled = false;
            if (_playerRb != null) _playerRb.isKinematic = true;

            // 无敌
            if (_damageablePlayer != null) _damageablePlayer.Invincible = true;

            Debug.Log("[AIBotPilotInputLock] 进入驾驶模式，玩家组件已禁用");
        }

        void OnPilotExited(AIBotPilotExitedEvent evt)
        {
            if (evt.Pilot != gameObject) return;

            _isPiloting = false;

            // 恢复玩家组件
            SetComponentEnabled(_playerController, true);
            SetComponentEnabled(_weaponAiming, true);
            SetComponentEnabled(_weaponShooting, true);
            SetComponentEnabled(_weaponSwitcher, true);
            SetComponentEnabled(_playerCombat, true);
            SetComponentEnabled(_playerInteraction, true);

            // 显示玩家模型
            if (_playerRenderers != null)
                foreach (var r in _playerRenderers)
                    if (r != null) r.enabled = true;

            // 恢复物理
            if (_playerCollider != null) _playerCollider.enabled = true;
            if (_playerRb != null) _playerRb.isKinematic = false;

            // 解除无敌
            if (_damageablePlayer != null) _damageablePlayer.Invincible = false;

            _currentPilot = null;
            Debug.Log("[AIBotPilotInputLock] 退出驾驶模式，玩家组件已恢复");
        }

        // ============================================================
        // 工具
        // ============================================================

        void SetComponentEnabled(MonoBehaviour comp, bool enabled)
        {
            if (comp != null) comp.enabled = enabled;
        }
    }
}
