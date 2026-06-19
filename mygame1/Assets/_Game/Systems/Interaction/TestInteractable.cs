using UnityEngine;
using _Game.Core;

namespace _Game.Systems.Interaction
{
    /// <summary>
    /// 测试用可交互物体
    /// 挂在一个 Cube 上验证交互系统是否工作
    /// </summary>
    public class TestInteractable : MonoBehaviour, IInteractable
    {
        [Header("交互设置")]
        public string prompt = "搜索箱子";
        public float holdTime = 0f;       // 设为 >0 测试进度条
        public bool interactable = true;

        public string InteractionPrompt => prompt;
        public float InteractionTime => holdTime;
        public bool IsInteractable => interactable;

        public void OnInteract(GameObject interactor)
        {
            EventBus.Publish(new InteractionEvent(prompt, true));
        }

        // 在场景中可视化检测范围
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
    }
}
