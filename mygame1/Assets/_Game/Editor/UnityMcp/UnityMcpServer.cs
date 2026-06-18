using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace _Game.Editor.UnityMcp
{
    /// <summary>
    /// Unity MCP Server 入口 — 编辑器启动时自动运行，通过 EditorApplication.update
    /// 在主线程排空请求队列并分发到对应 Handler。
    /// 架构对标 Blender MCP Addon (bpy.app.timers → EditorApplication.update)。
    /// </summary>
    [InitializeOnLoad]
    public static class UnityMcpServer
    {
        private static UnityWebSocketServer _server;
        private static readonly Dictionary<string, Func<string, string>> _handlers
            = new Dictionary<string, Func<string, string>>();

        static UnityMcpServer()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
            // 延迟启动，确保所有脚本已加载、编辑器已就绪
            EditorApplication.delayCall += () =>
            {
                if (_server == null || !_server.IsRunning)
                    StartServer();
            };
        }

        // ═══ Public API ═══

        public static bool IsRunning => _server?.IsRunning ?? false;

        /// <summary> 注册 Handler。Agent 在静态构造里调用。 </summary>
        public static void RegisterHandler(string method, Func<string, string> handler)
        {
            _handlers[method] = handler;
        }

        public static void StartServer()
        {
            if (_server?.IsRunning == true) return;

            // 注册所有 Handler
            _handlers.Clear();
            CaptureHandler.Register();
            InspectionHandler.Register();
            ModificationHandler.Register();
            ExecutionHandler.Register();

            _server = new UnityWebSocketServer("127.0.0.1", 9877);
            _server.Start();
        }

        public static void StopServer()
        {
            _server?.Stop();
            _server = null;
        }

        // ═══ Main-Thread Dispatch (对标 Blender bpy.app.timers) ═══

        private static void OnEditorUpdate()
        {
            if (_server == null || !_server.IsRunning) return;

            // 每次 tick 最多处理 5 个请求，避免卡编辑器
            for (int i = 0; i < 5; i++)
            {
                if (!_server.RequestQueue.TryDequeue(out var item))
                    break;

                var (reqId, method, paramsJson) = item;
                string resultJson;

                if (_handlers.TryGetValue(method, out var handler))
                {
                    try
                    {
                        resultJson = handler(paramsJson);
                    }
                    catch (Exception ex)
                    {
                        resultJson = SimpleJson.BuildError(reqId, -32603,
                            $"Handler exception: {ex.Message}\n{ex.StackTrace}");
                    }
                }
                else
                {
                    resultJson = SimpleJson.BuildError(reqId, -32601,
                        $"Unknown method: {method}");
                }

                // Wrap in proper response format
                string fullResponse = $"{{\"id\":\"{reqId}\",\"result\":{resultJson}}}";
                _server.SendResponse(reqId, resultJson);
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // 进入/退出播放模式时重启服务器（端口释放）
            if (change == PlayModeStateChange.ExitingEditMode ||
                change == PlayModeStateChange.ExitingPlayMode)
            {
                StopServer();
            }
            else if (change == PlayModeStateChange.EnteredPlayMode ||
                     change == PlayModeStateChange.EnteredEditMode)
            {
                // 延迟重启，让 Unity 编辑器稳定
                EditorApplication.delayCall += () =>
                {
                    if (_server == null || !_server.IsRunning)
                        StartServer();
                };
            }
        }
    }
}
