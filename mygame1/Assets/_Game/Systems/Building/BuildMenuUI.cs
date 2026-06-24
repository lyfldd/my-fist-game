using UnityEngine;
using UnityEngine.UI;
using _Game.Config;
using _Game.Core;
using _Game.UI;
using System.Collections.Generic;

namespace _Game.Systems.Building
{
    public class BuildMenuUI : MonoBehaviour
    {
        [Header("目录引用")]
        public BuildableCatalog catalog;

        [Header("布局参数")]
        public float buttonWidth = 120f;
        public float buttonHeight = 48f;
        public float buttonSpacing = 8f;
        public float bottomMargin = 20f;

        [Header("颜色")]
        public Color panelBackgroundColor = new Color(0f, 0f, 0f, 0.7f);
        public Color normalButtonColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        public Color selectedButtonColor = new Color(0f, 0.6f, 0f, 0.9f);
        public Color lockedButtonColor = new Color(0.2f, 0.15f, 0.15f, 0.7f);
        public Color textColor = Color.white;
        public Color lockedTextColor = new Color(0.7f, 0.5f, 0.5f);

        [Header("材料面板")]
        public Color matPanelBgColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        public float matPanelWidth = 260f;

        [Header("分类标签")]
        public float tabButtonWidth = 100f;
        public float tabButtonHeight = 28f;
        public float tabYOffset = 32f;

        [Header("滚动窗口")]
        public int maxVisibleSlots = 7;
        public int maxVisibleTabs = 7;

        // 运行时状态
        private BuildModeController _controller;
        private bool _isVisible;
        public static bool IsVisible { get; private set; }
        private int _selectedIndex = -1;
        private int _selectedTab;
        private int _scrollOffset;
        private int _tabScrollOffset;
        private BuildableCategory[] _availableCategories;
        private System.Collections.Generic.List<(BuildableCategory cat, int phase)> _currentTabs
            = new System.Collections.Generic.List<(BuildableCategory, int)>();

        // ============================================================
        // UGUI 字段
        // ============================================================
        private GameObject _canvasGo;
        private Font _font;

        // 底部面板
        private GameObject _bottomPanel;
        private Text _pageInfoText;
        private GameObject _tabBar;
        private ScrollRect _tabScrollRect;    // NEW: 标签滚动窗口
        private RectTransform _tabContent;
        private List<UnityEngine.UI.Button> _tabBtns = new List<UnityEngine.UI.Button>();
        private List<GameObject> _itemBtns = new List<GameObject>();
        private ScrollRect _itemScrollRect;   // NEW: 物品滚动窗口
        private RectTransform _itemContent;
        private int _lastTabOffset = -1, _lastScrollOffset = -1, _lastSelectedTab = -1;
        private float _bottomAreaTopY;

        // 材料提示面板
        private GameObject _tooltipPanel;
        private Text _tooltipTitle, _tooltipAttrs, _tooltipSkillReqs;
        private RectTransform _tooltipMatContent;
        private List<Text> _tooltipMatTexts = new List<Text>();
        private BuildableData _lastActiveBuildable;

        // ============================================================
        // 生命周期
        // ============================================================

        private void Awake()
        {
            if (catalog == null) catalog = Resources.Load<BuildableCatalog>("BuildableCatalog");

            if (GetComponent<GhostPreview>() == null)
                gameObject.AddComponent<GhostPreview>();
            if (GetComponent<BuildModeInputLock>() == null)
                gameObject.AddComponent<BuildModeInputLock>();

            _controller = GetComponent<BuildModeController>();
            if (_controller == null)
                _controller = gameObject.AddComponent<BuildModeController>();
        }

