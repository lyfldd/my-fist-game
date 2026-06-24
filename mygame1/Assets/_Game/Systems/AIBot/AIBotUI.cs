using UnityEngine;
using UnityEngine.UI;
using _Game.Config;
using _Game.Core;
using _Game.UI;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人主面板 (UGUI)。
    /// </summary>
    public class AIBotUI : MonoBehaviour
    {
        static AIBotUI _instance;
        AIBot _bot;
        AIBotCombat _combat;

        bool _visible;
        ItemData _equippedRightArmItem;
        ItemData _equippedLeftArmItem;
        System.Collections.Generic.Dictionary<string, ItemData> _knownAmmoItems
            = new System.Collections.Generic.Dictionary<string, ItemData>();

        // ============================================================
        // UGUI 字段
        // ============================================================
        private GameObject _canvasGo, _mainPanelGo;
        private Font _font;
        private RectTransform _mainContent;

        // HP/Shield
        private Image _hpBar, _shieldBar;
        private Text _hpText, _shieldText, _shieldStatusText;
        private GameObject _shieldRow, _shieldBtnRow;

        // Energy
        private Text _modeText, _consumptionText;
        private Image _batteryBar, _uraniumBar;
        private Text _batteryText, _uraniumText;
        private Button _ecoBtn, _burstBtn;
        private Text _ecoLabel, _burstLabel, _ecoWarnText;
        private Text _solarText, _nuclearText;
        private Slider _speedSlider;
        private Text _speedText;

        // Weapon slots
        private Text _rightArmText, _leftArmText;

        // Priority
        private Text _priorityText;

        // Alert range
        private Slider _alertSlider;
        private Text _alertText;

        // Command buttons
        private Button _followBtn, _guardBtn, _patrolBtn;

        // Action buttons
        private Button _inventoryBtn, _fuelBtn, _repairBtn, _pilotBtn, _aiOverrideBtn;
        private Text _pilotLabel, _aiOverrideLabel;

        // Sub-panels (expandable sections instead of floating windows)
        private GameObject _subFollow, _subGuard, _subPatrol, _subRightArm, _subLeftArm, _subReactor;
        private bool _lastVisible;
        private bool _needsRefresh = true;
        private int _lastRefreshFrame;

        // ============================================================
        // 静态接口
        // ============================================================

        public static void Show(AIBot bot)
        {
            if (_instance == null)
            {
                var go = new GameObject("AIBotUI");
                _instance = go.AddComponent<AIBotUI>();
            }
            _instance._bot = bot;
            _instance._combat = bot?.GetComponent<AIBotCombat>();
            _instance._visible = true;
            _instance._lastVisible = true;
            if (_instance._canvasGo != null) _instance._canvasGo.SetActive(UIModeConfig.UseUGUI);
            UIPanelManager.Instance?.Open("aiBot", onClose: Hide);
            _instance.MarkDirty();
        }

        public static void Hide()
        {
            if (_instance != null)
            {
                _instance._visible = false;
                _instance._lastVisible = false;
                if (_instance._canvasGo != null) _instance._canvasGo.SetActive(false);
                UIPanelManager.Instance?.Close("aiBot");
            }
        }

        public static bool IsVisible => _instance != null && _instance._visible;

        void Start()
        {
            try { CreateUGUI(); }
            catch (System.Exception e) { Debug.LogError($"[AIBotUI] UGUI 创建失败: {e.Message}\n{e.StackTrace}"); }
        }

        void MarkDirty() => _needsRefresh = true;

        void OnEnable() { }
        void OnDisable() { InputRouter.UnbindAll(this); }

        void Update()
        {
            if (_canvasGo != null)
            {
                bool shouldShow = _visible && UIModeConfig.UseUGUI;
                if (_canvasGo.activeSelf != shouldShow) _canvasGo.SetActive(shouldShow);
            }
            if (UIModeConfig.UseUGUI && _visible && _bot != null)
            {
                if (_needsRefresh || UnityEngine.Time.frameCount - _lastRefreshFrame > 30)
                {
                    _lastRefreshFrame = UnityEngine.Time.frameCount;
                    _needsRefresh = false;
                    RefreshUGUI();
                }
                // 点击主面板外关闭
                if (Input.GetMouseButtonDown(0) && _mainPanelGo != null)
                {
                    var pr = _mainPanelGo.GetComponent<RectTransform>();
                    if (!RectTransformUtility.RectangleContainsScreenPoint(pr, Input.mousePosition))
                    {
                        bool inSub = false;
                        foreach (var sub in new[] { _subFollow, _subGuard, _subPatrol, _subRightArm, _subLeftArm, _subReactor })
                            if (sub != null && sub.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(sub.GetComponent<RectTransform>(), Input.mousePosition))
                            { inSub = true; break; }
                        if (!inSub) Hide();
                    }
                }
            }
        }

        // ============================================================
        // UGUI — 创建
        // ============================================================

        void CreateUGUI()
        {
            _font = UGUIBuilder.DefaultFont;
            float pw = 420f, ph = 440f;

            _canvasGo = new GameObject("AIBotUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasGo.GetComponent<Canvas>().sortingOrder = 250;
            _canvasGo.SetActive(false);

            _mainPanelGo = new GameObject("MainPanel", typeof(RectTransform), typeof(Image));
            _mainPanelGo.transform.SetParent(_canvasGo.transform, false);
            UguiSetCenter(_mainPanelGo.GetComponent<RectTransform>(), pw, ph);
            _mainPanelGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.93f);

            // 标题栏（由UIPanelManager统一管理）
            UIPanelManager.AddPanelTitleBar(_mainPanelGo, "AI机器人", "aiBot", onClose: Hide);

            // 滚动内容区
            var scrollGo = new GameObject("Scroll", typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(_mainPanelGo.transform, false);
            var sr = scrollGo.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(1, 1);
            sr.offsetMin = new Vector2(8, 8); sr.offsetMax = new Vector2(-8, -38);
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var scr = scrollGo.GetComponent<ScrollRect>();
            scr.horizontal = false; scr.scrollSensitivity = 20f;

            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(scrollGo.transform, false);
            UguiSetStretch(vp.GetComponent<RectTransform>());
            scr.viewport = vp.GetComponent<RectTransform>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp.transform, false);
            _mainContent = content.GetComponent<RectTransform>();
            _mainContent.anchorMin = new Vector2(0, 1); _mainContent.anchorMax = new Vector2(1, 1);
            _mainContent.pivot = new Vector2(0.5f, 1); _mainContent.sizeDelta = new Vector2(0, 0);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4; vlg.childControlWidth = true; vlg.childControlHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scr.content = _mainContent;

            // --- HP Bar ---
            _hpBar = AddBarSection("❤ HP", 20, Color.green, out var hpRow);
            _hpText = UguiMakeRowLabel("HpText", "", 13, Color.white, 80);
            _hpText.transform.SetParent(hpRow.transform, false);

            // --- Shield ---
            _shieldBar = AddBarSection("🛡 能量盾", 14, new Color(0.3f, 0.5f, 1f), out _shieldRow);
            _shieldText = UguiMakeRowLabel("ShieldText", "", 13, Color.white, 80);
            _shieldText.transform.SetParent(_shieldRow.transform, false);
            _shieldStatusText = UguiMakeRowLabel("ShieldStatus", "", 12, Color.yellow, 100);
            _shieldStatusText.transform.SetParent(_shieldRow.transform, false);
            _shieldBtnRow = new GameObject("ShieldBtns", typeof(RectTransform));
            _shieldBtnRow.transform.SetParent(_mainContent, false);
            _shieldBtnRow.GetComponent<RectTransform>().sizeDelta = new Vector2(pw - 16, 26);
            _shieldBtnRow.AddComponent<LayoutElement>().minHeight = 26;
            var sbl = _shieldBtnRow.AddComponent<HorizontalLayoutGroup>();
            sbl.spacing = 4; sbl.childControlWidth = false; sbl.childControlHeight = false;
            sbl.padding = new RectOffset(28, 0, 0, 0);

            // --- Energy ---
            _modeText = UguiMakeRowLabel("ModeText", "", 14, Color.white, pw - 16);
            _modeText.transform.SetParent(_mainContent, false);
            _consumptionText = UguiMakeRowLabel("ConsumptionText", "", 11, Color.gray, pw - 16);
            _consumptionText.transform.SetParent(_mainContent, false);

            _batteryBar = AddBarSection("🔋 电池", 16, new Color(0.2f, 0.7f, 1f), out var batteryRow);
            _batteryText = UguiMakeRowLabel("BatteryText", "", 13, Color.white, 80);
            _batteryText.transform.SetParent(batteryRow.transform, false);

            _uraniumBar = AddBarSection("☢ 铀燃料", 16, new Color(1f, 0.5f, 0f), out var uraniumRow);
            _uraniumText = UguiMakeRowLabel("UraniumText", "", 13, Color.white, 80);
            _uraniumText.transform.SetParent(uraniumRow.transform, false);

            // Eco/Burst buttons
            var modeBtnRow = new GameObject("ModeBtns", typeof(RectTransform));
            modeBtnRow.transform.SetParent(_mainContent, false);
            modeBtnRow.GetComponent<RectTransform>().sizeDelta = new Vector2(pw - 16, 30);
            modeBtnRow.AddComponent<LayoutElement>().minHeight = 30;
            var mbl = modeBtnRow.AddComponent<HorizontalLayoutGroup>();
            mbl.spacing = 4; mbl.childControlWidth = false; mbl.childControlHeight = false;

            _ecoBtn = UguiMakeBigBtn("EcoBtn", "节能:关", 100, 28, out _ecoLabel);
            _ecoBtn.transform.SetParent(modeBtnRow.transform, false);
            _ecoBtn.onClick.AddListener(() => _bot.ToggleEcoMode());

            _burstBtn = UguiMakeBigBtn("BurstBtn", "爆发:关", 100, 28, out _burstLabel);
            _burstBtn.transform.SetParent(modeBtnRow.transform, false);
            _burstBtn.onClick.AddListener(() => _bot.ToggleBurstMode());

            _ecoWarnText = UguiMakeRowLabel("EcoWarn", "", 11, Color.green, pw - 16);
            _ecoWarnText.transform.SetParent(_mainContent, false);

            _solarText = UguiMakeRowLabel("SolarText", "", 13, Color.white, pw - 16);
            _solarText.transform.SetParent(_mainContent, false);
            _nuclearText = UguiMakeRowLabel("NuclearText", "", 13, Color.white, pw - 16);
            _nuclearText.transform.SetParent(_mainContent, false);

            // Speed slider
            var speedRow = new GameObject("SpeedRow", typeof(RectTransform));
            speedRow.transform.SetParent(_mainContent, false);
            speedRow.GetComponent<RectTransform>().sizeDelta = new Vector2(pw - 16, 30);
            speedRow.AddComponent<LayoutElement>().minHeight = 30;
            var srl = speedRow.AddComponent<HorizontalLayoutGroup>();
            srl.spacing = 4; srl.childControlWidth = false; srl.childControlHeight = false;

            var speedLabel = UguiMakeRowLabel("SpeedLabel", "移动速度:", 13, Color.white, 80);
            speedLabel.transform.SetParent(speedRow.transform, false);

            var sliderGo = new GameObject("SpeedSlider", typeof(Slider));
            sliderGo.transform.SetParent(speedRow.transform, false);
            sliderGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 20);
            _speedSlider = sliderGo.GetComponent<Slider>();
            _speedSlider.minValue = 0.25f; _speedSlider.maxValue = 1f;
            _speedSlider.onValueChanged.AddListener(v => _bot.speedSliderValue = Mathf.Round(v * 100f) / 100f);

            _speedText = UguiMakeRowLabel("SpeedText", "", 13, Color.white, 60);
            _speedText.transform.SetParent(speedRow.transform, false);

            // --- Alert Range ---
            var alertRow = new GameObject("AlertRow", typeof(RectTransform));
            alertRow.transform.SetParent(_mainContent, false);
            alertRow.GetComponent<RectTransform>().sizeDelta = new Vector2(pw - 16, 30);
            alertRow.AddComponent<LayoutElement>().minHeight = 30;
            var arl = alertRow.AddComponent<HorizontalLayoutGroup>();
            arl.spacing = 4; arl.childControlWidth = false; arl.childControlHeight = false;

            var alertLabel = UguiMakeRowLabel("AlertLabel", "警觉距离:", 13, Color.white, 80);
            alertLabel.transform.SetParent(alertRow.transform, false);

            var alertGo = new GameObject("AlertSlider", typeof(Slider));
            alertGo.transform.SetParent(alertRow.transform, false);
            alertGo.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 20);
            _alertSlider = alertGo.GetComponent<Slider>();
            _alertSlider.onValueChanged.AddListener(v => { if (_combat != null) _combat.alertRange = Mathf.Round(v); });

            _alertText = UguiMakeRowLabel("AlertText", "", 13, Color.white, 60);
            _alertText.transform.SetParent(alertRow.transform, false);

            // --- Priority ---
            _priorityText = UguiMakeRowLabel("PriorityText", "", 13, Color.white, pw - 16);
            _priorityText.transform.SetParent(_mainContent, false);
            var prioBtn = UguiMakeSmallBtn("PrioBtn", "切换", new Color(0.3f, 0.3f, 0.3f), 50, 22);
            prioBtn.transform.SetParent(_priorityText.transform.parent, false);
            prioBtn.onClick.AddListener(() => _combat?.CyclePriority());

            // --- Weapon Slots ---
            var weaponRow = new GameObject("WeaponRow", typeof(RectTransform));
            weaponRow.transform.SetParent(_mainContent, false);
            weaponRow.GetComponent<RectTransform>().sizeDelta = new Vector2(pw - 16, 50);
            weaponRow.AddComponent<LayoutElement>().minHeight = 50;
            var wrl = weaponRow.AddComponent<HorizontalLayoutGroup>();
            wrl.spacing = 8; wrl.childControlWidth = true; wrl.childControlHeight = true;

            var rightArmGo = new GameObject("RightArm", typeof(RectTransform));
            rightArmGo.transform.SetParent(weaponRow.transform, false);
            var rarmLayout = rightArmGo.AddComponent<VerticalLayoutGroup>();
            rarmLayout.spacing = 2; rarmLayout.childControlWidth = true; rarmLayout.childControlHeight = false;
            var rarmTL = new GameObject("RightArmTL", typeof(RectTransform));
            rarmTL.transform.SetParent(rightArmGo.transform, false);
            rarmTL.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 22);
            var rtl = rarmTL.AddComponent<HorizontalLayoutGroup>();
            rtl.spacing = 2; rtl.childControlWidth = false; rtl.childControlHeight = false;
            var rl = UguiMakeRowLabel("RaLbl", "┌ 右臂", 11, Color.gray, 50);
            rl.transform.SetParent(rarmTL.transform, false);
            var rgear = UguiMakeSmallBtn("RaGear", "⚙", new Color(0.2f, 0.2f, 0.2f), 28, 18);
            rgear.transform.SetParent(rarmTL.transform, false);
            rgear.onClick.AddListener(() => { var s = _subRightArm; ToggleSubPanel(ref s, "RightArm", 280, 320); _subRightArm = s; });
            _rightArmText = UguiMakeRowLabel("RightArmText", "空", 13, Color.white, 180);
            _rightArmText.transform.SetParent(rightArmGo.transform, false);

            var leftArmGo = new GameObject("LeftArm", typeof(RectTransform));
            leftArmGo.transform.SetParent(weaponRow.transform, false);
            var larmLayout = leftArmGo.AddComponent<VerticalLayoutGroup>();
            larmLayout.spacing = 2; larmLayout.childControlWidth = true; larmLayout.childControlHeight = false;
            var larmTL = new GameObject("LeftArmTL", typeof(RectTransform));
            larmTL.transform.SetParent(leftArmGo.transform, false);
            larmTL.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 22);
            var ltl = larmTL.AddComponent<HorizontalLayoutGroup>();
            ltl.spacing = 2; ltl.childControlWidth = false; ltl.childControlHeight = false;
            var ll = UguiMakeRowLabel("LaLbl", "┌ 左臂", 11, Color.gray, 50);
            ll.transform.SetParent(larmTL.transform, false);
            var lgear = UguiMakeSmallBtn("LaGear", "⚙", new Color(0.2f, 0.2f, 0.2f), 28, 18);
            lgear.transform.SetParent(larmTL.transform, false);
            lgear.onClick.AddListener(() => { var s = _subLeftArm; ToggleSubPanel(ref s, "LeftArm", 260, 280); _subLeftArm = s; });
            _leftArmText = UguiMakeRowLabel("LeftArmText", "空", 13, Color.white, 180);
            _leftArmText.transform.SetParent(leftArmGo.transform, false);

            // --- Command Buttons ---
            var cmdRow = new GameObject("CmdRow", typeof(RectTransform));
            cmdRow.transform.SetParent(_mainContent, false);
            cmdRow.GetComponent<RectTransform>().sizeDelta = new Vector2(pw - 16, 34);
            cmdRow.AddComponent<LayoutElement>().minHeight = 34;
            var cml = cmdRow.AddComponent<HorizontalLayoutGroup>();
            cml.spacing = 4; cml.childControlWidth = false; cml.childControlHeight = false;
            var cmdLbl = UguiMakeRowLabel("CmdLbl", "指令:", 13, Color.white, 40);
            cmdLbl.transform.SetParent(cmdRow.transform, false);

            _followBtn = UguiMakeBigBtn("FollowBtn", "跟随", 70, 32, out _);
            _followBtn.transform.SetParent(cmdRow.transform, false);
            _followBtn.onClick.AddListener(() => _bot.SetCommand(AIBotCommand.Follow));
            var fg = UguiMakeSmallBtn("FwGear", "⚙", new Color(0.2f, 0.2f, 0.2f), 28, 32);
            fg.transform.SetParent(cmdRow.transform, false);
            fg.onClick.AddListener(() => { var s = _subFollow; ToggleSubPanel(ref s, "Follow", 220, 200); _subFollow = s; });

            _guardBtn = UguiMakeBigBtn("GuardBtn", "驻守", 70, 32, out _);
            _guardBtn.transform.SetParent(cmdRow.transform, false);
            _guardBtn.onClick.AddListener(() => _bot.SetCommand(AIBotCommand.Guard));
            var gg = UguiMakeSmallBtn("GwGear", "⚙", new Color(0.2f, 0.2f, 0.2f), 28, 32);
            gg.transform.SetParent(cmdRow.transform, false);
            gg.onClick.AddListener(() => { var s = _subGuard; ToggleSubPanel(ref s, "Guard", 240, 200); _subGuard = s; });

            _patrolBtn = UguiMakeBigBtn("PatrolBtn", "巡逻", 70, 32, out _);
            _patrolBtn.transform.SetParent(cmdRow.transform, false);
            _patrolBtn.onClick.AddListener(() => _bot.SetCommand(AIBotCommand.Patrol));
            var pg = UguiMakeSmallBtn("PwGear", "⚙", new Color(0.2f, 0.2f, 0.2f), 28, 32);
            pg.transform.SetParent(cmdRow.transform, false);
            pg.onClick.AddListener(() => { var s = _subPatrol; ToggleSubPanel(ref s, "Patrol", 260, 280); _subPatrol = s; });

            // --- Action Buttons ---
            var actRow1 = new GameObject("ActRow1", typeof(RectTransform));
            actRow1.transform.SetParent(_mainContent, false);
            actRow1.GetComponent<RectTransform>().sizeDelta = new Vector2(pw - 16, 36);
            actRow1.AddComponent<LayoutElement>().minHeight = 36;
            var a1l = actRow1.AddComponent<HorizontalLayoutGroup>();
            a1l.spacing = 4; a1l.childControlWidth = true; a1l.childControlHeight = false;

            _inventoryBtn = UguiMakeBigBtn("InvBtn", "打开背包", pw, 34, out _);
            _inventoryBtn.transform.SetParent(actRow1.transform, false);
            _inventoryBtn.onClick.AddListener(() => { var bi = _bot.GetComponent<AIBotInventory>(); if (bi != null) AIBotInventoryUI.Show(_bot, bi); });

            _fuelBtn = UguiMakeBigBtn("FuelBtn", "加燃料", pw, 34, out _);
            _fuelBtn.transform.SetParent(actRow1.transform, false);
            _fuelBtn.onClick.AddListener(() => AddFuelFromPlayerInventory());

            _repairBtn = UguiMakeBigBtn("RepairBtn", "修理", pw, 34, out _);
            _repairBtn.transform.SetParent(actRow1.transform, false);
            _repairBtn.onClick.AddListener(() => RepairFromPlayerInventory());

            var actRow2 = new GameObject("ActRow2", typeof(RectTransform));
            actRow2.transform.SetParent(_mainContent, false);
            actRow2.GetComponent<RectTransform>().sizeDelta = new Vector2(pw - 16, 36);
            actRow2.AddComponent<LayoutElement>().minHeight = 36;
            var a2l = actRow2.AddComponent<HorizontalLayoutGroup>();
            a2l.spacing = 4; a2l.childControlWidth = true; a2l.childControlHeight = false;

            _pilotBtn = UguiMakeBigBtn("PilotBtn", "驾驶", pw, 34, out _pilotLabel);
            _pilotBtn.transform.SetParent(actRow2.transform, false);
            _pilotBtn.onClick.AddListener(() => {
                var p = _bot.GetComponent<AIBotPilot>();
                if (p == null) return;
                if (p.IsPiloting) p.ExitPilot();
                else p.EnterPilot(ServiceLocator.Get<_Game.Systems.Character.PlayerCharacter>()?.gameObject);
            });

            _aiOverrideBtn = UguiMakeBigBtn("AIOverrideBtn", "AI接管:关", pw, 34, out _aiOverrideLabel);
            _aiOverrideBtn.transform.SetParent(actRow2.transform, false);
            _aiOverrideBtn.onClick.AddListener(() => { if (_combat != null) _combat.aiWeaponOverride = !_combat.aiWeaponOverride; });

            // 重伤警告
            var hpWarnText = UguiMakeRowLabel("HpWarn", "⚠ 重伤！", 14, Color.red, pw - 16);
            hpWarnText.transform.SetParent(_mainContent, false);
            hpWarnText.gameObject.name = "HpWarnText";
        }

        Image AddBarSection(string label, float height, Color fillColor, out GameObject row)
        {
            row = new GameObject("BarRow_" + label, typeof(RectTransform));
            row.transform.SetParent(_mainContent, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(400, height + 4);
            row.AddComponent<LayoutElement>().minHeight = height + 4;
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4; hl.childControlWidth = false; hl.childControlHeight = true;

            var lbl = UguiMakeRowLabel("Label", label, 13, Color.white, 80);
            lbl.transform.SetParent(row.transform, false);

            var bgGo = new GameObject("Bg", typeof(Image));
            bgGo.transform.SetParent(row.transform, false);
            bgGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200, height);
            bgGo.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(bgGo.transform, false);
            var img = fillGo.GetComponent<Image>();
            img.color = fillColor;
            var fr = fillGo.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0, 1);
            fr.pivot = new Vector2(0, 0.5f); fr.sizeDelta = Vector2.zero;
            return img;
        }

        void ToggleSubPanel(ref GameObject sub, string name, float w, float h)
        {
            if (sub == null)
            {
                sub = new GameObject("Sub_" + name, typeof(RectTransform), typeof(Image));
                sub.transform.SetParent(_canvasGo.transform, false);
                sub.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
                sub.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.14f, 0.95f);
                // 放在主面板右侧
                var mr = _mainPanelGo.GetComponent<RectTransform>();
                var sr = sub.GetComponent<RectTransform>();
                sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
                sr.pivot = new Vector2(0.5f, 0.5f);
                sr.anchoredPosition = mr.anchoredPosition + new Vector2(mr.sizeDelta.x / 2 + w / 2 + 10, 0);

                // 标题栏
                var tb = new GameObject("Title", typeof(RectTransform), typeof(Image));
                tb.transform.SetParent(sub.transform, false);
                var tbr2 = tb.GetComponent<RectTransform>();
                tbr2.anchorMin = new Vector2(0, 1); tbr2.anchorMax = new Vector2(1, 1);
                tbr2.pivot = new Vector2(0.5f, 1); tbr2.sizeDelta = new Vector2(0, 22);
                tb.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.2f);

                var titleT = UguiMakeText("TpTitle", 13, FontStyle.Bold, TextAnchor.MiddleCenter, w, 22);
                titleT.transform.SetParent(tb.transform, false);
                titleT.text = name;
                UguiSetStretch(titleT.GetComponent<RectTransform>());

                var close = UguiMakeSmallBtn("Close", "✕", new Color(0.5f, 0.2f, 0.2f), 24, 20);
                close.transform.SetParent(tb.transform, false);
                var cbr2 = close.GetComponent<RectTransform>();
                cbr2.anchorMin = new Vector2(1, 0.5f); cbr2.anchorMax = new Vector2(1, 0.5f);
                cbr2.pivot = new Vector2(1, 0.5f); cbr2.anchoredPosition = new Vector2(-4, 0);
                var capSub = sub;
                close.onClick.AddListener(() => capSub.SetActive(false));

                // Scroll content
                var sc = new GameObject("Scroll", typeof(Image), typeof(ScrollRect));
                sc.transform.SetParent(sub.transform, false);
                var scr2 = sc.GetComponent<RectTransform>();
                scr2.anchorMin = new Vector2(0, 0); scr2.anchorMax = new Vector2(1, 1);
                scr2.offsetMin = new Vector2(4, 4); scr2.offsetMax = new Vector2(-4, -30);
                sc.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                var scrr = sc.GetComponent<ScrollRect>();
                scrr.horizontal = false; scrr.scrollSensitivity = 15f;
                var vp2 = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                vp2.transform.SetParent(sc.transform, false);
                UguiSetStretch(vp2.GetComponent<RectTransform>());
                scrr.viewport = vp2.GetComponent<RectTransform>();
                var ct2 = new GameObject("Content", typeof(RectTransform));
                ct2.transform.SetParent(vp2.transform, false);
                var ctr2 = ct2.GetComponent<RectTransform>();
                ctr2.anchorMin = new Vector2(0, 1); ctr2.anchorMax = new Vector2(1, 1);
                ctr2.pivot = new Vector2(0.5f, 1); ctr2.sizeDelta = new Vector2(0, 0);
                ct2.AddComponent<VerticalLayoutGroup>().spacing = 4;
                ct2.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scrr.content = ctr2;
                sub.name = name + "_SubPanel"; // mark content parent
            }
            sub.SetActive(!sub.activeSelf);
        }

        // ============================================================
        // UGUI — 刷新
        // ============================================================

        void RefreshUGUI()
        {
            // HP
            float hpPct = _bot.HealthPercent;
            _hpBar.GetComponent<RectTransform>().anchorMax = new Vector2(hpPct, 1);
            _hpBar.color = hpPct > 0.5f ? Color.green : (hpPct > 0.25f ? Color.yellow : Color.red);
            _hpText.text = $"{_bot.HP:F0}/{_bot.MaxHP:F0}";

            // Shield
            bool hasShield = _bot.IsShieldAvailable || _bot.shieldActive;
            _shieldRow.SetActive(hasShield);
            _shieldBtnRow.SetActive(false);
            if (hasShield)
            {
                float spct = _bot.ShieldPercent;
                _shieldBar.GetComponent<RectTransform>().anchorMax = new Vector2(spct, 1);
                _shieldBar.color = _bot.IsShieldActive && _bot.shieldStartupTimer <= 0f ? new Color(0.3f, 0.5f, 1f) : Color.gray;
                _shieldText.text = _bot.shieldActive ? $"{_bot.ShieldCurrentHP:F0}/{_bot.ShieldMaxHP:F0}" : "未开启";

                if (_bot.shieldStartupTimer > 0f)
                    _shieldStatusText.text = $"启动中... {_bot.shieldStartupTimer:F1}s";
                else if (_bot.shieldActive)
                    _shieldStatusText.text = _bot.shieldActive && _bot.shieldCurrentHP < _bot.shieldMaxHP
                        ? $"维持:{AIBot.SHIELD_MAINTENANCE_RECHARGE}铀/s 回复:{AIBot.SHIELD_REGEN_PER_SEC}盾/s"
                        : $"维持:{AIBot.SHIELD_MAINTENANCE_FULL}铀/s";
                else
                    _shieldStatusText.text = "";

                // Shield buttons
                if (_bot.IsShieldAvailable)
                {
                    _shieldBtnRow.SetActive(true);
                    foreach (Transform t in _shieldBtnRow.transform) Destroy(t.gameObject);
                    if (_bot.shieldActive)
                    {
                        var offBtn = UguiMakeSmallBtn("ShieldOff", "关闭能量盾", new Color(0.5f, 0.2f, 0.2f), 120, 24);
                        offBtn.transform.SetParent(_shieldBtnRow.transform, false);
                        offBtn.onClick.AddListener(() => _bot.DeactivateShield());
                    }
                    else
                    {
                        var onBtn = UguiMakeSmallBtn("ShieldOn", "开启能量盾 (30铀)", new Color(0.2f, 0.5f, 0.2f), 140, 24);
                        onBtn.transform.SetParent(_shieldBtnRow.transform, false);
                        onBtn.interactable = _bot.UraniumCurrent >= AIBot.SHIELD_STARTUP_COST;
                        onBtn.onClick.AddListener(() => _bot.ActivateShield());
                    }
                }
            }

            // HP warning
            var hpWarn = _mainContent.transform.Find("HpWarnText");
            if (hpWarn != null) hpWarn.gameObject.SetActive(_bot.IsLowHP);

            // Energy mode
            var mode = _bot.CurrentEnergyMode;
            string modeLabel = mode switch { EnergyMode.EnergySaving => "♻ 节能模式", EnergyMode.Electric => "● 电力模式", EnergyMode.Uranium => "☢ 铀模式", EnergyMode.Burst => "💥 爆发模式", _ => "? 未知" };
            _modeText.text = $"■ {modeLabel} (当前)";
            _modeText.color = mode switch { EnergyMode.EnergySaving => Color.green, EnergyMode.Electric => Color.cyan, EnergyMode.Uranium => new Color(1f, 0.6f, 0), EnergyMode.Burst => new Color(1f, 0.3f, 0), _ => Color.white };
            _consumptionText.text = $"消耗×{_bot.ConsumptionMultiplier:F2}  速度×{_bot.SpeedMultiplier:F2}  冷却×{_bot.CooldownMultiplier:F2}";

            // Battery
            _batteryBar.GetComponent<RectTransform>().anchorMax = new Vector2(_bot.BatteryPercent, 1);
            _batteryText.text = $"{_bot.BatteryCurrent:F1}/{_bot.BatteryMax:F0}";

            // Uranium
            _uraniumBar.GetComponent<RectTransform>().anchorMax = new Vector2(_bot.UraniumPercent, 1);
            _uraniumText.text = $"{_bot.UraniumCurrent:F1}/{_bot.UraniumMax:F0}";

            // Eco/Burst buttons
            _ecoLabel.text = _bot.ecoModeEnabled ? "节能:开" : "节能:关";
            _ecoBtn.GetComponent<Image>().color = _bot.ecoModeEnabled ? Color.green : Color.gray;
            _burstLabel.text = _bot.burstModeEnabled ? "爆发:开" : "爆发:关";
            _burstBtn.GetComponent<Image>().color = _bot.burstModeEnabled ? new Color(1f, 0.3f, 0) : Color.gray;
            _ecoWarnText.text = _bot.ecoModeEnabled ? "⚠ 节能中：激光/AI辅助/AI接管已禁用" : "";
            _ecoWarnText.gameObject.SetActive(_bot.ecoModeEnabled);

            // Solar/Nuclear
            _solarText.text = _bot.IsSolarActive ? $"☀ 太阳能板 (+{_bot.CurrentSolarRate:F1}/h)" : "☀ 太阳能板 (休眠)";
            _solarText.color = _bot.IsSolarActive ? Color.green : Color.gray;
            _nuclearText.text = _bot.IsNuclearActive ? $"⚛ 微型反应堆 (+{_bot.CurrentNuclearRate:F1}/h)" : "⚛ 微型反应堆 (休眠)";
            _nuclearText.color = _bot.IsNuclearActive ? new Color(1f, 0.6f, 0) : Color.gray;

            // Speed slider
            _speedSlider.SetValueWithoutNotify(_bot.speedSliderValue);
            float actualSpeed = (_bot.IsPiloted ? _bot.pilotBaseSpeed : _bot.moveSpeed) * _bot.SpeedMultiplier * _bot.speedSliderValue;
            _speedText.text = $"{actualSpeed:F1} m/s";

            // Alert range
            if (_combat != null)
            {
                _alertSlider.minValue = _combat.alertRangeMin;
                _alertSlider.maxValue = _combat.alertRangeMax;
                _alertSlider.SetValueWithoutNotify(_combat.alertRange);
                _alertText.text = $"{_combat.alertRange:F0}m";
            }

            // Priority
            if (_combat != null)
            {
                _priorityText.text = $"攻击优先级: [{PriorityName(_combat.slot1)}] → [{PriorityName(_combat.slot2)}] → [{PriorityName(_combat.slot3)}]";
            }

            // Weapon slots
            if (_combat != null)
            {
                _rightArmText.text = _combat.CurrentRightArm switch { RightArmWeapon.None => "空", RightArmWeapon.Pistol => "M1911", RightArmWeapon.Rifle => "AK-47", RightArmWeapon.Shotgun => "雷明顿870", RightArmWeapon.ElectromagneticRifle => "电磁步枪", _ => "?" };
                _leftArmText.text = _combat.CurrentLeftArm switch { LeftArmWeapon.None => "空", LeftArmWeapon.Shield => "盾牌 (受伤-30%)", LeftArmWeapon.Chainsaw => "电锯 (15/s)", LeftArmWeapon.Knife => "短刀 (20/1.5s)", _ => "?" };
            }

            // Command buttons highlight
            Color greenBg = new Color(0f, 0.5f, 0f);
            _followBtn.GetComponent<Image>().color = _bot.CurrentCommand == AIBotCommand.Follow ? greenBg : Color.gray;
            _guardBtn.GetComponent<Image>().color = _bot.CurrentCommand == AIBotCommand.Guard ? greenBg : Color.gray;
            _patrolBtn.GetComponent<Image>().color = _bot.CurrentCommand == AIBotCommand.Patrol ? greenBg : Color.gray;

            // Pilot button
            var pilot = _bot.GetComponent<AIBotPilot>();
            _pilotBtn.interactable = pilot != null;
            if (pilot != null)
            {
                bool isPiloting = pilot.IsPiloting;
                _pilotLabel.text = isPiloting ? "退出驾驶" : "驾驶";
                _pilotBtn.GetComponent<Image>().color = isPiloting ? new Color(1f, 0.5f, 0f) : Color.gray;
            }
            else { _pilotLabel.text = "驾驶(缺组件)"; }

            // AI Override
            if (_combat != null)
            {
                bool canOverride = _bot.IsAIWeaponOverrideEnabled;
                _aiOverrideBtn.interactable = canOverride;
                bool ov = _combat.aiWeaponOverride;
                _aiOverrideLabel.text = ov ? "AI接管:开" : "AI接管:关";
                _aiOverrideBtn.GetComponent<Image>().color = canOverride ? (ov ? Color.green : Color.gray) : Color.gray;
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

        Text UguiMakeText(string name, int size, FontStyle style, TextAnchor align, float w, float h)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.alignment = align; t.color = Color.white; t.raycastTarget = false;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            return t;
        }

        Text UguiMakeRowLabel(string name, string text, int size, Color color, float w)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.alignment = TextAnchor.MiddleLeft;
            t.color = color; t.text = text; t.raycastTarget = false;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, 20);
            go.GetComponent<LayoutElement>().minWidth = w;
            return t;
        }

        Button UguiMakeSmallBtn(string name, string text, Color bg, float w, float h)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = bg;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var lbl = new GameObject("Label", typeof(Text));
            lbl.transform.SetParent(go.transform, false);
            var t = lbl.GetComponent<Text>();
            t.font = _font; t.fontSize = 11; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.text = text;
            UguiSetStretch(lbl.GetComponent<RectTransform>());
            return go.GetComponent<Button>();
        }

        Button UguiMakeBigBtn(string name, string text, float w, float h, out Text label)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = Color.gray;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w > 400 ? w * 0.33f : w, h);
            var lbl = new GameObject("Label", typeof(Text));
            lbl.transform.SetParent(go.transform, false);
            label = lbl.GetComponent<Text>();
            label.font = _font; label.fontSize = 13; label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false;
            label.text = text;
            UguiSetStretch(lbl.GetComponent<RectTransform>());
            return go.GetComponent<Button>();
        }






        string PriorityName(AttackPriority p)
        {
            switch (p)
            {
                case AttackPriority.Laser: return "激光";
                case AttackPriority.RightArm: return "右臂";
                case AttackPriority.LeftArm: return "左臂";
                default: return "?";
            }
        }




        // ============================================================
        // 加燃料 / 修理
        // ============================================================

        void AddFuelFromPlayerInventory()
        {
            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.energyValue > 0 && placed.count > 0)
                {
                    int toConsume = Mathf.Min(placed.count, 5);
                    for (int i = 0; i < toConsume; i++)
                    {
                        inv.RemoveItem(placed.itemData, 1);
                        _bot.AddBatteryEnergy(40f);
                    }
                    return;
                }
            }

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.energyValue > 0 && placed.count > 0)
                {
                    inv.RemoveItem(placed.itemData, 1);
                    _bot.AddUraniumFuel();
                    return;
                }
            }
        }

        void RepairFromPlayerInventory()
        {
            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.repairValue > 0 && placed.count > 0)
                {
                    inv.RemoveItem(placed.itemData, 1);
                    _bot.RepairHP(50f);
                    return;
                }
            }

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.repairValue > 0 && placed.count > 0)
                {
                    inv.RemoveItem(placed.itemData, 1);
                    _bot.RepairHP(25f);
                    return;
                }
            }
        }



        // ============================================================
        // 武器/弹药操作
        // ============================================================

        void TryEquipRightArm(RightArmWeapon weapon, ItemData itemData)
        {
            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            if (_combat.CurrentRightArm != RightArmWeapon.None)
                TryUnequipRightArm();

            inv.RemoveItem(itemData, 1);
            _combat.EquipRightArm(weapon);
            _equippedRightArmItem = itemData;

            string ammoName = AIBotCombat.GetAmmoNameForWeapon(weapon);
            if (!string.IsNullOrEmpty(ammoName))
                LoadAmmoToBot(ammoName);
        }

        void TryUnequipRightArm()
        {
            var weapon = _combat.CurrentRightArm;
            if (weapon == RightArmWeapon.None) return;

            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            if (_equippedRightArmItem != null)
            {
                inv.AddItem(_equippedRightArmItem, 1);
                _equippedRightArmItem = null;
            }

            string ammoName = AIBotCombat.GetAmmoNameForWeapon(weapon);
            if (!string.IsNullOrEmpty(ammoName))
                UnloadAmmoFromBot(ammoName);

            _combat.UnequipRightArm();
        }

        void TryEquipLeftArm(LeftArmWeapon weapon, ItemData itemData)
        {
            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            if (_combat.CurrentLeftArm != LeftArmWeapon.None)
                TryUnequipLeftArm();

            inv.RemoveItem(itemData, 1);
            _combat.EquipLeftArm(weapon);
            _equippedLeftArmItem = itemData;
        }

        void TryUnequipLeftArm()
        {
            var weapon = _combat.CurrentLeftArm;
            if (weapon == LeftArmWeapon.None) return;

            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            if (_equippedLeftArmItem != null)
            {
                inv.AddItem(_equippedLeftArmItem, 1);
                _equippedLeftArmItem = null;
            }

            _combat.UnequipLeftArm();
        }

        void LoadAmmoToBot(string ammoName)
        {
            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            var itemData = FindPlayerItem(inv, ammoName);
            if (itemData == null) return;

            int playerCount = CountPlayerItem(inv, ammoName);
            if (playerCount <= 0) return;

            if (!_knownAmmoItems.ContainsKey(ammoName))
                _knownAmmoItems[ammoName] = itemData;

            int loaded = _combat.LoadAmmo(ammoName, playerCount);
            inv.RemoveItem(itemData, loaded);
        }

        void UnloadAmmoFromBot(string ammoName)
        {
            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            int botCount = _combat.GetAmmoCount(ammoName);
            if (botCount <= 0) return;

            var itemData = FindPlayerItem(inv, ammoName);
            if (itemData == null)
                _knownAmmoItems.TryGetValue(ammoName, out itemData);
            if (itemData == null) return;

            int unloaded = _combat.UnloadAmmo(ammoName, botCount);
            inv.AddItem(itemData, unloaded);
        }

        // ============================================================
        // 背包扫描工具
        // ============================================================

        int CountPlayerItem(_Game.Systems.Inventory.Inventory inv, string itemName)
        {
            int total = 0;
            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.itemName == itemName)
                    total += placed.count;
            }
            return total;
        }

        ItemData FindPlayerItem(_Game.Systems.Inventory.Inventory inv, string itemName)
        {
            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.itemName == itemName && placed.count > 0)
                    return placed.itemData;
            }
            return null;
        }

        string WeaponDisplayName(RightArmWeapon w)
        {
            switch (w)
            {
                case RightArmWeapon.None: return "空";
                case RightArmWeapon.Pistol: return "M1911";
                case RightArmWeapon.Rifle: return "AK-47";
                case RightArmWeapon.Shotgun: return "雷明顿870";
                case RightArmWeapon.ElectromagneticRifle: return "电磁步枪";
                default: return "?";
            }
        }

        string WeaponDisplayName(LeftArmWeapon w)
        {
            switch (w)
            {
                case LeftArmWeapon.None: return "空";
                case LeftArmWeapon.Shield: return "盾牌";
                case LeftArmWeapon.Chainsaw: return "电锯";
                case LeftArmWeapon.Knife: return "短刀";
                default: return "?";
            }
        }





        void LoadFusionCore(int slotIdx)
        {
            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData == null || placed.count <= 0) continue;
                bool isSmall = placed.itemData.fusionCoreSize == FusionCoreSize.Small;
                bool isLarge = placed.itemData.fusionCoreSize == FusionCoreSize.Large;
                if (!isSmall && !isLarge) continue;

                var itemRef = placed.itemData;
                inv.RemoveItem(placed.itemData, 1);

                _bot.reactorSlots[slotIdx] = new FusionCoreSlot
                {
                    itemName = itemRef.itemName,
                    itemData = itemRef,
                    burnTime = isLarge ? 12f : 4f,
                    burnRemaining = isLarge ? 12f : 4f,
                    outputRate = isLarge ? 45f : 30f
                };
                return;
            }
        }

        void ReturnFusionCore(int slotIdx)
        {
            var inv = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            var slot = _bot.reactorSlots[slotIdx];
            if (slot.IsEmpty) return;

            if (slot.itemData != null)
                inv.AddItem(slot.itemData, 1);

            _bot.reactorSlots[slotIdx] = default;
        }

    }
}
