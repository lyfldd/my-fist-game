using UnityEngine;

namespace _Game.Systems.Weapon
{
    /// <summary>
    /// 射击散布可视化 — 用 LineRenderer 在角色前方 3D 空间画出锥形散布窗口
    /// 锥形从 Y=1.3 发出，水平展开。中线红色，边界绿→黄→红。
    /// </summary>
    public class SpreadVisualizer : MonoBehaviour
    {
        [Header("可视化参数")]
        public float maxVisualRange = 30f;
        public int arcSegments = 30;
        public float lineWidth = 0.06f;
        public float coneOriginHeight = 1.3f;

        [Header("颜色阈值（度）")]
        public float preciseThreshold = 3f;
        public float warningThreshold = 8f;

        [Header("显示")]
        public bool alwaysShow = false;

        private WeaponShooting _shooting;
        private WeaponAiming _aiming;
        private _Game.Systems.PlayerInput.MouseGroundProjector _projector;
        private LineRenderer _lrCone;      // 锥形边界 + 弧
        private LineRenderer _lrCenter;    // 红色中线
        private Material _lrMat;

        void Awake()
        {
            _shooting = GetComponent<WeaponShooting>();
            _aiming = GetComponent<WeaponAiming>();
            _projector = GetComponent<_Game.Systems.PlayerInput.MouseGroundProjector>();

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            _lrMat = new Material(shader);
            _lrMat.color = Color.white;

            _lrCone = CreateLineRenderer("ConeBoundary", lineWidth);
            _lrCenter = CreateLineRenderer("ConeCenter", lineWidth * 0.5f);
        }

        LineRenderer CreateLineRenderer(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.allowOcclusionWhenDynamic = false;
            lr.material = _lrMat;
            return lr;
        }

        void LateUpdate()
        {
            bool isAiming = _aiming != null && _aiming.IsAimingDownSights;
            float spread = (_shooting != null && _shooting.enabled) ? _shooting.CurrentSpreadAngle : 0f;

            if (!isAiming || spread < 0.01f)
            {
                _lrCone.positionCount = 0;
                _lrCenter.positionCount = 0;
                return;
            }

            DrawCone(spread);
        }

        void DrawCone(float spreadAngle)
        {
            // 起点：角色胸部高度
            Vector3 origin = transform.position + Vector3.up * coneOriginHeight;

            // 真实 3D 瞄准方向（含俯角），不是展平的
            Vector3 aimDir3D = _aiming != null ? _aiming.AimDirection : transform.forward;
            if (aimDir3D.sqrMagnitude < 0.001f)
                aimDir3D = transform.forward;

            // 远端中心点（沿 3D 瞄准方向）
            Vector3 aimTarget = (_projector != null && _projector.HasValidTarget)
                ? _projector.GroundPoint
                : origin + aimDir3D * maxVisualRange;
            Vector3 toTarget = aimTarget - origin;
            float coneRange = Mathf.Clamp(toTarget.magnitude, 1f, maxVisualRange);
            Vector3 coneAxis = toTarget.normalized;
            Vector3 farCenter = origin + coneAxis * coneRange;

            // 构建垂直于轴线的正交基（用于画锥体截面圆）
            float halfRad = spreadAngle * 0.5f * Mathf.Deg2Rad;
            float farRadius = coneRange * Mathf.Tan(halfRad);

            Vector3 upRef = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(coneAxis, upRef)) > 0.99f)
                upRef = Vector3.forward;
            Vector3 perpRight = Vector3.Cross(upRef, coneAxis).normalized;
            Vector3 perpUp = Vector3.Cross(coneAxis, perpRight).normalized;

            // ── 锥体：4条母线 + 远端截面圆 ──
            int circlePts = arcSegments;
            int totalPts = 4 * 2 + circlePts;  // 4条边线(各2点) + 截面圆
            _lrCone.positionCount = totalPts;

            int idx = 0;

            // 4 条母线（右/上/左/下），每条从 origin 到截面圆周上的点
            Vector3[] dirs = { perpRight, perpUp, -perpRight, -perpUp };
            foreach (var d in dirs)
            {
                _lrCone.SetPosition(idx++, origin);
                _lrCone.SetPosition(idx++, farCenter + d * farRadius);
            }

            // 远端截面圆
            for (int i = 0; i < circlePts; i++)
            {
                float ang = (float)i / circlePts * Mathf.PI * 2f;
                Vector3 pt = farCenter + (perpRight * Mathf.Cos(ang) + perpUp * Mathf.Sin(ang)) * farRadius;
                _lrCone.SetPosition(idx++, pt);
            }

            Color coneColor = GetSpreadColor(spreadAngle);
            _lrCone.startColor = coneColor;
            _lrCone.endColor = coneColor;

            // ── 红色轴线：从胸部到瞄准点 ——
            _lrCenter.positionCount = 2;
            _lrCenter.SetPosition(0, origin);
            _lrCenter.SetPosition(1, farCenter);
            _lrCenter.startColor = new Color(0.9f, 0.05f, 0.05f, 0.7f);
            _lrCenter.endColor = new Color(0.9f, 0.05f, 0.05f, 0.95f);
        }

        Color GetSpreadColor(float spread)
        {
            if (spread <= preciseThreshold)
                return new Color(0f, 0.9f, 0f, 0.4f);
            if (spread <= warningThreshold)
                return new Color(1f, 0.85f, 0f, 0.5f);
            return new Color(1f, 0.1f, 0.1f, 0.6f);
        }

        void OnDestroy()
        {
            if (_lrMat != null) Destroy(_lrMat);
        }
    }
}
