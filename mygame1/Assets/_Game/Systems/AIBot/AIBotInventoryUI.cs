using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人独立背包窗口。6×5格(30格)，200kg负重。使用 GUI.Window 支持拖动。
    /// </summary>
    public class AIBotInventoryUI : MonoBehaviour
    {
        static AIBotInventoryUI _instance;
        AIBotInventory _inventory;
        AIBot _bot;

        bool _visible;
        Rect _panelRect;
        Vector2 _scrollPos;

        const float CELL_SIZE = 48f;
        const float CELL_GAP = 4f;
        const int WINDOW_ID = 910;

        GUIStyle _headerStyle, _normalStyle, _slotStyle, _slotOccupiedStyle, _btnStyle;
        GUIStyle _windowStyle;
        Texture2D _winBg;
        bool _stylesInit;

        public static void Show(AIBot bot, AIBotInventory inventory)
        {
            if (_instance == null)
            {
                var go = new GameObject("AIBotInventoryUI");
                _instance = go.AddComponent<AIBotInventoryUI>();
            }
            _instance._bot = bot;
            _instance._inventory = inventory;
            _instance._visible = true;
            // 重置位置，下次 OnGUI 居中
            _instance._panelRect = new Rect(0, 0, 0, 0);
        }

        public static void Hide()
        {
            if (_instance != null)
                _instance._visible = false;
        }

        void OnGUI()
        {
            if (UIModeConfig.UseUGUI) return;
            if (!_visible || _inventory == null) return;
            InitStyles();

            float gridW = AIBotInventory.GRID_WIDTH * (CELL_SIZE + CELL_GAP) + 20f;
            float gridH = AIBotInventory.GRID_HEIGHT * (CELL_SIZE + CELL_GAP) + 120f;
            float w = Mathf.Max(gridW, 380f);
            float h = gridH;

            // 首次或重置后居中
            if (_panelRect.width < 1f)
                _panelRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            _panelRect = GUI.Window(WINDOW_ID, _panelRect, InventoryWindowFunc,
                "AI机器人 背包", _windowStyle);

            // 点窗口外关闭
            if (Event.current.type == EventType.MouseDown &&
                !_panelRect.Contains(Event.current.mousePosition))
                Hide();
        }

        void InventoryWindowFunc(int id)
        {
            // 负重条
            GUILayout.BeginHorizontal();
            GUILayout.Label($"负重: {_inventory.CurrentWeight:F1}kg / {AIBotInventory.MAX_WEIGHT}kg", _normalStyle);
            float pct = _inventory.WeightPercent;
            Color barColor = pct > 0.8f ? Color.red : (pct > 0.5f ? Color.yellow : Color.green);
            Rect barRect = GUILayoutUtility.GetRect(100f, 14f);
            GUI.color = new Color(0.3f, 0.3f, 0.3f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // 格子网格
            float viewportH = Mathf.Min(AIBotInventory.GRID_HEIGHT * (CELL_SIZE + CELL_GAP), 260f);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(viewportH));
            DrawGrid();
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            if (GUILayout.Button("关闭", _btnStyle, GUILayout.Height(32)))
                Hide();

            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        void DrawGrid()
        {
            float startX = 10f;
            float startY = 4f;

            // 获取窗口在屏幕上的位置，用于修正 tooltip 坐标
            Vector2 windowPos = GUIUtility.GUIToScreenPoint(Vector2.zero);

            var slots = _inventory.GetAllSlots();
            for (int row = 0; row < AIBotInventory.GRID_HEIGHT; row++)
            {
                for (int col = 0; col < AIBotInventory.GRID_WIDTH; col++)
                {
                    int idx = row * AIBotInventory.GRID_WIDTH + col;
                    if (idx >= slots.Count) break;

                    var slot = slots[idx];
                    float cx = startX + col * (CELL_SIZE + CELL_GAP);
                    float cy = startY + row * (CELL_SIZE + CELL_GAP);
                    Rect cellRect = new Rect(cx, cy, CELL_SIZE, CELL_SIZE);

                    GUI.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
                    GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    if (slot.itemData != null)
                    {
                        string label = slot.itemData.itemName.Length > 4
                            ? slot.itemData.itemName.Substring(0, 4)
                            : slot.itemData.itemName;
                        GUI.Label(cellRect, $"{label}\n×{slot.count}", _slotOccupiedStyle);

                        // 修正：mousePosition 是屏幕坐标，cellRect 是窗口本地坐标
                        Vector2 localMouse = Event.current.mousePosition - windowPos;
                        if (cellRect.Contains(localMouse))
                        {
                            Vector2 tooltipPos = Event.current.mousePosition + new Vector2(15, -10);
                            float tw = 160f, th = 36f;
                            Rect tipRect = new Rect(tooltipPos.x, tooltipPos.y, tw, th);
                            GUI.color = new Color(0f, 0f, 0f, 0.85f);
                            GUI.DrawTexture(tipRect, Texture2D.whiteTexture);
                            GUI.color = Color.white;
                            GUI.Label(new Rect(tipRect.x + 4, tipRect.y + 2, tw - 8, th - 4),
                                $"{slot.itemData.itemName}\n重量: {slot.itemData.weight}kg ×{slot.count}",
                                _normalStyle);
                        }
                    }
                }
            }
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
                fontSize = 13, normal = { textColor = Color.white }
            };
            _slotStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray },
                wordWrap = true
            };
            _slotOccupiedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                wordWrap = true
            };
            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold
            };

            _winBg = new Texture2D(1, 1);
            _winBg.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.08f, 0.93f));
            _winBg.Apply();

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _winBg;
            _windowStyle.onNormal.background = _winBg;
            _windowStyle.border = new RectOffset(6, 6, 22, 6);
            _windowStyle.padding = new RectOffset(10, 10, 24, 10);
            _windowStyle.normal.textColor = Color.white;
            _windowStyle.fontSize = 14;
            _windowStyle.fontStyle = FontStyle.Bold;
            _windowStyle.alignment = TextAnchor.UpperCenter;
        }
    }
}
