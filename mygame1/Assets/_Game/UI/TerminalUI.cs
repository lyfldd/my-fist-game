using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Systems.Power;

namespace _Game.UI
{
    /// <summary>
    /// 用电终端面板 UI。静态方法 Show/Hide，UGUI。
    /// </summary>
    public class TerminalUI : MonoBehaviour
    {
        static TerminalUI _instance;
        PowerTerminal _currentTerminal;

        bool _visible;

        // --- UGUI ---
        private GameObject _canvasGo, _panelGo;
        private Text _titleText, _powerStatsText, _gridStatusText;
        private Text _sourceHeaderText, _consumerHeaderText, _connHeaderText;
        private RectTransform _sourceListContent, _consumerListContent, _connListContent;
        private Button _linkBtn, _disconnectAllBtn;
        private Font _font;

        public static void Show(PowerTerminal terminal)
        {
            if (_instance == null)
            {
                var go = new GameObject("TerminalUI");
                _instance = go.AddComponent<TerminalUI>();
            }
            _instance._currentTerminal = terminal;
            _instance._visible = true;
            if (UIModeConfig.UseUGUI && _instance._canvasGo != null)
                _instance._canvasGo.SetActive(true);
            _instance.MarkDirty();
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (UIModeConfig.UseUGUI && _instance._canvasGo != null)
                _instance._canvasGo.SetActive(false);
        }

        void Start()
        {
            if (UIModeConfig.UseUGUI)
                CreateUGUI();
        }

        private int _lastRefreshFrame;

        void Update()
        {
            // 仅 30 帧兜底刷新；操作时通过 MarkDirty() 触发
            if (UIModeConfig.UseUGUI && _visible && _currentTerminal != null
                && UnityEngine.Time.frameCount - _lastRefreshFrame > 30)
            {
                _lastRefreshFrame = UnityEngine.Time.frameCount;
                RefreshUGUI();
            }
        }

        void MarkDirty() { /* 操作时调 RefreshUGUI 直接刷新 */ RefreshUGUI(); }

        // ============================================================
        // UGUI
        // ============================================================

        void CreateUGUI()
        {
            _font = UGUIBuilder.DefaultFont;

            _canvasGo = new GameObject("TerminalUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasGo.GetComponent<Canvas>().sortingOrder = 200;
            _canvasGo.SetActive(false);

            // 背景按钮（点外部关闭）
            var bgGo = new GameObject("CloseCatch");
            bgGo.transform.SetParent(_canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.3f);
            bgGo.AddComponent<Button>().onClick.AddListener(Hide);
            SetStretch(bgGo.GetComponent<RectTransform>());

            // 面板
            _panelGo = new GameObject("Panel");
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            _panelGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);
            SetCenterSize(_panelGo.GetComponent<RectTransform>(), 400, 560);
            var layout = _panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 4;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            // 标题
            _titleText = MakeLabel("Title", 18, FontStyle.Bold, TextAnchor.MiddleCenter, 370, 28);
            _titleText.transform.SetParent(_panelGo.transform, false);

            // 发电端表头
            _sourceHeaderText = MakeLabel("SrcHeader", 14, FontStyle.Normal, TextAnchor.MiddleLeft, 370, 20);
            _sourceHeaderText.text = "── 发电端 ──";
            _sourceHeaderText.transform.SetParent(_panelGo.transform, false);

            // 发电端列表
            _sourceListContent = MakeScrollList("SourceScroll", _panelGo.transform, 370, 70, out _);

            // 功率统计
            _powerStatsText = MakeLabel("PowerStats", 14, FontStyle.Normal, TextAnchor.MiddleLeft, 370, 22);
            _powerStatsText.transform.SetParent(_panelGo.transform, false);

            // 电网状态
            _gridStatusText = MakeLabel("GridStatus", 14, FontStyle.Normal, TextAnchor.MiddleLeft, 370, 22);
            _gridStatusText.transform.SetParent(_panelGo.transform, false);

            // 设备表头
            _consumerHeaderText = MakeLabel("ConsHeader", 14, FontStyle.Normal, TextAnchor.MiddleLeft, 370, 20);
            _consumerHeaderText.text = "── 范围内设备 ──";
            _consumerHeaderText.transform.SetParent(_panelGo.transform, false);

            // 设备列表 (大滚动区)
            _consumerListContent = MakeScrollList("ConsumerScroll", _panelGo.transform, 370, 140, out _);

            // 连接终端表头
            _connHeaderText = MakeLabel("ConnHeader", 14, FontStyle.Normal, TextAnchor.MiddleLeft, 370, 20);
            _connHeaderText.text = "── 连接终端 ──";
            _connHeaderText.transform.SetParent(_panelGo.transform, false);

            // 连接终端列表
            _connListContent = MakeScrollList("ConnScroll", _panelGo.transform, 370, 60, out _);

            // 连接操作区
            var linkSection = MakeLabel("LinkHeader", 14, FontStyle.Normal, TextAnchor.MiddleLeft, 370, 20);
            linkSection.text = "── 连接操作 ──";
            linkSection.transform.SetParent(_panelGo.transform, false);

            // 连接按钮
            _linkBtn = MakePanelButton("LinkBtn", "⚡ 连接发电端 / 终端", new Color(0.2f, 0.5f, 0.8f), 370, 30, () =>
            {
                CableLinker.StartLinking(_currentTerminal);
                Hide();
            });
            _linkBtn.transform.SetParent(_panelGo.transform, false);

            // 断开全部按钮
            _disconnectAllBtn = MakePanelButton("DiscAllBtn", "🔌 断开所有连接", new Color(0.6f, 0.3f, 0.2f), 370, 30, () =>
            {
                _currentTerminal.connectedSources.Clear();
                var copy = new List<PowerTerminal>(_currentTerminal.connectedTerminals);
                foreach (var t in copy) _currentTerminal.DisconnectTerminal(t);
            });
            _disconnectAllBtn.transform.SetParent(_panelGo.transform, false);

            // 关闭按钮
            var closeBtn = MakePanelButton("CloseBtn", "关闭", new Color(0.4f, 0.4f, 0.4f), 370, 36, Hide);
            closeBtn.transform.SetParent(_panelGo.transform, false);
        }

