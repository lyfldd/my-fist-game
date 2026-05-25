using UnityEngine;

namespace _Game.Systems.Interaction
{
    /// <summary>
    /// 可交互物体接口
    /// 任何实现此接口的 MonoBehaviour 都可以被 PlayerInteraction 检测并交互
    /// </summary>
    public interface IInteractable
    {
        /// <summary> 交互提示文本（如"搜索柜子"、"打开门"）</summary>
        string InteractionPrompt { get; }

        /// <summary> 交互耗时（0=瞬间完成，>0=需要按住 E 读条）</summary>
        float InteractionTime { get; }

        /// <summary> 当前是否可交互（用于条件判断）</summary>
        bool IsInteractable { get; }

        /// <summary>
        /// 执行交互
        /// </summary>
        /// <param name="interactor">发起交互的 GameObject（通常是玩家）</param>
        void OnInteract(GameObject interactor);
    }
}
