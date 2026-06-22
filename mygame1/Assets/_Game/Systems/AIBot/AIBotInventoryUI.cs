using UnityEngine;
using UnityEngine.UI;
using _Game.Config;
using _Game.Core;
using _Game.UI;
using System.Collections.Generic;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人独立背包窗口。6×5格(30格)，200kg负重。使用 GUI.Window 支持拖动。
    /// UGUI/IMGUI 双模式。
    /// </summary>
    public class AIBotInventoryUI : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IDragHandler
    {
        static AIBotInventoryUI _instance;
        AIBotInventory _inventory;
        AIBot _bot;

        bool _visible;

        const float CELL_SIZE = 48f;
        const float CELL_GAP = 4f;

        // ============================================================
        // UGUI
        // ============================================================
        private GameObject _canvasGo, _panelGo;
        private Font _font;
        private Text _weightText;
        private Image _weightBar;
        private GameObject _gridContent;
        private List<GameObject> _cellGos = new List<GameObject>();
        private GameObject _tooltipPanel;
        private Text _tooltipText;
        private bool _uguiReady;

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
            if (_instance._canvasGo != null) _instance._canvasGo.SetActive(UIModeConfig.UseUGUI);
            _instance.MarkDirty();
        }

        public static void Hide()
        {
            if (_instance != null)
            {
                _instance._visible = false;
                if (_instance._canvasGo != null) _instance._canvasGo.SetActive(false);
            }
        }

        void Start()
        {
            try { CreateUGUI(); }
            catch (System.Exception e) { Debug.LogError($"[AIBotInventoryUI] UGUI 创建失败: {e.Message}\n{e.StackTrace}"); }
        }

        void OnEnable() { }
        void OnDisable() { InputRouter.UnbindAll(this); }

        void Update()
        {
            if (_canvasGo != null)
            {
                bool shouldShow = _visible && UIModeConfig.UseUGUI;
                if (_canvasGo.activeSelf != shouldShow) _canvasGo.SetActive(shouldShow);
            }
            if (UIModeConfig.UseUGUI && _visible && _inventory != null
                && UnityEngine.Time.frameCount - _lastRefreshFrame > 30)
            {
                _lastRefreshFrame = UnityEngine.Time.frameCount;
                RefreshUGUI();
            }
        }
        private int _lastRefreshFrame;
        public void MarkDirty() { _lastRefreshFrame = 0; }

        // 窗口拖动
        Vector2 _dragOffset;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData evt)
        {
            if (_panelGo != null)
                _dragOffset = (Vector2)_panelGo.GetComponent<RectTransform>().anchoredPosition - evt.position;
        }
        public void OnDrag(UnityEngine.EventSystems.PointerEventData evt)
        {
            if (_panelGo != null)
                _panelGo.GetComponent<RectTransform>().anchoredPosition = evt.position + _dragOffset;
        }

        void CreateUGUI()
        {
            _font = UGUIBuilder.DefaultFont;
            float panelW = 380f, panelH = 380f;

            _canvasGo = new GameObject("AIBotInventoryUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            _canvasGo.SetActive(false);

            _panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            UguiSetCenter(_panelGo.GetComponent<RectTransform>(), panelW, panelH);
            _panelGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.93f);

            // 标题栏（可拖动）
            var titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            titleBar.transform.SetParent(_panelGo.transform, false);
            var tbr = titleBar.GetComponent<RectTransform>();
            tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
            tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 22);
            titleBar.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f);

            var titleText = UguiMakeText("Title", 14, FontStyle.Bold, TextAnchor.MiddleCenter, panelW, 22);
            titleText.transform.SetParent(titleBar.transform, false);
            titleText.text = "AI机器人 背包";
            UguiSetStretch(titleText.GetComponent<RectTransform>());

            // 负重文本
            _weightText = UguiMakeText("Weight", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 180, 20);
            UguiAttach(_weightText, _panelGo, 10, -28, 0, 1);

            // 负重条
            var wbgGo = new GameObject("WeightBg", typeof(Image));
            UguiAttach(wbgGo, _panelGo, 190, -28, 0, 1);
            wbgGo.GetComponent<RectTransform>().sizeDelta = new Vector2(170, 14);
            wbgGo.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

            var wfillGo = new GameObject("WeightFill", typeof(Image));
            wfillGo.transform.SetParent(wbgGo.transform, false);
            _weightBar = wfillGo.GetComponent<Image>();
            var wfr = wfillGo.GetComponent<RectTransform>();
            wfr.anchorMin = Vector2.zero; wfr.anchorMax = new Vector2(0, 1);
            wfr.pivot = new Vector2(0, 0.5f); wfr.sizeDelta = Vector2.zero;

            // 分隔线
            var line = new GameObject("Line", typeof(Image));
            UguiAttach(line, _panelGo, 10, -48, 0, 1);
            line.GetComponent<RectTransform>().sizeDelta = new Vector2(panelW - 20, 1);
            line.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);

            // 网格（ScrollRect）
            var scrollGo = new GameObject("GridScroll", typeof(Image), typeof(ScrollRect));
            UguiAttach(scrollGo, _panelGo, 10, -54, 0, 1);
            scrollGo.GetComponent<RectTransform>().sizeDelta = new Vector2(panelW - 20, 260);
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.horizontal = false; sr.scrollSensitivity = 20f;

            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(scrollGo.transform, false);
            UguiSetStretch(vp.GetComponent<RectTransform>());
            sr.viewport = vp.GetComponent<RectTransform>();

            var content = new GameObject("GridContent", typeof(RectTransform));
            content.transform.SetParent(vp.transform, false);
            _gridContent = content;
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
            cr.pivot = new Vector2(0, 1); cr.sizeDelta = new Vector2(0, AIBotInventory.GRID_HEIGHT * (CELL_SIZE + CELL_GAP));
            sr.content = cr;

            // 关闭按钮
            var closeBtn = UguiMakeSmallBtn("CloseBtn", "关闭", new Color(0.3f, 0.3f, 0.3f), 100, 32);
            UguiAttach(closeBtn, _panelGo, 0, 28, 0.5f, 0);
            closeBtn.onClick.AddListener(() => Hide());

            // 提示面板（默认隐藏）
            _tooltipPanel = new GameObject("Tooltip", typeof(RectTransform), typeof(Image));
            _tooltipPanel.transform.SetParent(_canvasGo.transform, false);
            _tooltipPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.85f);
            _tooltipPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 40);
            _tooltipPanel.SetActive(false);

            _tooltipText = UguiMakeText("TpText", 11, FontStyle.Normal, TextAnchor.MiddleLeft, 152, 36);
            _tooltipText.transform.SetParent(_tooltipPanel.transform, false);
            UguiAttach(_tooltipText, _tooltipPanel, 4, -2, 0, 1);
            _tooltipText.color = Color.white;

            _uguiReady = true;
        }

        void RefreshUGUI()
        {
            float pct = _inventory.WeightPercent;
            _weightText.text = $"负重: {_inventory.CurrentWeight:F1}kg / {AIBotInventory.MAX_WEIGHT}kg";

            Color barColor = pct > 0.8f ? Color.red : (pct > 0.5f ? Color.yellow : Color.green);
            _weightBar.color = barColor;
            _weightBar.GetComponent<RectTransform>().anchorMax = new Vector2(pct, 1);

            // 重建格子（简单重建）
            foreach (var go in _cellGos) { if (go != null) Destroy(go); }
            _cellGos.Clear();

            var slots = _inventory.GetAllSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                int row = i / AIBotInventory.GRID_WIDTH;
                int col = i % AIBotInventory.GRID_WIDTH;
                float cx = col * (CELL_SIZE + CELL_GAP);
                float cy = -row * (CELL_SIZE + CELL_GAP);

                var cellGo = new GameObject($"Cell_{i}", typeof(RectTransform), typeof(Image));
                cellGo.transform.SetParent(_gridContent.transform, false);
                var cellR = cellGo.GetComponent<RectTransform>();
                cellR.anchorMin = new Vector2(0, 1); cellR.anchorMax = new Vector2(0, 1);
                cellR.pivot = new Vector2(0, 1); cellR.sizeDelta = new Vector2(CELL_SIZE, CELL_SIZE);
                cellR.anchoredPosition = new Vector2(cx, cy);
                cellGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

                if (slot.itemData != null)
                {
                    string label = slot.itemData.itemName.Length > 4
                        ? slot.itemData.itemName.Substring(0, 4) : slot.itemData.itemName;
                    var cellText = UguiMakeText($"Text_{i}", 10, FontStyle.Normal, TextAnchor.MiddleCenter, CELL_SIZE - 4, CELL_SIZE - 4);
                    cellText.color = Color.white;
                    cellText.text = $"{label}\n×{slot.count}";
                    cellText.raycastTarget = true;
                    cellText.transform.SetParent(cellGo.transform, false);
                    var ctr = cellText.GetComponent<RectTransform>();
                    ctr.anchorMin = Vector2.zero; ctr.anchorMax = Vector2.one;
                    ctr.sizeDelta = new Vector2(-4, -4); ctr.anchoredPosition = Vector2.zero;

                    // Hover tooltip via EventTrigger
                    var trigger = cellGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                    var enter = new UnityEngine.EventSystems.EventTrigger.Entry();
                    enter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
                    int capIdx = i;
                    enter.callback.AddListener((_) => ShowTooltip(slot, cellGo));
                    trigger.triggers.Add(enter);

                    var exit = new UnityEngine.EventSystems.EventTrigger.Entry();
                    exit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
                    exit.callback.AddListener((_) => _tooltipPanel.SetActive(false));
                    trigger.triggers.Add(exit);
                }
                _cellGos.Add(cellGo);
            }

            // 点击空白关闭
            if (Input.GetMouseButtonDown(0))
            {
                var mp = Input.mousePosition;
                var pr = _panelGo.GetComponent<RectTransform>();
                if (!RectTransformUtility.RectangleContainsScreenPoint(pr, mp))
                    Hide();
            }
        }

        void ShowTooltip(AIBotInventorySlot slot, GameObject cell)
        {
            if (_tooltipPanel == null || slot.itemData == null) return;
            _tooltipText.text = $"{slot.itemData.itemName}\n重量: {slot.itemData.weight}kg ×{slot.count}";
            Vector3 worldPos = cell.transform.position + new Vector3(CELL_SIZE + 10, 0, 0);
            _tooltipPanel.transform.position = worldPos;
            _tooltipPanel.SetActive(true);
        }

        // ============================================================
        // UGUI 辅助
        // ============================================================

        static void UguiSetStretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero; }
        static void UguiSetCenter(RectTransform r, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(w, h); r.anchoredPosition = Vector2.zero;
        }
        static void UguiAttach(GameObject go, GameObject parent, float x, float y, float anchorX, float anchorY)
        {
            go.transform.SetParent(parent.transform, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(anchorX, anchorY);
            r.pivot = new Vector2(anchorX, anchorY);
            r.anchoredPosition = new Vector2(x, y);
        }
        static void UguiAttach(Component c, GameObject parent, float x, float y, float anchorX, float anchorY)
            => UguiAttach(c.gameObject, parent, x, y, anchorX, anchorY);

        Text UguiMakeText(string name, int size, FontStyle style, TextAnchor align, float w, float h)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.alignment = align; t.color = Color.white; t.raycastTarget = false;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            return t;
        }

        Button UguiMakeSmallBtn(string name, string text, Color bg, float w, float h)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = bg;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var lblGo = new GameObject("Label", typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            var t = lblGo.GetComponent<Text>();
            t.font = _font; t.fontSize = 12; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.text = text;
            UguiSetStretch(lblGo.GetComponent<RectTransform>());
            return go.GetComponent<Button>();
        }

        // ============================================================
    }
}
