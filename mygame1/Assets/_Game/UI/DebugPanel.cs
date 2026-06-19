using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Audio;
using _Game.Systems.Character;
using _Game.Systems.WorldGen;
using _Game.Systems.Zombie;

namespace _Game.UI
{
    /// <summary>
    /// 开发者调试面板，按 Y 切换显示/隐藏。整合所有系统调试信息。
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        private bool _visible;
        private Vector2 _scrollPos;

        void OnEnable() { InputRouter.BindKey(KeyCode.Y, InputPriority.Debug, Toggle, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }
        bool Toggle() { _visible = !_visible; return true; }

    }
}
