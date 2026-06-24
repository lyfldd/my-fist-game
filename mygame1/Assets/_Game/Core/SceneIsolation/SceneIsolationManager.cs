using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Game.Core.SceneIsolation
{
    /// <summary>
    /// 场景隔离管理器 — 全局单例（DontDestroyOnLoad），统一管理多场景切换。
    ///
    /// 职责：
    /// 1. 切换场景前：通知旧场景退出 → 清理 SceneContext（EventBus/InputRouter/ServiceLocator）
    /// 2. 卸载旧场景
    /// 3. 加载新场景
    /// 4. 新场景就绪后：初始化新 SceneContext
    ///
    /// 多场景复用：只需在新场景根节点挂 SceneContext，其余自动处理。
    /// </summary>
    public class SceneIsolationManager : MonoBehaviour
    {
        public static SceneIsolationManager Instance { get; private set; }

        /// <summary> 是否正在切换场景 </summary>
        public bool IsSwitching { get; private set; }

        /// <summary> 当前场景名称 </summary>
        public string CurrentSceneName { get; private set; }

        /// <summary> 场景切换完成回调 </summary>
        public event Action<string> OnSceneSwitched;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentSceneName = SceneManager.GetActiveScene().name;
        }

        // ============================================================
        // 公开 API
        // ============================================================

        /// <summary>
        /// 切换到指定场景（单场景模式，卸载当前）。
        /// </summary>
        public void SwitchToScene(string sceneName, Action onComplete = null)
        {
            if (IsSwitching) return;
            StartCoroutine(SwitchRoutine(sceneName, onComplete));
        }

        /// <summary>
        /// 同步返回当前场景的 SceneContext（可能为 null）。
        /// </summary>
        public static SceneContext GetCurrentContext()
        {
            var go = GameObject.Find("SceneContext");
            return go != null ? go.GetComponent<SceneContext>() : null;
        }

        // ============================================================
        // 切换流程
        // ============================================================

        IEnumerator SwitchRoutine(string targetScene, Action onComplete)
        {
            IsSwitching = true;

            // 冻结输入和游戏时间
            UnityEngine.Time.timeScale = 0f;
            InputRouter.BindKey(KeyCode.Escape, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.Tab, InputPriority.Debug, () => true, this);
            InputRouter.BindKey(KeyCode.E, InputPriority.Debug, () => true, this);
            InputRouter.BindMouse(0, InputPriority.Debug, () => true, this);
            InputRouter.BindMouse(1, InputPriority.Debug, () => true, this);

            bool failed = false;

            // Phase 1: 清理旧场景
            var oldCtx = SceneContext.Current;
            if (oldCtx != null)
            {
                Debug.Log($"[SceneIsolation] 退出场景: {oldCtx.SceneLabel}");
                oldCtx.Cleanup();
            }

            EventBus.Publish(new SceneWillUnloadEvent(
                SceneManager.GetActiveScene().name, targetScene));

            yield return null;

            // Phase 2: 卸载当前场景
            var currentScene = SceneManager.GetActiveScene();
            var asyncUnload = SceneManager.UnloadSceneAsync(currentScene);
            if (asyncUnload != null)
            {
                while (!asyncUnload.isDone) yield return null;
            }

            // Phase 3: 加载目标场景
            var asyncLoad = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
            if (asyncLoad == null)
            {
                Debug.LogError($"[SceneIsolation] 场景加载失败: {targetScene}");
                failed = true;
            }

            if (!failed)
            {
                while (!asyncLoad.isDone) yield return null;

                var newScene = SceneManager.GetSceneByName(targetScene);
                if (newScene.IsValid())
                    SceneManager.SetActiveScene(newScene);

                CurrentSceneName = targetScene;

                // Phase 4: 初始化新场景上下文
                var newCtx = GetCurrentContext();
                if (newCtx == null)
                {
                    var ctxGo = new GameObject("SceneContext");
                    SceneManager.MoveGameObjectToScene(ctxGo, newScene);
                    newCtx = ctxGo.AddComponent<SceneContext>();
                    Debug.LogWarning($"[SceneIsolation] 场景 {targetScene} 缺少 SceneContext，已自动创建");
                }

                yield return null;
                yield return null;

                Debug.Log($"[SceneIsolation] 进入场景: {targetScene}");
                EventBus.Publish(new SceneDidLoadEvent(targetScene, CurrentSceneName));
                OnSceneSwitched?.Invoke(targetScene);
            }

            // Phase 5: 无论如何都要解冻
            UnityEngine.Time.timeScale = 1f;
            InputRouter.UnbindAll(this);
            IsSwitching = false;
            onComplete?.Invoke();
        }
    }

    // ============================================================
    // 场景生命周期事件
    // ============================================================

    /// <summary> 场景即将卸载。订阅者应在此事件中保存状态、清理引用。 </summary>
    public readonly struct SceneWillUnloadEvent
    {
        public string OldScene { get; }
        public string NewScene { get; }
        public SceneWillUnloadEvent(string oldScene, string newScene)
        { OldScene = oldScene; NewScene = newScene; }
    }

    /// <summary> 新场景已加载并就绪。订阅者应在此事件中初始化场景相关状态。 </summary>
    public readonly struct SceneDidLoadEvent
    {
        public string SceneName { get; }
        public string PreviousScene { get; }
        public SceneDidLoadEvent(string sceneName, string previousScene)
        { SceneName = sceneName; PreviousScene = previousScene; }
    }
}
