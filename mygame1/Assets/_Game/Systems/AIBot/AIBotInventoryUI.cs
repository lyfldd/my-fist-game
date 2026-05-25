using UnityEngine;
using _Game.Config;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人独立背包窗口。OnGUI 渲染，静态 Show/Hide。
    /// 6×5格(30格)，200kg负重。
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

        GUIStyle _headerStyle, _normalStyle, _slotStyle, _slotOccupiedStyle, _btnStyle;
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
        }

        public static void Hide()
        {
            if (_instance != null)
                _instance._visible = false;
        }

        void OnGUI()
        {
            if (!_visible || _inventory == null) return;
            InitStyles();

            float gridW = AIBotInventory.GRID_WIDTH * (CELL_SIZE + CELL_GAP) + 20f;
            float gridH = AIBotInventory.GRID_HEIGHT * (CELL_SIZE + CELL_GAP) + 120f;
            float w = Mathf.Max(gridW, 380f);
            float h = gridH;

            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            _panelRect = new Rect(x, y, w, h);

            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.93f);
            GUI.DrawTexture(_panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_panelRect);
            GUILayout.Space(8);

            GUILayout.Label("AI机器人 背包", _headerStyle);
            GUILayout.Space(4);

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

            // 格子网格（视口比内容小才能滚动）
            float gridContentH = AIBotInventory.GRID_HEIGHT * (CELL_SIZE + CELL_GAP) + 40f;
            float viewportH = Mathf.Min(gridContentH - 20f, 260f);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(viewportH));
            DrawGrid();
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            if (GUILayout.Button("关闭", _btnStyle, GUILayout.Height(32)))
                Hide();

            GUILayout.EndArea();

            // 点面板外关闭
            if (Event.current.type == EventType.MouseDown &&
                !_panelRect.Contains(Event.current.mousePosition))
                Hide();
        }

        void DrawGrid()
        {
            float startX = 10f;
            float startY = 80f;

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

                    // 格子背景
                    GUI.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
                    GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    if (slot.itemData != null)
                    {
                        // 物品名缩写
                        string label = slot.itemData.itemName.Length > 4
                            ? slot.itemData.itemName.Substring(0, 4)
                            : slot.itemData.itemName;
                        GUI.Label(cellRect, $"{label}\n×{slot.count}", _slotOccupiedStyle);

                        // 悬停提示
                        if (cellRect.Contains(Event.current.mousePosition))
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
        }
    }
}
