using System.Collections.Generic;
using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// UI 面板管理器 — 全局单例。按 id 管理面板栈，ESC 逐层关闭。
    /// 所有面板通过 Open(id, onClose) / Close(id) 操作，不自己创建代理。
    /// </summary>
    public class UIPanelManager : MonoBehaviour
    {
        public static UIPanelManager Instance { get; private set; }

        class PanelEntry
        {
            public string id;
            public string parentId;
            public System.Action onClose;
        }

        readonly List<PanelEntry> _stack = new();
        public int Count => _stack.Count;

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
            CloseTop();
            return true;
        }

        /// <summary> 打开面板。parentId 非空时校验父面板已在栈中。 </summary>
        public void Open(string id, string parentId = null, System.Action onClose = null)
        {
            if (string.IsNullOrEmpty(id)) return;
            // 子面板校验
            if (!string.IsNullOrEmpty(parentId) && !IsOpen(parentId))
            {
                Debug.LogWarning($"[UIPanelManager] 子面板 {id} 的父面板 {parentId} 未打开，拒绝");
                return;
            }
            // 已打开则忽略
            if (IsOpen(id)) return;

            _stack.Add(new PanelEntry { id = id, parentId = parentId, onClose = onClose });
        }

        public void Close(string id)
        {
            // 先关所有以本面板为父的子面板
            for (int i = _stack.Count - 1; i >= 0; i--)
                if (_stack[i].parentId == id) Close(_stack[i].id);

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].id == id)
                {
                    var cb = _stack[i].onClose;
                    _stack.RemoveAt(i);
                    cb?.Invoke();
                    return;
                }
            }
        }

        public void CloseTop()
        {
            if (_stack.Count == 0) return;
            Close(_stack[_stack.Count - 1].id);
        }

        public void CloseAll()
        {
            while (_stack.Count > 0) CloseTop();
        }

        public bool IsOpen(string id)
        {
            foreach (var e in _stack) if (e.id == id) return true;
            return false;
        }

        public string TopId => _stack.Count > 0 ? _stack[_stack.Count - 1].id : null;

        // ═══════════════════════════════════════════
        // UI 工具：给任意面板加标题栏 + 拖拽 + 关闭按钮
        // ═══════════════════════════════════════════

        /// <summary>
        /// 给面板的 Canvas 添加标题栏（可拖拽）+ 关闭按钮，返回标题栏 RectTransform。
        /// 调用时机：面板 CreateUI 之后。
        /// </summary>
        public static RectTransform AddPanelTitleBar(GameObject panelCanvas, string title, string panelId,
            System.Action onClose = null)
        {
            var bar = new GameObject("TitleBar", typeof(UnityEngine.UI.Image));
            bar.transform.SetParent(panelCanvas.transform, false);
            var brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(0.5f, 1);
            brt.sizeDelta = new Vector2(0, 30);
            brt.anchoredPosition = Vector2.zero;
            bar.GetComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);

            // 标题文本
            var label = new GameObject("Label", typeof(UnityEngine.UI.Text));
            label.transform.SetParent(bar.transform, false);
            var t = label.GetComponent<UnityEngine.UI.Text>();
            t.text = title;
            t.fontSize = 14;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10, 0); lrt.offsetMax = new Vector2(-40, 0);

            // 关闭按钮
            var closeBtn = new GameObject("CloseBtn", typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            closeBtn.transform.SetParent(bar.transform, false);
            var crt = closeBtn.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f);
            crt.pivot = new Vector2(1, 0.5f);
            crt.sizeDelta = new Vector2(24, 24);
            crt.anchoredPosition = new Vector2(-6, 0);
            closeBtn.GetComponent<UnityEngine.UI.Image>().color = new Color(0.4f, 0.2f, 0.2f);

            var closeLabel = new GameObject("X", typeof(UnityEngine.UI.Text));
            closeLabel.transform.SetParent(closeBtn.transform, false);
            var ct = closeLabel.GetComponent<UnityEngine.UI.Text>();
            ct.text = "×";
            ct.fontSize = 16;
            ct.color = Color.white;
            ct.alignment = TextAnchor.MiddleCenter;
            ct.raycastTarget = false;
            var clrt = closeLabel.GetComponent<RectTransform>();
            clrt.anchorMin = Vector2.zero; clrt.anchorMax = Vector2.one;
            clrt.sizeDelta = Vector2.zero;

            closeBtn.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                Instance?.Close(panelId);
                onClose?.Invoke();
            });

            // 拖拽
            bar.AddComponent<UIPanelDragHandler>();

            return brt;
        }
    }
}
