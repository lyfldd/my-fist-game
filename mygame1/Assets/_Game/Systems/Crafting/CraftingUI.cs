using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using _Game.Config;
using _Game.Core;
using _Game.UI;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 合成面板 UI — UGUI/IMGUI 双模式。
    /// UGUI 模式下代码自动创建 Canvas，无需手动搭建 Prefab。
    /// </summary>
    public class CraftingUI : MonoBehaviour
    {
        // ============================================================
        // IMGUI 布局参数 (保留)
        // ============================================================
        [Header("IMGUI 布局")]
        public float panelWidth = 640f;
        public float panelHeight = 420f;
        public float recipeListWidth = 220f;
        public float detailWidth = 400f;
        public float buttonHeight = 32f;
        public float padding = 10f;

        [Header("IMGUI 颜色")]
        public Color bgColor = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        public Color recipeNormalColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        public Color recipeSelectedColor = new Color(0f, 0.45f, 0f, 1f);
        public Color textColor = Color.white;
        public Color dimTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        public Color missingMatColor = new Color(0.9f, 0.3f, 0.3f, 1f);

        // ============================================================
        // 内部状态
        // ============================================================
        CraftingSystem _craftingSystem;
        bool _isVisible;
        List<RecipeData> _recipes;
        List<RecipeData> _displayedRecipes;
        RecipeData _selectedRecipe;
        int _selectedIndex = -1;
        int _craftableCount;

        RecipeCategory _activeCategory;
        bool _categoryFilterActive;
        List<RecipeCategory> _availableCategories;
        string _searchText = "";

        // ============================================================
        // UGUI (代码自动创建)
        // ============================================================
        private GameObject _canvasGo, _panelGo;
        private Font _font;

        // 顶部
        private Text _titleText;
        private InputField _searchInput;
        private Button _clearSearchBtn;
        private Text _searchHint;

        // 分类
        private GameObject _categoryBar;
        private List<Button> _categoryBtns = new List<Button>();

        // 配方列表
        private RectTransform _recipeContent;
        private Text _emptyRecipeHint;
        private List<Button> _recipeBtns = new List<Button>();

        // 详情
        private GameObject _detailPanel;
        private Text _detailTitle, _detailDesc, _skillReqsText, _craftInfoText, _craftableCountText;
        private RectTransform _materialsContent;
        private List<Text> _materialTexts = new List<Text>();
        private Button _craftBtn, _batchCraftBtn;
        private Text _craftBtnLabel, _batchBtnLabel;

        static CraftingUI _instance;

        // ============================================================
        // 生命周期
        // ============================================================

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this); return;
            }
            _instance = this;
            ServiceLocator.Register(this);
            _craftingSystem = CraftingSystem.Instance ?? ServiceLocator.Get<CraftingSystem>();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        void Start()
        {
            try
            {
                CreateUGUI();
            }
            catch
            {
            }
        }

        void OnEnable()
        {
            EventBus.Subscribe<WorkstationOpenedEvent>(OnStationOpened);
            EventBus.Subscribe<WorkstationClosedEvent>(OnStationClosed);
            InputRouter.BindKey(KeyCode.Escape, InputPriority.UI, HandleEsc, this);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<WorkstationOpenedEvent>(OnStationOpened);
            EventBus.Unsubscribe<WorkstationClosedEvent>(OnStationClosed);
            InputRouter.UnbindAll(this);
        }

        private bool _needsRefresh = true;
        private int _lastRefreshFrame;

        void Update()
        {
            if (_canvasGo != null)
            {
                bool shouldShow = _isVisible && UIModeConfig.UseUGUI;
                if (_canvasGo.activeSelf != shouldShow) { _canvasGo.SetActive(shouldShow); if (shouldShow) MarkDirty(); }
            }
            // 只脏刷新 + 30帧兜底 (0.5秒)
            if (UIModeConfig.UseUGUI && _isVisible && (_needsRefresh || UnityEngine.Time.frameCount - _lastRefreshFrame > 30))
            {
                _lastRefreshFrame = UnityEngine.Time.frameCount;
                _needsRefresh = false;
                RefreshUGUI();
            }
        }

        void MarkDirty() => _needsRefresh = true;

        // ============================================================
        // UGUI — 创建全部 UI (代码自动搭建)
        // ============================================================

        void CreateUGUI()
        {
            _font = UGUIBuilder.DefaultFont;

            // Canvas
            _canvasGo = new GameObject("CraftingUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            _canvasGo.SetActive(false);

            // 半透明背景（点击穿透，不拦截子元素交互）
            var bgGo = new GameObject("CloseCatch", typeof(RectTransform));
            bgGo.transform.SetParent(_canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.3f);
            bgImg.raycastTarget = false;
            UguiSetStretch(bgGo.GetComponent<RectTransform>());

            // 面板
            _panelGo = new GameObject("Panel", typeof(RectTransform));
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            _panelGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
            UguiSetCenter(_panelGo.GetComponent<RectTransform>(), 640, 420);

            // --- 标题栏（拖拽+关闭按钮） ---
            UIPanelManager.AddPanelTitleBar(_panelGo, "合成面板", "crafting", onClose: Close);
            _titleText = _panelGo.transform.Find("TitleBar/Label")?.GetComponent<Text>();
            UguiMakeBtnLabel(closeGo, "✕", 14);


            // --- 分隔线 ---
            var line = new GameObject("Line", typeof(Image));
            UguiAttach(line, _panelGo, 14, -48, 0, 1);
            line.GetComponent<RectTransform>().sizeDelta = new Vector2(612, 1);
            line.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

            // --- 搜索 ---
            var searchLbl = UguiMakeText("SearchLabel", 12, FontStyle.Normal, TextAnchor.MiddleLeft, 24, 22);
            UguiAttach(searchLbl, _panelGo, 14, -54, 0, 1);
            searchLbl.text = "🔍";

            _searchInput = UguiMakeInput("SearchInput", 200, 22);
            UguiAttach(_searchInput, _panelGo, 38, -54, 0, 1);
            _searchInput.onValueChanged.AddListener(OnSearchChanged);

            _clearSearchBtn = UguiMakeSmallBtn("ClearSearchBtn", "✕", new Color(0.4f, 0.4f, 0.4f), 24, 22);
            UguiAttach(_clearSearchBtn, _panelGo, 242, -54, 0, 1);
            _clearSearchBtn.gameObject.SetActive(false);
            _clearSearchBtn.onClick.AddListener(ClearSearch);

            _searchHint = UguiMakeText("SearchHint", 12, FontStyle.Normal, TextAnchor.MiddleLeft, 120, 22);
            UguiAttach(_searchHint, _panelGo, 270, -54, 0, 1);
            _searchHint.color = dimTextColor;

            // --- 分类栏 ---
            _categoryBar = new GameObject("CategoryBar", typeof(RectTransform));
            UguiAttach(_categoryBar, _panelGo, 14, -82, 0, 1);
            _categoryBar.GetComponent<RectTransform>().sizeDelta = new Vector2(612, 24);
            var catLayout = _categoryBar.AddComponent<HorizontalLayoutGroup>();
            catLayout.spacing = 4; catLayout.childAlignment = TextAnchor.MiddleLeft;
            catLayout.childControlWidth = false; catLayout.childControlHeight = false;

            // "全部" 按钮
            var allBtn = UguiMakeCatBtn("全部");
            allBtn.transform.SetParent(_categoryBar.transform, false);
            allBtn.onClick.AddListener(ClearCategoryFilter);

            // --- 配方列表 ---
            var scrollGo = new GameObject("RecipeScroll", typeof(Image), typeof(ScrollRect));
            UguiAttach(scrollGo, _panelGo, 14, -110, 0, 1);
            scrollGo.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 294);
            scrollGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.8f);
            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 25f;

            // ── Viewport：用 RectMask2D 代替 Mask，不依赖 Image alpha ──
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            UguiSetStretch(viewport.GetComponent<RectTransform>());

            var contentGo = new GameObject("RecipeContent", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            _recipeContent = contentGo.GetComponent<RectTransform>();
            _recipeContent.anchorMin = new Vector2(0, 1); _recipeContent.anchorMax = new Vector2(1, 1);
            _recipeContent.pivot = new Vector2(0.5f, 1); _recipeContent.sizeDelta = new Vector2(0, 0);
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2; vlg.childControlWidth = true; vlg.childControlHeight = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = _recipeContent;

            _emptyRecipeHint = UguiMakeText("EmptyHint", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 200, 24);
            UguiAttach(_emptyRecipeHint, _panelGo, 18, -110, 0, 1);
            _emptyRecipeHint.text = "没有配方"; _emptyRecipeHint.color = dimTextColor;
            _emptyRecipeHint.gameObject.SetActive(false);

            // --- 详情面板（可滚动） ---
            _detailPanel = new GameObject("DetailPanel", typeof(RectTransform));
            UguiAttach(_detailPanel, _panelGo, 242, -110, 0, 1);
            _detailPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(380, 294);

            var detailScrollGo = new GameObject("DetailScroll", typeof(Image), typeof(ScrollRect));
            detailScrollGo.transform.SetParent(_detailPanel.transform, false);
            UguiSetStretch(detailScrollGo.GetComponent<RectTransform>());
            detailScrollGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.5f);
            var detailScrollRect = detailScrollGo.GetComponent<ScrollRect>();
            detailScrollRect.horizontal = false;
            detailScrollRect.scrollSensitivity = 20f;

            var detailVp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            detailVp.transform.SetParent(detailScrollGo.transform, false);
            UguiSetStretch(detailVp.GetComponent<RectTransform>());
            detailScrollRect.viewport = detailVp.GetComponent<RectTransform>();

            // 内容 — 固定高度 500px，绝对定位所有子元素
            var detailContent = new GameObject("DetailContent", typeof(RectTransform));
            detailContent.transform.SetParent(detailVp.transform, false);
            var dcRect = detailContent.GetComponent<RectTransform>();
            dcRect.anchorMin = new Vector2(0, 1); dcRect.anchorMax = new Vector2(1, 1);
            dcRect.pivot = new Vector2(0.5f, 1); dcRect.sizeDelta = new Vector2(0, 500);
            detailScrollRect.content = dcRect;

            // ── 绝对定位的子元素 ──
            _detailTitle = UguiMakeText("DetailTitle", 18, FontStyle.Bold, TextAnchor.MiddleLeft, 360, 24);
            UguiAttach(_detailTitle, detailContent, 4, -6, 0, 1);

            _detailDesc = UguiMakeText("DetailDesc", 12, FontStyle.Normal, TextAnchor.MiddleLeft, 360, 20);
            _detailDesc.color = dimTextColor;
            UguiAttach(_detailDesc, detailContent, 4, -32, 0, 1);

            var divLine = new GameObject("DivLine", typeof(Image));
            UguiAttach(divLine, detailContent, 4, -56, 0, 1);
            divLine.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 1);
            divLine.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f);

            var matHeader = UguiMakeText("MatHeader", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 360, 20);
            matHeader.text = "所需材料:";
            UguiAttach(matHeader, detailContent, 4, -62, 0, 1);

            var matContainer = new GameObject("MaterialsContent", typeof(RectTransform));
            UguiAttach(matContainer, detailContent, 4, -84, 0, 1);
            _materialsContent = matContainer.GetComponent<RectTransform>();
            _materialsContent.sizeDelta = new Vector2(360, 80);
            var mvlg = matContainer.AddComponent<VerticalLayoutGroup>();
            mvlg.spacing = 0; mvlg.childControlWidth = true; mvlg.childControlHeight = false;
            mvlg.childForceExpandHeight = false;

            _skillReqsText = UguiMakeText("SkillReqs", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 360, 50);
            UguiAttach(_skillReqsText, detailContent, 4, -176, 0, 1);

            _craftInfoText = UguiMakeText("CraftInfo", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 360, 18);
            UguiAttach(_craftInfoText, detailContent, 4, -232, 0, 1);

            _craftableCountText = UguiMakeText("CraftableCount", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 360, 18);
            UguiAttach(_craftableCountText, detailContent, 4, -252, 0, 1);

            _craftBtn = UguiMakeBigBtn("CraftBtn", "制作 ×1", new Color(0f, 0.6f, 0f), 160, 36, out _craftBtnLabel);
            UguiAttach(_craftBtn, detailContent, 4, -296, 0, 1);
            _craftBtn.onClick.AddListener(CraftOne);

            _batchCraftBtn = UguiMakeBigBtn("BatchCraftBtn", "全部", new Color(0f, 0.6f, 0f), 120, 36, out _batchBtnLabel);
            UguiAttach(_batchCraftBtn, detailContent, 174, -296, 0, 1);
            _batchCraftBtn.gameObject.SetActive(false);
            _batchCraftBtn.onClick.AddListener(CraftAll);
        }

        // ============================================================
        // UGUI — 辅助创建方法
        // ============================================================

        static void UguiSetStretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero; }
        static void UguiSetCenter(RectTransform r, float w, float h) { r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(w, h); r.anchoredPosition = Vector2.zero; }

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

        InputField UguiMakeInput(string name, float w, float h)
        {
            var go = new GameObject(name, typeof(Image), typeof(InputField));
            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var input = go.GetComponent<InputField>();
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.font = _font; txt.fontSize = 13; txt.alignment = TextAnchor.MiddleLeft;
            txt.color = Color.white; txt.raycastTarget = false;
            txt.supportRichText = false;
            var txtRect = txtGo.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = new Vector2(-8, 0); txtRect.anchoredPosition = new Vector2(4, 0);
            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(go.transform, false);
            var ph = placeholderGo.AddComponent<Text>();
            ph.font = _font; ph.fontSize = 13; ph.fontStyle = FontStyle.Italic;
            ph.alignment = TextAnchor.MiddleLeft; ph.color = new Color(0.5f, 0.5f, 0.5f);
            ph.text = "搜索配方..."; ph.raycastTarget = false;
            var phRect = placeholderGo.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = new Vector2(-8, 0); phRect.anchoredPosition = new Vector2(4, 0);
            input.textComponent = txt; input.placeholder = ph;
            return input;
        }

        Button UguiMakeSmallBtn(string name, string text, Color bg, float w, float h)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = bg;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            UguiMakeBtnLabel(go, text, 11);
            return go.GetComponent<Button>();
        }

        Button UguiMakeCatBtn(string text)
        {
            var go = new GameObject("CatBtn", typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(48, 22);
            UguiMakeBtnLabel(go, text, 11);
            return go.GetComponent<Button>();
        }

        void UguiMakeBtnLabel(GameObject parent, string text, int size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.text = text;
            UguiSetStretch(go.GetComponent<RectTransform>());
        }

        Button UguiMakeBigBtn(string name, string text, Color bg, float w, float h, out Text label)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = bg;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            label = lblGo.AddComponent<Text>();
            label.font = _font; label.fontSize = 15; label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
            label.text = text; label.raycastTarget = false;
            UguiSetStretch(lblGo.GetComponent<RectTransform>());
            return go.GetComponent<Button>();
        }

        // ============================================================
        // UGUI — 刷新
        // ============================================================

        private int _lastRecipeCount = -1;   // 跟踪配方数量变化
        private int _lastSelectedIndex = -2;  // 跟踪选中变化

        Button UguiMakeRecipeBtn(int index)
        {
            var go = new GameObject($"RBtn_{index}", typeof(Image), typeof(Button));
            go.transform.SetParent(_recipeContent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 30);
            var lblGo = new GameObject("Label", typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            var t = lblGo.GetComponent<Text>();
            t.font = _font; t.fontSize = 13; t.alignment = TextAnchor.MiddleLeft;
            t.color = Color.white; t.raycastTarget = false;
            UguiSetStretch(lblGo.GetComponent<RectTransform>());
            return go.GetComponent<Button>();
        }

        void RefreshUGUI()
        {
            if (_craftingSystem == null) return;

            string stationName = GetStationName();
            int total = _recipes?.Count ?? 0, shown = _displayedRecipes?.Count ?? 0;
            string countStr = _categoryFilterActive ? $"{shown}/{total}" : $"{total}";
            _titleText.text = $"{stationName} — 合成 ({countStr} 配方)";

            _searchHint.text = !string.IsNullOrEmpty(_searchText) ? $"匹配 {shown} 个" : "";

            // 分类按钮高亮
            for (int i = 0; i < _categoryBtns.Count && i < (_availableCategories?.Count ?? 0); i++)
            {
                bool active = _categoryFilterActive && _activeCategory == _availableCategories[i];
                _categoryBtns[i].GetComponent<Image>().color = active ? recipeSelectedColor : recipeNormalColor;
            }

            // 只在配方数量变化时重建按钮列表
            int count = _displayedRecipes?.Count ?? 0;
            if (count != _lastRecipeCount)
            {
                _lastRecipeCount = count;
                RebuildRecipeButtons();
            }

            // 选中变化时只更新高亮，不重建
            if (_selectedIndex != _lastSelectedIndex)
            {
                _lastSelectedIndex = _selectedIndex;
                UpdateRecipeHighlights();
            }

            RefreshDetailUGUI();
        }

        void RebuildRecipeButtons()
        {
            foreach (var b in _recipeBtns) { if (b != null) Destroy(b.gameObject); }
            _recipeBtns.Clear();

            var list = _displayedRecipes;

            bool empty = list == null || list.Count == 0;
            _emptyRecipeHint.gameObject.SetActive(empty);
            if (empty) { _lastSelectedIndex = -2; return; }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                int idx = i;
                var btn = UguiMakeRecipeBtn(i);
                btn.GetComponentInChildren<Text>().text = list[i].recipeName;
                btn.GetComponent<Image>().color = (_selectedRecipe == list[i]) ? recipeSelectedColor : recipeNormalColor;
                btn.onClick.AddListener(() => { SelectRecipeByIndex(idx); MarkDirty(); });
                _recipeBtns.Add(btn);
            }
            _lastSelectedIndex = _selectedIndex;
        }

        void UpdateRecipeHighlights()
        {
            var list = _displayedRecipes;
            for (int i = 0; i < _recipeBtns.Count && i < (list?.Count ?? 0); i++)
            {
                if (_recipeBtns[i] == null || list[i] == null) continue;
                _recipeBtns[i].GetComponent<Image>().color = (i == _selectedIndex) ? recipeSelectedColor : recipeNormalColor;
            }
        }

        void RefreshDetailUGUI()
        {
            if (_selectedRecipe == null)
            {
                _detailTitle.text = "← 从左侧列表选择一个配方";
                _detailDesc.text = "";
                _skillReqsText.text = "";
                _craftInfoText.text = "";
                _craftableCountText.text = "";
                _craftBtn.gameObject.SetActive(false);
                _batchCraftBtn.gameObject.SetActive(false);
                foreach (var t in _materialTexts) { if (t != null) Destroy(t.gameObject); }
                _materialTexts.Clear();
                return;
            }

            _craftBtn.gameObject.SetActive(true);
            var r = _selectedRecipe;
            _detailTitle.text = $"《{r.recipeName}》   [{r.category}]";
            _detailDesc.text = r.description ?? "";

            // 材料
            foreach (var t in _materialTexts) { if (t != null) Destroy(t.gameObject); }
            _materialTexts.Clear();
            if (r.materials != null)
            {
                var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
                foreach (var req in r.materials)
                {
                    int owned = inv != null ? inv.GetItemCount(req.itemData) : 0;
                    bool enough = owned >= req.count;
                    var go = new GameObject("MatRow", typeof(Text));
                    go.transform.SetParent(_materialsContent, false);
                    var t = go.GetComponent<Text>();
                    t.font = _font; t.fontSize = 13; t.alignment = TextAnchor.MiddleLeft;
                    t.color = enough ? textColor : missingMatColor;
                    t.text = $"  {req.itemData?.itemName ?? "???"} ×{req.count}   (拥有: {owned})";
                    t.raycastTarget = false;
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 18);
                    _materialTexts.Add(t);
                }
            }

            // 技能需求
            if (r.skillRequirements != null && r.skillRequirements.Length > 0)
            {
                var lines = new List<string> { "技能需求:" };
                foreach (var sk in r.skillRequirements) lines.Add($"  {sk.skill} Lv.{sk.level}");
                _skillReqsText.text = string.Join("\n", lines);
            }
            else _skillReqsText.text = "";

            _craftInfoText.text = $"耗时: {r.craftTime:F1}秒  |  XP: +{r.xpReward:F0}";
            _craftableCount = _craftingSystem.GetCraftableCount(r);
            _craftableCountText.text = $"可制作: {_craftableCount} 次";

            bool canCraft = _craftingSystem.CanCraft(r) && _craftableCount > 0;
            _craftBtn.interactable = canCraft;
            _craftBtnLabel.text = canCraft ? "制作 ×1"
                : (!_craftingSystem.HasMaterialsFor(r) ? "材料不足" : "无法制作");

            _batchCraftBtn.gameObject.SetActive(canCraft && _craftableCount > 1);
            _batchBtnLabel.text = $"全部 ({_craftableCount})";
        }

        // ============================================================
        // UGUI 事件
        // ============================================================

        void OnSearchChanged(string val)
        {
            _searchText = val ?? "";
            _clearSearchBtn.gameObject.SetActive(!string.IsNullOrEmpty(_searchText));
            _selectedRecipe = null; _selectedIndex = -1;
            UpdateDisplayedRecipes();
            MarkDirty();
        }

        void ClearSearch()
        {
            _searchInput.text = "";
            _searchText = "";
            _selectedRecipe = null; _selectedIndex = -1;
            _clearSearchBtn.gameObject.SetActive(false);
            UpdateDisplayedRecipes();
            MarkDirty();
        }

        void CraftOne() { if (_selectedRecipe != null) { DoCraft(_selectedRecipe); MarkDirty(); } }
        void CraftAll()
        {
            if (_selectedRecipe == null) return;
            for (int i = 0; i < _craftableCount; i++)
                if (!_craftingSystem.Craft(_selectedRecipe)) break;
            PostCraftRefresh();
            MarkDirty();
        }

        // ============================================================
        // 共享逻辑
        // ============================================================

        bool HandleEsc() { if (!_isVisible) return false; Close(); return true; }

        void OnStationOpened(WorkstationOpenedEvent evt)
        {
            CloseOtherUIs();
            _isVisible = true;
            if (UIModeConfig.UseUGUI && _canvasGo != null)
                _canvasGo.SetActive(true);
            UIPanelManager.Instance?.Open("crafting", onClose: Close);
            RefreshRecipes();
            MarkDirty();
        }

        void OnStationClosed(WorkstationClosedEvent evt)
        {
            UIPanelManager.Instance?.Close("crafting");
        }

        void CloseOtherUIs()
        {
            var invUI = ServiceLocator.Get<_Game.UI.InventoryUI>();
            if (invUI != null)
            {
                if (invUI.overviewPanel != null) invUI.overviewPanel.SetActive(false);
                if (invUI.quickPanel != null) invUI.quickPanel.SetActive(false);
                invUI.SendMessage("SetOtherUIVisible", true, SendMessageOptions.DontRequireReceiver);
            }
            var bmc = ServiceLocator.Get<_Game.Systems.Building.BuildModeController>();
            if (bmc != null && bmc.IsBuildMode) bmc.ForceExit();
            var containerWin = ServiceLocator.Get<_Game.Systems.WorldContainer.ContainerWindowUI>();
            if (containerWin != null) containerWin.CloseWindow();
        }

        public void Close()
        {
            _isVisible = false;
            _selectedRecipe = null;
            if (_canvasGo != null) _canvasGo.SetActive(false);
            EventBus.Publish(new WorkstationClosedEvent(
                _craftingSystem != null ? _craftingSystem.ActiveStation : WorkstationTier.Hands));
        }

        void RefreshRecipes()
        {
            _recipes = new List<RecipeData>();
            _selectedRecipe = null; _selectedIndex = -1;
            _categoryFilterActive = false; _searchText = "";
            if (_searchInput != null) _searchInput.text = "";
            if (_craftingSystem == null) return;
            try { var all = _craftingSystem.GetAllRecipesForCurrentStation(); if (all != null) _recipes.AddRange(all); }
            catch { }
            BuildAvailableCategories();
            UpdateDisplayedRecipes();
        }

        void BuildAvailableCategories()
        {
            _availableCategories = new List<RecipeCategory>();
            var seen = new HashSet<RecipeCategory>();
            foreach (var r in _recipes) if (r != null && seen.Add(r.category)) _availableCategories.Add(r.category);
            _availableCategories.Sort((a, b) => string.Compare(GetCategoryLabel(a), GetCategoryLabel(b), System.StringComparison.Ordinal));

            // UGUI: 重建分类按钮
            if (UIModeConfig.UseUGUI && _categoryBar != null)
            {
                foreach (var b in _categoryBtns) { if (b != null) Destroy(b.gameObject); }
                _categoryBtns.Clear();
                // 第一个子物体是"全部"按钮（索引0），保持不变
                // 从索引1开始清除旧的分类按钮
                for (int i = _categoryBar.transform.childCount - 1; i >= 1; i--)
                    Destroy(_categoryBar.transform.GetChild(i).gameObject);

                foreach (var cat in _availableCategories)
                {
                    var btn = UguiMakeCatBtn(GetCategoryLabel(cat));
                    btn.transform.SetParent(_categoryBar.transform, false);
                    var captured = cat;
                    btn.onClick.AddListener(() =>
                    {
                        if (_categoryFilterActive && _activeCategory == captured) ClearCategoryFilter();
                        else SetCategoryFilter(captured);
                    });
                    _categoryBtns.Add(btn);
                }
            }
        }

        void UpdateDisplayedRecipes()
        {
            _displayedRecipes = new List<RecipeData>();
            foreach (var r in _recipes)
            {
                if (r == null) continue;
                if (_categoryFilterActive && r.category != _activeCategory) continue;
                if (!string.IsNullOrEmpty(_searchText) && !MatchesSearch(r, _searchText)) continue;
                _displayedRecipes.Add(r);
            }
        }

        bool MatchesSearch(RecipeData recipe, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (recipe.recipeName?.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (recipe.resultItem?.itemName?.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (recipe.materials != null)
                foreach (var m in recipe.materials)
                    if (m.itemData?.itemName?.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        void SetCategoryFilter(RecipeCategory cat)
        {
            _categoryFilterActive = true; _activeCategory = cat;
            _selectedRecipe = null; _selectedIndex = -1;            UpdateDisplayedRecipes();
        }

        void ClearCategoryFilter()
        {
            _categoryFilterActive = false;
            _selectedRecipe = null; _selectedIndex = -1;            UpdateDisplayedRecipes();
        }

        void SelectRecipeByIndex(int index)
        {
            var list = _displayedRecipes;
            if (list == null || index < 0 || index >= list.Count) return;
            _selectedRecipe = list[index]; _selectedIndex = index;
            _craftableCount = _craftingSystem.GetCraftableCount(_selectedRecipe);
        }

        void DoCraft(RecipeData recipe) { _craftingSystem.Craft(recipe); PostCraftRefresh(); }

        void PostCraftRefresh()
        {
            var saved = _selectedRecipe;
            RefreshRecipes();
            if (_displayedRecipes.Contains(saved))
            {
                _selectedRecipe = saved;
                _selectedIndex = _displayedRecipes.IndexOf(saved);
                _craftableCount = _craftingSystem.GetCraftableCount(saved);
            }
            else _selectedRecipe = null;
        }

        static string GetStationName()
        {
            var sys = CraftingSystem.Instance;
            if (sys == null) return "工作台";
            return sys.ActiveStation switch
            {
                WorkstationTier.Hands => "徒手制作", WorkstationTier.Campfire => "篝火",
                WorkstationTier.SimpleBench => "简易工作台", WorkstationTier.Furnace => "熔炉",
                WorkstationTier.MediumBench => "中级工作台", WorkstationTier.AdvancedBench => "高级工作台",
                WorkstationTier.Chemistry => "研究中心", WorkstationTier.Machining => "机械加工台",
                _ => "工作台"
            };
        }

        static string GetCategoryLabel(RecipeCategory cat)
        {
            return cat switch
            {
                RecipeCategory.Tool => "工具", RecipeCategory.Building => "建筑",
                RecipeCategory.Weapon => "武器", RecipeCategory.Armor => "护甲",
                RecipeCategory.Consumable => "药品", RecipeCategory.Ammo => "弹药",
                RecipeCategory.Vehicle => "车辆", RecipeCategory.Material => "材料",
                RecipeCategory.Cooking => "烹饪", RecipeCategory.Smelting => "冶炼",
                RecipeCategory.Industry => "工业", RecipeCategory.Furniture => "家具",
                RecipeCategory.Farming => "农业", RecipeCategory.Defense => "防御",
                _ => cat.ToString()
            };
        }

    }
}