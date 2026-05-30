using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 合成面板 UI（OnGUI 原型）。
    ///
    /// 流程：E键工作台 → WorkstationOpenedEvent → 显示面板
    ///       选中配方 → 显示详情 → 点击制作 → CraftingSystem.Craft()
    ///       Esc → WorkstationClosedEvent → 隐藏面板
    /// </summary>
    public class CraftingUI : MonoBehaviour
    {
        [Header("布局")]
        public float panelWidth = 640f;
        public float panelHeight = 420f;
        public float recipeListWidth = 220f;
        public float detailWidth = 400f;
        public float buttonHeight = 32f;
        public float padding = 10f;

        [Header("颜色")]
        public Color bgColor = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        public Color headerColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        public Color recipeNormalColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        public Color recipeSelectedColor = new Color(0f, 0.45f, 0f, 1f);
        public Color craftButtonColor = new Color(0f, 0.6f, 0f, 1f);
        public Color disabledButtonColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        public Color textColor = Color.white;
        public Color dimTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        public Color missingMatColor = new Color(0.9f, 0.3f, 0.3f, 1f);

        CraftingSystem _craftingSystem;
        bool _isVisible;
        List<RecipeData> _recipes;
        List<RecipeData> _displayedRecipes;
        RecipeData _selectedRecipe;
        int _selectedIndex = -1;
        Vector2 _scrollPos;
        int _craftableCount;
        Rect _recipeListRect; // 用于滚轮区域检测

        // 分类筛选
        RecipeCategory _activeCategory;
        bool _categoryFilterActive;
        List<RecipeCategory> _availableCategories;

        // 搜索
        string _searchText = "";
        bool _searchFocused;

        GUIStyle _headerStyle, _recipeStyle, _selectedRecipeStyle, _detailLabelStyle;
        GUIStyle _craftBtnStyle, _disabledBtnStyle, _closeBtnStyle, _dimLabelStyle;
        GUIStyle _categoryBtnStyle, _categoryActiveBtnStyle;
        bool _stylesReady;

        void Awake()
        {
            _craftingSystem = CraftingSystem.Instance;
            if (_craftingSystem == null)
                _craftingSystem = Object.FindObjectOfType<CraftingSystem>();
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

        bool HandleEsc()
        {
            if (!_isVisible) return false;
            Close();
            return true;
        }

        void OnStationOpened(WorkstationOpenedEvent evt)
        {
            CloseOtherUIs();
            _isVisible = true;
            RefreshRecipes();
        }

        void CloseOtherUIs()
        {
            // 关闭背包面板
            var invUI = Object.FindObjectOfType<_Game.UI.InventoryUI>();
            if (invUI != null)
            {
                if (invUI.overviewPanel != null) invUI.overviewPanel.SetActive(false);
                if (invUI.quickPanel != null) invUI.quickPanel.SetActive(false);
                invUI.SendMessage("SetOtherUIVisible", true, SendMessageOptions.DontRequireReceiver);
            }

            // 退出建造模式
            var bmc = Object.FindObjectOfType<_Game.Systems.Building.BuildModeController>();
            if (bmc != null && bmc.IsBuildMode)
                bmc.ForceExit();

            // 关闭容器窗口
            var containerWin = Object.FindObjectOfType<_Game.Systems.WorldContainer.ContainerWindowUI>();
            if (containerWin != null)
                containerWin.CloseWindow();
        }

        void OnStationClosed(WorkstationClosedEvent evt)
        {
            _isVisible = false;
            _selectedRecipe = null;
        }

        public void Close()
        {
            _isVisible = false;
            _selectedRecipe = null;
            EventBus.Publish(new WorkstationClosedEvent(
                _craftingSystem != null ? _craftingSystem.ActiveStation : WorkstationTier.Hands));
        }

        void RefreshRecipes()
        {
            _recipes = new List<RecipeData>();
            _selectedRecipe = null;
            _selectedIndex = -1;
            _categoryFilterActive = false;
            _searchText = "";
            if (_craftingSystem == null) return;

            try
            {
                var all = _craftingSystem.GetAllRecipesForCurrentStation();
                if (all != null) _recipes.AddRange(all);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CraftingUI] 获取配方失败: {e.Message}");
            }

            BuildAvailableCategories();
            UpdateDisplayedRecipes();
        }

        void BuildAvailableCategories()
        {
            _availableCategories = new List<RecipeCategory>();
            var seen = new HashSet<RecipeCategory>();
            foreach (var r in _recipes)
            {
                if (r != null && seen.Add(r.category))
                    _availableCategories.Add(r.category);
            }
            _availableCategories.Sort((a, b) => string.Compare(
                GetCategoryLabel(a), GetCategoryLabel(b), System.StringComparison.Ordinal));
        }

        void UpdateDisplayedRecipes()
        {
            _displayedRecipes = new List<RecipeData>();
            foreach (var r in _recipes)
            {
                if (r == null) continue;

                // 分类筛选
                if (_categoryFilterActive && r.category != _activeCategory)
                    continue;

                // 搜索筛选
                if (!string.IsNullOrEmpty(_searchText))
                {
                    if (!MatchesSearch(r, _searchText))
                        continue;
                }

                _displayedRecipes.Add(r);
            }
        }

        bool MatchesSearch(RecipeData recipe, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;

            // 配方名匹配
            if (recipe.recipeName != null &&
                recipe.recipeName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // 产出物匹配
            if (recipe.resultItem != null && recipe.resultItem.itemName != null &&
                recipe.resultItem.itemName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // 材料匹配
            if (recipe.materials != null)
            {
                foreach (var m in recipe.materials)
                {
                    if (m.itemData != null && m.itemData.itemName != null &&
                        m.itemData.itemName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }

        void SetCategoryFilter(RecipeCategory cat)
        {
            _categoryFilterActive = true;
            _activeCategory = cat;
            _selectedRecipe = null;
            _selectedIndex = -1;
            _scrollPos = Vector2.zero;
            UpdateDisplayedRecipes();
        }

        void ClearCategoryFilter()
        {
            _categoryFilterActive = false;
            _selectedRecipe = null;
            _selectedIndex = -1;
            _scrollPos = Vector2.zero;
            UpdateDisplayedRecipes();
        }

        void OnGUI()
        {
            if (!_isVisible || _craftingSystem == null) return;
            InitStyles();

            Rect panelRect = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth, panelHeight);

            GUI.color = bgColor;
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawHeader(panelRect);
            DrawCategoryBar(panelRect);
            DrawRecipeList(panelRect);
            DrawRecipeDetail(panelRect);

            // 滚轮切换：鼠标在配方列表区域内
            var dispList = _displayedRecipes;
            if (Event.current.type == EventType.ScrollWheel && dispList != null && dispList.Count > 1)
            {
                Vector2 smp = new Vector2(Event.current.mousePosition.x, Screen.height - Event.current.mousePosition.y);
                if (_recipeListRect.Contains(smp))
                {
                    int dir = Event.current.delta.y > 0f ? 1 : -1;
                    int newIdx = _selectedIndex + dir;
                    if (newIdx >= dispList.Count) newIdx = 0;
                    if (newIdx < 0) newIdx = dispList.Count - 1;
                    SelectRecipeByIndex(newIdx);
                    Event.current.Use();
                }
            }
        }

        void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor }
            };

            _recipeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor },
                padding = new RectOffset(8, 8, 4, 4)
            };

            _selectedRecipeStyle = new GUIStyle(_recipeStyle)
            {
                normal = { textColor = Color.white }
            };

            _detailLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor },
                wordWrap = true, padding = new RectOffset(6, 6, 3, 3)
            };

            _dimLabelStyle = new GUIStyle(_detailLabelStyle)
            {
                fontSize = 12,
                normal = { textColor = dimTextColor }
            };

            _craftBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(8, 8, 6, 6)
            };

            _disabledBtnStyle = new GUIStyle(_craftBtnStyle)
            {
                normal = { textColor = dimTextColor }
            };

            _closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor }
            };

            _categoryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = dimTextColor },
                padding = new RectOffset(6, 6, 2, 2),
                fixedHeight = 20
            };

            _categoryActiveBtnStyle = new GUIStyle(_categoryBtnStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        void DrawHeader(Rect panel)
        {
            string stationName = _craftingSystem.ActiveStation switch
            {
                WorkstationTier.Hands => "徒手制作",
                WorkstationTier.Campfire => "篝火",
                WorkstationTier.SimpleBench => "简易工作台",
                WorkstationTier.Furnace => "熔炉",
                WorkstationTier.MediumBench => "中级工作台",
                WorkstationTier.AdvancedBench => "高级工作台",
                WorkstationTier.Chemistry => "研究中心",
                WorkstationTier.Machining => "机械加工台",
                _ => "工作台"
            };

            Rect headerRect = new Rect(panel.x + padding, panel.y + padding,
                panel.width - padding * 2, 30f);
            int totalCount = _recipes != null ? _recipes.Count : 0;
            int shownCount = _displayedRecipes != null ? _displayedRecipes.Count : 0;
            string countStr = _categoryFilterActive
                ? $"{shownCount}/{totalCount} 配方"
                : $"{totalCount} 配方";
            GUI.Label(headerRect, $"  {stationName} — 合成 ({countStr})", _headerStyle);

            // 关闭按钮
            Rect closeRect = new Rect(panel.x + panel.width - 60f, panel.y + padding, 48f, 28f);
            if (GUI.Button(closeRect, "✕", _closeBtnStyle))
                Close();

            // 分割线
            Rect lineRect = new Rect(panel.x + padding, headerRect.y + headerRect.height + 2f,
                panel.width - padding * 2, 2f);
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            GUI.DrawTexture(lineRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 搜索栏
            float searchY = lineRect.y + 4f;
            Rect searchLabelRect = new Rect(panel.x + padding, searchY, 30f, 22f);
            GUI.Label(searchLabelRect, "🔍", _dimLabelStyle);

            Rect searchFieldRect = new Rect(panel.x + padding + 32f, searchY, 200f, 22f);
            GUI.SetNextControlName("CraftingSearch");
            string newSearch = GUI.TextField(searchFieldRect, _searchText, 20, GUI.skin.textField);

            // 搜索文本变化时更新列表
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                _selectedRecipe = null;
                _selectedIndex = -1;
                UpdateDisplayedRecipes();
            }

            // 清空按钮
            if (!string.IsNullOrEmpty(_searchText))
            {
                Rect clearRect = new Rect(panel.x + padding + 236f, searchY, 30f, 22f);
                if (GUI.Button(clearRect, "✕", _categoryBtnStyle))
                {
                    _searchText = "";
                    _selectedRecipe = null;
                    _selectedIndex = -1;
                    UpdateDisplayedRecipes();
                }
            }

            // 搜索结果提示
            if (!string.IsNullOrEmpty(_searchText))
            {
                int matchCount = _displayedRecipes != null ? _displayedRecipes.Count : 0;
                Rect hintRect = new Rect(panel.x + padding + 270f, searchY, 120f, 22f);
                GUI.Label(hintRect, $"匹配 {matchCount} 个", _dimLabelStyle);
            }
        }

        void DrawCategoryBar(Rect panel)
        {
            if (_availableCategories == null || _availableCategories.Count <= 1) return;

            float barX = panel.x + padding;
            float barY = panel.y + padding + 62f;
            float barW = panel.width - padding * 2;

            // 全部按钮
            float btnX = barX;
            bool isAll = !_categoryFilterActive;
            GUI.backgroundColor = isAll ? recipeSelectedColor : recipeNormalColor;
            if (GUI.Button(new Rect(btnX, barY, 40f, 22f), "全部",
                isAll ? _categoryActiveBtnStyle : _categoryBtnStyle))
            {
                ClearCategoryFilter();
            }
            btnX += 44f;

            // 各分类按钮
            float maxX = barX + barW;
            foreach (var cat in _availableCategories)
            {
                string label = GetCategoryLabel(cat);
                float btnW = _categoryBtnStyle.CalcSize(new GUIContent(label)).x + 12f;
                if (btnW < 44f) btnW = 44f;

                if (btnX + btnW > maxX) break; // 超出不画

                bool active = _categoryFilterActive && _activeCategory == cat;
                GUI.backgroundColor = active ? recipeSelectedColor : recipeNormalColor;
                if (GUI.Button(new Rect(btnX, barY, btnW, 22f), label,
                    active ? _categoryActiveBtnStyle : _categoryBtnStyle))
                {
                    if (active)
                        ClearCategoryFilter();
                    else
                        SetCategoryFilter(cat);
                }
                btnX += btnW + 4f;
            }

            GUI.backgroundColor = Color.white;
        }

        static string GetCategoryLabel(RecipeCategory cat)
        {
            return cat switch
            {
                RecipeCategory.Tool => "工具",
                RecipeCategory.Building => "建筑",
                RecipeCategory.Weapon => "武器",
                RecipeCategory.Armor => "护甲",
                RecipeCategory.Consumable => "药品",
                RecipeCategory.Ammo => "弹药",
                RecipeCategory.Vehicle => "车辆",
                RecipeCategory.Material => "材料",
                RecipeCategory.Cooking => "烹饪",
                RecipeCategory.Smelting => "冶炼",
                RecipeCategory.Industry => "工业",
                RecipeCategory.Furniture => "家具",
                RecipeCategory.Farming => "农业",
                RecipeCategory.Defense => "防御",
                _ => cat.ToString()
            };
        }

        void DrawRecipeList(Rect panel)
        {
            float listX = panel.x + padding;
            float listY = panel.y + padding + 90f;
            float listH = panel.height - padding * 2 - 94f;

            // 列表背景
            Rect listBg = new Rect(listX, listY, recipeListWidth, listH);
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.8f);
            GUI.DrawTexture(listBg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 记录列表区域（屏幕坐标，用于滚轮检测）
            _recipeListRect = new Rect(listX, Screen.height - (listY + listH), recipeListWidth, listH);

            var dispList = _displayedRecipes;
            if (dispList == null || dispList.Count == 0)
            {
                GUI.Label(new Rect(listX + 8f, listY + 8f, recipeListWidth - 16f, 24f),
                    "没有配方", _dimLabelStyle);
                return;
            }

            float totalH = dispList.Count * (buttonHeight + 2f);
            float viewH = listH - 8f;
            _scrollPos = GUI.BeginScrollView(
                new Rect(listX, listY + 4f, recipeListWidth, viewH),
                _scrollPos,
                new Rect(0, 0, recipeListWidth - 20f, totalH));

            for (int i = 0; i < dispList.Count; i++)
            {
                var recipe = dispList[i];
                if (recipe == null) continue;

                Rect btnRect = new Rect(2f, i * (buttonHeight + 2f), recipeListWidth - 24f, buttonHeight);
                bool isSelected = _selectedRecipe == recipe;

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = isSelected ? recipeSelectedColor : recipeNormalColor;

                if (GUI.Button(btnRect, recipe.recipeName, isSelected ? _selectedRecipeStyle : _recipeStyle))
                {
                    SelectRecipeByIndex(i);
                }

                GUI.backgroundColor = prev;
            }

            GUI.EndScrollView();
        }

        void DrawRecipeDetail(Rect panel)
        {
            float detailX = panel.x + padding + recipeListWidth + 8f;
            float detailY = panel.y + padding + 90f;
            float detailW = detailWidth;
            float detailH = panel.height - padding * 2 - 94f;

            // 详情背景
            Rect detailBg = new Rect(detailX, detailY, detailW, detailH);
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.8f);
            GUI.DrawTexture(detailBg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (_selectedRecipe == null)
            {
                GUI.Label(new Rect(detailX + 8f, detailY + 8f, detailW - 16f, 24f),
                    "← 从左侧列表选择一个配方", _dimLabelStyle);
                return;
            }

            float y = detailY + 8f;
            float x = detailX + 10f;

            // 配方名 + 分类
            GUI.Label(new Rect(x, y, detailW - 20f, 24f),
                $"《{_selectedRecipe.recipeName}》   [{_selectedRecipe.category}]", _headerStyle);
            y += 28f;

            // 描述
            if (!string.IsNullOrEmpty(_selectedRecipe.description))
            {
                GUI.Label(new Rect(x, y, detailW - 20f, 20f),
                    _selectedRecipe.description, _dimLabelStyle);
                y += 22f;
            }

            // 分割线
            GUI.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(x, y, detailW - 20f, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 8f;

            // 所需材料
            GUI.Label(new Rect(x, y, detailW - 20f, 22f), "所需材料:", _detailLabelStyle);
            y += 22f;

            if (_selectedRecipe.materials != null)
            {
                var inv = _craftingSystem.GetComponent<_Game.Systems.Inventory.Inventory>();
                if (inv == null) inv = Object.FindObjectOfType<_Game.Systems.Inventory.Inventory>();

                foreach (var req in _selectedRecipe.materials)
                {
                    int owned = inv != null ? inv.GetItemCount(req.itemData) : 0;
                    bool enough = owned >= req.count;
                    string matName = req.itemData != null ? req.itemData.itemName : "???";

                    GUIStyle matStyle = enough ? _detailLabelStyle : new GUIStyle(_detailLabelStyle)
                    { normal = { textColor = missingMatColor } };

                    GUI.Label(new Rect(x, y, detailW - 20f, 18f),
                        $"  {matName} ×{req.count}   (拥有: {owned})", matStyle);
                    y += 18f;
                }
            }

            y += 6f;

            // 技能需求
            if (_selectedRecipe.skillRequirements != null && _selectedRecipe.skillRequirements.Length > 0)
            {
                GUI.Label(new Rect(x, y, detailW - 20f, 18f), "技能需求:", _detailLabelStyle);
                y += 18f;
                foreach (var sk in _selectedRecipe.skillRequirements)
                {
                    GUI.Label(new Rect(x, y, detailW - 20f, 18f),
                        $"  {sk.skill} Lv.{sk.level}", _dimLabelStyle);
                    y += 18f;
                }
                y += 4f;
            }

            // 合成参数
            GUI.Label(new Rect(x, y, detailW - 20f, 18f),
                $"耗时: {_selectedRecipe.craftTime:F1}秒  |  XP: +{_selectedRecipe.xpReward:F0}", _dimLabelStyle);
            y += 20f;

            // 可制作次数
            _craftableCount = _craftingSystem.GetCraftableCount(_selectedRecipe);
            GUI.Label(new Rect(x, y, detailW - 20f, 18f),
                $"可制作: {_craftableCount} 次", _detailLabelStyle);
            y += 28f;

            // 制作按钮
            Rect craftRect = new Rect(x, y, 160f, 36f);
            bool canCraft = _craftingSystem.CanCraft(_selectedRecipe) && _craftableCount > 0;

            string btnText;
            if (canCraft)
                btnText = "制作 ×1";
            else if (!_craftingSystem.HasMaterialsFor(_selectedRecipe))
                btnText = "材料不足";
            else
                btnText = "无法制作";

            GUI.enabled = canCraft;
            GUI.backgroundColor = canCraft ? craftButtonColor : disabledButtonColor;
            if (GUI.Button(craftRect, btnText, _craftBtnStyle))
            {
                _craftingSystem.Craft(_selectedRecipe);
                var saved = _selectedRecipe;
                RefreshRecipes();
                // 如果所选配方还在列表中就保持选中
                if (_displayedRecipes.Contains(saved))
                {
                    _selectedRecipe = saved;
                    _selectedIndex = _displayedRecipes.IndexOf(saved);
                    _craftableCount = _craftingSystem.GetCraftableCount(saved);
                }
                else
                    _selectedRecipe = null;
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            // 批量制作
            if (canCraft && _craftableCount > 1)
            {
                Rect batchRect = new Rect(x + 170f, y, 120f, 36f);
                GUI.backgroundColor = craftButtonColor;
                if (GUI.Button(batchRect, $"全部 ({_craftableCount})", _craftBtnStyle))
                {
                    int count = _craftableCount;
                    for (int i = 0; i < count; i++)
                    {
                        if (!_craftingSystem.Craft(_selectedRecipe)) break;
                    }
                    var saved2 = _selectedRecipe;
                    RefreshRecipes();
                    if (_displayedRecipes.Contains(saved2))
                    {
                        _selectedRecipe = saved2;
                        _selectedIndex = _displayedRecipes.IndexOf(saved2);
                        _craftableCount = _craftingSystem.GetCraftableCount(saved2);
                    }
                    else
                        _selectedRecipe = null;
                }
                GUI.backgroundColor = Color.white;
            }
        }

        void SelectRecipeByIndex(int index)
        {
            var dispList = _displayedRecipes;
            if (dispList == null || index < 0 || index >= dispList.Count) return;
            _selectedRecipe = dispList[index];
            _selectedIndex = index;
            _craftableCount = _craftingSystem.GetCraftableCount(_selectedRecipe);
        }

        void SelectRecipe(RecipeData recipe)
        {
            _selectedRecipe = recipe;
            var dispList = _displayedRecipes;
            _selectedIndex = dispList != null ? dispList.IndexOf(recipe) : -1;
            if (_selectedRecipe != null)
                _craftableCount = _craftingSystem.GetCraftableCount(_selectedRecipe);
        }
    }
}
