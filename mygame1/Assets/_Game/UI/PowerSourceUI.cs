using System.Collections.Generic;
using UnityEngine;
using _Game.Systems.Power;

namespace _Game.UI
{
    /// <summary>
    /// 发电设备面板 UI。静态 Show/Hide，OnGUI 渲染。
    /// 显示发电状态、燃料、耐久、电网连接信息。
    /// </summary>
    public class PowerSourceUI : MonoBehaviour
    {
        static PowerSourceUI _instance;
        PowerSource _currentSource;

        bool _visible;
        Rect _panelRect;
        Vector2 _scrollPos;
        GUIStyle _headerStyle, _normalStyle, _greenStyle, _redStyle, _yellowStyle, _btnStyle;
        bool _stylesInit;

        public static void Show(PowerSource source)
        {
            if (_instance == null)
            {
                var go = new GameObject("PowerSourceUI");
                _instance = go.AddComponent<PowerSourceUI>();
            }
            _instance._currentSource = source;
            _instance._visible = true;
        }

        public static void Hide()
        {
            if (_instance != null)
                _instance._visible = false;
        }

        void OnGUI()
        {
            if (!_visible || _currentSource == null) return;
            InitStyles();

            float w = 380f, h = 440f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            _panelRect = new Rect(x, y, w, h);

            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            GUI.DrawTexture(_panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_panelRect);
            GUILayout.Space(12);

            // 标题
            string typeLabel = _currentSource.sourceType switch
            {
                PowerSourceType.Human => "人力",
                PowerSourceType.Solar => "太阳能",
                PowerSourceType.Wind => "风力",
                PowerSourceType.Water => "水力",
                PowerSourceType.Combustion => "燃烧",
                PowerSourceType.Thermal => "热力",
                _ => "未知"
            };
            GUILayout.Label($"{typeLabel}发电设备  ({_currentSource.maxOutput}W)", _headerStyle);
            GUILayout.Space(10);

            // 运转状态
            bool active = _currentSource.IsActive;
            GUI.color = active ? Color.green : new Color(1f, 0.4f, 0.4f);
            GUILayout.Label($"状态: {(active ? "● 运转中" : "● 已停摆")}", _normalStyle);
            GUI.color = Color.white;
            GUILayout.Space(4);
            GUILayout.Label($"输出: {_currentSource.CurrentOutput}W / {_currentSource.MaxOutput}W", _normalStyle);
            GUILayout.Space(8);

            // 燃料
            if (_currentSource.requiresFuel)
            {
                GUILayout.Label("── 燃料 ──", _normalStyle);
                float fuelH = _currentSource.FuelRemaining;
                string fuelLabel = fuelH > 0f
                    ? $"燃料剩余: {fuelH:F1} 小时"
                    : "燃料剩余: 0 (已耗尽)";
                GUI.color = fuelH > 2f ? Color.green : (fuelH > 0f ? Color.yellow : Color.red);
                GUILayout.Label(fuelLabel, _normalStyle);
                GUI.color = Color.white;

                // 燃料进度条
                float maxDisplay = 10f; // 最多显示10小时
                float pct = Mathf.Clamp01(fuelH / maxDisplay);
                Rect barRect = GUILayoutUtility.GetRect(200f, 16f);
                GUI.color = new Color(0.3f, 0.3f, 0.3f);
                GUI.DrawTexture(barRect, Texture2D.whiteTexture);
                GUI.color = fuelH > 2f ? new Color(0.2f, 0.8f, 0.3f) : Color.yellow;
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUILayout.Space(6);

                // 添加燃料按钮
                string fuelName = _currentSource.fuelItemName ?? "燃料";
                int fuelCount = CountFuelInInventory();
                GUI.enabled = fuelCount > 0;
                if (GUILayout.Button($"添加 {fuelName} (背包: {fuelCount})", _btnStyle, GUILayout.Height(34)))
                {
                    ConsumeFuelFromInventory();
                }
                GUI.enabled = true;
                GUILayout.Space(8);
            }

            // 条件
            GUILayout.Label("── 运行条件 ──", _normalStyle);
            if (_currentSource.daytimeOnly)
                GUILayout.Label("  需要白天", _yellowStyle);
            if (_currentSource.requiresOpenAir)
                GUILayout.Label("  需要露天", _yellowStyle);
            if (_currentSource.requiresWater)
                GUILayout.Label("  需要水边", _yellowStyle);
            if (_currentSource.noiseRadius > 0f)
                GUILayout.Label($"  噪音半径: {_currentSource.noiseRadius}m", _normalStyle);

            GUILayout.Space(8);

            // 耐久（预留）
            float duraPct = _currentSource.DurabilityPercent;
            string duraLabel = duraPct > 0.7f ? "良好" : (duraPct > 0.3f ? "磨损" : "严重损坏");
            GUI.color = duraPct > 0.7f ? Color.green : (duraPct > 0.3f ? Color.yellow : Color.red);
            GUILayout.Label($"耐久: {duraLabel} ({duraPct * 100:F0}%)", _normalStyle);
            GUI.color = Color.white;

            GUILayout.Space(8);

            // 已连接终端
            GUILayout.Label("── 电网连接 ──", _normalStyle);
            var linkedTerminals = FindLinkedTerminals();
            if (linkedTerminals.Count == 0)
                GUILayout.Label("  (未连接终端)", _redStyle);
            else
            {
                foreach (var t in linkedTerminals)
                {
                    if (t == null) continue;
                    GUILayout.BeginHorizontal();
                    float d = Vector3.Distance(_currentSource.transform.position, t.transform.position);
                    GUILayout.Label($"  → 终端 ({d:F1}m)  {t.GridPower}W/{t.GridLoad}W", _normalStyle);
                    if (GUILayout.Button("断开", _btnStyle, GUILayout.Width(50), GUILayout.Height(24)))
                    {
                        t.DisconnectSource(_currentSource);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(6);
            int cableCount = CableLinker.CountCables();
            string cableLabel = cableCount > 0 ? $"背包: {cableCount} 根" : "背包: 无电缆";
            GUI.enabled = cableCount > 0;
            if (GUILayout.Button($"⚡ 连接终端 ({cableLabel})", _btnStyle, GUILayout.Height(34)))
            {
                CableLinker.StartLinkingFromSource(_currentSource);
                Hide();
            }
            GUI.enabled = true;

            GUILayout.Space(12);

            // 关闭
            if (GUILayout.Button("关闭", GUILayout.Height(36)))
                Hide();

            GUILayout.EndArea();

            // 点面板外关闭
            if (Event.current.type == EventType.MouseDown &&
                !_panelRect.Contains(Event.current.mousePosition))
                Hide();
        }

        int CountFuelInInventory()
        {
            if (_currentSource.fuelItemData == null) return 0;
            var inv = FindObjectOfType<Systems.Inventory.Inventory>();
            if (inv == null) return 0;
            return inv.GetItemCount(_currentSource.fuelItemData);
        }

        void ConsumeFuelFromInventory()
        {
            if (_currentSource.fuelItemData == null) return;
            var inv = FindObjectOfType<Systems.Inventory.Inventory>();
            if (inv == null) return;

            int consumed = 0;
            // 每次添加1小时对应的燃料量
            int toConsume = Mathf.Max(1, Mathf.CeilToInt(_currentSource.fuelPerHour));
            while (consumed < toConsume && inv.HasItem(_currentSource.fuelItemData, 1))
            {
                inv.RemoveItem(_currentSource.fuelItemData, 1);
                consumed++;
            }
            if (consumed > 0)
            {
                _currentSource.AddFuel((float)consumed / _currentSource.fuelPerHour);
            }
        }

        List<PowerTerminal> FindLinkedTerminals()
        {
            var result = new List<PowerTerminal>();
            var all = FindObjectsOfType<PowerTerminal>();
            foreach (var t in all)
            {
                if (t.connectedSources.Contains(_currentSource))
                    result.Add(t);
            }
            return result;
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
            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold
            };
        }
    }
}
