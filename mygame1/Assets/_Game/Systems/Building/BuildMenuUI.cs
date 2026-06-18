using UnityEngine;
using _Game.Config;
using _Game.Core;

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
        private Rect _menuRect;
        private Rect _tabRect;
        private GUIStyle _buttonStyle;
        private GUIStyle _selectedButtonStyle;
        private GUIStyle _lockedButtonStyle;
        private GUIStyle _centeredLabelStyle;
        private GUIStyle _materialLabelStyle;
        private GUIStyle _materialOkStyle;
        private GUIStyle _materialMissingStyle;
        private GUIStyle _pageIndicatorStyle;
        private bool _stylesInitialized;
        private Vector2 _matScroll;

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

        private void OnEnable()
        {
            EventBus.Subscribe<BuildModeEnteredEvent>(OnBuildModeEntered);
            EventBus.Subscribe<BuildModeExitedEvent>(OnBuildModeExited);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BuildModeEnteredEvent>(OnBuildModeEntered);
            EventBus.Unsubscribe<BuildModeExitedEvent>(OnBuildModeExited);
        }

        private void OnBuildModeEntered(BuildModeEnteredEvent evt)
        {
            _isVisible = true;
            IsVisible = true;
            _scrollOffset = 0;
            _tabScrollOffset = 0;
            CloseOtherUIs();

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
            InputRouter.UnbindAll(this);
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
        // OnGUI
        // ============================================================

        private void OnGUI()
        {
            if (UIModeConfig.UseUGUI) return;
            if (!_isVisible || catalog == null || catalog.Count == 0)
                return;

            InitStyles();

            if (Event.current.type == EventType.ScrollWheel)
            {
                if (_tabRect.width > 0 && _tabRect.Contains(Event.current.mousePosition))
                {
                    int maxTabOffset = Mathf.Max(0, _currentTabs.Count - maxVisibleTabs);
                    if (Event.current.delta.y > 0f)
                        _tabScrollOffset = Mathf.Min(_tabScrollOffset + 1, maxTabOffset);
                    else if (Event.current.delta.y < 0f)
                        _tabScrollOffset = Mathf.Max(_tabScrollOffset - 1, 0);
                    Event.current.Use();
                }
                else if (_menuRect.Contains(Event.current.mousePosition))
                {
                    var items = GetFilteredBuildables();
                    int maxOffset = Mathf.Max(0, items.Length - maxVisibleSlots);
                    if (Event.current.delta.y > 0f)
                        _scrollOffset = Mathf.Min(_scrollOffset + 1, maxOffset);
                    else if (Event.current.delta.y < 0f)
                        _scrollOffset = Mathf.Max(_scrollOffset - 1, 0);
                    Event.current.Use();
                }
            }

            DrawCategoryTabs();
            DrawBuildMenu();
            DrawMaterialTooltip();

            if (Event.current.type == EventType.MouseDown)
            {
                if (Event.current.button == 0 && !_menuRect.Contains(Event.current.mousePosition))
                {
                    if (_controller != null && _controller.CurrentState == BuildModeState.Preview)
                        _controller.TryConfirmBuild();
                    Event.current.Use();
                }
                else if (Event.current.button == 1)
                {
                    if (_controller != null && _controller.CurrentState == BuildModeState.Preview)
                    {
                        _controller.CancelPreview();
                        Event.current.Use();
                    }
                }
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = textColor }
            };

            _selectedButtonStyle = new GUIStyle(_buttonStyle);
            _selectedButtonStyle.normal.textColor = Color.white;

            _lockedButtonStyle = new GUIStyle(_buttonStyle);
            _lockedButtonStyle.normal.textColor = lockedTextColor;

            _centeredLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor }
            };

            _materialLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.6f) }
            };

            _materialOkStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.3f, 0.9f, 0.3f) }
            };

            _materialMissingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.9f, 0.35f, 0.35f) }
            };
        }

        // ============================================================
        // 分类标签（按产业链分类，无子标签展开）
        // ============================================================

        private static readonly System.Collections.Generic.Dictionary<BuildableCategory, string> CategoryNames
            = new System.Collections.Generic.Dictionary<BuildableCategory, string>
            {
                { BuildableCategory.Wall, "墙壁" },
                { BuildableCategory.Floor, "地板" },
                { BuildableCategory.Furniture, "家具" },
                { BuildableCategory.Barricade, "路障" },
                { BuildableCategory.Workstation, "工作台" },
                { BuildableCategory.MetalIndustry, "金属工业" },
                { BuildableCategory.ElectronicsIndustry, "电子工业" },
                { BuildableCategory.ChemicalIndustry, "化学工业" },
                { BuildableCategory.BioIndustry, "生物食品" },
                { BuildableCategory.EnergyIndustry, "能源工业" },
            };

        private void DrawCategoryTabs()
        {
            if (catalog == null) return;

            // 按固定顺序构建标签列表
            var orderedCats = new BuildableCategory[]
            {
                BuildableCategory.Wall,
                BuildableCategory.Floor,
                BuildableCategory.Furniture,
                BuildableCategory.Barricade,
                BuildableCategory.Workstation,
                BuildableCategory.MetalIndustry,
                BuildableCategory.ElectronicsIndustry,
                BuildableCategory.ChemicalIndustry,
                BuildableCategory.BioIndustry,
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

            int visibleTabs = Mathf.Min(maxVisibleTabs, tabCount);
            int maxTabOffset = Mathf.Max(0, tabCount - visibleTabs);
            if (_tabScrollOffset > maxTabOffset) _tabScrollOffset = maxTabOffset;

            float totalTabW = visibleTabs * tabButtonWidth + (visibleTabs - 1) * buttonSpacing;
            float tabStartX = (Screen.width - totalTabW) * 0.5f;
            float tabY = Screen.height - tabYOffset - bottomMargin;

            _tabRect = new Rect(tabStartX - 12f, tabY - tabButtonHeight - 4f, totalTabW + 24f, tabButtonHeight + 12f);

            for (int slot = 0; slot < visibleTabs; slot++)
            {
                int tabIdx = _tabScrollOffset + slot;
                if (tabIdx >= tabCount) break;

                var (cat, _) = _currentTabs[tabIdx];
                string label = CategoryNames.TryGetValue(cat, out string cn) ? cn : cat.ToString();
                int count = catalog.GetByCategory(cat).Length;

                float tx = tabStartX + slot * (tabButtonWidth + buttonSpacing);
                Rect r = new Rect(tx, tabY, tabButtonWidth, tabButtonHeight);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = _selectedTab == tabIdx ? selectedButtonColor : normalButtonColor;
                if (GUI.Button(r, $"{label}({count})", _buttonStyle))
                {
                    _selectedTab = tabIdx;
                    _selectedIndex = -1;
                    _scrollOffset = 0;
                }
                GUI.backgroundColor = prev;
            }
        }

        // ============================================================
        // 物品列表
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

        private void DrawBuildMenu()
        {
            var items = GetFilteredBuildables();
            int count = items.Length;
            if (count == 0) return;

            int visibleSlots = Mathf.Min(maxVisibleSlots, count);
            int maxOffset = Mathf.Max(0, count - visibleSlots);
            if (_scrollOffset > maxOffset) _scrollOffset = maxOffset;

            float totalWidth = visibleSlots * buttonWidth + (visibleSlots - 1) * buttonSpacing;
            float startX = (Screen.width - totalWidth) * 0.5f;
            float y = Screen.height - buttonHeight - bottomMargin - 80f;

            float panelX = startX - 12f;
            float panelY = y - 8f;
            float panelW = totalWidth + 24f;
            float panelH = buttonHeight + 16f;

            GUI.color = panelBackgroundColor;
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string tabName = (_currentTabs.Count > 0 && _selectedTab >= 0 && _selectedTab < _currentTabs.Count)
                ? (CategoryNames.TryGetValue(_currentTabs[_selectedTab].cat, out string cn) ? cn : _currentTabs[_selectedTab].cat.ToString()) : "建造";
            string pageInfo = count <= visibleSlots ? "" : $"  ◀ {_scrollOffset + 1}~{_scrollOffset + visibleSlots}/{count} ▶";
            GUI.Label(new Rect(panelX, panelY - 22f, panelW, 20f),
                $"建造 [{tabName}] (滚轮翻页{pageInfo})", _centeredLabelStyle);

            for (int slot = 0; slot < visibleSlots; slot++)
            {
                int itemIdx = _scrollOffset + slot;
                if (itemIdx >= count) break;
                var buildable = items[itemIdx];
                if (buildable == null) continue;

                float x = startX + slot * (buttonWidth + buttonSpacing);
                Rect buttonRect = new Rect(x, y, buttonWidth, buttonHeight);

                bool isSelected = (_controller != null && _controller.activeBuildable == buildable);
                bool canBuild = HasMaterials(buildable);

                Color prevColor = GUI.backgroundColor;
                if (isSelected)
                    GUI.backgroundColor = canBuild ? selectedButtonColor : new Color(0.6f, 0.2f, 0.2f, 0.9f);
                else
                    GUI.backgroundColor = canBuild ? normalButtonColor : lockedButtonColor;

                string label = $"[{slot + 1}] {buildable.displayName}";
                if (!canBuild)
                    label += "\n(材料不足)";

                GUIStyle style = isSelected ? _selectedButtonStyle :
                    (!canBuild ? _lockedButtonStyle : _buttonStyle);

                if (GUI.Button(buttonRect, label, style))
                {
                    if (isSelected)
                        _controller.CancelPreview();
                    else
                    {
                        SelectBuildable(items, itemIdx);
                        _selectedIndex = itemIdx;
                    }
                }

                GUI.backgroundColor = prevColor;
            }

            // 菜单区域 = 标签区 + 按钮区 的并集
            float btnBottom = panelY - 10f;
            float btnTop = panelY + panelH + 30f;
            float menuMinX = _tabRect.width > 0 ? Mathf.Min(panelX, _tabRect.xMin) : panelX;
            float menuMaxX = _tabRect.width > 0 ? Mathf.Max(panelX + panelW, _tabRect.xMax) : panelX + panelW;
            float menuMinY = _tabRect.width > 0 ? Mathf.Min(btnBottom, _tabRect.yMin) : btnBottom;
            float menuMaxY = _tabRect.width > 0 ? Mathf.Max(btnTop, _tabRect.yMax) : btnTop;

            _menuRect = new Rect(menuMinX - 20f, menuMinY - 10f,
                menuMaxX - menuMinX + 40f, menuMaxY - menuMinY + 20f);
        }

        // ============================================================
        // 材料提示
        // ============================================================

        private void DrawMaterialTooltip()
        {
            if (_controller == null || _controller.activeBuildable == null)
                return;

            var buildable = _controller.activeBuildable;
            var inventory = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();

            // 计算内容高度
            int matCount = buildable.materials != null ? buildable.materials.Length : 0;
            int skillLineCount = buildable.skillRequirements != null ? buildable.skillRequirements.Length : 0;
            float headerH = 44f + skillLineCount * 18f;
            float matLineH = 18f;
            float contentH = headerH + matCount * matLineH + 6f;

            float panelY = 2f;
            float panelH = Mathf.Min(contentH + 12f, 260f);
            bool needScroll = contentH + 12f > panelH;

            float panelX = 8f;
            float panelW = matPanelWidth;

            // 面板背景（不透明）
            GUI.color = matPanelBgColor;
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 边框
            GUI.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 1f, panelW, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY, 1f, panelH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX + panelW - 1f, panelY, 1f, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 滚动区域
            float contentW = panelW - 28f;
            Rect viewRect = new Rect(0, 0, contentW, contentH);
            float innerX = panelX + 6f;
            float innerY = panelY + 4f;
            float scrollAreaW = panelW - 8f;
            float scrollAreaH = panelH - 8f - (needScroll ? 4f : 0f);

            _matScroll = GUI.BeginScrollView(
                new Rect(innerX, innerY, scrollAreaW, scrollAreaH),
                _matScroll, viewRect,
                false, true);

            float cy = 0f;

            // 标题
            GUI.Label(new Rect(0, cy, contentW, 20f),
                $"<b>{buildable.displayName}</b>", _centeredLabelStyle);
            cy += 22f;

            // 属性行
            string attrLine = $"耗时: {buildable.buildDuration:F1}秒 | 血量: {buildable.maxHealth}";
            GUI.Label(new Rect(0, cy, contentW, 18f), attrLine, _materialLabelStyle);
            cy += 20f;

            // 技能要求行
            if (buildable.skillRequirements != null && buildable.skillRequirements.Length > 0)
            {
                foreach (var req in buildable.skillRequirements)
                {
                    GUI.Label(new Rect(0, cy, contentW, 18f),
                        $"需要: {req.skill} Lv{req.level}", _materialLabelStyle);
                    cy += 18f;
                }
            }

            // 材料标题
            if (matCount > 0)
            {
                GUI.Label(new Rect(0, cy, contentW, 18f), "<b>材料需求:</b>", _materialLabelStyle);
                cy += 18f;

                for (int i = 0; i < matCount; i++)
                {
                    var req = buildable.materials[i];
                    string matName = req.itemData != null ? req.itemData.itemName : "???";
                    bool hasItem = inventory != null && inventory.HasItem(req.itemData, req.count);
                    GUIStyle style = hasItem ? _materialOkStyle : _materialMissingStyle;
                    string icon = hasItem ? " ✓" : " ✗";
                    GUI.Label(new Rect(2f, cy, contentW, matLineH),
                        $"{icon} {matName} x{req.count}", style);
                    cy += matLineH;
                }
            }

            GUI.EndScrollView();
        }

        // ============================================================
        // 材料检查
        // ============================================================

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