        private void Start()
        {
            try
            {
                CreateUGUI();
            }
            catch
            {
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BuildModeEnteredEvent>(OnBuildModeEntered);
            EventBus.Subscribe<BuildModeExitedEvent>(OnBuildModeExited);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BuildModeEnteredEvent>(OnBuildModeEntered);
            EventBus.Unsubscribe<BuildModeExitedEvent>(OnBuildModeExited);
            InputRouter.UnbindAll(this);
        }

        void Update()
        {
            // F7 切换模式时同步 Canvas 显隐
            if (_canvasGo != null)
            {
                bool shouldShow = _isVisible && UIModeConfig.UseUGUI;
                if (_canvasGo.activeSelf != shouldShow)
                    _canvasGo.SetActive(shouldShow);
            }

            if (UIModeConfig.UseUGUI && _isVisible)
            {
                RefreshUGUI();

                // 鼠标逻辑：左键点击菜单外=确认建造，右键=取消
                // ScrollRect 自动处理滚轮事件（鼠标在哪个 ScrollRect 上就滚动哪个）
                if (Input.GetMouseButtonDown(0))
                {
                    if (!IsMouseOverBottomPanel())
                    {
                        if (_controller != null && _controller.CurrentState == BuildModeState.Preview)
                            _controller.TryConfirmBuild();
                    }
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    if (_controller != null && _controller.CurrentState == BuildModeState.Preview)
                        _controller.CancelPreview();
                }
            }
        }

        bool IsMouseOverBottomPanel()
        {
            if (_bottomPanel == null) return false;
            // _bottomAreaTopY 在 RebuildItems 中更新
            Vector2 mp = Input.mousePosition;
            return mp.y <= _bottomAreaTopY;
        }

        private void OnBuildModeEntered(BuildModeEnteredEvent evt)
        {
            _isVisible = true;
            IsVisible = true;
            _scrollOffset = 0;
            _tabScrollOffset = 0;
            _lastTabOffset = -1; _lastScrollOffset = -1; _lastSelectedTab = -1;
            CloseOtherUIs();

            if (UIModeConfig.UseUGUI && _canvasGo != null)
                _canvasGo.SetActive(true);

            UIPanelManager.Instance?.Open("buildMenu", onClose: Hide);

            for (int i = 0; i < maxVisibleSlots; i++)
            {
                int idx = i;
                InputRouter.BindKey(KeyCode.Alpha1 + idx, InputPriority.UI, () => HandleNumberKey(idx), this);
                InputRouter.BindKey(KeyCode.Keypad1 + idx, InputPriority.UI, () => HandleNumberKey(idx), this);
            }
        }

        private void CloseOtherUIs()
        {
            var invUI = ServiceLocator.Get<_Game.UI.InventoryUI>();
            if (invUI != null)
            {
                if (invUI.overviewPanel != null) invUI.overviewPanel.SetActive(false);
                if (invUI.quickPanel != null) invUI.quickPanel.SetActive(false);
                invUI.SendMessage("SetOtherUIVisible", true, SendMessageOptions.DontRequireReceiver);
            }

            var craftSys = _Game.Systems.Crafting.CraftingSystem.Instance;
            var craftUI = ServiceLocator.Get<_Game.Systems.Crafting.CraftingUI>();
            if (craftUI != null)
            {
                EventBus.Publish(new WorkstationClosedEvent(
                    craftSys != null ? craftSys.ActiveStation : Config.WorkstationTier.Hands));
            }

            var containerWin = ServiceLocator.Get<_Game.Systems.WorldContainer.ContainerWindowUI>();
            if (containerWin != null)
                containerWin.CloseWindow();
        }

        private void OnBuildModeExited(BuildModeExitedEvent evt)
        {
            _isVisible = false;
            IsVisible = false;
            if (_canvasGo != null) _canvasGo.SetActive(false);
            UIPanelManager.Instance?.Close("buildMenu");
            InputRouter.UnbindAll(this);
        }

        /// <summary> 关闭建造菜单（标题栏关闭按钮回调） </summary>
        private void Hide()
        {
            EventBus.Publish(new BuildModeExitedEvent());
        }

        bool HandleNumberKey(int slotIndex)
        {
            var items = GetFilteredBuildables();
            int realIndex = _scrollOffset + slotIndex;
            if (realIndex < 0 || realIndex >= items.Length) return false;
            SelectBuildable(items, realIndex);
            _selectedIndex = realIndex;
            return true;
        }

        // ============================================================
        // UGUI — 创建与刷新
        // ============================================================

        void CreateUGUI()
        {
            _font = UGUIBuilder.DefaultFont;

            // Canvas
            _canvasGo = new GameObject("BuildMenuUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            _canvasGo.SetActive(false);

            // ====== 底部面板容器 ======
            _bottomPanel = new GameObject("BottomPanel", typeof(RectTransform));
            _bottomPanel.transform.SetParent(_canvasGo.transform, false);
            var bpRect = _bottomPanel.GetComponent<RectTransform>();
            bpRect.anchorMin = new Vector2(0.5f, 0); bpRect.anchorMax = new Vector2(0.5f, 0);
            bpRect.pivot = new Vector2(0.5f, 0); bpRect.sizeDelta = new Vector2(900, 160);

            // 标题栏（由UIPanelManager统一管理）
            UIPanelManager.AddPanelTitleBar(_bottomPanel, "建造菜单", "buildMenu", onClose: Hide);

            // --- 标题/翻页文字 ---
            _pageInfoText = UguiMakeText("PageInfo", 13, FontStyle.Bold, TextAnchor.MiddleCenter, 600, 22);
            UguiAttach(_pageInfoText, _bottomPanel, 0, -4, 0.5f, 1);

            // --- 分类标签栏（ScrollRect 横向滚动） ---
            float tabW = 100f; float tabH = 28f; float tabSpacing = 6f;
            float tabViewW = maxVisibleTabs * (tabW + tabSpacing) - tabSpacing; // 约 736
            var tabScrollGo = new GameObject("TabScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            tabScrollGo.transform.SetParent(_bottomPanel.transform, false);
            var tsrt = tabScrollGo.GetComponent<RectTransform>();
            tsrt.anchorMin = new Vector2(0.5f, 1); tsrt.anchorMax = new Vector2(0.5f, 1);
            tsrt.pivot = new Vector2(0.5f, 1); tsrt.anchoredPosition = new Vector2(0, -30);
            tsrt.sizeDelta = new Vector2(tabViewW, tabH);
            tabScrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            tabScrollGo.GetComponent<Image>().raycastTarget = true;
            _tabScrollRect = tabScrollGo.GetComponent<ScrollRect>();
            _tabScrollRect.horizontal = true; _tabScrollRect.vertical = false;
            _tabScrollRect.scrollSensitivity = -30f;

            var tabVp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            tabVp.transform.SetParent(tabScrollGo.transform, false);
            UguiSetStretch(tabVp.GetComponent<RectTransform>());
            tabVp.GetComponent<Image>().color = Color.clear;
            tabVp.GetComponent<Image>().raycastTarget = true;
            _tabScrollRect.viewport = tabVp.GetComponent<RectTransform>();

            _tabContent = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            _tabContent.gameObject.transform.SetParent(tabVp.transform, false);
            _tabContent.anchorMin = new Vector2(0, 0.5f); _tabContent.anchorMax = new Vector2(0, 0.5f);
            _tabContent.pivot = new Vector2(0, 0.5f);
            _tabContent.sizeDelta = new Vector2(100, tabH);
            var tabLayout = _tabContent.GetComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = tabSpacing; tabLayout.childAlignment = TextAnchor.MiddleLeft;
            tabLayout.childControlWidth = false; tabLayout.childControlHeight = false;
            tabLayout.childForceExpandWidth = false;
            _tabScrollRect.content = _tabContent;
            _tabBar = _tabContent.gameObject; // 兼容旧引用

            // ScrollRect 滚轮回调 → 触发翻页
            _tabScrollRect.onValueChanged.AddListener(_ => OnTabScrollChanged());

            // --- 物品按钮栏（ScrollRect 横向滚动） ---
            float btnW = 120f; float btnH = 48f; float btnSpacing = 6f;
            float itemViewW = maxVisibleSlots * (btnW + btnSpacing) - btnSpacing; // 约 876
            var itemScrollGo = new GameObject("ItemScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            itemScrollGo.transform.SetParent(_bottomPanel.transform, false);
            var isrt = itemScrollGo.GetComponent<RectTransform>();
            isrt.anchorMin = new Vector2(0.5f, 1); isrt.anchorMax = new Vector2(0.5f, 1);
            isrt.pivot = new Vector2(0.5f, 1); isrt.anchoredPosition = new Vector2(0, -82);
            isrt.sizeDelta = new Vector2(itemViewW, btnH);
            itemScrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            itemScrollGo.GetComponent<Image>().raycastTarget = true;
            _itemScrollRect = itemScrollGo.GetComponent<ScrollRect>();
            _itemScrollRect.horizontal = true; _itemScrollRect.vertical = false;
            _itemScrollRect.scrollSensitivity = -30f;

            var itemVp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            itemVp.transform.SetParent(itemScrollGo.transform, false);
            UguiSetStretch(itemVp.GetComponent<RectTransform>());
            itemVp.GetComponent<Image>().color = Color.clear;
            itemVp.GetComponent<Image>().raycastTarget = true;
            _itemScrollRect.viewport = itemVp.GetComponent<RectTransform>();

            _itemContent = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            _itemContent.gameObject.transform.SetParent(itemVp.transform, false);
            _itemContent.anchorMin = new Vector2(0, 0.5f); _itemContent.anchorMax = new Vector2(0, 0.5f);
            _itemContent.pivot = new Vector2(0, 0.5f);
            _itemContent.sizeDelta = new Vector2(100, btnH);
            var itemLayout2 = _itemContent.GetComponent<HorizontalLayoutGroup>();
            itemLayout2.spacing = btnSpacing; itemLayout2.childAlignment = TextAnchor.MiddleLeft;
            itemLayout2.childControlWidth = false; itemLayout2.childControlHeight = false;
            itemLayout2.childForceExpandWidth = false;
            _itemScrollRect.content = _itemContent;

            // ScrollRect 滚轮回调 → 触发翻页
            _itemScrollRect.onValueChanged.AddListener(_ => OnItemScrollChanged());

            // 翻页提示
            var hintText = UguiMakeText("HintText", 11, FontStyle.Normal, TextAnchor.MiddleCenter, 600, 18);
            UguiAttach(hintText, _bottomPanel, 0, -140, 0.5f, 1);
            hintText.text = "1-7 数字键选择 | 滚轮翻页 | 左键放置 右键取消";
            hintText.color = new Color(0.5f, 0.5f, 0.5f);

            // ====== 材料提示面板（左上角） ======
            _tooltipPanel = new GameObject("TooltipPanel", typeof(RectTransform));
            _tooltipPanel.transform.SetParent(_canvasGo.transform, false);
            var tpRect = _tooltipPanel.GetComponent<RectTransform>();
            tpRect.anchorMin = new Vector2(0, 1); tpRect.anchorMax = new Vector2(0, 1);
            tpRect.pivot = new Vector2(0, 1); tpRect.anchoredPosition = new Vector2(8, -2);
            tpRect.sizeDelta = new Vector2(270, 240);
            var tpImg = _tooltipPanel.AddComponent<Image>();
            tpImg.color = matPanelBgColor;
            tpImg.raycastTarget = false;

            // 内部滚动
            var tpScrollGo = new GameObject("TooltipScroll", typeof(Image), typeof(ScrollRect));
            tpScrollGo.transform.SetParent(_tooltipPanel.transform, false);
            UguiSetStretchMargin(tpScrollGo.GetComponent<RectTransform>(), 4);
            tpScrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var tpScrollRect = tpScrollGo.GetComponent<ScrollRect>();
            tpScrollRect.horizontal = false;
            tpScrollRect.scrollSensitivity = 20f;

            var tpVp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            tpVp.transform.SetParent(tpScrollGo.transform, false);
            UguiSetStretch(tpVp.GetComponent<RectTransform>());
            tpScrollRect.viewport = tpVp.GetComponent<RectTransform>();

            var tpContent = new GameObject("Content", typeof(RectTransform));
            tpContent.transform.SetParent(tpVp.transform, false);
            var tpcRect = tpContent.GetComponent<RectTransform>();
            tpcRect.anchorMin = new Vector2(0, 1); tpcRect.anchorMax = new Vector2(1, 1);
            tpcRect.pivot = new Vector2(0.5f, 1); tpcRect.sizeDelta = new Vector2(0, 300);
            var tpcFitter = tpContent.AddComponent<ContentSizeFitter>();
            tpcFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            tpScrollRect.content = tpcRect;

            _tooltipTitle = UguiMakeText("TpTitle", 14, FontStyle.Bold, TextAnchor.MiddleLeft, 244, 22);
            UguiAttach(_tooltipTitle, tpContent, 4, -2, 0, 1);

            _tooltipAttrs = UguiMakeText("TpAttrs", 11, FontStyle.Normal, TextAnchor.MiddleLeft, 244, 18);
            _tooltipAttrs.color = new Color(0.8f, 0.8f, 0.6f);
            UguiAttach(_tooltipAttrs, tpContent, 4, -26, 0, 1);

            _tooltipSkillReqs = UguiMakeText("TpSkills", 11, FontStyle.Normal, TextAnchor.MiddleLeft, 244, 60);
            _tooltipSkillReqs.color = new Color(0.8f, 0.8f, 0.6f);
            UguiAttach(_tooltipSkillReqs, tpContent, 4, -46, 0, 1);

            var matHeader = UguiMakeText("TpMatHeader", 11, FontStyle.Bold, TextAnchor.MiddleLeft, 244, 18);
            matHeader.text = "材料需求:";
            matHeader.color = new Color(0.8f, 0.8f, 0.6f);
            UguiAttach(matHeader, tpContent, 4, -110, 0, 1);

            var matContainer = new GameObject("TpMatContent", typeof(RectTransform));
            UguiAttach(matContainer, tpContent, 4, -130, 0, 1);
            _tooltipMatContent = matContainer.GetComponent<RectTransform>();
            _tooltipMatContent.sizeDelta = new Vector2(244, 80);
            var mvlg = matContainer.AddComponent<VerticalLayoutGroup>();
            mvlg.spacing = 0; mvlg.childControlWidth = true; mvlg.childControlHeight = false;
            mvlg.childForceExpandHeight = false;
        }

        void RefreshUGUI()
        {
            if (catalog == null || catalog.Count == 0) return;

            // 只在切换分类时重建（创建全部标签/物品，ScrollRect 管理滚动）
            if (_lastSelectedTab != _selectedTab)
            {
                _lastSelectedTab = _selectedTab;
                RebuildTabs();
                RebuildItems();
            }

            // 材料提示
            RefreshTooltip();
        }

        // ScrollRect 滚动时更新标签高亮
        void OnTabScrollChanged()
        {
            if (_tabScrollRect == null || _tabContent == null) return;
            // 根据滚动位置更新标签高亮颜色
            float x = -_tabContent.anchoredPosition.x;
            float itemW = 106f; // 100 + 6 spacing
            int firstVisible = Mathf.RoundToInt(x / itemW);
            _tabScrollOffset = firstVisible;
        }

        // ScrollRect 滚动时更新物品页信息
        void OnItemScrollChanged()
        {
            if (_itemScrollRect == null || _itemContent == null) return;
            float x = -_itemContent.anchoredPosition.x;
            float itemW = 126f; // 120 + 6 spacing
            int firstVisible = Mathf.RoundToInt(x / itemW);
            _scrollOffset = firstVisible;

            var items = GetFilteredBuildables();
            if (items.Length > 0)
            {
                int visibleSlots = Mathf.Min(maxVisibleSlots, items.Length);
                string tabName = (_currentTabs.Count > 0 && _selectedTab >= 0 && _selectedTab < _currentTabs.Count)
                    ? GetCategoryName(_currentTabs[_selectedTab].cat) : "建造";
                _pageInfoText.text = $"建造 [{tabName}] ({items.Length}个, 可见{firstVisible + 1}~{firstVisible + visibleSlots}) 滚轮浏览";
            }
        }

        void RebuildTabs()
        {
            foreach (var b in _tabBtns) { if (b != null) Destroy(b.gameObject); }
            _tabBtns.Clear();

            if (_tabContent == null) return;

            var orderedCats = new BuildableCategory[]
            {
                BuildableCategory.Wall, BuildableCategory.Floor,
                BuildableCategory.Furniture, BuildableCategory.Barricade,
                BuildableCategory.Workstation,
                BuildableCategory.MetalIndustry, BuildableCategory.ElectronicsIndustry,
                BuildableCategory.ChemicalIndustry, BuildableCategory.BioIndustry,
                BuildableCategory.EnergyIndustry,
            };

            _currentTabs.Clear();
            foreach (var cat in orderedCats)
            {
                var items = catalog.GetByCategory(cat);
                if (items != null && items.Length > 0)
                    _currentTabs.Add((cat, -1));
            }

            int tabCount = _currentTabs.Count;
            if (tabCount == 0) return;

            // 创建全部标签（ScrollRect 自动裁剪可见部分）
            for (int tabIdx = 0; tabIdx < tabCount; tabIdx++)
            {
                var (cat, _) = _currentTabs[tabIdx];
                string label = GetCategoryName(cat);
                int count = catalog.GetByCategory(cat).Length;

                var go = new GameObject($"Tab_{tabIdx}", typeof(Image), typeof(UnityEngine.UI.Button));
                go.transform.SetParent(_tabContent.transform, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 28);
                go.GetComponent<Image>().color = _selectedTab == tabIdx ? selectedButtonColor : normalButtonColor;

                var lblGo = new GameObject("Label", typeof(Text));
                lblGo.transform.SetParent(go.transform, false);
                var t = lblGo.GetComponent<Text>();
                t.font = _font; t.fontSize = 12; t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
                t.text = $"{label}({count})";
                UguiSetStretch(lblGo.GetComponent<RectTransform>());

                int captured = tabIdx;
                go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
                {
                    _controller?.CancelPreview();      // 切分类→取消当前选中
                    _selectedTab = captured;
                    _selectedIndex = -1;
                    _scrollOffset = 0;
                    _lastScrollOffset = -1;           // 强制重建物品栏
                    _lastSelectedTab = -1;            // 强制重建标签栏
                });
                _tabBtns.Add(go.GetComponent<UnityEngine.UI.Button>());
            }

            // 更新 Content 宽度以支持滚动
            float tw = 100f; float ts = 6f;
            _tabContent.sizeDelta = new Vector2(tabCount * (tw + ts) - ts, 28);

            if (_selectedTab >= tabCount) _selectedTab = 0;
        }

        void RebuildItems()
        {
            foreach (var go in _itemBtns) { if (go != null) Destroy(go); }
            _itemBtns.Clear();

            if (_itemContent == null) return;

            var items = GetFilteredBuildables();
            int count = items.Length;
            if (count == 0) return;

            string tabName = (_currentTabs.Count > 0 && _selectedTab >= 0 && _selectedTab < _currentTabs.Count)
                ? GetCategoryName(_currentTabs[_selectedTab].cat) : "建造";
            _pageInfoText.text = $"建造 [{tabName}] ({count}个物品，滚轮浏览)";

            // 创建全部物品（ScrollRect 自动裁剪可见部分）
            for (int itemIdx = 0; itemIdx < count; itemIdx++)
            {
                var buildable = items[itemIdx];
                if (buildable == null) continue;

                bool isSelected = (_controller != null && _controller.activeBuildable == buildable);
                bool canBuild = HasMaterials(buildable);

                var go = new GameObject($"Item_{itemIdx}", typeof(Image), typeof(UnityEngine.UI.Button));
                go.transform.SetParent(_itemContent.transform, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 48);

                Color bg = isSelected
                    ? (canBuild ? selectedButtonColor : new Color(0.6f, 0.2f, 0.2f, 0.9f))
                    : (canBuild ? normalButtonColor : lockedButtonColor);
                go.GetComponent<Image>().color = bg;

                var lblGo = new GameObject("Label", typeof(Text));
                lblGo.transform.SetParent(go.transform, false);
                var t = lblGo.GetComponent<Text>();
                t.font = _font; t.fontSize = 12; t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter; t.color = canBuild ? Color.white : lockedTextColor;
                t.raycastTarget = false;
                t.text = !canBuild
                    ? $"[{itemIdx + 1}] {buildable.displayName}\n(材料不足)"
                    : $"[{itemIdx + 1}] {buildable.displayName}";
                UguiSetStretch(lblGo.GetComponent<RectTransform>());

                int captured = itemIdx;
                go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
                {
                    if (isSelected)
                        _controller?.CancelPreview();
                    else
                    {
                        SelectBuildable(items, captured);
                        _selectedIndex = captured;
                    }
                });
                _itemBtns.Add(go);
            }

            // 更新 Content 宽度以支持滚动
            float iw = 120f; float isp = 6f;
            _itemContent.sizeDelta = new Vector2(count * (iw + isp) - isp, 48);

            // 计算底部面板顶部 Y（用于点击外区域检测）
            // _bottomPanel 锚定底部中央，总高 160px，顶部边界 ≈ 170px（加边距）
            _bottomAreaTopY = 180f;
        }

        void RefreshTooltip()
        {
            if (_controller == null || _controller.activeBuildable == null)
            {
                if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
                return;
            }

            var buildable = _controller.activeBuildable;
            if (buildable == _lastActiveBuildable) return; // 未变化，跳过
            _lastActiveBuildable = buildable;

            if (_tooltipPanel != null) _tooltipPanel.SetActive(true);

            var inventory = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();

            _tooltipTitle.text = buildable.displayName;
            _tooltipAttrs.text = $"耗时: {buildable.buildDuration:F1}秒 | 血量: {buildable.maxHealth}";

            // 技能需求
            if (buildable.skillRequirements != null && buildable.skillRequirements.Length > 0)
            {
                var lines = new System.Collections.Generic.List<string>();
                foreach (var req in buildable.skillRequirements)
                    lines.Add($"需要: {req.skill} Lv{req.level}");
                _tooltipSkillReqs.text = string.Join("\n", lines);
            }
            else _tooltipSkillReqs.text = "";

            // 材料
            foreach (var t in _tooltipMatTexts) { if (t != null) Destroy(t.gameObject); }
            _tooltipMatTexts.Clear();
            if (buildable.materials != null)
            {
                foreach (var req in buildable.materials)
                {
                    string matName = req.itemData != null ? req.itemData.itemName : "???";
                    bool hasItem = inventory != null && inventory.HasItem(req.itemData, req.count);
                    var go = new GameObject("MatRow", typeof(Text));
                    go.transform.SetParent(_tooltipMatContent, false);
                    var t = go.GetComponent<Text>();
                    t.font = _font; t.fontSize = 11; t.alignment = TextAnchor.MiddleLeft;
                    t.color = hasItem ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.35f, 0.35f);
                    t.text = $"{(hasItem ? " ✓" : " ✗")} {matName} x{req.count}";
                    t.raycastTarget = false;
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 16);
                    _tooltipMatTexts.Add(t);
                }
            }
        }

