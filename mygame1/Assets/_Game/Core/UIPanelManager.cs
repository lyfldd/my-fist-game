using System.Collections.Generic;
using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// UI 面板管理器 — 全局单例（DontDestroyOnLoad）。
    /// 栈管理所有打开的面板，ESC 逐层关闭。
    /// </summary>
    public class UIPanelManager : MonoBehaviour
    {
        public static UIPanelManager Instance { get; private set; }

        readonly Stack<UIPanel> _stack = new();
        public int Count => _stack.Count;
        public event System.Action<UIPanel> OnPanelOpened;
        public event System.Action<UIPanel> OnPanelClosed;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable() { InputRouter.BindKey(KeyCode.Escape, InputPriority.UI, HandleEsc, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }

        bool HandleEsc()
        {
            if (_stack.Count == 0) return false;
            CloseTopPanel();
            return true;
        }

        /// <summary> 打开面板。子面板需 parentPanel 非空且已在栈中。 </summary>
        public void OpenPanel(UIPanel panel)
        {
            if (panel == null) return;
            // 子面板校验
            if (panel.parentPanel != null && !IsPanelOpen(panel.parentPanel))
            {
                Debug.LogWarning($"[UIPanelManager] 子面板 {panel.panelId} 的父面板未打开，拒绝");
                return;
            }
            if (IsPanelOpen(panel)) return;

            _stack.Push(panel);
            panel.OnOpen();
            OnPanelOpened?.Invoke(panel);
        }

        public void ClosePanel(UIPanel panel)
        {
            if (panel == null || !IsPanelOpen(panel)) return;
            // 先关所有子面板
            var toClose = new List<UIPanel>();
            foreach (var p in _stack)
                if (p.parentPanel == panel) toClose.Add(p);
            foreach (var p in toClose) ClosePanel(p);

            // 从栈中移除
            var temp = new Stack<UIPanel>();
            while (_stack.Count > 0)
            {
                var p = _stack.Pop();
                if (p != panel) temp.Push(p);
            }
            while (temp.Count > 0) _stack.Push(temp.Pop());

            panel.OnClose();
            OnPanelClosed?.Invoke(panel);
        }

        public void CloseTopPanel()
        {
            if (_stack.Count == 0) return;
            var top = _stack.Peek();
            ClosePanel(top);
        }

        public void CloseAll()
        {
            while (_stack.Count > 0) CloseTopPanel();
        }

        public bool IsPanelOpen(UIPanel panel)
        {
            foreach (var p in _stack) if (p == panel) return true;
            return false;
        }

        public UIPanel TopPanel => _stack.Count > 0 ? _stack.Peek() : null;
    }

    /// <summary> 面板基类 — 所有可打开的面板继承此组件 </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        public string panelId;
        public UIPanel parentPanel;
        public bool isDraggable = true;
        public GameObject titleBar;
        public GameObject closeButton;

        public virtual void OnOpen() => gameObject.SetActive(true);
        public virtual void OnClose() => gameObject.SetActive(false);

        public void Open() => UIPanelManager.Instance?.OpenPanel(this);
        public void Close() => UIPanelManager.Instance?.ClosePanel(this);
    }
}
