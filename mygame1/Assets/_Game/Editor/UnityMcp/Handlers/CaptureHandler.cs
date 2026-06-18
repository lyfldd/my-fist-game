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
        private const string GET_STATUS = "unity_get_status";

        public static void Register()
        {
            UnityMcpServer.RegisterHandler(GET_STATUS, HandleGetStatus);
            UnityMcpServer.RegisterHandler(CAPTURE_GAME, HandleCaptureGame);
            UnityMcpServer.RegisterHandler(CAPTURE_SCENE, HandleCaptureScene);
            UnityMcpServer.RegisterHandler(CAPTURE_EDITOR, HandleCaptureEditor);
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

            try
            {
                // 优先用 ScreenCapture（包含 UI），回退到 Camera.Render()
                string b64 = CaptureGameViewWithUI(width, height);
                return $@"{{
                    ""success"": true,
                    ""image_base64"": ""{b64}"",
                    ""width"": {width},
                    ""height"": {height},
                    ""view"": ""game"",
                    ""play_mode"": true
                }}";
            }
            catch (Exception ex)
            {
                return $@"{{""success"": false, ""error"": ""{SimpleJson.Escape(ex.Message)}""}}";
            }
        }

        // ── Capture Scene View (Edit mode) ──

        static string HandleCaptureScene(string paramsJson)
        {
            int width = GetIntParam(paramsJson, "width", 1920);
            int height = GetIntParam(paramsJson, "height", 1080);

            try
            {
                string b64 = RenderCameraToBase64(GetSceneViewCamera(), width, height,
                    "Scene View camera not available. Open the Scene tab.");
                return $@"{{
                    ""success"": true,
                    ""image_base64"": ""{b64}"",
                    ""width"": {width},
                    ""height"": {height},
                    ""view"": ""scene"",
                    ""play_mode"": {Application.isPlaying.ToString().ToLower()}
                }}";
            }
            catch (Exception ex)
            {
                return $@"{{""success"": false, ""error"": ""{SimpleJson.Escape(ex.Message)}""}}";
            }
        }

        // ── Capture Editor (auto-detect) ──

        static string HandleCaptureEditor(string paramsJson)
        {
            if (Application.isPlaying)
                return HandleCaptureGame(paramsJson);
            else
                return HandleCaptureScene(paramsJson);
        }

        // ═══ Implementation ═══

        /// <summary> Game View 含 UI — 使用 ScreenCapture API 写文件 + 等待 </summary>
        static string CaptureGameViewWithUI(int width, int height)
        {
            // ScreenCapture 写入临时文件（异步，EndOfFrame）
            string tmpPath = Path.Combine(Application.temporaryCachePath, "_mcp_game_capture.png");

            // 删除旧文件
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            ScreenCapture.CaptureScreenshot(tmpPath, 2); // supersize=2 for quality

            // 等待文件写入（最多 3 秒）
            float timeout = 3f;
            float elapsed = 0f;
            while (!File.Exists(tmpPath) && elapsed < timeout)
            {
                System.Threading.Thread.Sleep(50);
                elapsed += 0.05f;
            }

            // 文件可能只有头，等待写入完成
            if (File.Exists(tmpPath))
            {
                long prevSize = 0;
                elapsed = 0f;
                while (elapsed < 2f)
                {
                    System.Threading.Thread.Sleep(100);
                    elapsed += 0.1f;
                    var fi = new FileInfo(tmpPath);
                    if (fi.Length > 0 && fi.Length == prevSize)
                        break; // 文件大小稳定
                    prevSize = fi.Length;
                }
            }

            if (!File.Exists(tmpPath))
            {
                // 回退：只用 Camera 渲染
                Debug.LogWarning("[UnityMcp] ScreenCapture 超时，回退到 Camera.Render()");
                var cam = Camera.main;
                if (cam == null)
                    throw new Exception("No Camera.main available for fallback capture");
                return RenderCameraToBase64(cam, width, height, "");
            }

            byte[] bytes = File.ReadAllBytes(tmpPath);
            try { File.Delete(tmpPath); } catch { }

            if (bytes.Length == 0)
                throw new Exception("Screenshot file empty");

            return Convert.ToBase64String(bytes);
        }

        /// <summary> 用 RenderTexture 从指定相机同步渲染 </summary>
        static string RenderCameraToBase64(Camera cam, int width, int height, string errorMsg)
        {
            if (cam == null)
                throw new Exception(string.IsNullOrEmpty(errorMsg) ? "Camera is null" : errorMsg);

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

            return Convert.ToBase64String(bytes);
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
