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
    }
}