        // ============================================================
        // UGUI 辅助方法
        // ============================================================

        static void UguiSetStretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero; }
        static void UguiSetStretchMargin(RectTransform r, float margin)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.sizeDelta = new Vector2(-margin * 2, -margin * 2);
            r.anchoredPosition = new Vector2(margin, margin);
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

        string GetCategoryName(BuildableCategory cat)
        {
            return cat switch
            {
                BuildableCategory.Wall => "墙壁",
                BuildableCategory.Floor => "地板",
                BuildableCategory.Furniture => "家具",
                BuildableCategory.Barricade => "路障",
                BuildableCategory.Workstation => "工作台",
                BuildableCategory.MetalIndustry => "金属工业",
                BuildableCategory.ElectronicsIndustry => "电子工业",
                BuildableCategory.ChemicalIndustry => "化学工业",
                BuildableCategory.BioIndustry => "生物食品",
                BuildableCategory.EnergyIndustry => "能源工业",
                _ => cat.ToString()
            };
        }

        // ============================================================
        // 材料检查
        // ============================================================

        private BuildableData[] GetFilteredBuildables()
        {
            if (_currentTabs.Count == 0)
            {
                if (catalog.buildables == null) return System.Array.Empty<BuildableData>();
                return System.Array.FindAll(catalog.buildables, b => b != null);
            }
            int tab = _selectedTab;
            if (tab < 0 || tab >= _currentTabs.Count)
                tab = 0;

            var (cat, _) = _currentTabs[tab];
            return catalog.GetByCategory(cat);
        }

        private void SelectBuildable(BuildableData[] items, int index)
        {
            if (_controller == null || index < 0 || index >= items.Length) return;
            _controller.SwitchBuildable(items[index]);
            _selectedIndex = index;
        }

        private bool HasMaterials(BuildableData buildable)
        {
            if (buildable.materials == null || buildable.materials.Length == 0)
                return true;

            var inventory = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inventory == null) return true;

            foreach (var req in buildable.materials)
            {
                if (!inventory.HasItem(req.itemData, req.count))
                    return false;
            }
            return true;
        }
    }
}
