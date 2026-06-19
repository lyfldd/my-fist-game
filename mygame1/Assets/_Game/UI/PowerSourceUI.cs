using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Systems.Power;

namespace _Game.UI
{
    /// <summary>
    /// 发电设备面板 UI。静态 Show/Hide，UGUI/IMGUI 双模式。
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

        // --- UGUI ---
        private GameObject _canvasGo;
        private GameObject _panelGo, _contentGo, _fuelSection, _fuelBtnGo, _linkBtnGo;
        private Text _titleText, _statusText, _outputText;
        private Text _fuelLabel, _durabilityLabel;
        private Image _fuelFill;
        private RectTransform _terminalListContent;
        private Button _closeBtn, _fuelBtn, _linkBtn;
        private Font _font;

        public static void Show(PowerSource source)
        {
            if (_instance == null)
            {
                var go = new GameObject("PowerSourceUI");
                _instance = go.AddComponent<PowerSourceUI>();
            }
            _instance._currentSource = source;
            _instance._visible = true;
            if (UIModeConfig.UseUGUI && _instance._canvasGo != null)
                _instance._canvasGo.SetActive(true);
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

        void Update()
        {
            if (UIModeConfig.UseUGUI && _visible && _currentSource != null)
                RefreshUGUI();
        }

        // ============================================================
        // UGUI
        // ============================================================

        void CreateUGUI()
        {
            _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

            // Canvas
            _canvasGo = new GameObject("PowerSourceUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            _canvasGo.SetActive(false);

            // 点击面板外关闭的背景
            var bgGo = new GameObject("CloseCatch");
            bgGo.transform.SetParent(_canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.3f);
            bgImg.raycastTarget = true;
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgBtn = bgGo.AddComponent<Button>();
            bgBtn.onClick.AddListener(Hide);

            // 面板
            _panelGo = new GameObject("Panel");
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            var panelImg = _panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            var panelRect = _panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(380, 480);
            panelRect.anchoredPosition = Vector2.zero;

            // 利用 VerticalLayoutGroup 自动排列内容
            var layout = _panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 标题
            _titleText = MakeLabel("Title", _font, 18, FontStyle.Bold, TextAnchor.MiddleCenter, 350, 28);
            _titleText.transform.SetParent(_panelGo.transform, false);

            // 状态
            _statusText = MakeLabel("Status", _font, 14, FontStyle.Normal, TextAnchor.MiddleLeft, 350, 22);
            _statusText.transform.SetParent(_panelGo.transform, false);

            // 输出
            _outputText = MakeLabel("Output", _font, 14, FontStyle.Normal, TextAnchor.MiddleLeft, 350, 22);
            _outputText.transform.SetParent(_panelGo.transform, false);

            // 燃料区 (默认隐藏)
            _fuelSection = new GameObject("FuelSection");
            _fuelSection.transform.SetParent(_panelGo.transform, false);
            var fuelLayout = _fuelSection.AddComponent<VerticalLayoutGroup>();
            fuelLayout.padding = new RectOffset(0, 0, 0, 0);
            fuelLayout.spacing = 4;
            fuelLayout.childAlignment = TextAnchor.UpperLeft;
            fuelLayout.childControlWidth = true;
            fuelLayout.childControlHeight = false;

            var fuelHeader = MakeLabel("FuelHeader", _font, 14, FontStyle.Normal, TextAnchor.MiddleLeft, 350, 20);
            fuelHeader.text = "── 燃料 ──";
            fuelHeader.transform.SetParent(_fuelSection.transform, false);

            _fuelLabel = MakeLabel("FuelLabel", _font, 14, FontStyle.Normal, TextAnchor.MiddleLeft, 350, 20);
            _fuelLabel.transform.SetParent(_fuelSection.transform, false);

            // 进度条
            var barGo = new GameObject("FuelBar");
            barGo.transform.SetParent(_fuelSection.transform, false);
            var barBg = barGo.AddComponent<Image>();
            barBg.color = new Color(0.3f, 0.3f, 0.3f);
            var barRect = barGo.GetComponent<RectTransform>();
            barRect.sizeDelta = new Vector2(350, 16);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barGo.transform, false);
            _fuelFill = fillGo.AddComponent<Image>();
            _fuelFill.color = Color.green;
            _fuelFill.type = Image.Type.Filled;
            _fuelFill.fillMethod = Image.FillMethod.Horizontal;
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            // 添加燃料按钮
            _fuelBtnGo = new GameObject("FuelBtn");
            _fuelBtnGo.transform.SetParent(_fuelSection.transform, false);
            var fuelBtnImg = _fuelBtnGo.AddComponent<Image>();
            fuelBtnImg.color = new Color(0.2f, 0.6f, 0.2f);
            _fuelBtn = _fuelBtnGo.AddComponent<Button>();
            _fuelBtn.onClick.AddListener(() => ConsumeFuelFromInventory());
            var fuelBtnRect = _fuelBtnGo.GetComponent<RectTransform>();
            fuelBtnRect.sizeDelta = new Vector2(350, 34);
            var fuelBtnTxt = MakeBtnLabel("FuelBtnText", _font, 14, "添加燃料");
            fuelBtnTxt.transform.SetParent(_fuelBtnGo.transform, false);
            _fuelSection.SetActive(false);

            // 运行条件
            var condHeader = MakeLabel("CondHeader", _font, 14, FontStyle.Normal, TextAnchor.MiddleLeft, 350, 20);
            condHeader.text = "── 运行条件 ──";
            condHeader.transform.SetParent(_panelGo.transform, false);

            var condText = MakeLabel("CondText", _font, 14, FontStyle.Normal, TextAnchor.MiddleLeft, 350, 80);
            condText.transform.SetParent(_panelGo.transform, false);

            // 耐久
            _durabilityLabel = MakeLabel("Durability", _font, 14, FontStyle.Normal, TextAnchor.MiddleLeft, 350, 22);
            _durabilityLabel.transform.SetParent(_panelGo.transform, false);

            // 电网连接
            var connHeader = MakeLabel("ConnHeader", _font, 14, FontStyle.Normal, TextAnchor.MiddleLeft, 350, 20);
            connHeader.text = "── 电网连接 ──";
            connHeader.transform.SetParent(_panelGo.transform, false);

            // 终端列表 (ScrollView)
            var scrollGo = new GameObject("TerminalScroll");
            scrollGo.transform.SetParent(_panelGo.transform, false);
            var scrollRectComp = scrollGo.AddComponent<ScrollRect>();
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0.1f, 0.1f, 0.15f, 0.5f);
            var scrollRectTrans = scrollGo.GetComponent<RectTransform>();
            scrollRectTrans.sizeDelta = new Vector2(350, 80);

            var scrollContent = new GameObject("Content");
            scrollContent.transform.SetParent(scrollGo.transform, false);
            var contentLayout = scrollContent.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 2;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            var contentFitter = scrollContent.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _terminalListContent = scrollContent.GetComponent<RectTransform>();
            _terminalListContent.anchorMin = new Vector2(0, 1);
            _terminalListContent.anchorMax = new Vector2(1, 1);
            _terminalListContent.pivot = new Vector2(0.5f, 1);
            _terminalListContent.sizeDelta = new Vector2(0, 0);
            scrollRectComp.content = _terminalListContent;
            scrollRectComp.horizontal = false;
            scrollRectComp.vertical = true;

            // 连接终端按钮
            _linkBtnGo = new GameObject("LinkBtn");
            _linkBtnGo.transform.SetParent(_panelGo.transform, false);
            var linkBtnImg = _linkBtnGo.AddComponent<Image>();
            linkBtnImg.color = new Color(0.2f, 0.5f, 0.8f);
            _linkBtn = _linkBtnGo.AddComponent<Button>();
            _linkBtn.onClick.AddListener(() => {
                CableLinker.StartLinkingFromSource(_currentSource);
                Hide();
            });
            var linkBtnRect = _linkBtnGo.GetComponent<RectTransform>();
            linkBtnRect.sizeDelta = new Vector2(350, 34);
            var linkBtnTxt = MakeBtnLabel("LinkBtnText", _font, 14, "⚡ 连接终端");
            linkBtnTxt.transform.SetParent(_linkBtnGo.transform, false);

            // 关闭按钮
            var closeBtnGo = new GameObject("CloseBtn");
            closeBtnGo.transform.SetParent(_panelGo.transform, false);
            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.4f, 0.4f, 0.4f);
            _closeBtn = closeBtnGo.AddComponent<Button>();
            _closeBtn.onClick.AddListener(Hide);
            var closeBtnRect = closeBtnGo.GetComponent<RectTransform>();
            closeBtnRect.sizeDelta = new Vector2(350, 36);
            var closeBtnTxt = MakeBtnLabel("CloseBtnText", _font, 14, "关闭");
            closeBtnTxt.transform.SetParent(closeBtnGo.transform, false);
        }

        Text MakeLabel(string name, Font font, int fontSize, FontStyle style, TextAnchor align, float w, float h)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            var r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(w, h);
            return t;
        }

        Text MakeBtnLabel(string name, Font font, int fontSize, string text)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
            return t;
        }

        void RefreshUGUI()
        {
            var src = _currentSource;
            // 标题
            string typeLabel = src.sourceType switch
            {
                PowerSourceType.Human => "人力",
                PowerSourceType.Solar => "太阳能",
                PowerSourceType.Wind => "风力",
                PowerSourceType.Water => "水力",
                PowerSourceType.Combustion => "燃烧",
                PowerSourceType.Thermal => "热力",
                _ => "未知"
            };
            _titleText.text = $"{typeLabel}发电设备  ({src.maxOutput}W)";

            // 状态
            bool active = src.IsActive;
            _statusText.text = $"状态: {(active ? "● 运转中" : "● 已停摆")}";
            _statusText.color = active ? Color.green : new Color(1f, 0.4f, 0.4f);

            // 输出
            _outputText.text = $"输出: {src.CurrentOutput}W / {src.MaxOutput}W";

            // 燃料
            if (src.requiresFuel)
            {
                _fuelSection.SetActive(true);
                float fuelH = src.FuelRemaining;
                _fuelLabel.text = fuelH > 0f ? $"燃料剩余: {fuelH:F1} 小时" : "燃料剩余: 0 (已耗尽)";
                _fuelLabel.color = fuelH > 2f ? Color.green : (fuelH > 0f ? Color.yellow : Color.red);

                _fuelFill.fillAmount = Mathf.Clamp01(fuelH / 10f);
                _fuelFill.color = fuelH > 2f ? new Color(0.2f, 0.8f, 0.3f) : Color.yellow;

                string fuelName = src.fuelItemName ?? "燃料";
                int fuelCount = CountFuelInInventory();
                _fuelBtn.interactable = fuelCount > 0;
                _fuelBtnGo.GetComponentInChildren<Text>().text = $"添加 {fuelName} (背包: {fuelCount})";
            }
            else
                _fuelSection.SetActive(false);

            // 运行条件 — 找到条件标签并更新
            var condText = _panelGo.transform.Find("CondText")?.GetComponent<Text>();
            if (condText != null)
            {
                var lines = new List<string>();
                if (src.daytimeOnly) lines.Add("  · 需要白天");
                if (src.requiresOpenAir) lines.Add("  · 需要露天");
                if (src.requiresWater) lines.Add("  · 需要水边");
                if (src.noiseRadius > 0f) lines.Add($"  · 噪音半径: {src.noiseRadius}m");
                condText.text = lines.Count > 0 ? string.Join("\n", lines) : "  无特殊要求";
            }

            // 耐久
            float duraPct = src.DurabilityPercent;
            string duraLabel = duraPct > 0.7f ? "良好" : (duraPct > 0.3f ? "磨损" : "严重损坏");
            _durabilityLabel.text = $"耐久: {duraLabel} ({duraPct * 100:F0}%)";
            _durabilityLabel.color = duraPct > 0.7f ? Color.green : (duraPct > 0.3f ? Color.yellow : Color.red);

            // 终端列表
            RefreshTerminalList();

            // 电缆按钮
            int cableCount = CableLinker.CountCables();
            _linkBtn.interactable = cableCount > 0;
            string cableLabel = cableCount > 0 ? $"背包: {cableCount} 根" : "背包: 无电缆";
            _linkBtnGo.GetComponentInChildren<Text>().text = $"⚡ 连接终端 ({cableLabel})";
        }

        void RefreshTerminalList()
        {
            // 清除旧列表
            foreach (Transform child in _terminalListContent)
                Destroy(child.gameObject);

            var linked = FindLinkedTerminals();
            if (linked.Count == 0)
            {
                var t = MakeLabel("NoConn", _font, 14, FontStyle.Normal, TextAnchor.MiddleLeft, 330, 20);
                t.text = "  (未连接终端)";
                t.color = new Color(1f, 0.4f, 0.4f);
                t.transform.SetParent(_terminalListContent, false);
            }
            else
            {
                foreach (var terminal in linked)
                {
                    if (terminal == null) continue;
                    var row = new GameObject("TermRow");
                    row.transform.SetParent(_terminalListContent, false);
                    var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
                    rowLayout.childControlWidth = false;
                    rowLayout.childControlHeight = false;

                    float d = Vector3.Distance(_currentSource.transform.position, terminal.transform.position);
                    var label = MakeLabel("TermLabel", _font, 12, FontStyle.Normal, TextAnchor.MiddleLeft, 240, 22);
                    label.text = $"  → 终端 ({d:F1}m)  {terminal.GridPower}W/{terminal.GridLoad}W";
                    label.transform.SetParent(row.transform, false);

                    var disBtnGo = new GameObject("DiscBtn");
                    disBtnGo.transform.SetParent(row.transform, false);
                    var disBtnImg = disBtnGo.AddComponent<Image>();
                    disBtnImg.color = new Color(0.6f, 0.2f, 0.2f);
                    var disBtn = disBtnGo.AddComponent<Button>();
                    var captured = terminal;
                    disBtn.onClick.AddListener(() => captured.DisconnectSource(_currentSource));
                    var disBtnRect = disBtnGo.GetComponent<RectTransform>();
                    disBtnRect.sizeDelta = new Vector2(50, 22);
                    var disBtnTxt = MakeBtnLabel("DiscBtnTxt", _font, 11, "断开");
                    disBtnTxt.transform.SetParent(disBtnGo.transform, false);
                }
            }
        }

        // ============================================================
        // IMGUI
        // ============================================================

        void OnGUI()
        {
            if (UIModeConfig.UseUGUI) return;
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

            string typeLabel = _currentSource.sourceType switch
            {
                PowerSourceType.Human => "人力", PowerSourceType.Solar => "太阳能",
                PowerSourceType.Wind => "风力", PowerSourceType.Water => "水力",
                PowerSourceType.Combustion => "燃烧", PowerSourceType.Thermal => "热力",
                _ => "未知"
            };
            GUILayout.Label($"{typeLabel}发电设备  ({_currentSource.maxOutput}W)", _headerStyle);
            GUILayout.Space(10);

            bool active = _currentSource.IsActive;
            GUI.color = active ? Color.green : new Color(1f, 0.4f, 0.4f);
            GUILayout.Label($"状态: {(active ? "● 运转中" : "● 已停摆")}", _normalStyle);
            GUI.color = Color.white;
            GUILayout.Space(4);
            GUILayout.Label($"输出: {_currentSource.CurrentOutput}W / {_currentSource.MaxOutput}W", _normalStyle);
            GUILayout.Space(8);

            if (_currentSource.requiresFuel)
            {
                GUILayout.Label("── 燃料 ──", _normalStyle);
                float fuelH = _currentSource.FuelRemaining;
                string fuelLabel = fuelH > 0f ? $"燃料剩余: {fuelH:F1} 小时" : "燃料剩余: 0 (已耗尽)";
                GUI.color = fuelH > 2f ? Color.green : (fuelH > 0f ? Color.yellow : Color.red);
                GUILayout.Label(fuelLabel, _normalStyle);
                GUI.color = Color.white;

                float maxDisplay = 10f;
                float pct = Mathf.Clamp01(fuelH / maxDisplay);
                Rect barRect = GUILayoutUtility.GetRect(200f, 16f);
                GUI.color = new Color(0.3f, 0.3f, 0.3f);
                GUI.DrawTexture(barRect, Texture2D.whiteTexture);
                GUI.color = fuelH > 2f ? new Color(0.2f, 0.8f, 0.3f) : Color.yellow;
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUILayout.Space(6);

                string fuelName = _currentSource.fuelItemName ?? "燃料";
                int fuelCount = CountFuelInInventory();
                GUI.enabled = fuelCount > 0;
                if (GUILayout.Button($"添加 {fuelName} (背包: {fuelCount})", _btnStyle, GUILayout.Height(34)))
                    ConsumeFuelFromInventory();
                GUI.enabled = true;
                GUILayout.Space(8);
            }

            GUILayout.Label("── 运行条件 ──", _normalStyle);
            if (_currentSource.daytimeOnly) GUILayout.Label("  需要白天", _yellowStyle);
            if (_currentSource.requiresOpenAir) GUILayout.Label("  需要露天", _yellowStyle);
            if (_currentSource.requiresWater) GUILayout.Label("  需要水边", _yellowStyle);
            if (_currentSource.noiseRadius > 0f) GUILayout.Label($"  噪音半径: {_currentSource.noiseRadius}m", _normalStyle);
            GUILayout.Space(8);

            float duraPct = _currentSource.DurabilityPercent;
            string duraLabel = duraPct > 0.7f ? "良好" : (duraPct > 0.3f ? "磨损" : "严重损坏");
            GUI.color = duraPct > 0.7f ? Color.green : (duraPct > 0.3f ? Color.yellow : Color.red);
            GUILayout.Label($"耐久: {duraLabel} ({duraPct * 100:F0}%)", _normalStyle);
            GUI.color = Color.white;
            GUILayout.Space(8);

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
                        t.DisconnectSource(_currentSource);
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

            if (GUILayout.Button("关闭", GUILayout.Height(36)))
                Hide();
            GUILayout.EndArea();

            if (Event.current.type == EventType.MouseDown &&
                !_panelRect.Contains(Event.current.mousePosition))
                Hide();
        }

        // ============================================================
        // Shared logic
        // ============================================================

        int CountFuelInInventory()
        {
            if (_currentSource == null || _currentSource.fuelItemData == null) return 0;
            var inv = ServiceLocator.Get<Systems.Inventory.Inventory>();
            if (inv == null) return 0;
            return inv.GetItemCount(_currentSource.fuelItemData);
        }

        void ConsumeFuelFromInventory()
        {
            if (_currentSource == null || _currentSource.fuelItemData == null) return;
            var inv = ServiceLocator.Get<Systems.Inventory.Inventory>();
            if (inv == null) return;

            int consumed = 0;
            int toConsume = Mathf.Max(1, Mathf.CeilToInt(_currentSource.fuelPerHour));
            while (consumed < toConsume && inv.HasItem(_currentSource.fuelItemData, 1))
            {
                inv.RemoveItem(_currentSource.fuelItemData, 1);
                consumed++;
            }
            if (consumed > 0)
                _currentSource.AddFuel((float)consumed / _currentSource.fuelPerHour);
        }

        List<PowerTerminal> FindLinkedTerminals()
        {
            var result = new List<PowerTerminal>();
            var all = ServiceLocator.GetAll<PowerTerminal>();
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
            _greenStyle = new GUIStyle(_normalStyle) { normal = { textColor = Color.green } };
            _redStyle = new GUIStyle(_normalStyle) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
            _yellowStyle = new GUIStyle(_normalStyle) { normal = { textColor = Color.yellow } };
            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
        }
    }
}
