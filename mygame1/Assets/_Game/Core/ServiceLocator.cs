using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// 统一服务定位器 — 替代 FindObjectOfType。
    /// 所有需要在系统间共享的服务在 Awake 时 Register，使用方通过 Get 获取。
    ///
    /// 用法:
    ///   // 注册（Awake）
    ///   ServiceLocator.Register(this);
    ///
    ///   // 获取
    ///   var inv = ServiceLocator.Get&lt;Inventory&gt;();
    /// </summary>
    public static class ServiceLocator
    {
        static readonly Dictionary<Type, object> _services = new();

        /// <summary>已注册的服务数量</summary>
        public static int Count => _services.Count;

        /// <summary>
        /// 注册服务实例。通常在 Awake 中调用。
        /// </summary>
        public static void Register<T>(T instance) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                // 如果已注册且不同实例，用新的覆盖（场景重载场景）
                if (_services[type] != (object)instance)
                {
                    Debug.LogWarning($"[ServiceLocator] {type.Name} 已注册，替换为新实例: {instance}");
                    _services[type] = instance;
                }
                return;
            }
            _services[type] = instance;
        }

        /// <summary>
        /// 获取已注册的服务。如果未注册，尝试 FindObjectOfType 兜底（过渡期）。
        /// </summary>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var svc))
            {
                // 检查 Unity Object 是否已被销毁
                if (svc is UnityEngine.Object uo && uo == null)
                {
                    _services.Remove(type);
                    return FallbackFind<T>(type);
                }
                return svc as T;
            }

            return FallbackFind<T>(type);
        }

        /// <summary>
        /// 强制覆盖注册（用于运行时切换）
        /// </summary>
        public static void Override<T>(T instance) where T : class
        {
            _services[typeof(T)] = instance;
        }

        /// <summary>
        /// 获取所有已注册实例（场景中可能存在多个）。不会被缓存，每次重新查找。
        /// </summary>
        public static T[] GetAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsOfType<T>();
        }

        /// <summary>
        /// 注销服务（按类型）
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>
        /// 注销服务（按实例）。从实例反查类型，匹配才删。
        /// </summary>
        public static void Unregister(object instance)
        {
            if (instance == null) return;
            var targetType = instance.GetType();
            // 遍历所有注册，找类型匹配且实例相同的
            var keysToRemove = new List<Type>();
            foreach (var kv in _services)
            {
                if (kv.Value == instance)
                    keysToRemove.Add(kv.Key);
            }
            foreach (var key in keysToRemove)
                _services.Remove(key);
        }

        /// <summary>
        /// 清空所有注册（场景切换时）
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
        }

        // ═══════════════════════════════════════════
        // Fallback
        // ═══════════════════════════════════════════

        static T FallbackFind<T>(Type type) where T : class
        {
            var found = UnityEngine.Object.FindObjectOfType(type);
            if (found != null)
            {
                _services[type] = found; // 缓存，下次不重复查找
#if UNITY_EDITOR
                Debug.LogWarning($"[ServiceLocator] ⚠️ {type.Name} 未注册，已通过 FindObjectOfType 兜底。请在它的 Awake 中添加 ServiceLocator.Register(this);");
#endif
                return found as T;
            }
            return null;
        }
    }
}
