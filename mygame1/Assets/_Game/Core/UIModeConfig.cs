using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// UI 渲染模式开关 — 控制使用 IMGUI (OnGUI) 还是 UGUI (Canvas)。
    /// PlayerPrefs 持久化，重启保持。
    /// </summary>
    public static class UIModeConfig
    {
        private const string KEY = "ui_use_ugui";

        /// <summary> true=UGUI 模式, false=IMGUI 模式（默认）</summary>
        public static bool UseUGUI { get; private set; }

        static UIModeConfig()
        {
            UseUGUI = PlayerPrefs.GetInt(KEY, 0) == 1;
        }

        /// <summary> 切换 UI 模式并持久化 </summary>
        public static void Toggle()
        {
            UseUGUI = !UseUGUI;
            PlayerPrefs.SetInt(KEY, UseUGUI ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
