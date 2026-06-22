using UnityEngine;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 持久化ID组件。
    /// 贴在需要跨会话追踪的 GameObject 上（PlacedStructure/AIBot/Vehicle/Zombie/...）。
    ///
    /// 两阶段初始化：
    ///   Awake() 生成 GUID 但不注册
    ///   Start() 自动注册（给加载系统覆盖 GUID 的窗口）
    ///   Initialize() 加载时显式调用，覆盖 GUID 并注册
    ///
    /// 竞态安全：Initialize() 先 Unregister 旧 GUID 再 Register 新 GUID，
    /// 无论 Awake/Start/Initialize 的执行顺序如何，Registry 中都是正确的 GUID。
    /// </summary>
    public class PersistentGUID : MonoBehaviour
    {
        [SerializeField] private string guid;
        [SerializeField] private string entityType;

        private bool _initialized;

        public string Guid => guid;
        public string EntityType => entityType;

        void Awake()
        {
            if (string.IsNullOrEmpty(guid))
                guid = System.Guid.NewGuid().ToString("N");
        }

        void Start()
        {
            // 延迟注册，给加载系统覆盖 GUID 的窗口
            if (!_initialized && !string.IsNullOrEmpty(guid))
            {
                PersistentGUIDRegistry.Register(guid, gameObject);
                _initialized = true;
            }
        }

        void OnDestroy()
        {
            if (!string.IsNullOrEmpty(guid))
                PersistentGUIDRegistry.Unregister(guid);
        }

        /// <summary>
        /// 加载存档时调用，覆盖 GUID 并注册。
        /// 先 Unregister 旧 GUID（Awake/Start 可能已自动注册），再 Register 新 GUID。
        /// </summary>
        public void Initialize(string overrideGuid, string type)
        {
            // 竞态安全：先注销可能已自动注册的旧 GUID
            if (!string.IsNullOrEmpty(guid))
                PersistentGUIDRegistry.Unregister(guid);

            guid = overrideGuid;
            entityType = type;
            PersistentGUIDRegistry.Register(guid, gameObject);
            _initialized = true;
        }

        /// <summary>
        /// 编辑器/调试用：直接设置 GUID（跳过注册，供加载流程控制）。
        /// </summary>
        public void SetGuidRaw(string newGuid)
        {
            guid = newGuid;
        }
    }
}
