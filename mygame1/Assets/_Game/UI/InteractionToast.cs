using UnityEngine;
using UnityEngine.UI;
using _Game.Core;

namespace _Game.UI
{
    /// <summary>
    /// 交互反馈 Toast（复用 FloatingToastText）
    /// 监听 InteractionEvent → 显示 Toast 提示
    /// 后续可以替换为更精美的 UI
    /// </summary>
    public class InteractionToast : MonoBehaviour
    {
        public Text toastText;

        void Start()
        {
            if (toastText != null)
                toastText.gameObject.SetActive(false);

            EventBus.Subscribe<InteractionEvent>(OnInteraction);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<InteractionEvent>(OnInteraction);
        }

        void OnInteraction(InteractionEvent evt)
        {
            if (toastText == null) return;

            toastText.gameObject.SetActive(true);
            toastText.text = $"{evt.ObjectName}";
            CancelInvoke(nameof(Hide));
            Invoke(nameof(Hide), 2f);
        }

        void Hide()
        {
            if (toastText != null)
                toastText.gameObject.SetActive(false);
        }
    }
}
