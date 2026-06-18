using UnityEngine;
using _Game.Core;
using _Game.Systems.Combat;
using _Game.Systems.PlayerInput;

namespace _Game.Systems.Weapon
{
    /// <summary>
    /// 3D 瞄准系统 — 鼠标→射线→世界坐标 + 辅助瞄准
    /// 挂在玩家 GameObject 上
    /// </summary>
    public class WeaponAiming : MonoBehaviour
    {
        [Header("瞄准配置")]
        public float assistRadius = GameConstants.AIM_ASSIST_RADIUS;  // 辅助瞄准吸附范围(米)
        public LayerMask zombieLayer;             // 僵尸层（Inspector 设置）
        public LayerMask aimBlockLayer;           // 射线阻挡层（Default|Environment，用于僵尸检测时排除射线问题）

        [Header("地面平面")]
        public float groundY = GameConstants.GROUND_PLANE_Y;   // 地面高度（Y 坐标）
        public Plane GroundPlane => new Plane(Vector3.up, new Vector3(0, groundY, 0));

        [Header("调试")]
        [SerializeField] bool _showDebugRays = true;

        // 输出（供 WeaponHolder / WeaponShooting 读取）
        /// <summary> 当前瞄准目标点（世界坐标）</summary>
        public Vector3 AimTarget { get; private set; }
        /// <summary> 枪口 → 目标的归一化方向</summary>
        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        /// <summary> 是否正在右键瞄准（供移动减速/散布显示等系统读取）</summary>
        public bool IsAimingDownSights { get; private set; }
        /// <summary> 是否有辅助瞄准激活</summary>
        public bool IsAssisting { get; private set; }
        /// <summary> 辅助瞄准的目标 Transform（null=无）</summary>
        public Transform AssistTarget { get; private set; }

        private Camera _cam;
        private WeaponHolder _holder;
        private MouseGroundProjector _projector;

        void Awake()
        {
            _cam = Camera.main;
            _holder = GetComponent<WeaponHolder>();
            _projector = GetComponent<MouseGroundProjector>();
            if (zombieLayer == 0) zombieLayer = LayerMask.GetMask("Default"); // 默认检测一切
        }

        void Update()
        {
            if (_cam == null) return;

            // 右键瞄准切换：点一次进入，再点一次退出
            // 只有当前装备热武器时才能进入瞄准
            var switcher = GetComponent<WeaponSwitcher>();
            bool hasGun = switcher != null
                       && switcher.ActiveWeapon != null
                       && switcher.ActiveWeapon.isFirearm;
            if (Input.GetMouseButtonDown(1) && hasGun)
                IsAimingDownSights = !IsAimingDownSights;
            // 切走枪时强制退出瞄准
            if (!hasGun)
                IsAimingDownSights = false;

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            AimTarget = Vector3.zero;
            IsAssisting = false;
            AssistTarget = null;

            // 1. 优先检测：射线是否命中僵尸（辅助瞄准）
            if (Physics.Raycast(ray, out var zombieHit, GameConstants.AIM_MAX_RAY_DISTANCE, zombieLayer))
            {
                var zombie = zombieHit.collider.GetComponentInParent<IDamageable>();
                if (zombie != null)
                {
                    float distToPlayer = Vector3.Distance(
                        transform.position,
                        zombieHit.collider.transform.position
                    );

                    if (distToPlayer <= assistRadius)
                    {
                        // 辅助瞄准：锁定僵尸胸部位置
                        AimTarget = zombieHit.collider.transform.position + Vector3.up * GameConstants.PLAYER_CHEST_HEIGHT;
                        IsAssisting = true;
                        AssistTarget = zombieHit.collider.transform;
                    }
                }
            }

            // 2. 无辅助瞄准 → 读取 MouseGroundProjector 的地面投影
            if (!IsAssisting)
            {
                if (_projector != null && _projector.HasValidTarget)
                    AimTarget = _projector.GroundPoint;
                else
                    AimTarget = transform.position + transform.forward * GameConstants.AIM_FALLBACK_DISTANCE; // 兜底
            }

            // 3. 计算瞄准方向（从右手挂载点到目标点）
            Vector3 muzzlePos = _holder != null
                ? _holder.HandWorldPos
                : transform.position + Vector3.up * GameConstants.PLAYER_HAND_HEIGHT;   // 兜底：角色右手高度

            Vector3 rawDir = (AimTarget - muzzlePos).normalized;
            if (rawDir.sqrMagnitude > 0.001f)
                AimDirection = rawDir;

            // 同步到 WeaponHolder
            if (_holder != null)
                _holder.AimDirection = AimDirection;
        }

        void OnDrawGizmos()
        {
            if (!_showDebugRays || !Application.isPlaying) return;

            var muzzlePos = transform.position + Vector3.up * GameConstants.PLAYER_GIZMO_TORSO_Y;

            // 瞄准方向
            Gizmos.color = IsAssisting ? Color.red : Color.green;
            Gizmos.DrawRay(muzzlePos, AimDirection * GameConstants.GIZMO_AIM_RAY_LENGTH);
            Gizmos.DrawWireSphere(AimTarget, GameConstants.GIZMO_AIM_SPHERE_RADIUS);

            // 辅助瞄准范围
            if (IsAssisting)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f);
                Gizmos.DrawWireSphere(transform.position, assistRadius);
            }
        }
    }
}
