using UnityEngine;
using _Game.Core;
using _Game.Systems.Building;

namespace _Game.Systems.Interaction
{
    /// <summary>
    /// 玩家交互组件（极简版）
    /// 检测前方可交互物体→靠近显示日志→按 E 执行交互
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        public float detectRadius = GameConstants.INTERACT_DETECT_RADIUS;

        private IInteractable _currentTarget;

        /// <summary>
        /// 建造模式开关。激活时跳过 E 键交互检测。
        /// BuildModeController 通过 EventBus 的 BuildModeEntered/Exited 事件控制此字段。
        /// </summary>
        [System.NonSerialized] public bool isBuildModeActive;

        private void Awake()
        {
            EventBus.Subscribe<BuildModeEnteredEvent>(OnBuildModeEntered);
            EventBus.Subscribe<BuildModeExitedEvent>(OnBuildModeExited);
        }

        void OnEnable() { InputRouter.BindKey(KeyCode.E, InputPriority.Action, HandleInteract, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<BuildModeEnteredEvent>(OnBuildModeEntered);
            EventBus.Unsubscribe<BuildModeExitedEvent>(OnBuildModeExited);
        }

        private void OnBuildModeEntered(BuildModeEnteredEvent evt) => isBuildModeActive = true;
        private void OnBuildModeExited(BuildModeExitedEvent evt) => isBuildModeActive = false;

        void Update()
        {
            if (isBuildModeActive) return;
            DetectTarget();
        }

        bool HandleInteract()
        {
            if (isBuildModeActive || _currentTarget == null) return false;

            Debug.Log($"玩家与 {(_currentTarget as MonoBehaviour)?.name} 交互: {_currentTarget.InteractionPrompt}");
            _currentTarget.OnInteract(gameObject);
            EventBus.Publish(new InteractionEvent(_currentTarget.InteractionPrompt, true));
            return true;
        }

        void DetectTarget()
        {
            _currentTarget = null;

            Collider[] colliders = Physics.OverlapSphere(transform.position, detectRadius, -1);
            float closestDist = float.MaxValue;

            foreach (var col in colliders)
            {
                var interactables = col.GetComponents<IInteractable>();
                if (interactables == null || interactables.Length == 0)
                    continue;

                // 同一GameObject上可能有多个IInteractable（如 PlacedStructure + WorkstationInteract）
                // 优先选择非PlacedStructure的（工作站合成 > 拆除）
                IInteractable best = interactables[0];
                for (int i = 1; i < interactables.Length; i++)
                {
                    if (best is PlacedStructure && !(interactables[i] is PlacedStructure))
                        best = interactables[i];
                }

                if (!best.IsInteractable)
                    continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    _currentTarget = best;
                }
            }

            // 日志：检测到目标
            if (_currentTarget != null)
            {
                Debug.Log($"检测到可交互物体: {(_currentTarget as MonoBehaviour)?.name}");
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRadius);
        }
    }
}
