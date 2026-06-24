using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using _Game.Config;
using _Game.Core;
using _Game.UI;
using _Game.Systems.Power;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 生产设备交互面板 UI（OnGUI 原型）。
    /// 流程：E键设备 → DeviceOpenedEvent → 显示面板
    ///       查看配方/取出产物/补充材料
    ///       Esc → DeviceClosedEvent → 隐藏面板
    /// </summary>
    public class ProductionDeviceUI : MonoBehaviour
    {
        [Header("布局")]
        public float panelWidth = 540f;
        public float panelHeight = 520f;
        public float leftWidth = 260f;
        public float rightWidth = 260f;
        public float buttonHeight = 28f;
        public float padding = 10f;

        [Header("颜色")]
        public Color bgColor = new Color(0.06f, 0.06f, 0.06f, 0.97f);
        public Color headerColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        public Color sectionBgColor = new Color(0.12f, 0.12f, 0.12f, 0.8f);
        public Color btnColor = new Color(0f, 0.5f, 0f, 1f);
        public Color fuelColor = new Color(0.9f, 0.55f, 0.1f, 1f);
        public Color textColor = Color.white;
        public Color dimTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);

        ProductionDevice _currentDevice;
        ProductionDeviceData _deviceData;
        Inventory.Inventory _playerInv;
        bool _isVisible;
        int _selectedRecipeIdx = -1;
        ProductionDevice[] _nearbyDevices;

        // ============================================================
        // UGUI 字段
        // ============================================================
        private GameObject _canvasGo, _panelGo;
        private Font _font;

        // 标题
        private Text _titleText;

        // 左侧配方
        private Text _recipeLockHint;
        private RectTransform _recipeContent;
        private List<Button> _recipeBtns = new List<Button>();

        // 右侧状态
        private Text _powerStatusText, _fuelText;
        private Image _fuelBar, _wearBarFill;
        private GameObject _fuelRow, _wearBarRow;
        private Text _wearText, _wearValueText;
        private Text _linkText;

        // 右侧产出
        private RectTransform _outputContent;
        private List<GameObject> _outputRows = new List<GameObject>();

        // 右侧输入
        private RectTransform _inputContent;
        private List<GameObject> _inputRows = new List<GameObject>();

        // 右侧补充按钮区
        private GameObject _supplySection;
        private List<Button> _supplyBtns = new List<Button>();

        private int _lastRecipeIdx = -2, _lastRecipeHash;
        private float _lastFuel;

        static ProductionDeviceUI _instance;

        void Awake()
        {
            // 单例：优先使用 Managers 上的实例（如果已有则销毁自己）
            if (_instance != null)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _playerInv = ServiceLocator.Get<Inventory.Inventory>();
            // 强制不透明，防止 Inspector 旧序列化值导致透出残影
            bgColor = new Color(0.06f, 0.06f, 0.06f, 1f);
            sectionBgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        }

        void Start()
        {
            try { CreateUGUI(); }
            catch { }
        }

        void OnEnable()
        {
            EventBus.Subscribe<DeviceOpenedEvent>(OnDeviceOpened);
            EventBus.Subscribe<DeviceClosedEvent>(OnDeviceClosed);
            InputRouter.BindKey(KeyCode.Escape, InputPriority.UI, HandleEsc, this);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<DeviceOpenedEvent>(OnDeviceOpened);
            EventBus.Unsubscribe<DeviceClosedEvent>(OnDeviceClosed);
            InputRouter.UnbindAll(this);
        }

        void Update()
        {
            if (_canvasGo != null)
            {
                bool shouldShow = _isVisible && UIModeConfig.UseUGUI;
                if (_canvasGo.activeSelf != shouldShow) _canvasGo.SetActive(shouldShow);
            }
            // 不自动定时刷新 — 只在选择配方或打开设备时手动刷新，按钮不会频繁重建
        }

        /// <summary> 手动触发刷新（仅在需要时调用） </summary>
        void MarkDirty()
        {
            if (UIModeConfig.UseUGUI && _isVisible)
                RefreshUGUI();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        bool HandleEsc()
        {
            if (!_isVisible) return false;
            Close();
            return true;
        }

        void OnDeviceOpened(DeviceOpenedEvent evt)
        {
            CloseOtherUIs();
            _currentDevice = evt.Device;
            _deviceData = evt.Device.Data;
            if (_playerInv == null)
                _playerInv = ServiceLocator.Get<Inventory.Inventory>();
            _selectedRecipeIdx = -1;
            _lastRecipeIdx = -2;
            _isVisible = true;
            if (UIModeConfig.UseUGUI && _canvasGo != null) _canvasGo.SetActive(true);
            ScanNearbyDevices();
            MarkDirty(); // 首次打开时渲染 UI
        }

        void ScanNearbyDevices()
        {
            if (_currentDevice == null) return;
            var all = Object.FindObjectsOfType<ProductionDevice>();
            var list = new System.Collections.Generic.List<ProductionDevice>();
            Vector3 pos = _currentDevice.transform.position;
            foreach (var d in all)
            {
                if (d == _currentDevice || d.Data == null) continue;
                if (Vector3.Distance(pos, d.transform.position) < 15f)
                    list.Add(d);
            }
            _nearbyDevices = list.ToArray();
        }

        void OnDeviceClosed(DeviceClosedEvent evt)
        {
            _isVisible = false;
            _currentDevice = null;
            _deviceData = null;
            if (_canvasGo != null) _canvasGo.SetActive(false);
        }

        void Close()
        {
            _isVisible = false;
            _currentDevice = null;
            _deviceData = null;
            if (_canvasGo != null) _canvasGo.SetActive(false);
            EventBus.Publish(new DeviceClosedEvent());
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
            if (bmc != null && bmc.IsBuildMode)
                bmc.ForceExit();

            var containerWin = ServiceLocator.Get<_Game.Systems.WorldContainer.ContainerWindowUI>();
            if (containerWin != null)
                containerWin.CloseWindow();

            // 关闭旧工作台UI，避免透出老UI残影
            var craftingUI = ServiceLocator.Get<CraftingUI>();
            if (craftingUI != null) craftingUI.Close();

            var researchUI = ServiceLocator.Get<ChemicalResearchUI>();
            if (researchUI != null) researchUI.Close();

            // 关闭电力/终端面板（同在屏幕正中，会叠影）
            _Game.UI.PowerSourceUI.Hide();
            _Game.UI.TerminalUI.Hide();
        }

        // ============================================================
        // UGUI — 创建与刷新
        // ============================================================

        void CreateUGUI()
        {
            _font = UGUIBuilder.DefaultFont;

            _canvasGo = new GameObject("ProductionDeviceUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 155;
            _canvasGo.SetActive(false);

            // 面板（居中）
            _panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            UguiSetCenter(_panelGo.GetComponent<RectTransform>(), panelWidth, panelHeight);
            _panelGo.GetComponent<Image>().color = bgColor;

            // 标题
            _titleText = UguiMakeText("Title", 18, FontStyle.Bold, TextAnchor.MiddleLeft, 300, 28);
            UguiAttach(_titleText, _panelGo, padding, -padding, 0, 1);

            var closeBtn = UguiMakeSmallBtn("CloseBtn", "✕", new Color(0.5f, 0.2f, 0.2f), 40, 28);
            UguiAttach(closeBtn, _panelGo, -60, -padding, 1, 1);
            closeBtn.onClick.AddListener(() => Close());

            var line = new GameObject("Line", typeof(Image));
            UguiAttach(line, _panelGo, padding, -padding - 32, 0, 1);
            line.GetComponent<RectTransform>().sizeDelta = new Vector2(panelWidth - padding * 2, 1);
            line.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

            // ---- 左侧：配方列表 ----
            float leftX = padding, leftW = leftWidth;
            float topY = padding + 48f;

            // 左侧面板背景
            var leftPanelGo = new GameObject("LeftPanel", typeof(RectTransform), typeof(Image));
            leftPanelGo.transform.SetParent(_panelGo.transform, false);
            var lpRect = leftPanelGo.GetComponent<RectTransform>();
            lpRect.anchorMin = new Vector2(0, 1); lpRect.anchorMax = new Vector2(0, 1);
            lpRect.pivot = new Vector2(0, 1);
            lpRect.anchoredPosition = new Vector2(leftX, -topY + 4);
            lpRect.sizeDelta = new Vector2(leftW, panelHeight - topY - padding + 4);
            leftPanelGo.GetComponent<Image>().color = sectionBgColor;
            var leftParent = leftPanelGo;

            var leftHeader = UguiMakeText("LeftHeader", 13, FontStyle.Bold, TextAnchor.MiddleLeft, leftW - 12, 20);
            UguiAttach(leftHeader, leftParent, 6, -4, 0, 1);
            leftHeader.text = "生产配方";

            _recipeLockHint = UguiMakeText("LockHint", 12, FontStyle.Normal, TextAnchor.MiddleLeft, leftW - 16, 50);
            UguiAttach(_recipeLockHint, leftParent, 8, -28, 0, 1);
            _recipeLockHint.color = dimTextColor;
            _recipeLockHint.gameObject.SetActive(false);

            // 配方滚动列表
            var recipeScrollGo = new GameObject("RecipeScroll", typeof(Image), typeof(ScrollRect));
            UguiAttach(recipeScrollGo, leftParent, 0, -28, 0, 1);
            recipeScrollGo.GetComponent<RectTransform>().sizeDelta = new Vector2(leftW, panelHeight - topY - padding - 24);
            recipeScrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var rsr = recipeScrollGo.GetComponent<ScrollRect>();
            rsr.horizontal = false; rsr.scrollSensitivity = 20f;

            var rvp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            rvp.transform.SetParent(recipeScrollGo.transform, false);
            UguiSetStretch(rvp.GetComponent<RectTransform>());
            rsr.viewport = rvp.GetComponent<RectTransform>();

            var rc = new GameObject("Content", typeof(RectTransform));
            rc.transform.SetParent(rvp.transform, false);
            _recipeContent = rc.GetComponent<RectTransform>();
            _recipeContent.anchorMin = new Vector2(0, 1); _recipeContent.anchorMax = new Vector2(1, 1);
            _recipeContent.pivot = new Vector2(0.5f, 1); _recipeContent.sizeDelta = new Vector2(0, 0);
            var rvlg = rc.AddComponent<VerticalLayoutGroup>();
            rvlg.spacing = 4; rvlg.childControlWidth = true; rvlg.childControlHeight = false;
            rc.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rsr.content = _recipeContent;

            // ---- 右侧：设备状态 (ScrollRect 流式布局，永不溢出) ----
            float rightX = padding + leftWidth + 8f, rightW = rightWidth;

            // 右侧滚动容器
            var rightScrollGo = new GameObject("RightScroll", typeof(Image), typeof(ScrollRect));
            rightScrollGo.transform.SetParent(_panelGo.transform, false);
            var rrsRect = rightScrollGo.GetComponent<RectTransform>();
            rrsRect.anchorMin = new Vector2(0, 1); rrsRect.anchorMax = new Vector2(0, 1);
            rrsRect.pivot = new Vector2(0, 1);
            rrsRect.anchoredPosition = new Vector2(rightX, -topY + 4);
            rrsRect.sizeDelta = new Vector2(rightW, panelHeight - topY - padding + 4);
            rightScrollGo.GetComponent<Image>().color = sectionBgColor;
            var rightScroll = rightScrollGo.GetComponent<ScrollRect>();
            rightScroll.horizontal = false; rightScroll.scrollSensitivity = 20f;
            var rightParent = rightScrollGo;

            // Viewport
            var rvp2 = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            rvp2.transform.SetParent(rightScrollGo.transform, false);
            UguiSetStretch(rvp2.GetComponent<RectTransform>());
            rightScroll.viewport = rvp2.GetComponent<RectTransform>();

            // Content (VerticalLayoutGroup)
            var rContent = new GameObject("Content", typeof(RectTransform));
            rContent.transform.SetParent(rvp2.transform, false);
            var rcRect = rContent.GetComponent<RectTransform>();
            rcRect.anchorMin = new Vector2(0, 1); rcRect.anchorMax = new Vector2(1, 1);
            rcRect.pivot = new Vector2(0.5f, 1); rcRect.sizeDelta = new Vector2(0, 0);
            var rvlg2 = rContent.AddComponent<VerticalLayoutGroup>();
            rvlg2.spacing = 2; rvlg2.childControlWidth = true; rvlg2.childControlHeight = false;
            rvlg2.padding = new RectOffset(4, 4, 4, 4);
            rContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rightScroll.content = rcRect;

            // --- 各行元素（流式添加） ---
            // 电力状态行
            _powerStatusText = AddRightRow(rContent, "", 13, FontStyle.Bold, Color.white, 22);
            // 燃料文本行
            _fuelText = AddRightRow(rContent, "", 13, FontStyle.Normal, dimTextColor, 20);
            _fuelText.gameObject.SetActive(false);
            // 燃料条行
            var fuelRow = AddRightRowGo(rContent, 10);
            _fuelRow = fuelRow;
            var fuelBg = new GameObject("FuelBg", typeof(Image));
            fuelBg.transform.SetParent(fuelRow.transform, false);
            fuelBg.GetComponent<RectTransform>().sizeDelta = new Vector2(rightW - 18, 8);
            fuelBg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
            fuelBg.SetActive(false);
            var fuelFill = new GameObject("FuelFill", typeof(Image));
            fuelFill.transform.SetParent(fuelBg.transform, false);
            _fuelBar = fuelFill.GetComponent<Image>();
            _fuelBar.color = fuelColor;
            var ffr = fuelFill.GetComponent<RectTransform>();
            ffr.anchorMin = Vector2.zero; ffr.anchorMax = new Vector2(0, 1);
            ffr.pivot = new Vector2(0, 0.5f); ffr.sizeDelta = Vector2.zero;
            // 磨损条行（加宽 + 百分比/数值）
            _wearBarRow = AddRightRowGo(rContent, 28);
            _wearText = AddRightRow(rContent, "", 11, FontStyle.Normal, dimTextColor, 28);
            _wearText.rectTransform.sizeDelta = new Vector2(100, 28);
            // 数值文本（右侧）
            var valGo = new GameObject("WearValue", typeof(RectTransform));
            valGo.transform.SetParent(_wearBarRow.transform, false);
            _wearValueText = valGo.AddComponent<Text>();
            _wearValueText.font = UGUIBuilder.DefaultFont;
            _wearValueText.fontSize = 11;
            _wearValueText.alignment = TextAnchor.MiddleRight;
            _wearValueText.color = dimTextColor;
            var vrt = valGo.GetComponent<RectTransform>();
            vrt.anchorMin = new Vector2(1, 0); vrt.anchorMax = new Vector2(1, 1);
            vrt.pivot = new Vector2(1, 0.5f);
            vrt.sizeDelta = new Vector2(80, 28);
            vrt.anchoredPosition = new Vector2(-4, 0);
            // 耐久条背景（加高到 10px）
            var wearBg = new GameObject("WearBg", typeof(Image));
            wearBg.transform.SetParent(_wearBarRow.transform, false);
            wearBg.GetComponent<RectTransform>().sizeDelta = new Vector2(rightW - 190, 10);
            wearBg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
            // Fill
            var wearFill = new GameObject("WearFill", typeof(Image));
            wearFill.transform.SetParent(wearBg.transform, false);
            _wearBarFill = wearFill.GetComponent<Image>();
            _wearBarFill.color = new Color(1f, 0.6f, 0f); // 橙色
            var wfr = wearFill.GetComponent<RectTransform>();
            wfr.anchorMin = Vector2.zero; wfr.anchorMax = new Vector2(0, 1);
            wfr.pivot = new Vector2(0, 0.5f); wfr.sizeDelta = Vector2.zero;
            _wearBarRow.SetActive(false);
            _wearText.gameObject.SetActive(false);
            _wearValueText.gameObject.SetActive(false);
            // 链接行
            _linkText = AddRightRow(rContent, "", 12, FontStyle.Normal, dimTextColor, 20);

            // 产出标题
            AddRightRow(rContent, "┃ 产出物品", 13, FontStyle.Bold, Color.white, 22);
            // 产出列表(嵌入的 ScrollRect，固定高度)
            var outScrollGo = AddInnerScroll(rContent, rightW - 12, 80, out _outputContent);
            // 取出全部按钮行
            var takeAllRow = AddRightRowGo(rContent, 26);
            var takeAllBtn = UguiMakeSmallBtn("TakeAllBtn", "取出全部", btnColor, 90, 22);
            takeAllBtn.transform.SetParent(takeAllRow.transform, false);
            takeAllBtn.onClick.AddListener(() => TakeAllOutput());

            // 分隔线
            var divRow = AddRightRowGo(rContent, 3);
            var divLine = new GameObject("DivLine", typeof(Image));
            divLine.transform.SetParent(divRow.transform, false);
            UguiSetStretch(divLine.GetComponent<RectTransform>());
            divLine.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 1);
            divLine.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.35f, 0.6f);

            // 输入标题
            AddRightRow(rContent, "┃ 输入材料", 13, FontStyle.Bold, Color.white, 22);
            // 输入列表(嵌入的 ScrollRect，固定高度)
            var inScrollGo = AddInnerScroll(rContent, rightW - 12, 50, out _inputContent);
            // 补充材料按钮区
            _supplySection = new GameObject("SupplySection", typeof(RectTransform), typeof(LayoutElement));
            _supplySection.transform.SetParent(rContent.transform, false);
            _supplySection.GetComponent<RectTransform>().sizeDelta = new Vector2(rightW - 14, 30);
            _supplySection.GetComponent<LayoutElement>().minHeight = 30;
            var ssvlg = _supplySection.AddComponent<VerticalLayoutGroup>();
            ssvlg.spacing = 2; ssvlg.childControlWidth = true; ssvlg.childControlHeight = false;
        }

        void RefreshUGUI()
        {
            if (_currentDevice == null || _deviceData == null) return;

            _titleText.text = _deviceData.deviceName;

            // ---- 左侧配方 ----
            _recipeLockHint.gameObject.SetActive(false);
            var researchMgr = ServiceLocator.Get<ChemicalResearchManager>();
            bool researched = researchMgr == null || researchMgr.IsDeviceUnlocked(_deviceData.deviceName);
            if (!researched)
            {
                _recipeLockHint.text = "未研究\n请在研究中心完成对应研究项目以解锁此设备配方";
                _recipeLockHint.gameObject.SetActive(true);
                foreach (var b in _recipeBtns) { if (b != null) Destroy(b.gameObject); }
                _recipeBtns.Clear();
            }
            else
            {
                var recipes = _deviceData.recipes;
                int recipeHash = recipes?.Length ?? 0;
                if (recipes != null) foreach (var r in recipes) recipeHash ^= r.GetHashCode();
                if (_lastRecipeHash != recipeHash || _lastRecipeIdx != _selectedRecipeIdx)
                {
                    _lastRecipeHash = recipeHash;
                    _lastRecipeIdx = _selectedRecipeIdx;
                    RebuildRecipes(recipes, researchMgr);
                }
            }

            // ---- 右侧状态 ----
            RefreshRightPanel();
        }

        void RebuildRecipes(ProductionRecipe[] recipes, ChemicalResearchManager researchMgr)
        {
            foreach (var b in _recipeBtns) { if (b != null) Destroy(b.gameObject); }
            _recipeBtns.Clear();

            if (recipes == null || recipes.Length == 0)
            {
                _recipeLockHint.text = "无配方";
                _recipeLockHint.gameObject.SetActive(true);
                return;
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                var r = recipes[i];
                int idx = i;
                bool selected = _selectedRecipeIdx == i;

                bool recipeLocked = false;
                if (!string.IsNullOrEmpty(r.recipeId) && researchMgr != null)
                    recipeLocked = !researchMgr.IsRecipeUnlocked(r.recipeId);

                string prefix = recipeLocked ? "🔒 " : "";
                string label;
                if (r.inputs != null && r.inputs.Length > 0)
                {
                    var parts = new List<string>();
                    foreach (var req in r.inputs) parts.Add($"{ItemName(req.itemData)}×{req.count}");
                    label = $"{prefix}{string.Join("+", parts)} → {ItemName(r.output)}×{r.outputCount}";
                }
                else
                    label = $"{prefix}{ItemName(r.input)}×{r.inputCount} → {ItemName(r.output)}×{r.outputCount}";

                var go = new GameObject($"Recipe_{idx}", typeof(Image), typeof(Button));
                go.transform.SetParent(_recipeContent, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(leftWidth - 24, buttonHeight + 2);
                go.GetComponent<Image>().color = recipeLocked ? new Color(0.12f, 0.12f, 0.12f, 0.7f)
                    : (selected ? new Color(0f, 0.45f, 0f) : new Color(0.18f, 0.18f, 0.18f));

                var lblGo = new GameObject("Label", typeof(Text));
                lblGo.transform.SetParent(go.transform, false);
                var t = lblGo.GetComponent<Text>();
                t.font = _font; t.fontSize = 11; t.alignment = TextAnchor.MiddleLeft;
                t.color = recipeLocked ? dimTextColor : Color.white;
                t.text = label; t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                UguiSetStretch(lblGo.GetComponent<RectTransform>());

                if (!recipeLocked)
                    go.GetComponent<Button>().onClick.AddListener(() => { _selectedRecipeIdx = idx; _lastRecipeIdx = -2; MarkDirty(); });

                _recipeBtns.Add(go.GetComponent<Button>());
            }
        }

        void RefreshRightPanel()
        {
            float rightW = rightWidth;

            // 电力/煤模式状态
            var consumer = _currentDevice?.GetComponent<Power.PowerConsumer>();
            if (consumer != null)
            {
                if (_currentDevice.IsElectricPowered)
                {
                    _powerStatusText.text = $"⚡ 电网供电 ({consumer.requiredPower}W)";
                    _powerStatusText.color = Color.green;
                    _fuelText.text = $"速度倍率: ×{consumer.electricSpeedMultiplier}";
                    _fuelText.gameObject.SetActive(true);
                    _fuelRow.SetActive(false);
                }
                else if (_currentDevice.IsCoalPowered)
                {
                    _powerStatusText.text = "🔥 烧煤运转";
                    _powerStatusText.color = new Color(1f, 0.7f, 0.2f);
                    _fuelText.gameObject.SetActive(false);
                    _fuelRow.SetActive(false);
                }
                else
                {
                    _powerStatusText.text = consumer.allowCoal ? "⏳ 等待供电/加煤" : "❌ 无电力 - 设备停摆";
                    _powerStatusText.color = new Color(1f, 0.35f, 0.35f);
                    _fuelText.gameObject.SetActive(false);
                    _fuelRow.SetActive(false);
                }
            }
            else
            {
                _powerStatusText.text = "";
                _fuelText.gameObject.SetActive(false);
                _fuelRow.SetActive(false);
            }

            // 燃料条（非通电模式）
            if (!_currentDevice.IsElectricPowered && _deviceData.requiresFuel)
            {
                _fuelText.gameObject.SetActive(true);
                _fuelText.text = $"燃料: {_currentDevice.FuelRemaining:F0} 轮  {(_currentDevice.FuelRemaining > 0 ? "运行中" : "待加注")}";
                _fuelText.color = _currentDevice.FuelRemaining > 5f ? Color.white : dimTextColor;
                _fuelRow.SetActive(true);

                float fuelPct = Mathf.Clamp01(_currentDevice.FuelRemaining / (_deviceData.fuelPerCycle * 30f));
                var fuelBarRect = _fuelBar.GetComponent<RectTransform>();
                fuelBarRect.anchorMax = new Vector2(fuelPct, 1);
            }

            // 磨损条（始终显示）
            if (_currentDevice != null)
            {
                float wearRatio = _currentDevice.DeviceDurabilityRatio;
                float cur = _currentDevice.DeviceDurability;
                float max = GameConstants.PRODUCTION_DEVICE_MAX_DURABILITY;
                _wearText.text = $"磨损: {(wearRatio * 100):F0}%";
                _wearText.color = wearRatio > 0.5f ? Color.white : wearRatio > 0.2f ? Color.yellow : Color.red;
                _wearValueText.text = $"{cur:F0} / {max:F0}";
                _wearValueText.color = wearRatio > 0.5f ? dimTextColor : wearRatio > 0.2f ? Color.yellow : Color.red;
                _wearText.gameObject.SetActive(true);
                _wearValueText.gameObject.SetActive(true);
                _wearBarRow.SetActive(true);
                var wearRect = _wearBarFill.GetComponent<RectTransform>();
                wearRect.anchorMax = new Vector2(wearRatio, 1);
                _wearBarFill.color = wearRatio > 0.5f
                    ? new Color(1f, 0.6f, 0f) : wearRatio > 0.2f ? Color.yellow : Color.red;
            }
            else
            {
                _wearText.gameObject.SetActive(false);
                _wearValueText.gameObject.SetActive(false);
                _wearBarRow.SetActive(false);
            }

            // 链接
            var dest = _currentDevice.OutputDestination;
            if (dest != null)
            {
                string destName = dest.Data != null ? dest.Data.deviceName : "???";
                _linkText.text = $"→ 链接: {destName}";
            }
            else
            {
                int linkable = 0;
                if (_nearbyDevices != null)
                    foreach (var d in _nearbyDevices) if (d != null && d.Data != null && CanLinkTo(d)) linkable++;
                _linkText.text = linkable > 0 ? $"可链接 {linkable} 个设备" : "";
            }

            // 产出
            var outSlot = _currentDevice.OutputSlot;
            foreach (var go in _outputRows) { if (go != null) Destroy(go); }
            _outputRows.Clear();
            if (outSlot != null && outSlot.placedItems != null && outSlot.placedItems.Count > 0)
            {
                foreach (var pi in outSlot.placedItems)
                {
                    if (pi.itemData == null) continue;
                    var rowGo = new GameObject("OutRow", typeof(RectTransform));
                    rowGo.transform.SetParent(_outputContent, false);
                    rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(rightW - 28, buttonHeight);
                    var rl = rowGo.AddComponent<HorizontalLayoutGroup>();
                    rl.spacing = 4; rl.childAlignment = TextAnchor.MiddleLeft;
                    rl.childControlWidth = false; rl.childControlHeight = true;
                    rl.childForceExpandWidth = false;

                    var nameText = UguiMakeRowText(pi.itemData.itemName + $" ×{pi.count}", 13, Color.white, rightW - 100);
                    nameText.transform.SetParent(rowGo.transform, false);

                    var itemCap = pi;
                    var takeBtn = UguiMakeSmallBtn("TakeBtn", "取出", btnColor, 60, 24);
                    takeBtn.transform.SetParent(rowGo.transform, false);
                    takeBtn.onClick.AddListener(() =>
                    {
                        if (_playerInv != null)
                        {
                            int added = _playerInv.AddItem(itemCap.itemData, itemCap.count);
                            if (added > 0) { outSlot.RemoveItem(itemCap.itemData, added); MarkDirty(); }
                        }
                    });
                    _outputRows.Add(rowGo);
                }
            }

            // 输入
            var inSlot = _currentDevice.InputSlot;
            foreach (var go in _inputRows) { if (go != null) Destroy(go); }
            _inputRows.Clear();
            if (inSlot != null && inSlot.placedItems != null && inSlot.placedItems.Count > 0)
            {
                foreach (var pi in inSlot.placedItems)
                {
                    if (pi.itemData == null) continue;
                    var go = new GameObject("InRow", typeof(Text));
                    go.transform.SetParent(_inputContent, false);
                    var t = go.GetComponent<Text>();
                    t.font = _font; t.fontSize = 12; t.alignment = TextAnchor.MiddleLeft;
                    t.color = dimTextColor; t.raycastTarget = false;
                    t.text = $"{pi.itemData.itemName} ×{pi.count}";
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(rightW - 28, 22);
                    _inputRows.Add(go);
                }
            }

            // 补充按钮
            foreach (var b in _supplyBtns) { if (b != null) Destroy(b.gameObject); }
            _supplyBtns.Clear();
            if (_selectedRecipeIdx >= 0 && _deviceData.recipes != null && _selectedRecipeIdx < _deviceData.recipes.Length)
            {
                var sel = _deviceData.recipes[_selectedRecipeIdx];
                if (sel.inputs != null && sel.inputs.Length > 0)
                {
                    foreach (var req in sel.inputs)
                    {
                        if (req.itemData == null) continue;
                        int canSupply = _playerInv?.GetItemCount(req.itemData) ?? 0;
                        bool canFeed = canSupply >= req.count;
                        string text = canFeed
                            ? $"▼ 补充 {ItemName(req.itemData)}×{req.count}  (拥有:{canSupply})"
                            : $"✗ 不足 {ItemName(req.itemData)}×{req.count}  (拥有:{canSupply})";

                        var btn = UguiMakeSupplyBtn(text, canFeed ? btnColor : new Color(0.3f, 0.3f, 0.3f, 0.6f), rightW - 14, 28);
                        btn.transform.SetParent(_supplySection.transform, false);
                        btn.interactable = canFeed;
                        var capItem = req.itemData; var capCount = req.count;
                        btn.onClick.AddListener(() => {
                            Debug.Log($"[ProductionDeviceUI] 🔘 点击补充: {capItem?.itemName} ×{capCount}");
                            SupplyInput(capItem, capCount);
                        });
                        _supplyBtns.Add(btn);
                    }
                }
                else if (sel.input != null)
                {
                    int canSupply = _playerInv?.GetItemCount(sel.input) ?? 0;
                    bool canFeed = canSupply >= sel.inputCount;
                    string text = canFeed
                        ? $"▼ 补充材料 ({ItemName(sel.input)}×{sel.inputCount})  (拥有:{canSupply})"
                        : $"✗ 材料不足 ({ItemName(sel.input)}×{sel.inputCount})  (拥有:{canSupply})";

                    var btn = UguiMakeSupplyBtn(text, canFeed ? btnColor : new Color(0.3f, 0.3f, 0.3f, 0.6f), rightW - 14, 30);
                    btn.transform.SetParent(_supplySection.transform, false);
                    btn.interactable = canFeed;
                    var capItem = sel.input; var capCount = sel.inputCount;
                    btn.onClick.AddListener(() => {
                        Debug.Log($"[ProductionDeviceUI] 🔘 点击补充材料: {capItem?.itemName} ×{capCount}");
                        SupplyInput(capItem, capCount);
                    });
                    _supplyBtns.Add(btn);
                }
            }
        }

        // ============================================================
        // UGUI 辅助
        // ============================================================

        static void UguiSetStretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero; }
        static void UguiSetCenter(RectTransform r, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(w, h); r.anchoredPosition = Vector2.zero;
        }

        // 右侧流式布局辅助
        Text AddRightRow(GameObject parent, string text, int size, FontStyle style, Color color, float height)
        {
            var go = AddRightRowGo(parent, height);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.alignment = TextAnchor.MiddleLeft; t.color = color; t.raycastTarget = false;
            t.text = text; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UguiSetStretch(go.GetComponent<RectTransform>());
            return t;
        }

        GameObject AddRightRowGo(GameObject parent, float height)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, height);
            go.GetComponent<LayoutElement>().minHeight = height;
            return go;
        }

        GameObject AddInnerScroll(GameObject parent, float width, float height, out RectTransform contentRect)
        {
            var scrollGo = new GameObject("InnerScroll", typeof(RectTransform), typeof(LayoutElement));
            scrollGo.transform.SetParent(parent.transform, false);
            scrollGo.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            scrollGo.GetComponent<LayoutElement>().minHeight = height;
            scrollGo.GetComponent<LayoutElement>().preferredHeight = height;
            var img = scrollGo.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            var sr = scrollGo.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.scrollSensitivity = 15f;

            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(scrollGo.transform, false);
            UguiSetStretch(vp.GetComponent<RectTransform>());
            sr.viewport = vp.GetComponent<RectTransform>();

            var ct = new GameObject("Content", typeof(RectTransform));
            ct.transform.SetParent(vp.transform, false);
            contentRect = ct.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1); contentRect.sizeDelta = new Vector2(0, 0);
            var vlg = ct.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2; vlg.childControlWidth = true; vlg.childControlHeight = false;
            ct.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = contentRect;

            return scrollGo;
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

        Text UguiMakeRowText(string text, int size, Color color, float w)
        {
            var go = new GameObject("Text", typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.alignment = TextAnchor.MiddleLeft;
            t.color = color; t.text = text; t.raycastTarget = false;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, buttonHeight);
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

        Button UguiMakeSupplyBtn(string text, Color bg, float w, float h)
        {
            var go = new GameObject("SupplyBtn", typeof(Image), typeof(Button));
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


        // ---- 辅助方法 ----

        void TakeAllOutput()
        {
            var outSlot = _currentDevice?.OutputSlot;
            if (outSlot == null || _playerInv == null) return;

            var items = new List<_Game.Systems.Inventory.PlacedItem>(outSlot.placedItems);
            foreach (var pi in items)
            {
                if (pi.itemData == null) continue;
                int added = _playerInv.AddItem(pi.itemData, pi.count);
                if (added > 0)
                    outSlot.RemoveItem(pi.itemData, added);
            }
            MarkDirty();
        }

        void SupplyInput(ItemData item, int count)
        {
            // 三重兜底找 Inventory
            if (_playerInv == null)
                _playerInv = ServiceLocator.Get<Inventory.Inventory>();
            if (_playerInv == null)
                _playerInv = FindObjectOfType<Inventory.Inventory>();
            if (_playerInv == null)
                _playerInv = PlayerRegistry.Get<Inventory.Inventory>();

            if (_playerInv == null) { Debug.LogError("[ProductionDeviceUI] ❌ SupplyInput: 找不到 Inventory"); return; }
            if (_currentDevice == null) { Debug.Log("[ProductionDeviceUI] SupplyInput: _currentDevice 为 null"); return; }
            if (item == null) { Debug.Log("[ProductionDeviceUI] SupplyInput: item 为 null"); return; }

            var inSlot = _currentDevice.InputSlot;
            if (inSlot == null) { Debug.Log("[ProductionDeviceUI] SupplyInput: InputSlot 为 null，尝试初始化..."); return; }

            int toTransfer = Mathf.Min(count, _playerInv.GetItemCount(item));
            if (toTransfer <= 0) { Debug.Log($"[ProductionDeviceUI] SupplyInput: 背包无 {item.itemName}"); return; }

            int added = inSlot.AddItem(item, toTransfer, float.MaxValue);
            if (added > 0)
            {
                _playerInv.RemoveItem(item, added);
                Debug.Log($"[ProductionDeviceUI] ✅ 补充成功: {item.itemName} ×{added}");
                MarkDirty();
            }
        }

        /// <summary>当前设备的产出能否被目标设备作为原料接收</summary>
        bool CanLinkTo(ProductionDevice target)
        {
            if (_deviceData?.recipes == null || target?.Data?.recipes == null) return false;
            foreach (var myRecipe in _deviceData.recipes)
            {
                if (myRecipe.output == null) continue;
                foreach (var theirRecipe in target.Data.recipes)
                {
                    if (theirRecipe.input == myRecipe.output)
                        return true;
                }
            }
            return false;
        }

        static string ItemName(ItemData item)
        {
            return item != null ? item.itemName : "???";
        }

    }
}
