using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using _Game.Config;
using _Game.Core;
using _Game.UI;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 研究中心科技树面板 v2.0。
    /// 大类可展开子项列表，子项需大类先完成。
    /// </summary>
    public class ChemicalResearchUI : MonoBehaviour
    {
        [Header("布局")]
        public float panelX = 20f, panelY = 80f;
        public float panelWidth = 520f;
        public float rowHeight = 34f;
        public float childIndent = 24f;
        public float padding = 10f;

        bool _isVisible;
        ChemicalResearchManager _manager;
        Inventory.Inventory _inventory;
        ResearchTier _activeTier = ResearchTier.Early;
        HashSet<string> _expandedCategories = new HashSet<string>(); // 已展开的大类ID

        // ============================================================
        // UGUI 字段
        // ============================================================
        private GameObject _canvasGo;
        private Font _font;
        private GameObject _panelGo;
        private Text _titleText;
        private GameObject _tabBar;
        private List<Button> _tabBtns = new List<Button>();
        private RectTransform _rowContent;
        private List<GameObject> _rowGos = new List<GameObject>();
        private int _lastTier = -1;
        private int _lastExpandedHash;

        static readonly ResearchTier[] AllTiers = { ResearchTier.Early, ResearchTier.Mid, ResearchTier.Late, ResearchTier.Endgame };
        static readonly string[] TierNames = { "前期", "中期", "后期", "终局" };
        static readonly Color[] TierColors =
        {
            new Color(0.5f, 0.8f, 0.5f), new Color(0.5f, 0.6f, 1f),
            new Color(0.9f, 0.6f, 0.3f), new Color(1f, 0.35f, 0.35f),
        };

        static ChemicalResearchUI _instance;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            ServiceLocator.Register(this);
            _manager = GetComponent<ChemicalResearchManager>();
            _inventory = GetComponent<Inventory.Inventory>();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        void Start()
        {
            try { CreateUGUI(); }
            catch { }
        }

        void OnEnable()
        {
            EventBus.Subscribe<ResearchStationOpenedEvent>(OnResearchOpened);
            InputRouter.BindKey(KeyCode.Escape, InputPriority.UI, HandleEsc, this);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ResearchStationOpenedEvent>(OnResearchOpened);
            InputRouter.UnbindAll(this);
        }

        void Update()
        {
            if (_canvasGo != null)
            {
                bool shouldShow = _isVisible && UIModeConfig.UseUGUI;
                if (_canvasGo.activeSelf != shouldShow) _canvasGo.SetActive(shouldShow);
            }
            if (UIModeConfig.UseUGUI && _isVisible) RefreshUGUI();
        }

        public void Close() { _isVisible = false; if (_canvasGo != null) _canvasGo.SetActive(false); }
        bool HandleEsc() { if (!_isVisible) return false; Close(); return true; }
        void OnResearchOpened(ResearchStationOpenedEvent evt) { _isVisible = true; if (UIModeConfig.UseUGUI && _canvasGo != null) _canvasGo.SetActive(true); }

        // ============================================================
        // UGUI — 创建与刷新
        // ============================================================

        void CreateUGUI()
        {
            _font = UGUIBuilder.DefaultFont;

            _canvasGo = new GameObject("ChemicalResearchUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 160;
            _canvasGo.SetActive(false);

            // 面板
            _panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            var pr = _panelGo.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0, 1); pr.anchorMax = new Vector2(0, 1);
            pr.pivot = new Vector2(0, 1); pr.anchoredPosition = new Vector2(panelX, -panelY);
            pr.sizeDelta = new Vector2(panelWidth, 400);
            _panelGo.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.95f);

            // 标题行
            var titleGo = new GameObject("TitleBar", typeof(RectTransform));
            titleGo.transform.SetParent(_panelGo.transform, false);
            var tr = titleGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1);
            tr.pivot = new Vector2(0, 1); tr.sizeDelta = new Vector2(0, 28);
            tr.anchoredPosition = new Vector2(padding, -padding);

            _titleText = UguiMakeText("Title", 18, FontStyle.Bold, TextAnchor.MiddleLeft, 300, 28);
            _titleText.transform.SetParent(titleGo.transform, false);
            _titleText.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            _titleText.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
            _titleText.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 0);
            _titleText.color = new Color(0.6f, 1f, 0.6f);
            _titleText.text = "研究中心";

            var closeBtn = UguiMakeSmallBtn("CloseBtn", "✕", new Color(0.5f, 0.2f, 0.2f), 30, 24);
            closeBtn.transform.SetParent(titleGo.transform, false);
            var cbr = closeBtn.GetComponent<RectTransform>();
            cbr.anchorMin = new Vector2(1, 0.5f); cbr.anchorMax = new Vector2(1, 0.5f);
            cbr.pivot = new Vector2(1, 0.5f); cbr.anchoredPosition = Vector2.zero;
            closeBtn.onClick.AddListener(() => Close());

            // 分隔线
            var line = new GameObject("Line", typeof(Image));
            line.transform.SetParent(_panelGo.transform, false);
            var lr = line.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, 1); lr.anchorMax = new Vector2(1, 1);
            lr.pivot = new Vector2(0, 1); lr.sizeDelta = new Vector2(0, 1);
            lr.anchoredPosition = new Vector2(padding, -padding - 30);
            line.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

            // 标签栏
            _tabBar = new GameObject("TabBar", typeof(RectTransform));
            _tabBar.transform.SetParent(_panelGo.transform, false);
            var tbr = _tabBar.GetComponent<RectTransform>();
            tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
            tbr.pivot = new Vector2(0, 1); tbr.sizeDelta = new Vector2(0, 30);
            tbr.anchoredPosition = new Vector2(padding, -padding - 36);
            var tabLayout = _tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4; tabLayout.childAlignment = TextAnchor.MiddleLeft;
            tabLayout.childControlWidth = false; tabLayout.childControlHeight = false;

            // 滚动列表
            var scrollGo = new GameObject("Scroll", typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(_panelGo.transform, false);
            var sr = scrollGo.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(1, 1);
            sr.offsetMin = new Vector2(padding, padding);
            sr.offsetMax = new Vector2(-padding, -padding - 70);
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.scrollSensitivity = 20f;

            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(scrollGo.transform, false);
            UguiSetStretch(vp.GetComponent<RectTransform>());
            scrollRect.viewport = vp.GetComponent<RectTransform>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp.transform, false);
            _rowContent = content.GetComponent<RectTransform>();
            _rowContent.anchorMin = new Vector2(0, 1); _rowContent.anchorMax = new Vector2(1, 1);
            _rowContent.pivot = new Vector2(0.5f, 1); _rowContent.sizeDelta = new Vector2(0, 0);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4; vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = _rowContent;
        }

        void RefreshUGUI()
        {
            if (_manager == null || _manager.Data == null) return;

            var allProjects = _manager.Data.projects;
            if (allProjects == null || allProjects.Length == 0) return;

            // 标签高亮
            for (int i = 0; i < _tabBtns.Count && i < AllTiers.Length; i++)
            {
                bool active = _activeTier == AllTiers[i];
                _tabBtns[i].GetComponent<Image>().color = active ? TierColors[i] : new Color(0.2f, 0.2f, 0.2f, 1f);
            }

            // 列表：只在层级或展开状态变化时重建
            int expandedHash = 0;
            foreach (var id in _expandedCategories) expandedHash ^= id.GetHashCode();
            if (_lastTier != (int)_activeTier || _lastExpandedHash != expandedHash)
            {
                _lastTier = (int)_activeTier;
                _lastExpandedHash = expandedHash;
                RebuildRows(allProjects);
            }
        }

        void RebuildTabs(ChemicalResearchProject[] allProjects)
        {
            foreach (var b in _tabBtns) { if (b != null) Destroy(b.gameObject); }
            _tabBtns.Clear();

            for (int i = 0; i < AllTiers.Length; i++)
            {
                var tier = AllTiers[i];
                int count = 0;
                foreach (var p in allProjects) if (p.tier == tier && p.isCategory) count++;
                int idx = i;

                var go = new GameObject($"Tab_{i}", typeof(Image), typeof(Button));
                go.transform.SetParent(_tabBar.transform, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 26);
                go.GetComponent<Image>().color = _activeTier == tier ? TierColors[i] : new Color(0.2f, 0.2f, 0.2f, 1f);

                var lbl = new GameObject("Label", typeof(Text));
                lbl.transform.SetParent(go.transform, false);
                var t = lbl.GetComponent<Text>();
                t.font = _font; t.fontSize = 13; t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
                t.text = $"{TierNames[i]} ({count})";
                UguiSetStretch(lbl.GetComponent<RectTransform>());

                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _activeTier = AllTiers[idx];
                    _lastTier = -1;
                });
                _tabBtns.Add(go.GetComponent<Button>());
            }
        }

        void RebuildRows(ChemicalResearchProject[] allProjects)
        {
            foreach (var go in _rowGos) { if (go != null) Destroy(go); }
            _rowGos.Clear();

            if (_tabBtns.Count == 0)
                RebuildTabs(allProjects);

            var displayList = BuildDisplayList(allProjects);

            foreach (var entry in displayList)
            {
                var proj = entry.project;
                bool isCategory = proj.isCategory;
                bool done = _manager.IsResearched(proj.researchId);
                bool categoryDone = true;
                if (entry.isChild) categoryDone = _manager.IsCategoryResearched(entry.parentId);
                bool canDo = _manager.CanResearch(proj.researchId, _inventory);
                if (entry.isChild && !categoryDone) canDo = false;

                float indent = entry.isChild ? childIndent : 0f;
                float rowH = rowHeight + 4f;

                var rowGo = new GameObject($"Row_{proj.researchId}", typeof(RectTransform), typeof(Image));
                rowGo.transform.SetParent(_rowContent, false);
                rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, rowH);
                rowGo.GetComponent<Image>().color = isCategory ? new Color(0.12f, 0.14f, 0.12f, 0.5f) : new Color(0.08f, 0.08f, 0.08f, 0.3f);
                var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 2; rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.childControlWidth = false; rowLayout.childControlHeight = true;
                rowLayout.padding = new RectOffset(2, 4, 2, 2);

                // 缩进
                if (indent > 0)
                {
                    var indentGo = new GameObject("Indent", typeof(RectTransform));
                    indentGo.transform.SetParent(rowGo.transform, false);
                    indentGo.GetComponent<RectTransform>().sizeDelta = new Vector2(indent, rowH);
                    indentGo.AddComponent<LayoutElement>().minWidth = indent;
                }

                // 展开/折叠按钮（大类）
                if (isCategory)
                {
                    bool expanded = _expandedCategories.Contains(proj.researchId);
                    string arrow = expanded ? "▼" : "▶";
                    var arrowBtn = UguiMakeSmallBtn($"Arrow_{proj.researchId}", arrow, new Color(0.2f, 0.3f, 0.2f), 22, rowH - 4);
                    arrowBtn.transform.SetParent(rowGo.transform, false);
                    var capId = proj.researchId;
                    arrowBtn.onClick.AddListener(() =>
                    {
                        if (_expandedCategories.Contains(capId)) _expandedCategories.Remove(capId);
                        else _expandedCategories.Add(capId);
                        _lastExpandedHash = -1; // 强制重建
                    });
                }
                else if (entry.isChild)
                {
                    var spGo = new GameObject("Spacer", typeof(RectTransform));
                    spGo.transform.SetParent(rowGo.transform, false);
                    spGo.GetComponent<RectTransform>().sizeDelta = new Vector2(4, rowH);
                    spGo.AddComponent<LayoutElement>().minWidth = 4;
                }

                // 状态图标
                string statusIcon;
                Color statusColor;
                if (done) { statusIcon = "✓"; statusColor = new Color(0.3f, 0.8f, 0.3f); }
                else if (!categoryDone && entry.isChild) { statusIcon = "🔒"; statusColor = new Color(0.8f, 0.7f, 0.4f); }
                else if (canDo) { statusIcon = "→"; statusColor = new Color(0.5f, 1f, 0.5f); }
                else { statusIcon = "✗"; statusColor = new Color(0.8f, 0.7f, 0.4f); }
                var statusText = UguiMakeRowLabel(proj.researchId + "_status", statusIcon, 14, FontStyle.Bold, statusColor, 24);
                statusText.transform.SetParent(rowGo.transform, false);

                // 名称 + 描述 (垂直)
                var nameDescGo = new GameObject("NameDesc", typeof(RectTransform));
                nameDescGo.transform.SetParent(rowGo.transform, false);
                nameDescGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200, rowH);
                nameDescGo.AddComponent<LayoutElement>().flexibleWidth = 1;
                var ndLayout = nameDescGo.AddComponent<VerticalLayoutGroup>();
                ndLayout.childControlWidth = true; ndLayout.childControlHeight = false;
                ndLayout.childForceExpandHeight = false;

                var nameText = UguiMakeRowLabel("Name", proj.displayName, isCategory ? 14 : 13, FontStyle.Bold,
                    isCategory ? new Color(0.8f, 1f, 0.5f) : Color.white, 200);
                nameText.transform.SetParent(nameDescGo.transform, false);

                string desc = proj.description ?? "";
                if (proj.unlockedDeviceNames != null && proj.unlockedDeviceNames.Length > 0)
                    desc += "  [" + string.Join("、", proj.unlockedDeviceNames) + "]";
                if (proj.unlockedRecipeIds != null && proj.unlockedRecipeIds.Length > 0)
                    desc += "  配方:" + string.Join("、", proj.unlockedRecipeIds);
                var descText = UguiMakeRowLabel("Desc", desc, 10, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f), 200);
                descText.transform.SetParent(nameDescGo.transform, false);

                // 费用
                string costStr = "";
                if (proj.cost != null)
                {
                    var parts = new List<string>();
                    foreach (var c in proj.cost) parts.Add($"{ItemName(c.itemData)}×{c.count}");
                    costStr = string.Join(" ", parts);
                }
                var costText = UguiMakeRowLabel("Cost", costStr, 10, FontStyle.Normal, new Color(0.8f, 0.7f, 0.4f), 160);
                costText.transform.SetParent(rowGo.transform, false);

                // 智力
                var intText = UguiMakeRowLabel("Int", $"智{proj.requiredIntellectLevel}", 10, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f), 28);
                intText.transform.SetParent(rowGo.transform, false);

                // 研究按钮 / 已研究标签
                if (!done)
                {
                    var researchBtn = UguiMakeSmallBtn("Research", "研究",
                        canDo ? new Color(0.2f, 0.5f, 0.2f) : new Color(0.3f, 0.3f, 0.3f), 50, rowH - 4);
                    researchBtn.transform.SetParent(rowGo.transform, false);
                    researchBtn.interactable = canDo;
                    var capProj = proj;
                    researchBtn.onClick.AddListener(() => _manager.TryResearch(capProj.researchId, _inventory));
                }
                else
                {
                    var doneText = UguiMakeRowLabel("Done", "已研究", 14, FontStyle.Bold, new Color(0.3f, 0.8f, 0.3f), 50);
                    doneText.transform.SetParent(rowGo.transform, false);
                }

                _rowGos.Add(rowGo);
            }
        }

        // ============================================================
        // UGUI 辅助
        // ============================================================

        static void UguiSetStretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero; }

        Text UguiMakeText(string name, int size, FontStyle style, TextAnchor align, float w, float h)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.alignment = align; t.color = Color.white; t.raycastTarget = false;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            return t;
        }

        Text UguiMakeRowLabel(string name, string text, int size, FontStyle style, Color color, float w)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.alignment = TextAnchor.MiddleLeft; t.color = color; t.raycastTarget = false;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, rowHeight);
            go.GetComponent<LayoutElement>().minWidth = w;
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
            t.font = _font; t.fontSize = 11; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.text = text;
            UguiSetStretch(lblGo.GetComponent<RectTransform>());
            return go.GetComponent<Button>();
        }


        // 显示条目
        struct DisplayEntry
        {
            public ChemicalResearchProject project;
            public bool isChild;
            public string parentId;
        }

        List<DisplayEntry> BuildDisplayList(ChemicalResearchProject[] all)
        {
            var list = new List<DisplayEntry>();
            foreach (var p in all)
            {
                if (p.tier != _activeTier) continue;

                if (p.isCategory)
                {
                    // 大类始终显示
                    list.Add(new DisplayEntry { project = p, isChild = false });

                    // 如果已展开，插入子项
                    if (_expandedCategories.Contains(p.researchId))
                    {
                        var children = _manager.GetChildProjects(p.researchId);
                        foreach (var c in children)
                            list.Add(new DisplayEntry { project = c, isChild = true, parentId = p.researchId });
                    }
                }
                else if (string.IsNullOrEmpty(p.parentResearchId))
                {
                    // 无父类的独立项目（兼容旧数据）
                    list.Add(new DisplayEntry { project = p, isChild = false });
                }
            }
            return list;
        }


        static string ItemName(ItemData item) => item != null ? item.itemName : "???";
    }
}
