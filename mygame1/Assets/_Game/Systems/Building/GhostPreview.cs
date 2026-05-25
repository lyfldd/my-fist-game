using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.PlayerInput;

namespace _Game.Systems.Building
{
    /// <summary>
    /// 建造预览虚影系统
    /// 
    /// Phase 1 功能：
    /// - 进入建造模式时生成本透明预览体
    /// - 每帧跟随鼠标/准星的 3D 射线落点（地面平面）
    /// - 网格吸附（按 BuildableData.snapSize）
    /// - 重叠检测（Physics.OverlapBox），绿色(可放)/红色(不可放)
    /// - 退出建造模式时销毁预览体
    ///
    /// 订阅：BuildModeEnteredEvent, BuildModeExitedEvent
    /// 依赖：Camera.main, 地面 Plane(Y=0)
    /// </summary>
    public class GhostPreview : MonoBehaviour
    {
        [Header("地面平面")]
        public float groundY = GameConstants.GROUND_PLANE_Y;

        [Header("放置检测")]
        [Tooltip("检测哪些层的碰撞体阻挡放置（如 Default, Environment）")]
        public LayerMask blockLayers = 1; // Default layer
        [Tooltip("忽略哪些层的碰撞体（如 Player 自身）")]
        public LayerMask ignoreLayers;

        [Header("颜色反馈")]
        [ColorUsage(false)]
        public Color validColor = new Color(0f, 1f, 0f, 0.3f);
        [ColorUsage(false)]
        public Color invalidColor = new Color(1f, 0f, 0f, 0.3f);

        [Header("调试")]
        [SerializeField] bool _showDebugBox = true;

        // 运行时状态
        private GameObject _previewInstance;
        private BuildableData _currentBuildable;
        private Material _previewMaterial;
        private bool _isValidPlacement;
        private Camera _cam;
        private MouseGroundProjector _projector;

        // 属性
        /// <summary> 当前预览的世界坐标（已吸附网格）</summary>
        public Vector3 PreviewPosition { get; private set; }
        /// <summary> 当前位置是否可以放置</summary>
        public bool IsValidPlacement => _isValidPlacement;
        /// <summary> 当前建造数据</summary>
        public BuildableData CurrentBuildable => _currentBuildable;

        private void Awake()
        {
            _cam = Camera.main;
            _projector = GetComponent<MouseGroundProjector>();
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
            DestroyPreview();
        }

        private void OnDestroy()
        {
            DestroyPreview();
        }

        // ============================================================
        // 事件回调
        // ============================================================

        private void OnBuildModeEntered(BuildModeEnteredEvent evt)
        {
            _currentBuildable = evt.Buildable;
            SpawnPreview();
        }

        private void OnBuildModeExited(BuildModeExitedEvent evt)
        {
            DestroyPreview();
            _currentBuildable = null;
        }

        // ============================================================
        // 预览体生命周期
        // ============================================================

        private void SpawnPreview()
        {
            DestroyPreview(); // 安全兜底

            if (_currentBuildable == null)
            {
                Debug.LogWarning("[GhostPreview] BuildModeEntered 但 BuildableData 为 null");
                return;
            }

            if (_currentBuildable.previewPrefab != null)
            {
                _previewInstance = Instantiate(_currentBuildable.previewPrefab);
            }
            else
            {
                // 兜底：没有预览模型时生成一个半透明方块
                _previewInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                // 移除自带碰撞体（预览体不需要碰撞，用 OverlapBox 检测）
                Destroy(_previewInstance.GetComponent<Collider>());
            }

            _previewInstance.name = $"[Ghost] {_currentBuildable.displayName}";
            _previewInstance.transform.localScale = _currentBuildable.placementSize;

            // 预览材质：遍历所有 Renderer，设为半透明
            ApplyGhostMaterial(_previewInstance);

            // 初始位置
            _previewInstance.transform.position = transform.position + transform.forward * 3f;
        }

        private void ApplyGhostMaterial(GameObject obj)
        {
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                // 保留原材质引用以便恢复，这里简化：克隆首个材质并设半透明
                var mats = renderer.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = new Material(mats[i]);
                    SetMaterialTransparent(mat);
                    mats[i] = mat;
                }
                renderer.materials = mats;

