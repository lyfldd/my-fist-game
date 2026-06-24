using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Game.Core.SceneIsolation
{
    /// <summary>
    /// 场景上下文 — 每个场景根节点挂一个，追踪该场景内所有注册的资源。
    /// 场景卸载时自动清理 EventBus 订阅 / InputRouter 绑定 / ServiceLocator 注册。
    ///
    /// 用法：
    ///   var ctx = SceneContext.Current;
    ///   ctx.Track(EventBus.Subscribe&lt;Foo&gt;(OnFoo));      // 场景退出自动退订
    ///   ctx.TrackDisposable(someDisposable);              // 场景退出自动 Dispose
    ///   ctx.RegisterService(this);                        // 场景退出自动从 ServiceLocator 注销
    /// </summary>
    public class SceneContext : MonoBehaviour
    {
        /// <summary> 当前活跃的场景上下文（场景内任意位置可访问）</summary>
        public static SceneContext Current { get; private set; }

        /// <summary> 场景名称 </summary>
        [SerializeField] string _sceneLabel;

        /// <summary> 待清理的退订 Action </summary>
        readonly List<Action> _unsubscribes = new(64);

        /// <summary> 待 Dispose 的可释放对象 </summary>
        readonly List<IDisposable> _disposables = new(16);

        /// <summary> 注册到 ServiceLocator 的组件列表 </summary>
        readonly List<MonoBehaviour> _registeredServices = new(16);

        /// <summary> 是否正在退出 </summary>
        public bool IsExiting { get; private set; }

        /// <summary> 场景标签（编辑器可读）</summary>
        public string SceneLabel => _sceneLabel;

        void Awake()
        {
            Current = this;
        }

        void OnDestroy()
        {
            Cleanup();
            if (Current == this) Current = null;
        }

        // ============================================================
        // 公开 API
        // ============================================================

        /// <summary> 追踪一个退订 Action，场景退出时自动调用 </summary>
        public void Track(Action unsubscribe)
        {
            if (unsubscribe == null) return;
            _unsubscribes.Add(unsubscribe);
        }

        /// <summary> 追踪一个 IDisposable，场景退出时自动 Dispose </summary>
        public void TrackDisposable(IDisposable d)
        {
            if (d == null) return;
            _disposables.Add(d);
        }

        /// <summary>
        /// 注册服务到 ServiceLocator 同时追踪生命周期。
        /// 场景退出时自动从 ServiceLocator 注销并 Destroy（如果不是 DontDestroyOnLoad）。
        /// </summary>
        public void RegisterService(MonoBehaviour service)
        {
            if (service == null) return;
            ServiceLocator.Register(service);
            _registeredServices.Add(service);
        }

        /// <summary>
        /// 主动清理（场景卸载前由 SceneIsolationManager 调用）。
        /// 先清理订阅/绑定/释放/注销，给各组件 OnDisable 留干净的退场环境。
        /// </summary>
        public void Cleanup()
        {
            if (IsExiting) return;
            IsExiting = true;

            // 1. 退订所有 EventBus
            foreach (var unsub in _unsubscribes)
            {
                try { unsub(); }
                catch (Exception ex) { Debug.LogWarning($"[SceneContext] 退订异常: {ex.Message}"); }
            }
            _unsubscribes.Clear();

            // 2. Dispose 所有可释放对象
            foreach (var d in _disposables)
            {
                try { d.Dispose(); }
                catch (Exception ex) { Debug.LogWarning($"[SceneContext] Dispose 异常: {ex.Message}"); }
            }
            _disposables.Clear();

            // 3. 从 ServiceLocator 注销
            foreach (var svc in _registeredServices)
            {
                try { ServiceLocator.Unregister(svc); }
                catch (Exception ex) { Debug.LogWarning($"[SceneContext] 注销服务异常: {ex.Message}"); }
            }
            _registeredServices.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            Current = null;
        }
    }
}
