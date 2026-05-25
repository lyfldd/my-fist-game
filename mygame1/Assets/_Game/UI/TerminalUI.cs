using UnityEngine;
using _Game.Systems.Power;

namespace _Game.UI
{
    /// <summary>
    /// 用电终端面板 UI。静态方法 Show/Hide，OnGUI 渲染。
    /// </summary>
    public class TerminalUI : MonoBehaviour
    {
        static TerminalUI _instance;
        PowerTerminal _currentTerminal;

        bool _visible;
        Rect _panelRect;
        Vector2 _scrollPos;
        GUIStyle _headerStyle, _normalStyle, _greenStyle, _redStyle, _yellowStyle;
        bool _stylesInit;

        public static void Show(PowerTerminal terminal)
        {
            if (_instance == null)
            {
                var go = new GameObject("TerminalUI");
                _instance = go.AddComponent<TerminalUI>();
            }
            _instance._currentTerminal = terminal;
            _instance._visible = true;
        }

        public static void Hide()
        {
            if (_instance != null)
                _instance._visible = false;
        }

        void OnGUI()
        {
            if (!_visible || _currentTerminal == null) return;
            InitStyles();

            float w = 400f, h = 560f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            _panelRect = new Rect(x, y, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            GUI.DrawTexture(_panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_panelRect);
            GUILayout.Space(12);
            GUILayout.Label($"电网终端  供电半径: {_currentTerminal.supplyRadius}m", _headerStyle);
            GUILayout.Space(8);

            // 发电端列表
            GUILayout.Label("── 发电端 ──", _normalStyle);
            if (_currentTerminal.connectedSources.Count == 0)
                GUILayout.Label("  (无连接发电端)", _redStyle);
            else
            {
                for (int i = _currentTerminal.connectedSources.Count - 1; i >= 0; i--)
                {
                    var src = _currentTerminal.connectedSources[i];
                    if (src == null) { _currentTerminal.connectedSources.RemoveAt(i); continue; }
                    GUILayout.BeginHorizontal();
                    string status = src.IsActive ? "✓" : "✗";
                    string fuel = src.requiresFuel ? $" (燃料: {src.FuelRemaining:F1}h)" : "";
                    GUILayout.Label($"  {src.sourceType}  {src.maxOutput}W {status}{fuel}", _normalStyle);
                    if (GUILayout.Button("断开", GUILayout.Width(50), GUILayout.Height(24)))
                    {
                        _currentTerminal.DisconnectSource(src);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8);
            GUILayout.Label($"总功率: {_currentTerminal.GridPower}W  |  总负载: {_currentTerminal.GridLoad}W", _normalStyle);

            // 电网状态
            if (_currentTerminal.GridPower <= 0)
                GUILayout.Label("状态: ● 无电源", _redStyle);
            else if (_currentTerminal.IsGridOk)
                GUILayout.Label($"状态: ● 正常 (余量 {_currentTerminal.GridPower - _currentTerminal.GridLoad}W)", _greenStyle);
            else
                GUILayout.Label($"状态: ● 超载 (缺 {_currentTerminal.GridLoad - _currentTerminal.GridPower}W)", _redStyle);

            GUILayout.Space(8);

            // 范围内设备
            GUILayout.Label("── 范围内设备 ──", _normalStyle);
            var consumers = _currentTerminal.ConsumersInRange;
            if (consumers == null || consumers.Count == 0)
                GUILayout.Label("  (无设备)", _normalStyle);
            else
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(160));
                for (int i = consumers.Count - 1; i >= 0; i--)
                {
                    var c = consumers[i];
                    if (c == null) continue;
                    GUILayout.BeginHorizontal();
                    string statusIcon = c.IsRunning ? "✓" : "✗";
                    Color statusColor = c.IsRunning ? Color.green : new Color(1f, 0.4f, 0.4f);
                    GUI.color = statusColor;
                    GUILayout.Label($"{statusIcon} {c.DisplayName}", _normalStyle, GUILayout.Width(160));
                    GUI.color = Color.white;
                    GUILayout.Label($"功耗: {c.requiredPower}W", _normalStyle, GUILayout.Width(80));
                    if (c.IsManuallyOff)
                    {
                        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
                        if (GUILayout.Button("开启", GUILayout.Width(50), GUILayout.Height(22)))
                            c.IsManuallyOff = false;
                        GUI.backgroundColor = Color.white;
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.7f, 0.3f, 0.3f);
                        if (GUILayout.Button("关闭", GUILayout.Width(50), GUILayout.Height(22)))
                            c.IsManuallyOff = true;
                        GUI.backgroundColor = Color.white;
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(8);

            // 连接终端
            GUILayout.Label("── 连接终端 ──", _normalStyle);
            if (_currentTerminal.connectedTerminals.Count == 0)
                GUILayout.Label("  (无连接)", _normalStyle);
            else
            {
                for (int i = _currentTerminal.connectedTerminals.Count - 1; i >= 0; i--)
                {
                    var t = _currentTerminal.connectedTerminals[i];
                    if (t == null) { _currentTerminal.connectedTerminals.RemoveAt(i); continue; }
                    GUILayout.BeginHorizontal();
                    float dist = Vector3.Distance(_currentTerminal.transform.position, t.transform.position);
                    GUILayout.Label($"  → 终端 ({dist:F1}m)  半径{t.supplyRadius}m", _normalStyle);
                    if (GUILayout.Button("断开", GUILayout.Width(50), GUILayout.Height(24)))
                    {
                        _currentTerminal.DisconnectTerminal(t);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("── 连接操作 ──", _normalStyle);

            int cableCount = CableLinker.CountCables();
            string cableLabel = cableCount > 0 ? $"背包: {cableCount} 根电缆" : "背包: 无电缆";
            GUI.enabled = cableCount > 0;
            if (GUILayout.Button($"⚡ 连接发电端 / 终端 ({cableLabel})", GUILayout.Height(30)))
            {
                CableLinker.StartLinking(_currentTerminal);
                Hide();
            }
            GUI.enabled = true;

            if (_currentTerminal.connectedSources.Count > 0 || _currentTerminal.connectedTerminals.Count > 0)
            {
                if (GUILayout.Button("🔌 断开所有连接", GUILayout.Height(30)))
                {
                    _currentTerminal.connectedSources.Clear();
                    var copy = new System.Collections.Generic.List<PowerTerminal>(_currentTerminal.connectedTerminals);
                    foreach (var t in copy)
                        _currentTerminal.DisconnectTerminal(t);
                }
            }

            GUILayout.Space(12);

            if (GUILayout.Button("关闭", GUILayout.Height(36)))
                Hide();

            GUILayout.EndArea();

            // 点面板外关闭
            if (Event.current.type == EventType.MouseDown &&
                !_panelRect.Contains(Event.current.mousePosition))
                Hide();
        }

        void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _normalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, normal = { textColor = Color.white }
            };
            _greenStyle = new GUIStyle(_normalStyle)
            {
                normal = { textColor = Color.green }
            };
            _redStyle = new GUIStyle(_normalStyle)
            {
                normal = { textColor = new Color(1f, 0.4f, 0.4f) }
            };
            _yellowStyle = new GUIStyle(_normalStyle)
            {
                normal = { textColor = Color.yellow }
            };
        }
    }
}
