using UnityEngine;
using UnityEngine.UI;
using _Game.Core;

namespace _Game.UI
{
    /// <summary>
    /// 交互提示 UI（靠近物体时显示"按 E 搜索"）
    /// 挂在 Canvas 上，通过 WorldToScreenPoint 跟踪物体位置
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("绑定")]
        public Text promptText;
        public Image progressBar;       // 进度条前景
        public GameObject progressRoot; // 进度条容器
        public CanvasGroup canvasGroup;

        [Header("外观")]
        public float fadeSpeed = 5f;

        private Camera _mainCamera;
        private Vector3 _targetWorldPos;
        private bool _hasTarget = false;

        void Start()
        {
            if (!UIModeConfig.UseUGUI) { enabled = false; return; }
            _mainCamera = Camera.main;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (progressRoot != null)
                progressRoot.SetActive(false);
        }

        void Update()
        {
            if (!_hasTarget || _mainCamera == null)
            {
                // 淡出
                if (canvasGroup != null && canvasGroup.alpha > 0f)
                    canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, fadeSpeed * Time.deltaTime);
                return;
            }

            // 跟踪物体位置
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(_targetWorldPos);
            transform.position = screenPos;

            // 淡入
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, fadeSpeed * Time.deltaTime);
        }

        /// <summary> 显示交互提示 </summary>
        public void ShowPrompt(string text, Vector3 worldPos)
        {
            _targetWorldPos = worldPos;
            _hasTarget = true;

            if (promptText != null)
                promptText.text = text;
        }

        public void HidePrompt()
        {
            _hasTarget = false;
        }

        /// <summary> 显示"按住 E"提示 </summary>
        public void ShowProgressHint()
        {
            if (progressRoot != null)
                progressRoot.SetActive(true);
        }

        /// <summary> 更新进度条 </summary>
        public void UpdateProgress(float value)
        {
            if (progressBar != null)
                progressBar.fillAmount = value;
        }

        public void HideProgress()
        {
            if (progressRoot != null)
                progressRoot.SetActive(false);
            if (progressBar != null)
                progressBar.fillAmount = 0f;
        }
    }
}
