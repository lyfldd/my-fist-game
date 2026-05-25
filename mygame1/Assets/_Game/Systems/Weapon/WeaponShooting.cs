using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Character;
using _Game.Systems.Combat;
using _Game.Systems.Audio;

namespace _Game.Systems.Weapon
{
    /// <summary>
    /// 射击系统 — 锥形散射 + 射线命中 + 伤害。支持全自动/半自动。
    /// 挂在玩家 GameObject 上，由 WeaponSwitcher 控制 enabled。
    /// </summary>
    public class WeaponShooting : MonoBehaviour
    {
        [Header("射线配置")]
        public LayerMask shootLayer = ~0;
        public float debugRayDuration = GameConstants.DEBUG_RAY_DURATION;

        [Header("射击键")]
        public KeyCode fireKey = KeyCode.Mouse0;

        [Header("自动/半自动分界")]
        [Tooltip("fireRate > 此值的武器为半自动（单发=一次点击）")]
        public float semiAutoFireRateThreshold = 0.25f;

        private WeaponAiming _aiming;
        private WeaponHolder _holder;
        private WeaponSwitcher _switcher;
        private PlayerCharacter _playerCharacter;
        private float _fireTimer;
        private float _currentSpread;
        private bool _lastFrameFiring;

        void Awake()
        {
            _aiming = GetComponent<WeaponAiming>();
            _holder = GetComponent<WeaponHolder>();
            _switcher = GetComponent<WeaponSwitcher>();
            if (_switcher == null)
                _switcher = gameObject.AddComponent<WeaponSwitcher>();
            _playerCharacter = GetComponent<PlayerCharacter>();
        }

        void OnEnable()
        {
            _fireTimer = 0f;
            _lastFrameFiring = false;
            _wantsSemiFire = false;
            var weapon = GetActiveWeapon();
            if (weapon != null) _currentSpread = weapon.baseSpread;
            InputRouter.BindMouse(0, InputPriority.Gameplay, OnFireButton, this);
        }

        void OnDisable()
        {
            InputRouter.UnbindAll(this);
            _wantsSemiFire = false;
        }

        bool _wantsSemiFire;

        bool OnFireButton()
        {
            _wantsSemiFire = true;
            return true;
        }

        void Update()
        {
            var weapon = GetActiveWeapon();
            if (weapon == null || !weapon.isFirearm) return;

            if (_fireTimer > 0f)
                _fireTimer -= UnityEngine.Time.deltaTime;

            bool wantFire = weapon.fireRate <= semiAutoFireRateThreshold
                ? Input.GetKey(fireKey)
                : _wantsSemiFire;

            _wantsSemiFire = false;

            if (!wantFire && _currentSpread > weapon.baseSpread)
            {
                _currentSpread = Mathf.MoveTowards(
                    _currentSpread,
                    weapon.baseSpread,
                    weapon.spreadRecovery * UnityEngine.Time.deltaTime
                );
            }

            if (wantFire)
                TryFire(weapon);
        }

        ItemData GetActiveWeapon()
        {
            if (_switcher == null) return null;
            return _switcher.ActiveWeapon;
        }

        /// <summary> 获取枪口位置（世界坐标）</summary>
        Vector3 GetMuzzlePosition()
        {
            if (_holder != null)
                return _holder.HandWorldPos;
            return transform.position + transform.forward * GameConstants.MUZZLE_FORWARD_OFFSET + Vector3.up * GameConstants.MUZZLE_UP_OFFSET;
        }

        void TryFire(ItemData weapon)
        {
            if (_fireTimer > 0f) return;

            // 扣动扳机：散射扩大
            _currentSpread = Mathf.Min(_currentSpread + weapon.spreadPerShot, weapon.maxSpread);
            _fireTimer = weapon.fireRate;

            Vector3 aimDir = _aiming != null ? _aiming.AimDirection : transform.forward;

            // 散射：枪械专精每级减少 GUN_SKILL_SPREAD_REDUCTION
            int gunLevel = _playerCharacter != null ? _playerCharacter.GetSkillLevel(SkillType.枪械专精) : 0;
            float effectiveSpread = _currentSpread * (1f - gunLevel * GameConstants.GUN_SKILL_SPREAD_REDUCTION);
            Vector3 spreadDir = GetSpreadDirection(aimDir, effectiveSpread * 0.5f);

            // 射线检测
            Vector3 muzzle = GetMuzzlePosition();
            bool hitSomething = Physics.Raycast(muzzle, spreadDir, out var hit, weapon.weaponRange, shootLayer);

            if (hitSomething)
            {
                // 跳过AI机器人（友军）
                if (hit.collider.GetComponentInParent<_Game.Systems.AIBot.AIBot>() == null)
                {
                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(weapon.weaponDamage);
                        Debug.Log($"命中 {hit.collider.name}！伤害={weapon.weaponDamage}");
                    }
                }

                // 调试射线
                Debug.DrawRay(muzzle, spreadDir * hit.distance, Color.red, debugRayDuration);
            }
            else
            {
                Debug.DrawRay(muzzle, spreadDir * weapon.weaponRange, Color.yellow, debugRayDuration);
            }

            // 发布事件（供音效/特效系统订阅）
            EventBus.Publish(new WeaponFiredEvent(
                EquipSlot.RightHand,
                spreadDir,
                hitSomething,
                hitSomething ? hit.collider.gameObject : null
            ));

            // 声音
            SoundEmitter.EmitGunshot(muzzle);
        }

        /// <summary>
        /// 在瞄准方向的锥形范围内生成随机散射方向
        /// 使用 Rodrigues 旋转公式
        /// </summary>
        /// <param name="aimDir">瞄准方向（归一化）</param>
        /// <param name="spreadHalfAngle">锥形半角（度）</param>
        Vector3 GetSpreadDirection(Vector3 aimDir, float spreadHalfAngle)
        {
            if (spreadHalfAngle <= 0.001f) return aimDir;

            // 构建垂直平面坐标系
            Vector3 perpAxis = Vector3.Cross(Vector3.up, aimDir).normalized;
            if (perpAxis.sqrMagnitude < 0.001f)
                perpAxis = Vector3.Cross(Vector3.right, aimDir).normalized; // 瞄准正上方/正下方时
            Vector3 upAxis = Vector3.Cross(aimDir, perpAxis).normalized;

            // 在锥形截面内随机采样（极坐标 → 笛卡尔）
            float angle = Random.Range(0f, spreadHalfAngle * Mathf.Deg2Rad);
            float rotation = Random.Range(0f, 360f * Mathf.Deg2Rad);

            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);
            Vector3 offset = (perpAxis * Mathf.Cos(rotation) + upAxis * Mathf.Sin(rotation)) * sinA;

            return (aimDir * cosA + offset).normalized;
        }

        /// <summary> 当前散射半角（度）— 供 UI/HUD 读取 </summary>
        public float CurrentSpreadAngle => _currentSpread;
    }
}