                // 缓存第一个材质用于颜色切换
                if (_previewMaterial == null && mats.Length > 0)
                    _previewMaterial = mats[0];
            }
        }

        private void SetMaterialTransparent(Material mat)
        {
            // Built-in RP: 设置 Standard Shader 的透明模式
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        private void DestroyPreview()
        {
            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
                _previewInstance = null;
            }
            _previewMaterial = null;
        }

        // ============================================================
        // 每帧更新
        // ============================================================

        private void Update()
        {
            if (_previewInstance == null || _currentBuildable == null)
                return;

            // 1. 读取 MouseGroundProjector 的地面投影
            Vector3 hitPoint = (_projector != null && _projector.HasValidTarget)
                ? _projector.GroundPoint : Vector3.zero;
            if (hitPoint == Vector3.zero && _previewInstance != null)
            {
                // 射线没打到地面，隐藏预览
                _previewInstance.SetActive(false);
                return;
            }

            _previewInstance.SetActive(true);

            // 2. 网格吸附
            PreviewPosition = SnapToGrid(hitPoint, _currentBuildable.snapSize);

            // 3. 更新预览位置
            _previewInstance.transform.position = PreviewPosition;

            // 4. 重叠检测
            _isValidPlacement = CheckPlacement(PreviewPosition, _currentBuildable.placementSize);

            // 5. 颜色反馈
            UpdateGhostColor(_isValidPlacement);
        }

        // ============================================================
        // 吸附
        // ============================================================

        /// <summary>
        /// 网格吸附：将世界坐标对齐到 snap 步长网格。
        /// snapSize=0 表示自由放置，不吸附。
        /// </summary>
        public static Vector3 SnapToGrid(Vector3 worldPos, float snapSize)
        {
            if (snapSize <= 0f)
                return worldPos;

            return new Vector3(
                Mathf.Round(worldPos.x / snapSize) * snapSize,
                worldPos.y, // Y 不吸附（由地面决定）
                Mathf.Round(worldPos.z / snapSize) * snapSize
            );
        }

        // ============================================================
        // 放置有效性检测
        // ============================================================

        private bool CheckPlacement(Vector3 position, Vector3 size)
        {
            // OverlapBox 检测阻挡物。Y 轴缩小 0.1 并微抬，避免检测到地面本身
            Vector3 halfExtents = size * 0.5f;
            halfExtents.y = Mathf.Max(0.05f, halfExtents.y - 0.1f);
            Vector3 boxCenter = position + Vector3.up * (halfExtents.y + 0.05f);

            int mask = blockLayers & ~ignoreLayers;

            Collider[] overlaps = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, mask);
            return overlaps.Length == 0;
        }

        private void UpdateGhostColor(bool valid)
        {
            if (_previewMaterial == null) return;

            Color target = valid ? validColor : invalidColor;

            foreach (var renderer in _previewInstance.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    mat.color = target;
                }
            }
        }

        /// <summary> 隐藏虚影但不退出建造模式（取消选中物品时调用）</summary>
        public void HidePreview()
        {
            DestroyPreview();
            _currentBuildable = null;
        }

        // ============================================================
        // 调试可视化
        // ============================================================

        private void OnDrawGizmos()
        {
            if (!_showDebugBox || _currentBuildable == null)
                return;

            if (_previewInstance != null)
            {
                Vector3 boxCenter = PreviewPosition + Vector3.up * (_currentBuildable.placementSize.y * 0.5f);
                Gizmos.color = _isValidPlacement
                    ? new Color(0, 1, 0, 0.4f)
                    : new Color(1, 0, 0, 0.4f);

                Gizmos.matrix = Matrix4x4.TRS(boxCenter, Quaternion.identity, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, _currentBuildable.placementSize);
                Gizmos.matrix = Matrix4x4.identity;

                // 吸附点
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(PreviewPosition, 0.15f);
            }

            // 供电半径预览（终端/电源放置时显示）
            if (_currentBuildable.powerSupplyRadius > 0f)
            {
                Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
                DrawGizmoCircle(PreviewPosition, _currentBuildable.powerSupplyRadius, 48);
            }
        }

        static void DrawGizmoCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments * Mathf.Deg2Rad;
            Vector3 prev = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * angleStep;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
