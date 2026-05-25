using UnityEngine;
using _Game.Core;
using _Game.Config;
using _Game.Systems.Interaction;

namespace _Game.Systems.Vehicle
{
    /// <summary>
    /// 车辆交互组件 — IInteractable 实现
    ///
    /// 职责：
    /// - 实现 IInteractable（上车交互）
    /// - 管理驾驶员进出逻辑
    /// - 发布 VehicleEnteredEvent / VehicleExitedEvent
    ///
    /// 挂载：车辆根 GameObject（和 VehicleController 同物体）
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleInteraction : MonoBehaviour, IInteractable
    {
        private VehicleController _controller;
        private Collider _driverOriginalCollider;
        private Rigidbody _driverOriginalRb;

        // ============================================================
        // IInteractable 实现
        // ============================================================

        public string InteractionPrompt => _controller.HasDriver ? "下车 [E]" : "上车 [E]";
        public float InteractionTime => 0f;  // 瞬间完成
        public bool IsInteractable { get { return !_controller.HasDriver; } }

        public void OnInteract(GameObject interactor)
        {
            if (!_controller.HasDriver)
            {
                Enter(interactor);
            }
        }

        // ============================================================
        // 进出逻辑
        // ============================================================

        void Awake()
        {
            _controller = GetComponent<VehicleController>();
        }

        /// <summary>
        /// 玩家上车
        /// </summary>
        public void Enter(GameObject driver)
        {
            if (_controller.HasDriver)
            {
                Debug.LogWarning($"[VehicleInteraction] {gameObject.name} 已经有驾驶员了");
                return;
            }

            // 保存并禁用驾驶员的物理组件
            _driverOriginalRb = driver.GetComponent<Rigidbody>();
            if (_driverOriginalRb != null)
            {
                _driverOriginalRb.isKinematic = true;
                _driverOriginalRb.velocity = Vector3.zero;
            }
            _driverOriginalCollider = driver.GetComponent<Collider>();
            if (_driverOriginalCollider != null)
            {
                _driverOriginalCollider.enabled = false;
            }

            // 设置驾驶员到座位上
            Vector3 seatPos = transform.position + transform.rotation * GetDriverSeatOffset();
            driver.transform.position = seatPos;
            driver.transform.SetParent(transform);

            // 通知控制器
            _controller.SetDriver(driver);

            // 发布事件
            EventBus.Publish(new VehicleEnteredEvent(gameObject, driver));

            Debug.Log($"[VehicleInteraction] {driver.name} 上了 {gameObject.name}");
        }

        /// <summary>
        /// 玩家下车（由 VehicleInputLock 的 E 键触发）
        /// </summary>
        public void Exit()
        {
            if (!_controller.HasDriver) return;

            GameObject driver = _controller.Driver;

            // 恢复驾驶员物理
            if (_driverOriginalRb != null)
            {
                _driverOriginalRb.isKinematic = false;
            }
            if (_driverOriginalCollider != null)
            {
                _driverOriginalCollider.enabled = true;
            }

            // 从车辆解除父子关系
            driver.transform.SetParent(null);

            // 放置到车辆旁边的下车位置
            Vector3 exitPos = transform.position + transform.rotation * GetExitOffset();
            exitPos.y = driver.transform.position.y;  // 保持地面高度
            driver.transform.position = exitPos;

            // 通知控制器
            _controller.SetDriver(null);

            // 发布事件
            EventBus.Publish(new VehicleExitedEvent(gameObject, driver));

            _driverOriginalRb = null;
            _driverOriginalCollider = null;

            Debug.Log($"[VehicleInteraction] {driver.name} 下了 {gameObject.name}");
        }

        // ============================================================
        // 辅助
        // ============================================================

        private Vector3 GetDriverSeatOffset()
        {
            // 非运行模式下 _controller 可能未初始化（OnDrawGizmosSelected 中调用）
            var vc = _controller != null ? _controller : GetComponent<VehicleController>();
            if (vc != null && vc.vehicleData != null)
                return vc.vehicleData.driverSeatOffset;
            return new Vector3(-0.5f, 0.5f, 0f);
        }

        private Vector3 GetExitOffset()
        {
            var vc = _controller != null ? _controller : GetComponent<VehicleController>();
            if (vc != null && vc.vehicleData != null)
                return vc.vehicleData.exitOffset;
            return new Vector3(-2f, 0f, 0f);
        }

        void OnDrawGizmosSelected()
        {
            // 可视化座位和下车点（编辑器中可用，不依赖 Awake 初始化）
            Gizmos.color = Color.green;
            Vector3 seat = transform.position + transform.rotation * GetDriverSeatOffset();
            Gizmos.DrawWireSphere(seat, 0.3f);

            Gizmos.color = Color.yellow;
            Vector3 exit = transform.position + transform.rotation * GetExitOffset();
            Gizmos.DrawWireSphere(exit, 0.3f);
            Gizmos.DrawLine(seat, exit);
        }
    }
}
