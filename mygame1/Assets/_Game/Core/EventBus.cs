using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// 全局事件总线
    /// 泛型约束 where T : struct 确保事件是值类型，避免 GC 压力
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        /// <summary>
        /// 订阅事件
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            Type type = typeof(T);

            if (_handlers.TryGetValue(type, out Delegate existing))
            {
                _handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[type] = handler;
            }
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            Type type = typeof(T);

            if (_handlers.TryGetValue(type, out Delegate existing))
            {
                Delegate newDelegate = Delegate.Remove(existing, handler);
                if (newDelegate == null)
                {
                    _handlers.Remove(type);
                }
                else
                {
                    _handlers[type] = newDelegate;
                }
            }
        }

        /// <summary>
        /// 发布事件
        /// 逐个调用订阅者，每个独立 try-catch，防止一个异常中断整条链
        /// </summary>
        public static void Publish<T>(T eventData) where T : struct
        {
            Type type = typeof(T);

            if (_handlers.TryGetValue(type, out Delegate handler))
            {
                foreach (Delegate single in handler.GetInvocationList())
                {
                    try
                    {
                        (single as Action<T>)?.Invoke(eventData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[EventBus] {type.Name} 的订阅者 {single.Method.DeclaringType?.Name}.{single.Method.Name} 异常：{e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 清空所有事件订阅（通常在场景切换时调用）
        /// </summary>
        public static void Clear()
        {
            _handlers.Clear();
        }

        /// <summary>
        /// 获取某个事件类型的订阅者数量（调试用）
        /// </summary>
        public static int GetSubscriberCount<T>() where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out Delegate handler))
            {
                return handler.GetInvocationList().Length;
            }
            return 0;
        }

        /// <summary>
        /// 获取所有事件订阅的完整摘要（调试用）
        /// </summary>
        public static string GetAllSubscriptions()
        {
            StringBuilder sb = new StringBuilder();

            if (_handlers.Count == 0)
            {
                sb.AppendLine("[EventBus] 当前没有任何事件订阅");
                return sb.ToString();
            }

            foreach (var kvp in _handlers)
            {
                sb.AppendLine($"{kvp.Key.Name}: {kvp.Value.GetInvocationList().Length} 个订阅者");
                foreach (Delegate d in kvp.Value.GetInvocationList())
                {
                    string targetInfo = d.Target != null
                        ? $"(instance: {d.Target.GetType().Name})"
                        : "(static)";
                    sb.AppendLine($"  - {d.Method.DeclaringType?.Name}.{d.Method.Name} {targetInfo}");
                }
            }

            return sb.ToString();
        }
    }
}
