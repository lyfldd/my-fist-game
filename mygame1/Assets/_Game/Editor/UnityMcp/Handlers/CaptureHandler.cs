using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace _Game.Editor.UnityMcp
{
    /// <summary>
    /// 截图 Handler — 使用 RenderTexture 同步捕获 Game/Scene View，输出 base64 PNG。
    /// Play 模式捕获 Game View，Edit 模式捕获 Scene View。
    /// </summary>
    public static class CaptureHandler
    {
        private const string CAPTURE_GAME = "unity_capture_game";
        private const string CAPTURE_SCENE = "unity_capture_scene";
        private const string CAPTURE_EDITOR = "unity_capture_editor";
        private const string CAPTURE_FILE = "unity_capture_to_file";
        private const string GET_STATUS = "unity_get_status";

        public static void Register()
        {
            UnityMcpServer.RegisterHandler(GET_STATUS, HandleGetStatus);
            UnityMcpServer.RegisterHandler(CAPTURE_GAME, HandleCaptureGame);
            UnityMcpServer.RegisterHandler(CAPTURE_SCENE, HandleCaptureScene);
            UnityMcpServer.RegisterHandler(CAPTURE_EDITOR, HandleCaptureEditor);
            UnityMcpServer.RegisterHandler(CAPTURE_FILE, HandleCaptureToFile);
        }

        // ── Status ──

        static string HandleGetStatus(string paramsJson)
        {
            bool inPlayMode = Application.isPlaying;
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            int objCount = UnityEngine.Object.FindObjectsOfType<GameObject>(true).Length;
            var cam = Camera.main;

            return $@"{{
                ""success"": true,
                ""play_mode"": {inPlayMode.ToString().ToLower()},
                ""scene"": ""{SimpleJson.Escape(sceneName)}"",
                ""objects"": {objCount},
                ""has_main_camera"": {(cam != null).ToString().ToLower()},
                ""screen_w"": {Screen.width},
                ""screen_h"": {Screen.height}
            }}";
        }

        // ── Capture Game View (Play mode) ──

        static string HandleCaptureGame(string paramsJson)
        {
            int width = GetIntParam(paramsJson, "width", 1920);
            int height = GetIntParam(paramsJson, "height", 1080);

            if (!Application.isPlaying)
            {
                return $@"{{
                    ""success"": false,
                    ""error"": ""capture_game 仅在 Play 模式下可用。请先进入 Play 模式，或用 capture_scene。""
                }}";
            }

            // 截图到文件（避免 WebSocket 大帧超时）
            return CaptureToFileJson(Camera.main, width, height, "game",
                "No Camera.main available for game capture");
        }

        // ── Capture Scene View (Edit mode) ──

        static string HandleCaptureScene(string paramsJson)
        {
            int width = GetIntParam(paramsJson, "width", 1920);
            int height = GetIntParam(paramsJson, "height", 1080);

            // 截图到文件（避免 WebSocket 大帧超时）
            return CaptureToFileJson(GetSceneViewCamera(), width, height, "scene",
                "Scene View camera not available. Open the Scene tab.");
        }

        // ── Capture Editor (auto-detect) ──

        static string HandleCaptureEditor(string paramsJson)
        {
            if (Application.isPlaying)
                return HandleCaptureGame(paramsJson);
            else
                return HandleCaptureScene(paramsJson);
        }

        // ── Capture to File ──

        static string HandleCaptureToFile(string paramsJson)
        {
            int width = GetIntParam(paramsJson, "width", 1920);
            int height = GetIntParam(paramsJson, "height", 1080);
            Camera cam = Application.isPlaying ? Camera.main : GetSceneViewCamera();
            string view = Application.isPlaying ? "game" : "scene";
            return CaptureToFileJson(cam, width, height, view, "Camera not available");
        }

        // ═══ Implementation ═══

        /// <summary> 渲染截图到临时文件，返回文件路径 JSON </summary>
        static string CaptureToFileJson(Camera cam, int width, int height, string view, string errorMsg)
        {
            try
            {
                if (cam == null)
                    throw new Exception(string.IsNullOrEmpty(errorMsg) ? "Camera is null" : errorMsg);

                byte[] bytes;

                if (Application.isPlaying)
                {
                    // Play 模式：将 Overlay Canvas 临时切到 Camera 模式，
                    // 确保 UI 能渲染到相机的 RenderTexture 中被捕获
                    var allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
                    var overlayCanvases = new System.Collections.Generic.List<Canvas>();
                    var originalModes = new System.Collections.Generic.List<RenderMode>();
                    var originalCameras = new System.Collections.Generic.List<Camera>();

                    foreach (var cv in allCanvases)
                    {
                        if (cv.renderMode == RenderMode.ScreenSpaceOverlay)
                        {
                            overlayCanvases.Add(cv);
                            originalModes.Add(cv.renderMode);
                            originalCameras.Add(cv.worldCamera);
                            cv.renderMode = RenderMode.ScreenSpaceCamera;
                            cv.worldCamera = cam;
                        }
                    }

                    var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                    rt.antiAliasing = 1;
                    var prevTarget = cam.targetTexture;
                    var prevActive = RenderTexture.active;
                    cam.targetTexture = rt;
                    cam.Render();
                    Canvas.ForceUpdateCanvases();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();
                    cam.targetTexture = prevTarget;
                    RenderTexture.active = prevActive;
                    RenderTexture.ReleaseTemporary(rt);

                    // 恢复 Canvas 原始模式
                    for (int i = 0; i < overlayCanvases.Count; i++)
                    {
                        overlayCanvases[i].renderMode = originalModes[i];
                        overlayCanvases[i].worldCamera = originalCameras[i];
                    }

                    bytes = tex.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                else
                {
                    // Edit 模式：直接渲染相机
                    bytes = RenderToBytes(cam, width, height);
                }

                string filePath = Path.Combine(Application.temporaryCachePath, "_mcp_capture.png");
                File.WriteAllBytes(filePath, bytes);

                return $@"{{
                    ""success"": true,
                    ""path"": ""{SimpleJson.Escape(filePath)}"",
                    ""width"": {width},
                    ""height"": {height},
                    ""view"": ""{view}"",
                    ""size_bytes"": {bytes.Length},
                    ""play_mode"": {Application.isPlaying.ToString().ToLower()}
                }}";
            }
            catch (Exception ex)
            {
                return $@"{{""success"": false, ""error"": ""{SimpleJson.Escape(ex.Message)}""}}";
            }
        }

        static byte[] RenderToBytes(Camera cam, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            byte[] bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return bytes;
        }

        static Camera GetSceneViewCamera()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                throw new Exception("No active Scene View. Open Window → General → Scene in Unity.");
            var cam = sceneView.camera;
            if (cam == null)
                throw new Exception("Scene View camera is null.");
            return cam;
        }

        // ═══ JSON Helpers ═══

        static int GetIntParam(string json, string key, int defaultValue)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return defaultValue;

            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return defaultValue;

            int start = colon + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t'))
                start++;

            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '.'))
                end++;

            if (end > start && int.TryParse(json.Substring(start, end - start), out int val))
                return val;

            return defaultValue;
        }
    }
}
