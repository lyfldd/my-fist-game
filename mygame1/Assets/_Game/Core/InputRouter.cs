using System.Collections.Generic;
using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// 统一输入路由 — 带优先级的按键拦截链。
    /// ScriptExecutionOrder = -100，在所有系统之前运行。
    ///
    /// 用法：
    ///   InputRouter.BindKey(KeyCode.F1, InputPriority.Debug, OnF1, this);
    ///   InputRouter.BindMouse(0, InputPriority.Gameplay, OnClick, this);
    ///   InputRouter.UnbindAll(this);  // OnDisable 时调用
    ///
    /// 回调返回 true = 消费（阻断低优先级），false = 放行。
    /// </summary>
    public class InputRouter : MonoBehaviour
    {
        public static InputRouter Instance { get; private set; }

        // ---- 绑定存储 ----
        readonly Dictionary<KeyCode, List<KeyBinding>> _keyMap = new Dictionary<KeyCode, List<KeyBinding>>();
        readonly Dictionary<int, List<MouseBinding>> _mouseMap = new Dictionary<int, List<MouseBinding>>();

        // 每帧收集待移除项，避免迭代时修改
        readonly List<KeyBinding> _deadKeys = new List<KeyBinding>();
        readonly List<MouseBinding> _deadMice = new List<MouseBinding>();

        class KeyBinding
        {
            public InputPriority priority;
            public System.Func<bool> callback;
            public MonoBehaviour owner;
        }

        class MouseBinding
        {
            public InputPriority priority;
            public System.Func<bool> callback;
            public MonoBehaviour owner;
        }

        // ============================================================
        // 生命周期
        // ============================================================

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 快照缓冲，避免回调中 Bind/Unbind 导致的 collection modified
        readonly List<System.Func<bool>> _pending = new List<System.Func<bool>>();
        readonly List<KeyValuePair<KeyCode, List<KeyBinding>>> _keySnapshot = new List<KeyValuePair<KeyCode, List<KeyBinding>>>();
        readonly List<KeyValuePair<int, List<MouseBinding>>> _mouseSnapshot = new List<KeyValuePair<int, List<MouseBinding>>>();

        // 同一帧内同一按键只处理一次
        private int _lastKeyFrame;
        private KeyCode _lastKey;
        private int _lastMouseFrame;
        private int _lastMouseBtn;

        void Update()
        {
            CleanDeadBindings();

            // ---- 键盘：全部快照后再遍历（回调可能修改 _keyMap） ----
            _keySnapshot.Clear();
            foreach (var kv in _keyMap) _keySnapshot.Add(kv);

            foreach (var kv in _keySnapshot)
            {
                if (!Input.GetKeyDown(kv.Key)) continue;

                // 同一帧内同键已处理过则跳过
                if (_lastKeyFrame == UnityEngine.Time.frameCount && _lastKey == kv.Key) continue;
                _lastKeyFrame = UnityEngine.Time.frameCount;
                _lastKey = kv.Key;

                _pending.Clear();
                foreach (var b in kv.Value)
                    if (b.owner != null) _pending.Add(b.callback);

                foreach (var cb in _pending)
                    if (cb()) break;
            }

            // ---- 鼠标 ----
            _mouseSnapshot.Clear();
            foreach (var kv in _mouseMap) _mouseSnapshot.Add(kv);

            foreach (var kv in _mouseSnapshot)
            {
                if (!Input.GetMouseButtonDown(kv.Key)) continue;

                if (_lastMouseFrame == UnityEngine.Time.frameCount && _lastMouseBtn == kv.Key) continue;
                _lastMouseFrame = UnityEngine.Time.frameCount;
                _lastMouseBtn = kv.Key;

                _pending.Clear();
                foreach (var b in kv.Value)
                    if (b.owner != null) _pending.Add(b.callback);

                foreach (var cb in _pending)
                    if (cb()) break;
            }
        }

        void CleanDeadBindings()
        {
            _deadKeys.Clear();
            _deadMice.Clear();

            foreach (var kv in _keyMap)
            {
                var list = kv.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].owner == null)
                        list.RemoveAt(i);
                }
                if (list.Count == 0)
                    _deadKeys.Add(null); // 标记空列表
            }

            foreach (var kv in _mouseMap)
            {
                var list = kv.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].owner == null)
                        list.RemoveAt(i);
                }
            }

            // 移出外层后需要完整的键移除，这里仅清理 null owner
            // 空列表保留也没关系，下次循环会跳过
        }

        // ============================================================
        // 公开 API — 键盘
        // ============================================================

        static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("InputRouter");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<InputRouter>();
            Debug.Log("[InputRouter] 运行时自动创建（场景中未找到，已新建）");
        }

        /// <summary>
        /// 绑定按键。回调返回 true 表示消费（阻断低优先级）。
        /// 同一 owner 对同一按键重复绑定会自动覆盖旧绑定。
        /// </summary>
        public static void BindKey(KeyCode key, InputPriority priority,
            System.Func<bool> callback, MonoBehaviour owner)
        {
            EnsureInstance();
            Instance.BindKeyInternal(key, priority, callback, owner);
        }

        /// <summary>
        /// 绑定持续按住按键（每帧触发）。
        /// </summary>
        public static void BindKeyHeld(KeyCode key, InputPriority priority,
            System.Func<bool> callback, MonoBehaviour owner)
        {
            // GetKey 的持续调用与 GetKeyDown 不同，这里暂不实现
            // 持续输入（WASD/Shift）不移入路由，保留直接 Input 调用
        }

        void BindKeyInternal(KeyCode key, InputPriority priority,
            System.Func<bool> callback, MonoBehaviour owner)
        {
            if (!_keyMap.TryGetValue(key, out var list))
            {
                list = new List<KeyBinding>();
                _keyMap[key] = list;
            }

            // 同 owner 同按键覆盖
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].owner == owner)
                {
                    list[i].priority = priority;
                    list[i].callback = callback;
                    SortList(list);
                    return;
                }
            }

            list.Add(new KeyBinding
            {
                priority = priority,
                callback = callback,
                owner = owner
            });
            SortList(list);
        }

        // ============================================================
        // 公开 API — 鼠标
        // ============================================================

        /// <summary>
        /// 绑定鼠标按键。button: 0=左键, 1=右键, 2=中键。
        /// </summary>
        public static void BindMouse(int button, InputPriority priority,
            System.Func<bool> callback, MonoBehaviour owner)
        {
            EnsureInstance();
            Instance.BindMouseInternal(button, priority, callback, owner);
        }

        void BindMouseInternal(int button, InputPriority priority,
            System.Func<bool> callback, MonoBehaviour owner)
        {
            if (!_mouseMap.TryGetValue(button, out var list))
            {
                list = new List<MouseBinding>();
                _mouseMap[button] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].owner == owner)
                {
                    list[i].priority = priority;
                    list[i].callback = callback;
                    SortList(list);
                    return;
                }
            }

            list.Add(new MouseBinding
            {
                priority = priority,
                callback = callback,
                owner = owner
            });
            SortList(list);
        }

        // ============================================================
        // 解绑
        // ============================================================

        /// <summary>
        /// 移除指定 owner 的所有按键/鼠标绑定。在 OnDisable/OnDestroy 中调用。
        /// </summary>
        public static void UnbindAll(MonoBehaviour owner)
        {
            if (Instance == null) return; // 未初始化则无需解绑
            Instance.UnbindAllInternal(owner);
        }

        void UnbindAllInternal(MonoBehaviour owner)
        {
            foreach (var kv in _keyMap)
            {
                var list = kv.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].owner == owner)
                        list.RemoveAt(i);
                }
            }
            foreach (var kv in _mouseMap)
            {
                var list = kv.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].owner == owner)
                        list.RemoveAt(i);
                }
            }
        }

        // ============================================================
        // 工具
        // ============================================================

        void SortList<T>(List<T> list) where T : class
        {
            // 按优先级降序排序
            if (list is List<KeyBinding> kb)
            {
                kb.Sort((a, b) => b.priority.CompareTo(a.priority));
            }
            else if (list is List<MouseBinding> mb)
            {
                mb.Sort((a, b) => b.priority.CompareTo(a.priority));
            }
        }
    }

    /// <summary>
    /// 输入优先级。数值越大越先收到按键。
    /// </summary>
    public enum InputPriority
    {
        /// <summary> 200 — 调试工具面板 </summary>
        Debug = 200,
        /// <summary> 100 — UI 面板 (背包/合成/容器/建造菜单) </summary>
        UI = 100,
        /// <summary> 50 — 交互动作 (建造确认/取消, E键交互, 上下车) </summary>
        Action = 50,
        /// <summary> 0 — 游戏玩法 (射击/近战/快捷栏/WASD) </summary>
        Gameplay = 0
    }
}