        // --- UGUI helpers ---

        Text MakeLabel(string name, int size, FontStyle style, TextAnchor anchor, float w, float h)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.alignment = anchor; t.color = Color.white; t.raycastTarget = false;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            return t;
        }

        RectTransform MakeScrollList(string name, Transform parent, float w, float h, out ScrollRect scroll)
        {
            var sgo = new GameObject(name);
            sgo.transform.SetParent(parent, false);
            sgo.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.5f);
            sgo.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            scroll = sgo.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;

            var content = new GameObject("Content");
            content.transform.SetParent(sgo.transform, false);
            var ctRect = content.GetComponent<RectTransform>();
            ctRect.anchorMin = new Vector2(0, 1); ctRect.anchorMax = new Vector2(1, 1);
            ctRect.pivot = new Vector2(0.5f, 1); ctRect.sizeDelta = new Vector2(0, 0);
            content.AddComponent<VerticalLayoutGroup>().spacing = 2;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = ctRect;
            return ctRect;
        }

        Button MakePanelButton(string name, string text, Color bg, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.AddComponent<Image>().color = bg;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var t = lbl.AddComponent<Text>();
            t.font = _font; t.fontSize = 14; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            t.text = text; t.raycastTarget = false;
            SetStretch(lbl.GetComponent<RectTransform>());
            return btn;
        }

        // --- RectTransform helpers ---
        static void SetStretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero; }
        static void SetCenterSize(RectTransform r, float w, float h) { r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(w, h); r.anchoredPosition = Vector2.zero; }

        void RefreshUGUI()
        {
            var term = _currentTerminal;
            _titleText.text = $"电网终端  供电半径: {term.supplyRadius}m";

            // 发电端列表
            RefreshSourceList();

            // 功率 + 状态
            _powerStatsText.text = $"总功率: {term.GridPower}W  |  总负载: {term.GridLoad}W";
            if (term.GridPower <= 0)
            {
                _gridStatusText.text = "状态: ● 无电源";
                _gridStatusText.color = new Color(1f, 0.4f, 0.4f);
            }
            else if (term.IsGridOk)
            {
                _gridStatusText.text = $"状态: ● 正常 (余量 {term.GridPower - term.GridLoad}W)";
                _gridStatusText.color = Color.green;
            }
            else
            {
                _gridStatusText.text = $"状态: ● 超载 (缺 {term.GridLoad - term.GridPower}W)";
                _gridStatusText.color = new Color(1f, 0.4f, 0.4f);
            }

            // 设备列表
            RefreshConsumerList();

            // 连接终端列表
            RefreshConnTerminalList();

            // 电缆按钮
            int cableCount = CableLinker.CountCables();
            _linkBtn.interactable = cableCount > 0;
            _linkBtn.GetComponentInChildren<Text>().text =
                $"⚡ 连接发电端 / 终端 (背包: {(cableCount > 0 ? $"{cableCount} 根电缆" : "无电缆")})";

            // 断开全部按钮
            _disconnectAllBtn.gameObject.SetActive(
                term.connectedSources.Count > 0 || term.connectedTerminals.Count > 0);
        }

        void RefreshSourceList()
        {
            ClearContent(_sourceListContent);
            if (_currentTerminal.connectedSources.Count == 0)
            {
                var t = MakeLabel("NoSrc", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 340, 20);
                t.text = "  (无连接发电端)"; t.color = new Color(1f, 0.4f, 0.4f);
                t.transform.SetParent(_sourceListContent, false);
            }
            else
            {
                for (int i = _currentTerminal.connectedSources.Count - 1; i >= 0; i--)
                {
                    var src = _currentTerminal.connectedSources[i];
                    if (src == null) { _currentTerminal.connectedSources.RemoveAt(i); continue; }
                    var row = new GameObject("SrcRow");
                    row.transform.SetParent(_sourceListContent, false);
                    row.AddComponent<HorizontalLayoutGroup>().spacing = 6;
                    string status = src.IsActive ? "✓" : "✗";
                    string fuel = src.requiresFuel ? $" (燃料: {src.FuelRemaining:F1}h)" : "";
                    var label = MakeLabel("SrcLbl", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 260, 22);
                    label.text = $"  {src.sourceType}  {src.maxOutput}W {status}{fuel}";
                    label.transform.SetParent(row.transform, false);
                    var captured = src;
                    MakeRowButton("DiscSrc", "断开", new Color(0.6f, 0.2f, 0.2f), 50, 22,
                        () => _currentTerminal.DisconnectSource(captured)).transform.SetParent(row.transform, false);
                }
            }
        }

        void RefreshConsumerList()
        {
            ClearContent(_consumerListContent);
            var consumers = _currentTerminal.ConsumersInRange;
            if (consumers == null || consumers.Count == 0)
            {
                var t = MakeLabel("NoCons", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 340, 20);
                t.text = "  (无设备)"; t.transform.SetParent(_consumerListContent, false);
            }
            else
            {
                for (int i = consumers.Count - 1; i >= 0; i--)
                {
                    var c = consumers[i];
                    if (c == null) continue;
                    var row = new GameObject("ConsRow");
                    row.transform.SetParent(_consumerListContent, false);
                    row.AddComponent<HorizontalLayoutGroup>().spacing = 4;
                    string icon = c.IsRunning ? "✓" : "✗";
                    var label = MakeLabel("ConsLbl", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 160, 22);
                    label.text = $"{icon} {c.DisplayName}";
                    label.color = c.IsRunning ? Color.green : new Color(1f, 0.4f, 0.4f);
                    label.transform.SetParent(row.transform, false);
                    var pwrLbl = MakeLabel("ConsPwr", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 80, 22);
                    pwrLbl.text = $"功耗: {c.requiredPower}W";
                    pwrLbl.transform.SetParent(row.transform, false);
                    var captured = c;
                    if (c.IsManuallyOff)
                        MakeRowButton("OnBtn", "开启", new Color(0.3f, 0.7f, 0.3f), 50, 22,
                            () => captured.IsManuallyOff = false).transform.SetParent(row.transform, false);
                    else
                        MakeRowButton("OffBtn", "关闭", new Color(0.7f, 0.3f, 0.3f), 50, 22,
                            () => captured.IsManuallyOff = true).transform.SetParent(row.transform, false);
                }
            }
        }

        void RefreshConnTerminalList()
        {
            ClearContent(_connListContent);
            if (_currentTerminal.connectedTerminals.Count == 0)
            {
                var t = MakeLabel("NoConn", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 340, 20);
                t.text = "  (无连接)"; t.transform.SetParent(_connListContent, false);
            }
            else
            {
                for (int i = _currentTerminal.connectedTerminals.Count - 1; i >= 0; i--)
                {
                    var ct = _currentTerminal.connectedTerminals[i];
                    if (ct == null) { _currentTerminal.connectedTerminals.RemoveAt(i); continue; }
                    var row = new GameObject("ConnRow");
                    row.transform.SetParent(_connListContent, false);
                    row.AddComponent<HorizontalLayoutGroup>().spacing = 6;
                    float dist = Vector3.Distance(_currentTerminal.transform.position, ct.transform.position);
                    var label = MakeLabel("ConnLbl", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 260, 22);
                    label.text = $"  → 终端 ({dist:F1}m)  半径{ct.supplyRadius}m";
                    label.transform.SetParent(row.transform, false);
                    var captured = ct;
                    MakeRowButton("DiscConn", "断开", new Color(0.6f, 0.2f, 0.2f), 50, 22,
                        () => _currentTerminal.DisconnectTerminal(captured)).transform.SetParent(row.transform, false);
                }
            }
        }

        Button MakeRowButton(string name, string text, Color bg, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.AddComponent<Image>().color = bg;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var lbl = new GameObject("Lbl");
            lbl.transform.SetParent(go.transform, false);
            var t = lbl.AddComponent<Text>();
            t.font = _font; t.fontSize = 12; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            t.text = text; t.raycastTarget = false;
            SetStretch(lbl.GetComponent<RectTransform>());
            return btn;
        }

        void ClearContent(RectTransform content)
        {
            foreach (Transform child in content) Destroy(child.gameObject);
        }

    }
}
