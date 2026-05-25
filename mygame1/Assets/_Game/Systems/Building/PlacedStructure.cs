using System.Collections;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Interaction;

namespace _Game.Systems.Building
{
    /// <summary>
    /// 已放置的建造物运行时组件
    ///
    /// Phase 1 功能：
    /// - 挂载到 BuildModeController 生成的建造物实例上
    /// - 记录 BuildableData 引用
    /// - 实现 IInteractable：按 E 拆除（带读条）
    /// - 管理血量：收到伤害 → 血量归零 → 摧毁
    /// - 拆除/摧毁时发布 StructureDeconstructedEvent
    ///
    /// 后续扩展（Phase 2+）：
    /// - IDamageable 实现（僵尸可攻击建造物）
    /// - 加固/升级
    /// - 多段破坏外观
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlacedStructure : MonoBehaviour, IInteractable
    {
        [Header("建造数据")]
        public BuildableData buildableData;

        [Header("当前状态")]
        [SerializeField] private float _currentHealth;
        [SerializeField] private bool _isDestroyed;

        // 组件引用
        private Collider _collider;

        // 事件委托（供外部订阅前置处理）
        public System.Action<PlacedStructure> OnDestroyed;

        // ============================================================
        // IInteractable 实现
        // ============================================================

        public virtual string InteractionPrompt => buildableData != null
            ? $"拆除 {buildableData.displayName}"
            : "拆除建造物";

        public virtual float InteractionTime => buildableData != null
            ? buildableData.buildDuration * 0.5f // 拆除比建造快
            : 2f;

        public virtual bool IsInteractable => !_isDestroyed && gameObject.activeInHierarchy;

        public virtual void OnInteract(GameObject interactor)
        {
            if (_isDestroyed) return;

            // 启动拆除协程（如果玩家身上有 Inventory，返还材料）
            var inventory = interactor.GetComponent<Inventory.Inventory>();
            StartCoroutine(DeconstructRoutine(inventory));
        }

        // ============================================================
        // Unity 生命周期
        // ============================================================

        private void Awake()
        {
            _collider = GetComponent<Collider>();

            if (buildableData != null)
                _currentHealth = buildableData.maxHealth;
        }

        private void OnEnable()
        {
            // 确保碰撞体是可触发的（用于 OverlapBox 检测交互）
            if (_collider != null && !_collider.isTrigger)
                _collider.isTrigger = false;
        }

        // ============================================================
        // 血量系统
        // ============================================================

        /// <summary>
        /// 受到伤害。血量归零时触发摧毁。
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_isDestroyed) return;
            if (buildableData == null) return;
            if (buildableData.maxHealth <= 0) return; // 无敌

            _currentHealth -= damage;

            if (_currentHealth <= 0f)
            {
                _currentHealth = 0f;
                DestroyStructure();
            }
        }

        /// <summary>
        /// 获取当前血量百分比（0~1）
        /// </summary>
        public float HealthPercent => buildableData != null && buildableData.maxHealth > 0
            ? Mathf.Clamp01(_currentHealth / buildableData.maxHealth)
            : 1f;

        // ============================================================
        // 拆除逻辑
        // ============================================================

        private IEnumerator DeconstructRoutine(Inventory.Inventory inventory)
        {
            float duration = InteractionTime;
            float elapsed = 0f;

            Debug.Log($"[PlacedStructure] 开始拆除 {buildableData?.displayName ?? name}（{duration:F1}秒）");

            while (elapsed < duration)
            {
                elapsed += UnityEngine.Time.deltaTime;
                yield return null;
            }

            // 返还材料
            if (inventory != null && buildableData != null)
            {
                RefundMaterials(inventory);
            }

            // 发布拆除事件
            EventBus.Publish(new StructureDeconstructedEvent(
                buildableData,
                transform.position,
                gameObject
            ));

            Debug.Log($"[PlacedStructure] 拆除完成: {buildableData?.displayName ?? name}");

            DestroyStructure();
        }

        private void RefundMaterials(Inventory.Inventory inventory)
        {
            if (buildableData == null || buildableData.materials == null)
                return;

            float rate = buildableData.deconstructReturnRate;
            foreach (var req in buildableData.materials)
            {
                int refundCount = Mathf.Max(1, Mathf.RoundToInt(req.count * rate));
                inventory.AddItem(req.itemData, refundCount);
                Debug.Log($"[PlacedStructure] 返还材料: {req.itemData.itemName} x{refundCount} (返还率 {rate:P0})");
            }
        }

        // ============================================================
        // 摧毁
        // ============================================================

        private void DestroyStructure()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            // 如果是被暴力摧毁（非拆除），也发事件
            OnDestroyed?.Invoke(this);

            Debug.Log($"[PlacedStructure] 建造物摧毁: {buildableData?.displayName ?? name}");

            // 延迟销毁，让事件处理完
            Destroy(gameObject, 0.05f);
        }

        // ============================================================
        // 调试
        // ============================================================

        private void OnDrawGizmosSelected()
        {
            if (buildableData == null) return;
            if (_isDestroyed) return;

            // 血量条（世界空间）
            Vector3 barPos = transform.position + Vector3.up * (buildableData.placementSize.y + 0.3f);
            float healthPct = HealthPercent;

            Gizmos.color = Color.gray;
            Gizmos.DrawCube(barPos, new Vector3(1f, 0.1f, 0.1f));
            Gizmos.color = healthPct > 0.5f ? Color.green : healthPct > 0.25f ? Color.yellow : Color.red;
            Gizmos.DrawCube(barPos + Vector3.left * (1f - healthPct) * 0.5f, new Vector3(healthPct, 0.1f, 0.1f));
        }
    }
}
