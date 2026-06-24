using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using _Game.Core;
using _Game.Config;
using _Game.Systems.Inventory;
using _Game.Systems.Character;
using _Game.Systems.Durability;
using Inv = _Game.Systems.Inventory.Inventory;
using System.Collections.Generic;

namespace _Game.UI
{
    /// <summary>
    /// 背包 UI（双面板）
    /// - Tab = 总览（显示所有容器）
    /// - V = 循环切换容器（胸挂→上衣→腰带→裤子→背包→循环）
    /// - ESC = 关闭所有面板
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("总览面板（Tab）")]
        public GameObject overviewPanel;            // 总览面板
        public Text overviewInfoText;
        public GameObject overviewGridContainer;    // 总览的格子容器

        [Header("快捷面板（V 循环切换）")]
        public GameObject quickPanel;
        public Text quickInfoText;
        public GameObject quickGridContainer;

        [Header("浮动提示")]
        public Text floatingToastText;

        [Header("外观")]
        public Color emptyColor = new Color(0.2f, 0.2f, 0.2f);
        public Color occupiedColor = new Color(0.3f, 0.7f, 0.3f);
        public Color overloadColor = new Color(0.8f, 0.3f, 0.3f);

        private Inv _inventory;

        private GameObject _survivalHUDGo;
        private GameObject _quickItemBarGo;

        // V 循环顺序
        private static readonly EquipSlot[] CycleOrder = new EquipSlot[]
        {
            EquipSlot.Vest, EquipSlot.Tops, EquipSlot.Belt,
            EquipSlot.Pants, EquipSlot.Backpack
        };
        // 兜底检索顺序（按这个顺序找第一个可用容器）
        private static readonly EquipSlot[] FallbackOrder = new EquipSlot[]
        {
            EquipSlot.Tops, EquipSlot.Vest, EquipSlot.Belt,
            EquipSlot.Pants, EquipSlot.Backpack
        };
        private int _cycleIndex = 0;
        private EquipSlot _lastQuickSlot = EquipSlot.Tops;  // V 最后打开的容器
        private float _overviewScrollY;
        private float _overviewContentHeight;
        private RectTransform _overviewScrollContent;

        // 容器折叠状态
        private Dictionary<EquipSlot, bool> _containerCollapsed = new Dictionary<EquipSlot, bool>();
        private Dictionary<EquipSlot, Image> _dollDurBars = new Dictionary<EquipSlot, Image>(); // 纸娃娃装备槽耐久条
        private bool _showOverviewPending;
        private GameObject _itemDetailPanel; // 右键物品详情浮窗
        private Text _detailText;


        void Awake()
        {
            ServiceLocator.Register(this);
        }

        void Start()
        {
            if (!UIModeConfig.UseUGUI) { enabled = false; return; }
            _inventory = ServiceLocator.Get<Inv>();

            // 确保 EventSystem 存在（拖拽必需）
            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 确保有 Canvas（兜底：Player 上没有预置 Canvas 时自动创建）
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("InventoryCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                canvasGo.transform.SetParent(transform, false);
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 0;
                Debug.LogWarning("[InventoryUI] Canvas 缺失，已自动创建");
            }
            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 自动创建拖拽管理器
            if (DragDropManager.Instance == null)
            {
                var ddGo = new GameObject("DragDropManager", typeof(DragDropManager));
                DontDestroyOnLoad(ddGo);
            }

            // 兜底：自动创建总览面板和快捷面板（不再依赖编辑器手动赋值）
            EnsurePanelsExist();

            // 查找其他 UI 对象
            _survivalHUDGo = GameObject.Find("SurvivalHUD");
            _quickItemBarGo = GameObject.Find("QuickItemBar");

            // 禁用 ScrollRect（与物品拖拽冲突）
            if (overviewPanel != null)
            {
                var sr = overviewPanel.GetComponent<UnityEngine.UI.ScrollRect>();
                if (sr != null) sr.enabled = false;
            }

            // 两个面板默认都隐藏
            if (overviewPanel != null) overviewPanel.SetActive(false);
            if (quickPanel != null) quickPanel.SetActive(false);
            if (floatingToastText != null) floatingToastText.gameObject.SetActive(false);

            // 默认上衣
            _inventory.ActiveContainer = _inventory.GetContainer(EquipSlot.Tops);

            EventBus.Subscribe<InventoryChanged>(OnInventoryChanged);
            EventBus.Subscribe<InventoryViewChangedEvent>(OnViewChanged);
            EventBus.Subscribe<CharacterStatsChanged>(OnCharacterStatsChanged);
            EventBus.Subscribe<DurabilityChangedEvent>(OnDurabilityChanged);

        }

        /// <summary> 兜底创建 UI 面板（场景丢失/编辑器未赋值时自动恢复） </summary>
        void EnsurePanelsExist()
        {
            // 确定父节点：优先挂在 Canvas 下（UGUI 必须）
            var canvas = GetComponentInParent<Canvas>();
            var panelParent = canvas != null ? canvas.transform : transform;

            if (overviewPanel == null)
                overviewPanel = panelParent.Find("OverviewPanel")?.gameObject;
            if (quickPanel == null)
                quickPanel = panelParent.Find("QuickPanel")?.gameObject;

            if (overviewPanel == null)
            {
                overviewPanel = UGUIBuilder.CreateStretchPanel("OverviewPanel", panelParent,
                    new Color(0.08f, 0.08f, 0.1f, 0.95f));
                overviewPanel.SetActive(false);
                overviewGridContainer = new GameObject("GridContainer", typeof(RectTransform));
                overviewGridContainer.transform.SetParent(overviewPanel.transform, false);
                var grt = overviewGridContainer.GetComponent<RectTransform>();
                grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
                grt.offsetMin = new Vector2(10, 10); grt.offsetMax = new Vector2(-10, -10);
                Debug.LogWarning("[InventoryUI] overviewPanel 缺失，已自动创建兜底面版");
            }
            if (quickPanel == null)
            {
                quickPanel = UGUIBuilder.CreateStretchPanel("QuickPanel", panelParent,
                    new Color(0.05f, 0.05f, 0.08f, 0.9f));
                quickPanel.SetActive(false);
                quickGridContainer = new GameObject("GridContainer", typeof(RectTransform));
                quickGridContainer.transform.SetParent(quickPanel.transform, false);
                var grt = quickGridContainer.GetComponent<RectTransform>();
                grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
                grt.offsetMin = new Vector2(10, 10); grt.offsetMax = new Vector2(-10, -10);
                Debug.LogWarning("[InventoryUI] quickPanel 缺失，已自动创建兜底面版");
            }
        }

        void OnEnable()
        {
            InputRouter.BindKey(KeyCode.Escape, InputPriority.UI, HandleEsc, this);
            InputRouter.BindKey(KeyCode.Tab, InputPriority.UI, HandleTab, this);
            InputRouter.BindKey(KeyCode.V, InputPriority.UI, HandleV, this);
        }

        void OnDisable() { InputRouter.UnbindAll(this); }

        bool HandleEsc()
        {
            bool anyOpen = false;
            if (overviewPanel != null && overviewPanel.activeSelf)
            { overviewPanel.SetActive(false); SetOtherUIVisible(true); anyOpen = true; }
            if (quickPanel != null && quickPanel.activeSelf)
            { quickPanel.SetActive(false); anyOpen = true; }
            if (anyOpen)
            {
                if (DragDropManager.Instance != null) DragDropManager.Instance.DeselectItem();
                return true;
            }
            return false;
        }

        bool HandleTab()
        {
            if (overviewPanel == null)
            {
                Debug.LogWarning("[InventoryUI] HandleTab: overviewPanel 为 null，无法打开背包。请检查 EnsurePanelsExist 是否已执行。");
                return false;
            }

            // 打开背包时关闭建造模式
            ExitBuildModeIfActive();

            if (quickPanel != null && quickPanel.activeSelf)
                quickPanel.SetActive(false);

            bool wasActive = overviewPanel.activeSelf;
            overviewPanel.SetActive(!wasActive);
            SetOtherUIVisible(wasActive);
            if (!wasActive)
                ShowOverview();
            else if (DragDropManager.Instance != null)
                DragDropManager.Instance.DeselectItem();
            return true;
        }

        bool HandleV()
        {
            // 打开背包时关闭建造模式
            ExitBuildModeIfActive();

            bool overviewActive = overviewPanel != null && overviewPanel.activeSelf;
            if (overviewActive) return false; // 总览打开时 V 不生效
            bool isOpen = quickPanel != null && quickPanel.activeSelf;
            if (isOpen)
                CycleToNextContainer();
            else
                OpenContainer(_lastQuickSlot);
            return true;
        }

        void ExitBuildModeIfActive()
        {
            var bmc = ServiceLocator.Get<_Game.Systems.Building.BuildModeController>();
            if (bmc != null && bmc.IsBuildMode)
                bmc.ForceExit();
        }

        void Update()
        {
            // 总览打开时：滚轮滚动，仅鼠标在面板区域内生效（拖拽时不滚动）
            bool overviewActive = overviewPanel != null && overviewPanel.activeSelf;
            if (overviewActive && _overviewContentHeight > 0
                && !(DragDropManager.Instance != null && DragDropManager.Instance.IsDragging)
                && IsMouseOverOverviewPanel())
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel") * 60f;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _overviewScrollY = Mathf.Clamp(_overviewScrollY - scroll, 0, _overviewContentHeight);
                    if (_overviewScrollContent != null)
                        _overviewScrollContent.anchoredPosition = new Vector2(0, _overviewScrollY);
                }
            }
        }

        bool IsMouseOverOverviewPanel()
        {
            if (overviewPanel == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                overviewPanel.GetComponent<RectTransform>(), Input.mousePosition);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<InventoryChanged>(OnInventoryChanged);
            EventBus.Unsubscribe<InventoryViewChangedEvent>(OnViewChanged);
            EventBus.Unsubscribe<CharacterStatsChanged>(OnCharacterStatsChanged);
        }

        void OnInventoryChanged(InventoryChanged evt)
        {
            if (_inventory == null) return;

            // 浮动提示
            if (floatingToastText != null)
            {
                string msg = evt.Action switch
                {
                    "overload_warning" => $"负重超载！{evt.ItemName} 太重了",
                    "slot_full" => $"空间不够！放不下 {evt.ItemName}",
                    "belt_full" => $"腰带空间不足！放不下 {evt.ItemName}",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(msg))
                {
                    floatingToastText.gameObject.SetActive(true);
                    floatingToastText.text = msg;
                    CancelInvoke(nameof(HideToast));
                    Invoke(nameof(HideToast), 2.5f);
                }
            }

            // 刷新当前打开的面板
            if (overviewPanel != null && overviewPanel.activeSelf)
                ShowOverview();
            else if (quickPanel != null && quickPanel.activeSelf)
                ShowQuickView();
        }

        void OnViewChanged(InventoryViewChangedEvent evt)
        {
            if (_inventory == null) return;

            // 总面板打开时，刷新内容区（保留标签栏）
            if (overviewPanel != null && overviewPanel.activeSelf)
            {
                ClearContainer(overviewGridContainer);
                ShowEquipTabContent();
            }
        }

        void HideToast()
        {
            if (floatingToastText != null)
                floatingToastText.gameObject.SetActive(false);
        }

        // ===== 总览面板（Tab） =====

        // 当前选中的标签
        private string _currentTab = "装备容器";
        private static readonly string[] TabNames = { "装备容器", "角色", "制作", "地图", "设置" };

        public void ShowOverview()
        {
            if (_showOverviewPending) return;
            _showOverviewPending = true;
            StartCoroutine(ShowOverviewCoroutine());
        }

        System.Collections.IEnumerator ShowOverviewCoroutine()
        {
            yield return null;
            _showOverviewPending = false;
            ShowOverviewInternal();
        }

        void ShowOverviewInternal()
        {
            if (overviewGridContainer == null || _inventory == null) return;
            Transform root = overviewGridContainer.transform.parent;
            var oldBar = root.Find("TopTabBar");
            if (oldBar != null) DestroyImmediate(oldBar.gameObject);
            CreateTopTabBar(root);
            var gridRt = overviewGridContainer.GetComponent<RectTransform>();
            gridRt.offsetMax = new Vector2(gridRt.offsetMax.x, -45);
            ClearContainer(overviewGridContainer);
            if (DragDropManager.Instance != null) DragDropManager.Instance.ClearCells();
            var glg = overviewGridContainer.GetComponent<GridLayoutGroup>();
            if (glg != null) DestroyImmediate(glg);
            var vlg = overviewGridContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);
            switch (_currentTab)
            {
                case "装备容器": ShowEquipTabContent(); break;
                case "角色": ShowCharacterTabContent(); break;
                default: ShowPlaceholderTab(_currentTab); break;
            }
            if (DragDropManager.Instance != null) DragDropManager.Instance.RefreshSelectionBorder();
        }

        void CreateTopTabBar(Transform root)
        {
            var bar = new GameObject("TopTabBar", typeof(Image), typeof(RectTransform));
            bar.transform.SetParent(root, false);
            var barImg = bar.GetComponent<Image>();
            barImg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            var barRt = bar.GetComponent<RectTransform>();
            // 锚定到顶部
            barRt.anchorMin = new Vector2(0, 1);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.offsetMin = new Vector2(10, -40);
            barRt.offsetMax = new Vector2(-10, 0);

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 6;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(8, 8, 4, 4);

            foreach (var name in TabNames)
            {
                bool isActive = name == _currentTab;
                var btnObj = new GameObject($"Tab_{name}", typeof(Image), typeof(Button));
                btnObj.transform.SetParent(bar.transform, false);

                var img = btnObj.GetComponent<Image>();
                img.color = isActive ? new Color(0.3f, 0.5f, 0.8f, 0.9f) : new Color(0.15f, 0.15f, 0.15f, 0.8f);
                img.raycastTarget = true;

                var textObj = new GameObject("Label", typeof(Text));
                textObj.transform.SetParent(btnObj.transform, false);
                var textRt = textObj.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;

                var text = textObj.GetComponent<Text>();
                text.text = name;
                text.fontSize = 14;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = isActive ? Color.white : new Color(0.5f, 0.5f, 0.5f);

                var btn = btnObj.GetComponent<Button>();
                var capturedName = name;
                btn.onClick.AddListener(() => OnTabClicked(capturedName));
            }
        }

        void OnTabClicked(string tabName)
        {
            _currentTab = tabName;
            if (tabName != "装备容器" && tabName != "角色")
            {
                ShowToast($"{tabName} 开发中...");
            }
            else
            {
                SetOtherUIVisible(false);
            }
            ShowOverview();
        }

        // ═══ GridInventory2D 渲染路径 ═══

        /// <summary> 用 SlotData 渲染单个容器网格（O(slots)，替代 O(w×h) 遍历） </summary>
        void DrawContainerFromView(RectTransform parent, ContainerView view, float panelW, ref float y)
        {
            float cellSize = 44f, spacing = 3f;
            float headerH = 24f;
            bool hasEquip = true;

            // 折叠头部
            var container = _inventory?.GetContainer(view.equipSlot);
            string label = container != null ? container.containerName : view.id;
            var headerRt = MakeRect($"Hdr_{view.id}", parent, 0, y, panelW, headerH);
            var headerImg = headerRt.gameObject.AddComponent<Image>();
            headerImg.color = hasEquip ? new Color(0.12f, 0.18f, 0.14f, 0.9f) : new Color(0.08f, 0.08f, 0.1f, 0.85f);

            var info = AddLabel(headerRt, $"{label}  {view.usedSlots}/{view.totalSlots}");
            info.fontSize = 12;
            info.color = view.usedSlots >= view.totalSlots ? new Color(1f, 0.5f, 0f) : Color.white;

            y -= headerH + 4f;

            bool isCollapsed = _containerCollapsed.ContainsKey(view.equipSlot) && _containerCollapsed[view.equipSlot];
            if (isCollapsed) return;

            float gridAreaH = view.height * cellSize + (view.height - 1) * spacing;
            float gridAreaW = view.width * cellSize + (view.width - 1) * spacing;

            var gridBg = MakeRect($"Grid_{view.id}", parent, 4, y, panelW - 8, gridAreaH + 4f);
            var gridBgImg = gridBg.gameObject.AddComponent<Image>();
            gridBgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.4f);
            gridBgImg.raycastTarget = false;

            // O(slots): 直接遍历物品列表，不双重循环
            foreach (var slot in view.slots)
            {
                if (slot.isGhost) continue;
                int cx = slot.x, cy = slot.y;
                float cellX = 2f + cx * (cellSize + spacing);
                float cellY = y - 2f - cy * (cellSize + spacing);
                float ow = slot.w * cellSize + (slot.w - 1) * spacing;
                float oh = slot.h * cellSize + (slot.h - 1) * spacing;

                // 物品覆盖层
                var overlayRt = MakeRect($"OV_{slot.itemName}", parent, cellX, cellY, ow, oh);
                overlayRt.SetAsLastSibling();
                var oImg = overlayRt.gameObject.AddComponent<Image>();
                oImg.color = new Color(0.18f, 0.3f, 0.2f, 0.85f);
                oImg.raycastTarget = true;

                // 名称
                var nameRt = MakeRect("IName", overlayRt, 0, 0, ow, oh * 0.6f);
                var nTxt = nameRt.gameObject.AddComponent<Text>();
                nTxt.font = UGUIBuilder.DefaultFont;
                nTxt.text = slot.itemName;
                nTxt.fontSize = 11;
                nTxt.color = Color.white;
                nTxt.fontStyle = FontStyle.Bold;
                nTxt.alignment = TextAnchor.UpperCenter;
                nTxt.raycastTarget = false;

                // 数量
                var cRt = MakeRect("ICnt", overlayRt, ow - 24f, -(oh - 18f), 20f, 16f);
                var cTxt = cRt.gameObject.AddComponent<Text>();
                cTxt.font = UGUIBuilder.DefaultFont;
                cTxt.text = "x" + slot.count;
                cTxt.fontSize = 10;
                cTxt.color = new Color(1f, 0.85f, 0.3f);
                cTxt.fontStyle = FontStyle.Bold;
                cTxt.alignment = TextAnchor.LowerRight;
                cTxt.raycastTarget = false;

                // 拖拽处理器
                var drag = overlayRt.gameObject.AddComponent<ItemDragHandler>();
                var itemData = FindItemData(slot.itemName);
                var placedItem = new PlacedItem(itemData, slot.count, slot.x, slot.y);
                drag.Setup(placedItem, _inventory.GetContainer(view.equipSlot), slot.x, slot.y, overlayRt);

                // 注册到 DragDropManager
                for (int sy = 0; sy < slot.h; sy++)
                    for (int sx = 0; sx < slot.w; sx++)
                    {
                        float cx2 = 2f + (slot.x + sx) * (cellSize + spacing);
                        float cy2 = y - 2f - (slot.y + sy) * (cellSize + spacing);
                        var cellRt = MakeRect($"C_{view.id}_{slot.x + sx}_{slot.y + sy}",
                            parent, cx2, cy2, cellSize, cellSize);
                        var cellImg = cellRt.gameObject.AddComponent<Image>();
                        cellImg.color = new Color(0.15f, 0.3f, 0.2f, 0.9f);
                        cellImg.raycastTarget = true;
                        var c = _inventory.GetContainer(view.equipSlot);
                        if (c != null && DragDropManager.Instance != null)
                            DragDropManager.Instance.RegisterCellRect(cellRt, c, slot.x + sx, slot.y + sy);
                    }

                // 耐久条
                if (slot.durabilityRatio < 1f)
                {
                    var durFill = UGUIBuilder.CreateDurabilityBar($"Dur_{slot.slotId}", overlayRt, ow - 4);
                    UGUIBuilder.SetDurabilityFill(durFill, slot.durabilityRatio);
                    durFill.rectTransform.parent.gameObject.SetActive(true);
                }
            }

            y -= gridAreaH + 8f;
        }

        ItemData FindItemData(string itemName)
        {
            if (_inventory == null) return null;
            foreach (var c in _inventory.containers)
                foreach (var p in c.placedItems)
                    if (p.itemData != null && p.itemData.itemName == itemName)
                        return p.itemData;
            return null;
        }

        void ShowEquipTabContent()
        {
            if (_inventory == null || overviewGridContainer == null) return;

            var gridRt = overviewGridContainer.GetComponent<RectTransform>();
            float pw = gridRt.rect.width;
            float ph = gridRt.rect.height;
            float m = 8f;
            float sp = 6f;

            var view = _inventory.BuildViewData();

            // 创建滚动内容容器
            var scrollContent = MakeRect("ScrollContent", gridRt, 0, 0, pw, ph);
            var scrollImg = scrollContent.gameObject.AddComponent<Image>();
            scrollImg.color = new Color(0, 0, 0, 0);
            scrollImg.raycastTarget = false;
            _overviewScrollY = 0;
            _overviewScrollContent = scrollContent;

            // ===== 新布局：左面板(纸娃娃+武器+属性) | 右面板(折叠容器列表) =====
            float leftW = pw * 0.28f;  // 窄面板
            float rightW = pw - leftW - sp - m * 2;
            float leftX = m;
            float rightX = leftX + leftW + sp;

            // ── 左面板 ──
            var leftPanel = MakeRect("LeftPanel", scrollContent, leftX, -m, leftW, ph - m * 2);
            var lpImg = leftPanel.gameObject.AddComponent<Image>();
            lpImg.color = new Color(0.05f, 0.05f, 0.08f, 0.6f);
            lpImg.raycastTarget = false;

            float ly = -m;
            float innerW = leftW - m * 2;

            // 武器槽 — 2×2 方格，放在装备区上面
            float weaponH = ph * 0.33f;
            DrawWeaponSlots(leftPanel, innerW, ref ly, weaponH, view, m);
            ly -= sp;

            // 纸娃娃装备区
            float dollH = ph * 0.42f;
            var dollRt = MakeRect("PaperDoll", leftPanel, m, ly, innerW, dollH);
            CreatePaperDoll(dollRt.transform, view, innerW, dollH);
            ly -= dollH + sp;

            // 属性面板
            float statsH = ph * 0.15f;
            DrawStatsPanel(leftPanel, view, innerW, ref ly, statsH, m);

            // ── 右面板：折叠容器列表 ──
            var rightPanel = MakeRect("RightPanel", scrollContent, rightX, -m, rightW, ph - m * 2);
            var rpImg = rightPanel.gameObject.AddComponent<Image>();
            rpImg.color = new Color(0.06f, 0.06f, 0.09f, 0.6f);
            rpImg.raycastTarget = false;

            // 所有装备容器的显示顺序
            var containerSlots = new EquipSlot[] {
                EquipSlot.Vest, EquipSlot.Tops, EquipSlot.Belt,
                EquipSlot.Pants, EquipSlot.Backpack
            };

            float ry = -m;
            float cellSize = 44f;
            float spacing = 2f;

            foreach (var slot in containerSlots)
            {
                var container = _inventory.GetContainer(slot);
                if (container == null) continue;
                string equipName = GetEquipName(view, slot);
                ry = DrawCollapsibleContainer(rightPanel, container, slot, equipName,
                    ref ry, rightW - m * 2, cellSize, spacing, view);
                ry -= sp;
            }

            // 计算滚动高度
            float lowestY = 0;
            foreach (Transform child in scrollContent)
            {
                var rt = child as RectTransform;
                if (rt == null) continue;
                float bottomY = rt.anchoredPosition.y - rt.sizeDelta.y;
                if (bottomY < lowestY) lowestY = bottomY;
            }
            // 用右侧面板底部检测
            float rightBottom = -(Mathf.Abs(ry) + m);
            _overviewContentHeight = Mathf.Max(0, Mathf.Abs(rightBottom) + m - ph);
            scrollContent.sizeDelta = new Vector2(pw, Mathf.Abs(rightBottom) + m * 2);

            if (DragDropManager.Instance != null)
                DragDropManager.Instance.RefreshSelectionBorder();
        }

        // ═══ 新布局辅助方法 ═══

        /// <summary> 纸娃娃人体预览 — 大号装备槽 </summary>
        void CreatePaperDoll(Transform parent, InventoryViewData view, float areaW, float areaH)
        {
            var parentRt = parent.GetComponent<RectTransform>();
            float cx = areaW / 2f;
            float topM = 2f;
            float gap = 3f;

            // 身体轮廓背景
            float bodyW = areaW * 0.9f;
            float totalBodyH = areaH - topM;

            var bodyOutline = MakeRect("BodyOutline", parentRt,
                cx - bodyW / 2f, -topM, bodyW, totalBodyH);
            var outlineImg = bodyOutline.gameObject.AddComponent<Image>();
            outlineImg.color = new Color(0.12f, 0.13f, 0.16f, 0.4f);
            outlineImg.raycastTarget = false;

            // 均分高度：头1份 + 躯干3份(胸挂/上衣/防弹) + 腰带1份 + 裤子1份 + 背包 = 7份
            float unitH = (totalBodyH - gap * 6) / 7f;
            float curY = -topM;

            // 头
            float headW = bodyW * 0.55f;
            CreateDollSlot(parentRt, "头部", EquipSlot.Head, view,
                cx - headW / 2f, curY, headW, unitH, false, 12);
            curY -= unitH + gap;

            // 躯干三行
            float torsoW = bodyW * 0.92f;
            CreateDollSlot(parentRt, "胸挂", EquipSlot.Vest, view,
                cx - torsoW / 2f, curY, torsoW, unitH, false, 12);
            curY -= unitH + gap;
            CreateDollSlot(parentRt, "上衣", EquipSlot.Tops, view,
                cx - torsoW / 2f, curY, torsoW, unitH, false, 12);
            curY -= unitH + gap;
            CreateDollSlot(parentRt, "防弹衣", EquipSlot.BodyArmor, view,
                cx - torsoW / 2f, curY, torsoW, unitH, false, 12);
            curY -= unitH + gap;

            // 腰带
            float beltW = bodyW * 0.7f;
            CreateDollSlot(parentRt, "腰带", EquipSlot.Belt, view,
                cx - beltW / 2f, curY, beltW, unitH, false, 12);
            curY -= unitH + gap;

            // 裤子
            float legsW = bodyW * 0.55f;
            CreateDollSlot(parentRt, "裤子", EquipSlot.Pants, view,
                cx - legsW / 2f, curY, legsW, unitH, false, 12);
            curY -= unitH + gap;

            // 背包（最后一份，用剩余空间）
            float bpW = bodyW * 0.6f;
            float bpH = unitH;
            CreateDollSlot(parentRt, "背包", EquipSlot.Backpack, view,
                cx - bpW / 2f, curY, bpW, bpH, false, 11);
        }

        /// <summary> 纸娃娃单个装备槽 — 带点击卸下 + 拖拽装备注册 </summary>
        void CreateDollSlot(RectTransform parent, string label, EquipSlot slot, InventoryViewData view,
            float x, float y, float w, float h, bool locked, int fontSize = 10)
        {
            bool hasEquip = !locked && view.equippedNames != null
                && view.equippedNames.TryGetValue(slot, out var name) && !string.IsNullOrEmpty(name);
            string equipName = hasEquip ? view.equippedNames[slot] : "";

            var rt = MakeRect($"Doll_{slot}", parent, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = true;

            // 颜色：已装备亮绿，空位暗色，锁定灰色
            if (locked)
                img.color = new Color(0.05f, 0.05f, 0.06f, 0.5f);
            else if (hasEquip)
                img.color = new Color(0.15f, 0.3f, 0.22f, 0.9f);
            else
                img.color = new Color(0.1f, 0.1f, 0.13f, 0.75f);

            // 标签
            string displayText = locked ? $"{label}(锁)" : (hasEquip ? equipName : $"{label}: 空");
            var txt = MakeChildText($"Lbl_{slot}", rt, 2, -1, w - 4, h - 2);
            txt.text = displayText;
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = locked ? new Color(0.25f, 0.25f, 0.25f)
                : (hasEquip ? new Color(0.95f, 0.92f, 0.35f) : new Color(0.45f, 0.45f, 0.45f));
            txt.fontStyle = hasEquip ? FontStyle.Bold : FontStyle.Normal;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = fontSize - 3;
            txt.resizeTextMaxSize = fontSize;

            // 装备槽耐久条（底部 3px，默认隐藏）
            if (!locked && slot != EquipSlot.None)
            {
                var durFill = UGUIBuilder.CreateDurabilityBar($"Dur_{slot}", rt, w);
                durFill.rectTransform.parent.gameObject.SetActive(false);
                _dollDurBars[slot] = durFill;
            }

            if (locked) return;

            // 双击卸下
            var btn = rt.gameObject.AddComponent<Button>();
            var capturedSlot = slot;
            btn.onClick.AddListener(() => OnEquipSlotDoubleClick(capturedSlot));

            // 注册为拖拽目标（支持拖入装备）
            if (DragDropManager.Instance != null)
            {
                var container = _inventory?.GetContainer(slot);
                if (container != null)
                    DragDropManager.Instance.RegisterEquipSlot(rt, container, 0, 0, slot);
                else if (Inv.IsWeaponSlot(slot))
                    DragDropManager.Instance.RegisterEquipSlot(rt, null, 0, 0, slot);
            }
        }

        /// <summary> 武器槽 — 2×2 小方格，清晰标注槽位 </summary>
        void DrawWeaponSlots(RectTransform parent, float innerW, ref float y, float areaH, InventoryViewData view, float m)
        {
            // 标题
            var title = MakeChildText("WpnTitle", parent, 0, y, innerW, 18f);
            title.text = "— 武器 —";
            title.fontSize = 11;
            title.color = new Color(0.5f, 0.6f, 0.5f);
            title.alignment = TextAnchor.MiddleCenter;
            y -= 20f;

            float cellSize = Mathf.Min((innerW - 4f) / 2f, (areaH - 22f) / 2f);
            float gap = 2f;
            float gridW = cellSize * 2 + gap;
            float startX = (innerW - gridW) / 2f;

            bool beltEquipped = view.equippedNames != null
                && view.equippedNames.ContainsKey(EquipSlot.Belt)
                && !string.IsNullOrEmpty(view.equippedNames[EquipSlot.Belt]);

            var weaponSlots = new[] {
                ("主武", EquipSlot.RightHand, false),
                ("副武", EquipSlot.LeftHand, false),
                ("小刀", EquipSlot.KnifeBelt, !beltEquipped),
                ("手枪", EquipSlot.SidearmBelt, !beltEquipped)
            };

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    int idx = row * 2 + col;
                    var (label, slot, locked) = weaponSlots[idx];
                    bool hasWeapon = !locked && view.equippedNames != null
                        && view.equippedNames.TryGetValue(slot, out var name) && !string.IsNullOrEmpty(name);

                    float cx = startX + col * (cellSize + gap);
                    float cy = y - row * (cellSize + gap);

                    var cellRt = MakeRect("Wpn_" + slot, parent, cx, cy, cellSize, cellSize);
                    var cellImg = cellRt.gameObject.AddComponent<Image>();
                    cellImg.raycastTarget = true;
                    cellImg.color = locked ? new Color(0.04f, 0.04f, 0.05f, 0.5f)
                        : hasWeapon ? new Color(0.2f, 0.35f, 0.25f, 0.9f)
                        : new Color(0.08f, 0.08f, 0.1f, 0.8f);

                    // 槽位标签（小号，左上角）
                    var slotLbl = MakeChildText("SL_" + slot, cellRt, 2, -1, cellSize * 0.55f, cellSize * 0.45f);
                    slotLbl.text = locked ? $"{label}🔒" : label;
                    slotLbl.fontSize = Mathf.FloorToInt(cellSize * 0.22f);
                    slotLbl.color = locked ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.5f, 0.5f, 0.5f);
                    slotLbl.alignment = TextAnchor.UpperLeft;

                    // 装备名（下半部分）
                    var eqLbl = MakeChildText("EQ_" + slot, cellRt, 1, -(cellSize * 0.5f), cellSize - 2, cellSize * 0.5f);
                    eqLbl.text = locked ? "" : (hasWeapon ? view.equippedNames[slot] : "空");
                    eqLbl.fontSize = Mathf.FloorToInt(cellSize * 0.18f);
                    eqLbl.color = hasWeapon ? new Color(0.95f, 0.9f, 0.35f) : new Color(0.35f, 0.35f, 0.35f);
                    eqLbl.alignment = TextAnchor.LowerCenter;
                    eqLbl.fontStyle = hasWeapon ? FontStyle.Bold : FontStyle.Normal;

                    if (!locked)
                    {
                        var btn = cellRt.gameObject.AddComponent<Button>();
                        var capturedSlot = slot;
                        btn.onClick.AddListener(() => OnEquipSlotDoubleClick(capturedSlot));

                        if (DragDropManager.Instance != null)
                        {
                            var container = _inventory?.GetContainer(slot);
                            if (container != null)
                                DragDropManager.Instance.RegisterEquipSlot(cellRt, container, 0, 0, slot);
                            else if (Inv.IsWeaponSlot(slot))
                                DragDropManager.Instance.RegisterEquipSlot(cellRt, null, 0, 0, slot);
                        }
                    }
                }
            }
            y -= cellSize * 2 + gap;
        }

        /// <summary> 属性面板（护甲/保暖/负重）</summary>
        void DrawStatsPanel(RectTransform parent, InventoryViewData view, float panelW, ref float y, float areaH, float m)
        {
            var bg = MakeRect("StatsPanel", parent, m, y, panelW - m * 2, areaH);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.09f, 0.5f);
            bgImg.raycastTarget = false;

            float rowH = 22f;
            float barW = panelW - m * 4 - 50f; // 留空间给数值
            float sy = -m * 0.5f;

            // 分隔线
            var sep = MakeRect("StatsSep", bg, m, sy, barW + 50f, 1f);
            sep.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
            sy -= 8f;

            // 护甲 — 标签+进度条+数值 在同一行
            float valX = m + barW + 8f;
            DrawStatRow(bg.transform, "护甲", view.totalArmor, 10f, barW, rowH, m, sy,
                new Color(0.5f, 0.55f, 0.6f), valX, view.totalArmor.ToString("F0"));
            sy -= rowH + 3f;

            DrawStatRow(bg.transform, "保暖", view.totalWarmth, 5f, barW, rowH, m, sy,
                new Color(0.9f, 0.55f, 0.2f), valX, view.totalWarmth.ToString("F1"));
            sy -= rowH + 3f;

            float weightRatio = view.maxWeight > 0 ? view.currentWeight / view.maxWeight : 0;
            Color wc = view.isHardOverloaded ? new Color(0.9f, 0.2f, 0.2f)
                : (weightRatio > 0.8f ? new Color(0.9f, 0.7f, 0.2f) : new Color(0.3f, 0.7f, 0.4f));
            DrawStatRow(bg.transform, "负重", view.currentWeight, view.maxWeight, barW, rowH, m, sy,
                wc, valX, $"{view.currentWeight:F1}/{view.maxWeight:F0}");

            y -= areaH;
        }

        /// <summary> 属性条行：标签 + 进度条 + 对齐的数值 </summary>
        void DrawStatRow(Transform parent, string label, float value, float max,
            float barW, float rowH, float x, float y, Color barColor, float valX, string valText)
        {
            // 标签
            var lbl = MakeChildText("StatL_" + label, parent.GetComponent<RectTransform>(), x, y, 32f, rowH);
            lbl.text = label;
            lbl.fontSize = 12;
            lbl.alignment = TextAnchor.MiddleLeft;
            lbl.color = new Color(0.6f, 0.6f, 0.6f);

            // 进度条背景
            float barX = x + 36f;
            float barHeight = rowH - 6f;
            var barBgRt = MakeRect("StatBg_" + label, parent.GetComponent<RectTransform>(),
                barX, y + 3f, barW, barHeight);
            var barBg = barBgRt.gameObject.AddComponent<Image>();
            barBg.color = new Color(0.08f, 0.08f, 0.1f);
            barBg.raycastTarget = false;

            // 进度条填充
            float ratio = max > 0 ? Mathf.Clamp01(value / max) : 0;
            var fillRt = MakeRect("StatFill_" + label, barBgRt, 0, 0, ratio * barW, barHeight);
            fillRt.gameObject.AddComponent<Image>().color = barColor;

            // 数值 — 右对齐
            var valLbl = MakeChildText("StatV_" + label, parent.GetComponent<RectTransform>(),
                valX, y, 42f, rowH);
            valLbl.text = valText;
            valLbl.fontSize = 11;
            valLbl.alignment = TextAnchor.MiddleRight;
            valLbl.color = Color.white;
            valLbl.fontStyle = FontStyle.Bold;
        }

        /// <summary> 折叠容器区块（▶/▼ 切换 + 网格物品）</summary>
        float DrawCollapsibleContainer(RectTransform parent, InventoryContainer container, EquipSlot slot,
            string equipName, ref float y, float panelW, float cellSize, float spacing, InventoryViewData view)
        {
            float headerH = 28f;
            float labelH = 14f;
            bool isCollapsed = _containerCollapsed.ContainsKey(slot) && _containerCollapsed[slot];
            int totalCells = container.TotalCells;
            bool hasEquip = !string.IsNullOrEmpty(equipName);

            // ── 折叠头部 ──
            var headerRt = MakeRect($"Hdr_{container.containerName}", parent, 0, y, panelW, headerH);
            var headerImg = headerRt.gameObject.AddComponent<Image>();
            headerImg.color = hasEquip ? new Color(0.12f, 0.18f, 0.14f, 0.9f)
                : new Color(0.08f, 0.08f, 0.1f, 0.85f);
            headerImg.raycastTarget = true;

            // 折叠箭头
            string arrow = isCollapsed ? "▶" : "▼";
            var arrowTxt = MakeChildText($"Arrow_{container.containerName}", headerRt, 6, -4, 20f, headerH - 8);
            arrowTxt.text = arrow;
            arrowTxt.fontSize = 12;
            arrowTxt.alignment = TextAnchor.MiddleCenter;
            arrowTxt.color = new Color(0.5f, 0.7f, 0.5f);

            // 容器名
            string headerText = container.containerName;
            if (hasEquip) headerText += $" ({equipName})";
            else if (totalCells == 0) headerText += " 未装备";
            var nameTxt = MakeChildText($"Name_{container.containerName}", headerRt, 28, -4, panelW * 0.5f, headerH - 8);
            nameTxt.text = headerText;
            nameTxt.fontSize = 12;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.color = hasEquip ? new Color(0.85f, 0.9f, 0.85f) : new Color(0.5f, 0.5f, 0.5f);
            nameTxt.fontStyle = hasEquip ? FontStyle.Bold : FontStyle.Normal;

            // 容量指示
            if (totalCells > 0)
            {
                var cntTxt = MakeChildText($"Cnt_{container.containerName}", headerRt, panelW - 80, -4, 70f, headerH - 8);
                cntTxt.text = $"{container.UsedCells}/{totalCells}";
                cntTxt.fontSize = 11;
                cntTxt.alignment = TextAnchor.MiddleRight;
                cntTxt.color = container.UsedCells >= totalCells ? new Color(0.9f, 0.3f, 0.3f)
                    : new Color(0.5f, 0.7f, 0.5f);
            }

            // 点击切换折叠
            var btn = headerRt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (!_containerCollapsed.ContainsKey(slot))
                    _containerCollapsed[slot] = false;
                _containerCollapsed[slot] = !_containerCollapsed[slot];
                RefreshOverview(); // 重绘
            });
            y -= headerH;

            // ── 折叠内容 ──
            if (!isCollapsed && totalCells > 0)
            {
                int gw = container.gridWidth;
                int gh = container.gridHeight;
                float gridAreaH = gh * cellSize + (gh - 1) * spacing;
                float gridAreaW = gw * cellSize + (gw - 1) * spacing;

                // 网格背景
                var gridBg = MakeRect($"Grid_{container.containerName}", parent, 4, y, panelW - 8, gridAreaH + 4f);
                var gridBgImg = gridBg.gameObject.AddComponent<Image>();
                gridBgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.4f);
                gridBgImg.raycastTarget = false;

                // 画每个格子
                for (int cy = 0; cy < gh; cy++)
                {
                    for (int cx = 0; cx < gw; cx++)
                    {
                        float cellX = 2f + cx * (cellSize + spacing);
                        float cellY = y - 2f - cy * (cellSize + spacing);

                        var cellRt = MakeRect($"C_{container.containerName}_{cx}_{cy}",
                            parent, cellX, cellY, cellSize, cellSize);
                        var cellImg = cellRt.gameObject.AddComponent<Image>();
                        cellImg.raycastTarget = true;

                        bool occupied = false;
                        PlacedItem? placedAt = null;
                        foreach (var p in container.placedItems)
                        {
                            if (cx >= p.gridX && cx < p.gridX + p.GridWidth &&
                                cy >= p.gridY && cy < p.gridY + p.GridHeight)
                            {
                                occupied = true;
                                if (p.gridX == cx && p.gridY == cy)
                                    placedAt = p;
                                break;
                            }
                        }

                        // 装备但无物品时的占位色
                        bool isEquipSlot = !occupied && hasEquip && gw == 1 && gh == 1;
                        cellImg.color = occupied
                            ? (view.isHardOverloaded ? new Color(0.45f, 0.2f, 0.2f, 0.9f) : new Color(0.15f, 0.3f, 0.2f, 0.9f))
                            : (isEquipSlot ? new Color(0.12f, 0.25f, 0.18f, 0.8f) : new Color(0.08f, 0.08f, 0.1f, 0.8f));

                        if (DragDropManager.Instance != null)
                            DragDropManager.Instance.RegisterCellRect(cellRt, container, cx, cy);

                        // 物品覆盖层
                        if (occupied && placedAt.HasValue && !placedAt.Value.isGhost
                            && placedAt.Value.gridX == cx && placedAt.Value.gridY == cy)
                        {
                            var item = placedAt.Value;
                            float ow = item.GridWidth * cellSize + (item.GridWidth - 1) * spacing;
                            float oh = item.GridHeight * cellSize + (item.GridHeight - 1) * spacing;

                            var overlayRt = MakeRect($"OV_{item.itemData.itemName}",
                                parent, cellX, cellY, ow, oh);
                            overlayRt.SetAsLastSibling();
                            var oImg = overlayRt.gameObject.AddComponent<Image>();
                            oImg.color = view.isHardOverloaded
                                ? new Color(0.45f, 0.2f, 0.2f, 0.85f)
                                : new Color(0.18f, 0.3f, 0.2f, 0.85f);
                            oImg.raycastTarget = false;

                            // 物品名
                            float nH = oh * 0.6f;
                            var nRt = MakeRect("IName", overlayRt, 0, 0, ow, nH);
                            var nTxt = nRt.gameObject.AddComponent<Text>();
                            nTxt.text = item.itemData.itemName;
                            nTxt.font = GetFont();
                            nTxt.fontSize = Mathf.FloorToInt(Mathf.Clamp(ow * 0.15f, 8f, 14f));
                            nTxt.alignment = TextAnchor.MiddleCenter;
                            nTxt.color = Color.white;
                            nTxt.fontStyle = FontStyle.Bold;
                            nTxt.raycastTarget = false;
                            nTxt.resizeTextForBestFit = true;
                            nTxt.resizeTextMinSize = 6;
                            nTxt.resizeTextMaxSize = nTxt.fontSize;

                            // 数量 xN
                            float cW = ow * 0.5f;
                            float cH = oh * 0.4f;
                            var cRt = MakeRect("ICnt", overlayRt, ow - cW, -(oh - cH), cW, cH);
                            var cTxt = cRt.gameObject.AddComponent<Text>();
                            cTxt.text = $"x{item.count}";
                            cTxt.font = GetFont();
                            cTxt.fontSize = Mathf.FloorToInt(Mathf.Clamp(oh * 0.26f, 8f, 13f));
                            cTxt.alignment = TextAnchor.LowerRight;
                            cTxt.color = new Color(1f, 0.85f, 0.3f); // 金色
                            cTxt.fontStyle = FontStyle.Bold;
                            cTxt.raycastTarget = false;

                            var drag = cellRt.gameObject.AddComponent<ItemDragHandler>();
                            drag.Setup(item, container, cx, cy, overlayRt);
                        }
                    }
                }
                y -= gridAreaH + 4f;
            }
            else if (!isCollapsed && totalCells == 0)
            {
                // 未装备提示
                var emptyRt = MakeRect($"Empty_{container.containerName}", parent, 4, y, panelW - 8, cellSize * 0.6f);
                var emptyTxt = MakeChildText("EmptyTxt", emptyRt, 0, 0, panelW - 8, cellSize * 0.6f);
                emptyTxt.text = "未装备";
                emptyTxt.fontSize = 10;
                emptyTxt.alignment = TextAnchor.MiddleCenter;
                emptyTxt.color = new Color(0.35f, 0.35f, 0.35f);
                y -= cellSize * 0.6f + 2f;
            }

            return y;
        }

        // ═══ 旧版兼容方法（保留，不删除） ═══

        /// <summary>
        /// 绘制装备槽位（头、上衣/胸挂/防弹、裤、腰、背）
        /// 每个槽位是 2×2 格子，双击卸下
        /// </summary>
        void DrawEquipSlots(RectTransform parent, float colX, float colW, InventoryViewData view, float m, float ph)
        {
            float cell = Mathf.Min(colW * 0.3f, 44f);
            float labelH = 14f;
            float gap = 4f;

            float tripleCell = Mathf.Min(colW * 0.28f, 38f);
            float tripleRowW = tripleCell * 3 + 4f * 2;
            float tripleStartX = colX + (colW - tripleRowW) * 0.5f;

            float curY = -m - labelH;

            // 装备区背景
            var equipBg = MakeRect("EquipBg", parent, colX, -m, colW, ph - m * 2);
            var bgImg = equipBg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.1f, 0.5f);
            bgImg.raycastTarget = false;
            equipBg.SetAsFirstSibling();

            // 检查腰带是否已装备（决定小刀/手枪位是否解锁）
            bool beltEquipped = view.equippedNames != null
                && view.equippedNames.ContainsKey(EquipSlot.Belt)
                && !string.IsNullOrEmpty(view.equippedNames[EquipSlot.Belt]);

            // 画每个槽位
            DrawOneSlot(parent, "头", EquipSlot.Head, view,
                colX + (colW - cell) * 0.5f, curY, cell, labelH, false);
            curY -= cell + labelH + gap;

            for (int i = 0; i < 3; i++)
            {
                var slotLabel = new[] { "上衣", "胸挂", "防弹" }[i];
                var eqSlot = new[] { EquipSlot.Tops, EquipSlot.Vest, EquipSlot.BodyArmor }[i];
                float sx = tripleStartX + i * (tripleCell + 4f);
                DrawOneSlot(parent, slotLabel, eqSlot, view, sx, curY, tripleCell, labelH, false);
            }
            curY -= tripleCell + labelH + gap;

            DrawOneSlot(parent, "裤", EquipSlot.Pants, view,
                colX + (colW - cell) * 0.5f, curY, cell, labelH, false);
            curY -= cell + labelH + gap;

            DrawOneSlot(parent, "腰", EquipSlot.Belt, view,
                colX + (colW - cell) * 0.5f, curY, cell, labelH, false);
            curY -= cell + labelH + gap;

            // 武器槽（主武+副武一行排列，小刀+手枪一行排列）
            float weaponCell = Mathf.Min(colW * 0.38f, 42f);
            float weaponRowW = weaponCell * 2 + 4f;
            float weaponStartX = colX + (colW - weaponRowW) * 0.5f;

            DrawOneSlot(parent, "主武", EquipSlot.RightHand, view,
                weaponStartX, curY, weaponCell, labelH, false);
            DrawOneSlot(parent, "副武", EquipSlot.LeftHand, view,
                weaponStartX + weaponCell + 4f, curY, weaponCell, labelH, false);
            curY -= weaponCell + labelH + gap;

            DrawOneSlot(parent, "小刀", EquipSlot.KnifeBelt, view,
                weaponStartX, curY, weaponCell, labelH, !beltEquipped);
            DrawOneSlot(parent, "手枪", EquipSlot.SidearmBelt, view,
                weaponStartX + weaponCell + 4f, curY, weaponCell, labelH, !beltEquipped);
            curY -= weaponCell + labelH + gap;

            DrawOneSlot(parent, "背", EquipSlot.Backpack, view,
                colX + (colW - cell) * 0.5f, curY, cell, labelH, false);
        }

        void DrawOneSlot(RectTransform parent, string slotName, EquipSlot slot, InventoryViewData view,
            float x, float y, float sz, float labelH, bool locked = false)
        {
            bool hasItem = !locked && view.equippedNames != null && view.equippedNames.ContainsKey(slot)
                && !string.IsNullOrEmpty(view.equippedNames[slot]);
            string equipName = hasItem ? view.equippedNames[slot] : "";

            // 文字在上层
            var lbl = MakeChildText("L_" + slotName, parent, x, y, sz, labelH);
            lbl.text = locked ? $"{slotName}(锁)" : slotName;
            lbl.fontSize = 10;
            lbl.color = locked ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
            lbl.alignment = TextAnchor.MiddleCenter;

            // 格子在下层（紧贴文字下方）
            float cellY = y + labelH;
            var cellRt = MakeRect(slotName + "_slot", parent, x, cellY, sz, sz);
            var cellImg = cellRt.gameObject.AddComponent<Image>();
            cellImg.color = locked ? new Color(0.06f, 0.06f, 0.08f, 0.6f) : new Color(0.12f, 0.12f, 0.15f, 0.9f);
            cellImg.raycastTarget = true;

            if (locked) return; // 锁定不响应任何操作

            if (hasItem)
            {
                var nTxt = MakeChildText("N", cellRt, 0, 0, sz, sz);
                nTxt.text = equipName;
                nTxt.fontSize = Mathf.FloorToInt(Mathf.Clamp(sz * 0.16f, 8f, 12f));
                nTxt.alignment = TextAnchor.MiddleCenter;
                nTxt.color = Color.white;
                nTxt.resizeTextForBestFit = true;
                nTxt.resizeTextMinSize = 6;
                nTxt.resizeTextMaxSize = nTxt.fontSize;
            }

            var btn = cellRt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => OnEquipSlotDoubleClick(slot));

            var container = _inventory?.GetContainer(slot);
            if (DragDropManager.Instance != null)
            {
                if (container != null)
                    DragDropManager.Instance.RegisterEquipSlot(cellRt, container, 0, 0, slot);
                else if (Inv.IsWeaponSlot(slot))
                    DragDropManager.Instance.RegisterEquipSlot(cellRt, null, 0, 0, slot);
            }
        }

        /// <summary> 双击装备槽卸下回背包 </summary>
        void OnEquipSlotDoubleClick(EquipSlot slot)
        {
            if (_inventory == null) return;
            var item = _inventory.UnequipSlot(slot);
            if (item != null)
                SpawnEquipDrop(item, slot);
            RefreshOverview();
        }

        /// <summary> 在玩家面前丢出装备物品（不经过背包容器）</summary>
        void SpawnEquipDrop(ItemData item, EquipSlot slot)
        {
            if (item == null) return;
            var player = PlayerRegistry.GameObject;
            if (player == null) return;

            var go = new GameObject($"World_{item.itemName}");
            go.transform.position = player.transform.position + player.transform.forward * 1.2f + Vector3.up * 0.1f;
            var wi = go.AddComponent<_Game.Systems.WorldContainer.WorldItem>();
            wi.itemData = item;
            wi.count = 1;
        }

        /// <summary> 创建文字子对象（无Image，同MakeRect风格）</summary>
        Text MakeChildText(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var txt = go.AddComponent<Text>();
            txt.font = UGUIBuilder.DefaultFont;
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary> 创建一个 RectTransform 子对象（绝对定位，top-left 对齐）</summary>
        RectTransform MakeRect(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        /// <summary> 精确坐标绘制容器格子（GridLayoutGroup 废弃版）</summary>
        float BuildContainerGrid(Transform parent, InventoryContainer container,
            float parentW, ref float y, bool isGrid, float cellSize, float spacing, float labelH,
            bool isHardOverloaded = false, string equippedItemName = null)
        {
            if (container == null) return y;
            string displayName = container.containerName;
            float used = container.UsedCells;
            float total = container.TotalCells;

            // 容器标签
            string labelSuffix = "";
            if (!string.IsNullOrEmpty(equippedItemName))
                labelSuffix = $" ({equippedItemName})";
            else if (total > 0)
                labelSuffix = $"  {used}/{total}";
            var label = MakeRect("L_" + displayName, parent.GetComponent<RectTransform>(), 0, y, parentW, labelH);
            var labelTxt = label.gameObject.AddComponent<Text>();
            labelTxt.text = total > 0 ? $"{displayName}{labelSuffix}" : displayName;
            labelTxt.font = GetFont();
            labelTxt.fontSize = 12;
            labelTxt.alignment = TextAnchor.MiddleLeft;
            labelTxt.color = new Color(0.6f, 0.6f, 0.6f);
            y -= labelH;

            if (total == 0)
            {
                // 未装备
                var emptyRt = MakeRect("Empty", parent.GetComponent<RectTransform>(), 0, y, parentW, cellSize);
                var emptyTxt = emptyRt.gameObject.AddComponent<Text>();
                emptyTxt.text = "未装备";
                emptyTxt.font = GetFont();
                emptyTxt.fontSize = 10;
                emptyTxt.color = new Color(0.4f, 0.4f, 0.4f);
                emptyTxt.alignment = TextAnchor.MiddleCenter;
                y -= cellSize + 1;
                return y;
            }

            Color fillColor = isHardOverloaded ? overloadColor : occupiedColor;
            Color equipColor = new Color(0.15f, 0.35f, 0.25f, 0.9f);

            if (isGrid)
            {
                // 网格：每格精确坐标，独立着色
                for (int cy = 0; cy < container.gridHeight; cy++)
                    for (int cx = 0; cx < container.gridWidth; cx++)
                    {
                        float cellX = cx * (cellSize + spacing);
                        float cellY = y - cy * (cellSize + spacing);

                        var cellRt = MakeRect("G_" + cx + "_" + cy,
                            parent.GetComponent<RectTransform>(), cellX, cellY, cellSize, cellSize);
                        var cellImg = cellRt.gameObject.AddComponent<Image>();
                        cellImg.raycastTarget = true;

                        bool occupied = false;
                        PlacedItem? placedAt = null;
                        foreach (var p in container.placedItems)
                            if (cx >= p.gridX && cx < p.gridX + p.GridWidth &&
                                cy >= p.gridY && cy < p.gridY + p.GridHeight)
                            { occupied = true; placedAt = p; break; }

                        bool isEquipSlot = !occupied && !string.IsNullOrEmpty(equippedItemName)
                            && container.gridWidth == 1 && container.gridHeight == 1;
                        cellImg.color = occupied ? fillColor : (isEquipSlot ? equipColor : emptyColor);

                        if (DragDropManager.Instance != null)
                            DragDropManager.Instance.RegisterCellRect(cellRt, container, cx, cy);

                        if (isEquipSlot)
                        {
                            // 显示已装备物品（不注册拖拽，暂不支持从装备槽拖出）
                            var nameRt = MakeRect("EQ_" + equippedItemName,
                                parent.GetComponent<RectTransform>(), cellX, cellY, cellSize, cellSize);
                            nameRt.SetAsLastSibling();
                            var nameImg = nameRt.gameObject.AddComponent<Image>();
                            nameImg.color = equipColor;
                            nameImg.raycastTarget = false;

                            var txtObj = new GameObject("Name", typeof(Text));
                            txtObj.transform.SetParent(nameRt, false);
                            var txtRt = txtObj.GetComponent<RectTransform>();
                            txtRt.anchorMin = Vector2.zero;
                            txtRt.anchorMax = Vector2.one;
                            txtRt.offsetMin = Vector2.zero;
                            txtRt.offsetMax = Vector2.zero;
                            var equipTxt = txtObj.GetComponent<Text>();
                            equipTxt.text = equippedItemName;
                            equipTxt.font = GetFont();
                            equipTxt.fontSize = 8;
                            equipTxt.alignment = TextAnchor.MiddleCenter;
                            equipTxt.color = Color.white;
                            equipTxt.resizeTextForBestFit = true;
                            equipTxt.resizeTextMinSize = 7;
                            equipTxt.resizeTextMaxSize = 11;
                        }
                        else if (occupied && placedAt.HasValue && !placedAt.Value.isGhost
                            && placedAt.Value.gridX == cx && placedAt.Value.gridY == cy)
                        {
                            var item = placedAt.Value;

                            // --- 物品名称/数量覆盖层（兄弟节点，与cell同级）---
                            float overlayW = item.GridWidth * cellSize + (item.GridWidth - 1) * spacing;
                            float overlayH = item.GridHeight * cellSize + (item.GridHeight - 1) * spacing;

                            var overlayRt = MakeRect("OV_" + item.itemData.itemName,
                                parent.GetComponent<RectTransform>(), cellX, cellY, overlayW, overlayH);
                            overlayRt.SetAsLastSibling();

                            var overlayImg = overlayRt.gameObject.AddComponent<Image>();
                            overlayImg.color = fillColor;
                            overlayImg.raycastTarget = false;

                            float nameH = overlayH * 0.65f;
                            var nameRt = MakeRect("Name", overlayRt, 0, 0, overlayW, nameH);
                            var nameTxt = nameRt.gameObject.AddComponent<Text>();
                            nameTxt.text = item.itemData.itemName;
                            nameTxt.font = GetFont();
                            nameTxt.fontSize = Mathf.FloorToInt(Mathf.Clamp(overlayW * 0.16f, 8f, 18f));
                            nameTxt.alignment = TextAnchor.MiddleCenter;
                            nameTxt.color = Color.white;
                            nameTxt.fontStyle = FontStyle.Bold;
                            nameTxt.raycastTarget = false;
                            nameTxt.resizeTextForBestFit = true;
                            nameTxt.resizeTextMinSize = 7;
                            nameTxt.resizeTextMaxSize = nameTxt.fontSize;

                            // 数量 xN
                            {
                                float cntW = overlayW * 0.55f;
                                float cntH = overlayH * 0.4f;
                                float cntX = overlayW - cntW;
                                float cntY = -(overlayH - cntH);
                                var cntRt = MakeRect("Count", overlayRt, cntX, cntY, cntW, cntH);
                                var cntTxt = cntRt.gameObject.AddComponent<Text>();
                                cntTxt.text = $"x{item.count}";
                                cntTxt.font = GetFont();
                                cntTxt.fontSize = Mathf.FloorToInt(Mathf.Clamp(overlayH * 0.28f, 8f, 14f));
                                cntTxt.alignment = TextAnchor.LowerRight;
                                cntTxt.color = Color.yellow;
                                cntTxt.fontStyle = FontStyle.Bold;
                                cntTxt.raycastTarget = false;
                                cntTxt.resizeTextForBestFit = true;
                                cntTxt.resizeTextMinSize = 7;
                                cntTxt.resizeTextMaxSize = cntTxt.fontSize;
                            }

                            var drag = cellRt.gameObject.AddComponent<ItemDragHandler>();
                            drag.Setup(item, container, cx, cy, overlayRt);

                        }
                    }
                y -= container.gridHeight * (cellSize + spacing);
            }
            else
            {
                // 横条：显示所有网格格子（一行排开）
                int totalCells = container.gridWidth * container.gridHeight;
                for (int i = 0; i < totalCells; i++)
                {
                    int cx = i % container.gridWidth;
                    int cy = i / container.gridWidth;
                    float cellX = i * (cellSize + spacing);
                    float cellY = y;

                    var slotRt = MakeRect("H_" + displayName + "_" + i,
                        parent.GetComponent<RectTransform>(), cellX, cellY, cellSize, cellSize);
                    var slotImg = slotRt.gameObject.AddComponent<Image>();
                    slotImg.raycastTarget = true;

                    bool occupied = false;
                    PlacedItem? placedAt = null;
                    foreach (var p in container.placedItems)
                        if (cx >= p.gridX && cx < p.gridX + p.GridWidth &&
                            cy >= p.gridY && cy < p.gridY + p.GridHeight)
                        { occupied = true; placedAt = p; break; }

                    slotImg.color = occupied ? fillColor : emptyColor;

                    if (DragDropManager.Instance != null)
                        DragDropManager.Instance.RegisterCellRect(slotRt, container, cx, cy);

                    if (occupied && placedAt.HasValue && !placedAt.Value.isGhost && placedAt.Value.gridX == cx && placedAt.Value.gridY == cy)
                    {
                        if (placedAt.Value.itemData.icon != null)
                        {
                            var iconObj = new GameObject("Icon", typeof(Image));
                            iconObj.transform.SetParent(slotRt, false);
                            var iconRt = iconObj.GetComponent<RectTransform>();
                            iconRt.anchorMin = Vector2.zero;
                            iconRt.anchorMax = Vector2.one;
                            iconRt.offsetMin = Vector2.zero;
                            iconRt.offsetMax = Vector2.zero;
                            var iconImg = iconObj.GetComponent<Image>();
                            iconImg.sprite = placedAt.Value.itemData.icon;
                            iconImg.preserveAspect = true;
                        }

                        var drag = slotRt.gameObject.AddComponent<ItemDragHandler>();
                        drag.Setup(placedAt.Value, container, placedAt.Value.gridX, placedAt.Value.gridY);
                    }
                }

                y -= cellSize + spacing;
            }

            return y;
        }

        /// <summary>
        /// 绘制装备区域（骨架+填充两遍式）
        /// 先画标签和空网格骨架，再填充已放置物品
        /// </summary>
        void DrawEquipArea(RectTransform parent, string equipName, InventoryContainer container,
            ref float y, float parentW, float cellSize, float labelH, InventoryViewData view, EquipSlot slot)
        {
            if (container == null) return;
            string displayName = container.containerName;
            int gh = container.gridHeight;
            int gw = container.gridWidth;
            float spacing = 1f;
            bool hasItem = !string.IsNullOrEmpty(equipName);

            // 第一遍：骨架（标签 + 空网格背景）
            // --- 标签 ---
            string labelText = displayName;
            if (hasItem) labelText += $" ({equipName})";
            else if (container.TotalCells > 0) labelText += $"  {container.UsedCells}/{container.TotalCells}";
            
            var labelRt = MakeRect("LB_" + displayName, parent, 0, y, parentW, labelH);
            var labelImg = labelRt.gameObject.AddComponent<Image>();
            labelImg.color = new Color(0.10f, 0.10f, 0.12f, 0.8f);
            labelImg.raycastTarget = false;
            var labelTxtObj = new GameObject("LTxt", typeof(Text));
            labelTxtObj.transform.SetParent(labelRt, false);
            var ltRt = labelTxtObj.GetComponent<RectTransform>();
            ltRt.anchorMin = Vector2.zero;
            ltRt.anchorMax = Vector2.one;
            ltRt.offsetMin = Vector2.zero;
            ltRt.offsetMax = Vector2.zero;
            var lt = labelTxtObj.GetComponent<Text>();
            lt.text = labelText;
            lt.font = GetFont();
            lt.fontSize = 12;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.color = hasItem ? new Color(0.7f, 0.8f, 0.7f) : new Color(0.5f, 0.5f, 0.5f);
            
            y -= labelH;

            // --- 网格骨架 ---
            // 先画整个区域的暗色背景
            float gridAreaH = gh * cellSize + (gh - 1) * spacing;
            var bgRt = MakeRect("BG_" + displayName, parent, 0, y, parentW, gridAreaH);
            var bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.08f, 0.6f);
            bgImg.raycastTarget = false;

            // 画每个格子（空槽位，暗色）
            for (int cy = 0; cy < gh; cy++)
            {
                for (int cx = 0; cx < gw; cx++)
                {
                    float cellX = cx * (cellSize + spacing);
                    float cellY = y - cy * (cellSize + spacing);
                    
                    var cellRt = MakeRect("C_" + displayName + "_" + cx + "_" + cy,
                        parent, cellX, cellY, cellSize, cellSize);
                    var cellImg = cellRt.gameObject.AddComponent<Image>();
                    cellImg.color = new Color(0.12f, 0.12f, 0.15f, 0.9f);
                    cellImg.raycastTarget = true;

                    if (DragDropManager.Instance != null)
                        DragDropManager.Instance.RegisterCellRect(cellRt, container, cx, cy);

                    // 第二遍：填充已有物品
                    bool occupied = false;
                    PlacedItem? placedAt = null;
                    foreach (var p in container.placedItems)
                    {
                        if (cx >= p.gridX && cx < p.gridX + p.GridWidth &&
                            cy >= p.gridY && cy < p.gridY + p.GridHeight)
                        {
                            if (p.gridX == cx && p.gridY == cy)
                            { occupied = true; placedAt = p; }
                            cellImg.color = new Color(0.2f, 0.25f, 0.2f, 0.9f);
                            break;
                        }
                    }

                    // 装备了物品但网格为空（1x1装备槽，双击脱下用）
                    bool isEquipSlot = !occupied && hasItem && gw == 1 && gh == 1;
                    if (isEquipSlot)
                        cellImg.color = new Color(0.15f, 0.3f, 0.2f, 0.9f);

                    if (occupied && placedAt.HasValue && !placedAt.Value.isGhost
                        && placedAt.Value.gridX == cx && placedAt.Value.gridY == cy)
                    {
                        var item = placedAt.Value;

                        float overlayW = item.GridWidth * cellSize + (item.GridWidth - 1) * spacing;
                        float overlayH = item.GridHeight * cellSize + (item.GridHeight - 1) * spacing;
                        var overlayRt = MakeRect("OV_" + item.itemData.itemName,
                            parent, cellX, cellY, overlayW, overlayH);
                        overlayRt.SetAsLastSibling();
                        var oImg = overlayRt.gameObject.AddComponent<Image>();
                        oImg.color = view.isHardOverloaded
                            ? new Color(0.4f, 0.2f, 0.2f, 0.9f)
                            : new Color(0.25f, 0.3f, 0.2f, 0.9f);
                        oImg.raycastTarget = false;

                        float nameH = overlayH * 0.65f;
                        var nameRt = MakeRect("Name", overlayRt, 0, 0, overlayW, nameH);
                        var nTxt = nameRt.gameObject.AddComponent<Text>();
                        nTxt.text = item.itemData.itemName;
                        nTxt.font = GetFont();
                        nTxt.fontSize = Mathf.FloorToInt(Mathf.Clamp(overlayW * 0.16f, 8f, 18f));
                        nTxt.alignment = TextAnchor.MiddleCenter;
                        nTxt.color = Color.white;
                        nTxt.fontStyle = FontStyle.Bold;
                        nTxt.raycastTarget = false;
                        nTxt.resizeTextForBestFit = true;
                        nTxt.resizeTextMinSize = 7;
                        nTxt.resizeTextMaxSize = nTxt.fontSize;

                        float cntW = overlayW * 0.55f;
                        float cntH = overlayH * 0.4f;
                        float cntX = overlayW - cntW;
                        float cntY2 = -(overlayH - cntH);
                        var cntRt = MakeRect("Cnt", overlayRt, cntX, cntY2, cntW, cntH);
                        var cTxt = cntRt.gameObject.AddComponent<Text>();
                        cTxt.text = $"x{item.count}";
                        cTxt.font = GetFont();
                        cTxt.fontSize = Mathf.FloorToInt(Mathf.Clamp(overlayH * 0.28f, 8f, 14f));
                        cTxt.alignment = TextAnchor.LowerRight;
                        cTxt.color = Color.yellow;
                        cTxt.fontStyle = FontStyle.Bold;
                        cTxt.raycastTarget = false;
                        cTxt.resizeTextForBestFit = true;
                        cTxt.resizeTextMinSize = 7;
                        cTxt.resizeTextMaxSize = cTxt.fontSize;

                        var drag = cellRt.gameObject.AddComponent<ItemDragHandler>();
                        drag.Setup(item, container, cx, cy, overlayRt);

                    }
                }
            }
            y -= gridAreaH;
        }

        void AddSelectionBorder(RectTransform overlayRt, float w, float h)
        {
            int bw = 2;
            var top = MakeRect("SelTop", overlayRt, 0, 0, w, bw);
            top.gameObject.AddComponent<Image>().color = Color.white;
            top.GetComponent<Image>().raycastTarget = false;

            var bot = MakeRect("SelBot", overlayRt, 0, -(h - bw), w, bw);
            bot.gameObject.AddComponent<Image>().color = Color.white;
            bot.GetComponent<Image>().raycastTarget = false;

            var left = MakeRect("SelLeft", overlayRt, 0, -bw, bw, h - bw * 2);
            left.gameObject.AddComponent<Image>().color = Color.white;
            left.GetComponent<Image>().raycastTarget = false;

            var right = MakeRect("SelRight", overlayRt, w - bw, -bw, bw, h - bw * 2);
            right.gameObject.AddComponent<Image>().color = Color.white;
            right.GetComponent<Image>().raycastTarget = false;
        }

        void CreatePlaceholderSlot(Transform parent, string text, Color bgColor)
        {
            var obj = new GameObject($"PH_{text}", typeof(Image), typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var img = obj.GetComponent<Image>();
            img.color = bgColor;

            var txtObj = new GameObject("Label", typeof(Text));
            txtObj.transform.SetParent(obj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            var txt = txtObj.GetComponent<Text>();
            txt.text = text;
            txt.fontSize = 12;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.6f, 0.6f, 0.6f);
        }

        void CreateSideMiniGrid(Transform parent, InventoryContainer container)
        {
            // 容器名
            var nameObj = new GameObject($"N_{container.containerName}", typeof(Text));
            nameObj.transform.SetParent(parent, false);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.text = $"{container.containerName}  {container.UsedCells}/{container.TotalCells}";
            nameTxt.fontSize = 11;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;

            // 网格
            var gridObj = new GameObject($"G_{container.containerName}", typeof(RectTransform));
            gridObj.transform.SetParent(parent, false);
            var grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.childAlignment = TextAnchor.MiddleCenter;

            float spacing = 1f;
            float cell = Mathf.Floor(22f);

            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = container.gridWidth;

            float gridW = cell * container.gridWidth + spacing * (container.gridWidth - 1);
            float gridH = cell * container.gridHeight + spacing * (container.gridHeight - 1);
            var gr = gridObj.GetComponent<RectTransform>();
            gr.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, gridW);
            gr.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, gridH);

            Color fillColor = _inventory.IsHardOverloaded ? overloadColor : occupiedColor;
            for (int y = 0; y < container.gridHeight; y++)
                for (int x = 0; x < container.gridWidth; x++)
                {
                    var cellObj = new GameObject($"c_{x}_{y}", typeof(Image));
                    cellObj.transform.SetParent(gridObj.transform, false);
                    var cellImg = cellObj.GetComponent<Image>();
                    bool occupied = false;
                    PlacedItem? placedAt = null;
                    foreach (var p in container.placedItems)
                        if (x >= p.gridX && x < p.gridX + p.GridWidth &&
                            y >= p.gridY && y < p.gridY + p.GridHeight)
                        { occupied = true; placedAt = p; break; }
                    cellImg.color = occupied ? fillColor : emptyColor;

                    if (DragDropManager.Instance != null)
                        DragDropManager.Instance.RegisterCellRect(cellObj.GetComponent<RectTransform>(), container, x, y);

                    if (occupied && placedAt.HasValue && !placedAt.Value.isGhost
                        && placedAt.Value.gridX == x && placedAt.Value.gridY == y)
                    {
                        var drag = cellObj.AddComponent<ItemDragHandler>();
                        drag.Setup(placedAt.Value, container, x, y);
                    }
                }
        }

        /// <summary>
        /// 横向物品条（上衣/裤子专用，每个已放置物品显示一格）
        /// </summary>
        void CreateHorizontalStrip(Transform parent, InventoryContainer container)
        {
            var nameObj = new GameObject($"NH_{container.containerName}", typeof(Text));
            nameObj.transform.SetParent(parent, false);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.text = $"{container.containerName}  {container.UsedCells}/{container.TotalCells}";
            nameTxt.fontSize = 11;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;

            int itemCount = container.placedItems.Count;
            int displayCount = Mathf.Max(itemCount, 1);
            float cellSize = 28f;
            float spacing = 2f;
            float totalW = displayCount * cellSize + (displayCount - 1) * spacing;

            var stripObj = new GameObject($"HS_{container.containerName}", typeof(RectTransform));
            stripObj.transform.SetParent(parent, false);
            var stripRt = stripObj.GetComponent<RectTransform>();
            stripRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalW);
            stripRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize);

            var glg = stripObj.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(cellSize, cellSize);
            glg.spacing = new Vector2(spacing, 0);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = displayCount;

            Color fillColor = _inventory.IsHardOverloaded ? overloadColor : occupiedColor;
            foreach (var p in container.placedItems)
            {
                if (p.isGhost) continue;

                var slotObj = new GameObject("HSlot", typeof(Image));
                slotObj.transform.SetParent(stripObj.transform, false);
                var img = slotObj.GetComponent<Image>();
                img.color = fillColor;

                if (p.itemData != null && p.itemData.icon != null)
                {
                    var iconObj = new GameObject("HIcon", typeof(Image));
                    iconObj.transform.SetParent(slotObj.transform, false);
                    var iconRt = iconObj.GetComponent<RectTransform>();
                    iconRt.anchorMin = Vector2.zero;
                    iconRt.anchorMax = Vector2.one;
                    iconRt.offsetMin = Vector2.zero;
                    iconRt.offsetMax = Vector2.zero;
                    var iconImg = iconObj.GetComponent<Image>();
                    iconImg.sprite = p.itemData.icon;
                    iconImg.preserveAspect = true;
                }

                // 添加拖拽
                var drag = slotObj.AddComponent<ItemDragHandler>();
                drag.Setup(p, container, p.gridX, p.gridY);
            }

            if (itemCount == 0)
            {
                var emptySlot = new GameObject("HEmpty", typeof(Image));
                emptySlot.transform.SetParent(stripObj.transform, false);
                emptySlot.GetComponent<Image>().color = emptyColor;
            }
        }

        void CreateBottomBackpackGrid(Transform parent, InventoryContainer container, float cell, float spacing, float margin)
        {
            var gridObj = new GameObject("BackpackGrid", typeof(RectTransform));
            gridObj.transform.SetParent(parent, false);

            var grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.childAlignment = TextAnchor.MiddleCenter;

            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = container.gridWidth;

            float gridW = cell * container.gridWidth + spacing * (container.gridWidth - 1);
            float gridH = cell * container.gridHeight + spacing * (container.gridHeight - 1);
            var gr = gridObj.GetComponent<RectTransform>();
            gr.anchorMin = new Vector2(0.5f, 0);
            gr.anchorMax = new Vector2(0.5f, 0);
            gr.pivot = new Vector2(0.5f, 0);
            gr.sizeDelta = new Vector2(gridW, gridH);
            gr.anchoredPosition = new Vector2(0, margin);

            Color fillColor = _inventory.IsHardOverloaded ? overloadColor : occupiedColor;
            for (int y = 0; y < container.gridHeight; y++)
                for (int x = 0; x < container.gridWidth; x++)
                {
                    var cellObj = new GameObject($"c_{x}_{y}", typeof(Image));
                    cellObj.transform.SetParent(gridObj.transform, false);
                    var cellImg = cellObj.GetComponent<Image>();
                    bool occupied = false;
                    PlacedItem? placedAt = null;
                    foreach (var p in container.placedItems)
                        if (x >= p.gridX && x < p.gridX + p.GridWidth &&
                            y >= p.gridY && y < p.gridY + p.GridHeight)
                        { occupied = true; placedAt = p; break; }
                    cellImg.color = occupied ? fillColor : emptyColor;

                    if (DragDropManager.Instance != null)
                        DragDropManager.Instance.RegisterCellRect(cellObj.GetComponent<RectTransform>(), container, x, y);

                    if (occupied && placedAt.HasValue && !placedAt.Value.isGhost
                        && placedAt.Value.gridX == x && placedAt.Value.gridY == y)
                    {
                        var drag = cellObj.AddComponent<ItemDragHandler>();
                        drag.Setup(placedAt.Value, container, x, y);
                    }
                }
        }

        // ===== 玩家预览（纯UI身体轮廓，替代Camera）=====

        void CreateBodyPreview(Transform parent, InventoryViewData view)
        {
            var parentRt = parent.GetComponent<RectTransform>();
            float pw = parentRt.rect.width;
            float ph = parentRt.rect.height;
            float m = 6f;
            float cx = pw / 2f;

            // 身体轮廓背景
            var bodyRt = MakeRect("BodyBg", parentRt, pw * 0.1f, -m, pw * 0.8f, ph - m * 2);
            var bodyImg = bodyRt.gameObject.AddComponent<Image>();
            bodyImg.color = new Color(0.12f, 0.12f, 0.15f, 0.6f);
            bodyImg.raycastTarget = false;

            // 头部（头盔）
            float headW = pw * 0.35f;
            float headH = ph * 0.15f;
            var headRt = MakeRect("Head", parentRt, cx - headW / 2f, -m, headW, headH);
            var headImg = headRt.gameObject.AddComponent<Image>();
            headImg.color = new Color(0.2f, 0.2f, 0.25f, 0.7f);
            headImg.raycastTarget = false;
            var headTxt = AddLabel(headRt, GetEquipName(view, EquipSlot.Head, "无头盔"));
            headTxt.color = string.IsNullOrEmpty(GetEquipName(view, EquipSlot.Head)) ? new Color(0.4f, 0.4f, 0.4f) : Color.white;

            // 身体（胸挂+上衣）
            float torsoY = -m - headH - 4;
            float torsoW = pw * 0.65f;
            float torsoH = ph * 0.35f;
            var torsoRt = MakeRect("Torso", parentRt, cx - torsoW / 2f, torsoY, torsoW, torsoH);
            var torsoImg = torsoRt.gameObject.AddComponent<Image>();
            torsoImg.color = new Color(0.22f, 0.22f, 0.28f, 0.7f);
            torsoImg.raycastTarget = false;
            string torsoText = "";
            string vestName = GetEquipName(view, EquipSlot.Vest);
            string topName = GetEquipName(view, EquipSlot.Tops);
            if (!string.IsNullOrEmpty(vestName)) torsoText += vestName;
            if (!string.IsNullOrEmpty(topName))
                torsoText += (string.IsNullOrEmpty(torsoText) ? "" : "\n") + topName;
            if (string.IsNullOrEmpty(torsoText)) torsoText = "无胸甲";
            var torsoLabel = AddLabel(torsoRt, torsoText);
            torsoLabel.fontSize = 10;
            torsoLabel.color = torsoText == "无胸甲" ? new Color(0.4f, 0.4f, 0.4f) : Color.white;

            // 腰带
            float beltY = torsoY - torsoH - 4;
            float beltW = pw * 0.5f;
            float beltH = ph * 0.08f;
            var beltRt = MakeRect("Belt", parentRt, cx - beltW / 2f, beltY, beltW, beltH);
            var beltImg = beltRt.gameObject.AddComponent<Image>();
            beltImg.color = new Color(0.2f, 0.2f, 0.25f, 0.7f);
            beltImg.raycastTarget = false;
            var beltTxt = AddLabel(beltRt, GetEquipName(view, EquipSlot.Belt, "无腰带"));
            beltTxt.color = string.IsNullOrEmpty(GetEquipName(view, EquipSlot.Belt)) ? new Color(0.4f, 0.4f, 0.4f) : Color.white;

            // 腿部（裤子）
            float legsY = beltY - beltH - 4;
            float legsW = pw * 0.5f;
            float legsH = ph * 0.22f;
            var legsRt = MakeRect("Legs", parentRt, cx - legsW / 2f, legsY, legsW, legsH);
            var legsImg = legsRt.gameObject.AddComponent<Image>();
            legsImg.color = new Color(0.2f, 0.2f, 0.25f, 0.7f);
            legsImg.raycastTarget = false;
            var legsTxt = AddLabel(legsRt, GetEquipName(view, EquipSlot.Pants, "无裤子"));
            legsTxt.color = string.IsNullOrEmpty(GetEquipName(view, EquipSlot.Pants)) ? new Color(0.4f, 0.4f, 0.4f) : Color.white;

            // 背包（身体右侧小标签）
            float bpX = cx + torsoW / 2f + 6;
            float bpY = torsoY + torsoH * 0.3f;
            float bpW = pw * 0.2f;
            float bpH = ph * 0.15f;
            var bpRt = MakeRect("Backpack", parentRt, bpX, bpY, bpW, bpH);
            var bpImg = bpRt.gameObject.AddComponent<Image>();
            bpImg.color = new Color(0.15f, 0.15f, 0.2f, 0.7f);
            bpImg.raycastTarget = false;
            var bpTxt = AddLabel(bpRt, GetEquipName(view, EquipSlot.Backpack, "无"));
            bpTxt.fontSize = 8;
            bpTxt.color = string.IsNullOrEmpty(GetEquipName(view, EquipSlot.Backpack)) ? new Color(0.4f, 0.4f, 0.4f) : Color.white;
        }

        /// <summary> 从 ViewData 获取装备名（字典安全读取）</summary>
        string GetEquipName(InventoryViewData view, EquipSlot slot, string fallback = null)
        {
            if (view.equippedNames != null && view.equippedNames.TryGetValue(slot, out var name))
                return name;
            return fallback ?? "";
        }

        /// <summary> 在 RectTransform 上添加居中文本 </summary>
        Text AddLabel(RectTransform rt, string text)
        {
            var obj = new GameObject("Label", typeof(Text));
            obj.transform.SetParent(rt, false);
            var txtRt = obj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            var txt = obj.GetComponent<Text>();
            txt.text = text;
            txt.font = GetFont();
            txt.fontSize = 9;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 6;
            txt.resizeTextMaxSize = 11;
            return txt;
        }

        void OnCharacterStatsChanged(CharacterStatsChanged evt)
        {
            if (overviewPanel != null && overviewPanel.activeSelf && _currentTab == "角色")
                ShowOverview();
        }

        // ===== 装备耐久条刷新 =====

        void OnDurabilityChanged(DurabilityChangedEvent evt)
        {
            if (_inventory == null) return;
            foreach (var kv in _dollDurBars)
            {
                int id = _inventory.GetEquippedInstanceId(kv.Key);
                if (id == evt.InstanceId && id > 0)
                {
                    bool show = evt.Ratio < 1f;
                    kv.Value.rectTransform.parent.gameObject.SetActive(show);
                    if (show) UGUIBuilder.SetDurabilityFill(kv.Value, evt.Ratio);
                    return;
                }
            }
        }

        // ===== 角色面板 =====

        void ShowCharacterTabContent()
        {
            if (overviewGridContainer == null) return;
            var xpSys = SurvivalXPSystem.Instance;
            if (xpSys == null) return;

            var gridRt = overviewGridContainer.GetComponent<RectTransform>();
            float pw = gridRt.rect.width;
            float ph = gridRt.rect.height;
            float m = 8f;
            float sp = 6f;

            var content = MakeRect("CharContent", gridRt, 0, 0, pw, ph);
            var cImg = content.gameObject.AddComponent<Image>();
            cImg.color = new Color(0, 0, 0, 0);
            cImg.raycastTarget = false;

            float leftW = pw * 0.38f;
            float rightW = pw - leftW - m - sp;
            float leftX = m;
            float rightX = leftX + leftW + sp;

            var leftPanel = MakeRect("LeftPanel", content, leftX, -m, leftW, ph - m * 2);
            var lpImg = leftPanel.gameObject.AddComponent<Image>();
            lpImg.color = new Color(0.08f, 0.08f, 0.1f, 0.6f);
            lpImg.raycastTarget = false;

            var rightPanel = MakeRect("RightPanel", content, rightX, -m, rightW, ph - m * 2);
            var rpImg = rightPanel.gameObject.AddComponent<Image>();
            rpImg.color = new Color(0.08f, 0.08f, 0.1f, 0.6f);
            rpImg.raycastTarget = false;

            // ===== 左面板 =====
            float ly = -m;

            // -- 职业 --
            var pc = ServiceLocator.Get<PlayerCharacter>();
            string profName = "无职业";
            if (pc != null && pc.characterData != null && pc.characterData.profession != null)
                profName = pc.characterData.profession.templateName;
            var profLabel = MakeChildText("Prof", leftPanel, m, ly, leftW - m * 2, 22f);
            profLabel.text = $"职业: {profName}";
            profLabel.fontSize = 14;
            profLabel.color = new Color(0.9f, 0.8f, 0.4f);
            profLabel.fontStyle = FontStyle.Bold;
            ly -= 28f;

            // -- 经验条 + 兑换 --
            float barH = 22f;
            int curXP = xpSys.TotalXP;
            int perPoint = GameConstants.XP_PER_SKILL_POINT;
            float fill = Mathf.Clamp01((float)curXP / perPoint);
            float barW = leftW - m * 2;

            var xpLabel = MakeChildText("XPLabel", leftPanel, m, ly, barW, 16f);
            xpLabel.text = $"生存经验: {curXP} / {perPoint}";
            xpLabel.fontSize = 12;
            xpLabel.color = new Color(0.7f, 0.7f, 0.7f);
            ly -= 18f;

            // XP 进度条
            var barBg = MakeRect("XPBarBg", leftPanel, m, ly, barW, barH);
            var barBgImg = barBg.gameObject.AddComponent<Image>();
            barBgImg.color = new Color(0.15f, 0.15f, 0.15f);
            barBgImg.raycastTarget = false;
            var barFill = MakeRect("XPBarFill", barBg, 0, 0, barW * fill, barH);
            var barFillImg = barFill.gameObject.AddComponent<Image>();
            barFillImg.color = new Color(0.2f, 0.6f, 0.9f);
            barFillImg.raycastTarget = false;
            ly -= barH + 4f;

            // 兑换按钮
            var convBtnRt = MakeRect("ConvertBtn", leftPanel, m, ly, barW, 28f);
            var convBtnImg = convBtnRt.gameObject.AddComponent<Image>();
            bool canConvert = curXP >= perPoint;
            convBtnImg.color = canConvert ? new Color(0.25f, 0.55f, 0.25f) : new Color(0.15f, 0.15f, 0.15f);
            convBtnImg.raycastTarget = true;
            var convBtn = convBtnRt.gameObject.AddComponent<Button>();
            convBtn.onClick.AddListener(() => {
                if (xpSys.ConvertXPToPoints() > 0)
                {
                    ShowToast("兑换成功！");
                    ShowOverview();
                }
            });
            var convTxt = MakeChildText("ConvTxt", convBtnRt, 0, 0, barW, 28f);
            convTxt.text = canConvert ? $"兑换技能点 ({curXP / perPoint}点)" : "经验不足，无法兑换";
            convTxt.fontSize = 13;
            convTxt.alignment = TextAnchor.MiddleCenter;
            convTxt.color = canConvert ? Color.white : new Color(0.4f, 0.4f, 0.4f);
            ly -= 36f;

            // 可用技能点
            int avail = xpSys.AvailablePoints;
            var ptsLabel = MakeChildText("PtsLabel", leftPanel, m, ly, barW, 20f);
            ptsLabel.text = $"可用技能点: {avail}";
            ptsLabel.fontSize = 14;
            ptsLabel.color = avail > 0 ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.5f, 0.5f, 0.5f);
            ptsLabel.fontStyle = FontStyle.Bold;
            ly -= 28f;

            // 分隔线
            var sep1 = MakeRect("Sep1", leftPanel, m, ly, barW, 2f);
            sep1.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);
            ly -= 10f;

            // -- 属性 --
            var attrHeader = MakeChildText("AttrHeader", leftPanel, m, ly, barW, 18f);
            attrHeader.text = "身体属性";
            attrHeader.fontSize = 13;
            attrHeader.color = new Color(0.8f, 0.8f, 0.8f);
            attrHeader.fontStyle = FontStyle.Bold;
            ly -= 22f;

            var attrTypes = new[] { AttributeType.力量, AttributeType.敏捷, AttributeType.体质, AttributeType.耐力 };
            var attrNames = new[] { "力量", "敏捷", "体质", "耐力" };
            for (int i = 0; i < 4; i++)
            {
                int val = xpSys.GetAttributeValue(attrTypes[i]);
                int cost = xpSys.GetAttributeUpgradeCost(attrTypes[i]);
                bool canUp = cost > 0 && avail >= cost && val < 10;

                float rowW = barW;
                var nameTxt = MakeChildText("AN_" + i, leftPanel, m, ly, rowW * 0.35f, 24f);
                nameTxt.text = attrNames[i];
                nameTxt.fontSize = 13;
                nameTxt.color = Color.white;

                // 数值条
                float valBarW = rowW * 0.35f;
                var vBg = MakeRect("AVBg_" + i, leftPanel, m + rowW * 0.35f, ly + 2f, valBarW, 20f);
                vBg.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f);
                var vFill = MakeRect("AVFill_" + i, vBg, 0, 0, valBarW * val / 10f, 20f);
                var vFillImg = vFill.gameObject.AddComponent<Image>();
                vFillImg.color = new Color(0.7f, 0.3f, 0.3f);
                vFillImg.raycastTarget = false;
                var vTxt = MakeChildText("AVTxt_" + i, vBg, 0, 0, valBarW, 20f);
                vTxt.text = $"{val}/10";
                vTxt.fontSize = 11;
                vTxt.alignment = TextAnchor.MiddleCenter;
                vTxt.color = Color.white;

                // 升级按钮
                float btnX = m + rowW * 0.7f;
                float btnW = rowW * 0.3f;
                var upBtnRt = MakeRect("AUp_" + i, leftPanel, btnX, ly + 2f, btnW, 20f);
                var upBtnImg = upBtnRt.gameObject.AddComponent<Image>();
                upBtnImg.color = canUp ? new Color(0.25f, 0.5f, 0.25f) : new Color(0.12f, 0.12f, 0.12f);
                upBtnImg.raycastTarget = true;
                var upBtn = upBtnRt.gameObject.AddComponent<Button>();
                int capturedI = i;
                upBtn.onClick.AddListener(() => {
                    if (xpSys.SpendAttributePoint(attrTypes[capturedI]))
                        FlashGreen(upBtnRt, () => ShowOverview());
                });
                var upTxt = MakeChildText("AUTxt_" + i, upBtnRt, 0, 0, btnW, 20f);
                upTxt.text = cost > 0 ? $"+{cost}点" : "满";
                upTxt.fontSize = 10;
                upTxt.alignment = TextAnchor.MiddleCenter;
                upTxt.color = canUp ? Color.white : new Color(0.4f, 0.4f, 0.4f);

                ly -= 26f;
            }

            // ===== 右面板 =====
            float ry = -m;
            var skillHeader = MakeChildText("SkillHeader", rightPanel, m, ry, rightW - m * 2, 20f);
            skillHeader.text = "技能";
            skillHeader.fontSize = 14;
            skillHeader.color = new Color(0.8f, 0.8f, 0.8f);
            skillHeader.fontStyle = FontStyle.Bold;
            ry -= 24f;

            var allSkills = new[] {
                SkillType.近战专精, SkillType.枪械专精, SkillType.防御专精,
                SkillType.资源采集, SkillType.医疗生存, SkillType.野外求生,
                SkillType.工匠制作, SkillType.建造拆解, SkillType.汽车改造,
                SkillType.智力
            };
            var skillNames = new[] { "近战", "枪械", "防御", "采集", "医疗", "求生", "工匠", "建造", "汽改", "智力" };

            string currentCat = "";
            float skillRowH = 28f;
            float catHeaderH = 18f;

            for (int i = 0; i < allSkills.Length; i++)
            {
                var sk = allSkills[i];
                string cat = SkillCostTable.GetCategoryName(sk);
                if (cat != currentCat)
                {
                    currentCat = cat;
                    ry -= 4f;
                    var catLabel = MakeChildText("Cat_" + cat, rightPanel, m, ry, rightW - m * 2, catHeaderH);
                    catLabel.text = cat;
                    catLabel.fontSize = 12;
                    catLabel.color = new Color(0.5f, 0.7f, 0.9f);
                    catLabel.fontStyle = FontStyle.Bold;
                    ry -= catHeaderH + 2f;
                }

                int lv = xpSys.GetSkillLevel(sk);
                int scost = xpSys.GetSkillUpgradeCost(sk);
                bool canUpSkill = scost > 0 && avail >= scost && lv < 10;

                float nameW = rightW * 0.15f;
                float barW2 = rightW * 0.38f;
                float lvW = rightW * 0.12f;
                float btnW2 = rightW * 0.22f;
                float gap = 4f;

                float curX = m;

                // 技能名
                var snTxt = MakeChildText("SN_" + i, rightPanel, curX, ry + 4f, nameW - gap, 20f);
                snTxt.text = skillNames[i];
                snTxt.fontSize = 12;
                snTxt.color = Color.white;
                curX += nameW;

                // 进度条
                var sBg = MakeRect("SBg_" + i, rightPanel, curX, ry + 6f, barW2 - gap, 16f);
                sBg.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);
                var sFill = MakeRect("SFill_" + i, sBg, 0, 0, (barW2 - gap) * lv / 10f, 16f);
                var sFillImg = sFill.gameObject.AddComponent<Image>();
                sFillImg.color = new Color(0.3f, 0.6f, 0.35f);
                sFillImg.raycastTarget = false;
                var sLvTxt = MakeChildText("SLvTxt_" + i, sBg, 0, 0, barW2 - gap, 16f);
                sLvTxt.text = $"Lv.{lv}";
                sLvTxt.fontSize = 10;
                sLvTxt.alignment = TextAnchor.MiddleCenter;
                sLvTxt.color = Color.white;
                curX += barW2;

                // 升级按钮
                var sUpRt = MakeRect("SUp_" + i, rightPanel, curX, ry + 6f, btnW2 - gap, 16f);
                var sUpImg = sUpRt.gameObject.AddComponent<Image>();
                sUpImg.color = canUpSkill ? new Color(0.25f, 0.5f, 0.25f) : new Color(0.1f, 0.1f, 0.1f);
                sUpImg.raycastTarget = true;
                var sUpBtn = sUpRt.gameObject.AddComponent<Button>();
                int capturedIdx = i;
                sUpBtn.onClick.AddListener(() => {
                    if (xpSys.SpendPoint(allSkills[capturedIdx]))
                        FlashGreen(sUpRt, () => ShowOverview());
                });
                var sUpTxt = MakeChildText("SUTxt_" + i, sUpRt, 0, 0, btnW2 - gap, 16f);
                if (lv >= 10)
                    sUpTxt.text = "MAX";
                else if (scost > 0)
                    sUpTxt.text = $"{scost}点";
                else
                    sUpTxt.text = "--";
                sUpTxt.fontSize = 9;
                sUpTxt.alignment = TextAnchor.MiddleCenter;
                sUpTxt.color = canUpSkill ? Color.white : new Color(0.4f, 0.4f, 0.4f);

                ry -= skillRowH;
            }

            // 存储滚动信息
            _overviewContentHeight = Mathf.Max(0, Mathf.Abs(ry) - ph + m * 2);
            _overviewScrollContent = content;
            content.sizeDelta = new Vector2(pw, Mathf.Abs(ry) + m * 2 + ph);
        }

        void FlashGreen(RectTransform target, System.Action onComplete = null)
        {
            StartCoroutine(FlashGreenRoutine(target, onComplete));
        }

        System.Collections.IEnumerator FlashGreenRoutine(RectTransform target, System.Action onComplete)
        {
            if (target == null) yield break;
            var images = target.GetComponentsInChildren<Image>();
            var origColors = new Color[images.Length];
            for (int i = 0; i < images.Length; i++)
                origColors[i] = images[i].color;

            for (int i = 0; i < images.Length; i++)
                images[i].color = new Color(0.2f, 0.9f, 0.3f);

            yield return new WaitForSeconds(0.15f);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                    images[i].color = origColors[i];
            }

            onComplete?.Invoke();
        }

        void ShowPlaceholderTab(string tabName)
        {
            var phObj = new GameObject("Placeholder", typeof(Text));
            phObj.transform.SetParent(overviewGridContainer.transform, false);
            var phText = phObj.GetComponent<Text>();
            string msg = tabName switch
            {
                "制作" => "请靠近工作台按 E 打开制作面板",
                "地图" => "地图功能尚未开放",
                "设置" => "设置功能尚未开放",
                _ => $"<b>{tabName}</b> 开发中..."
            };
            phText.text = $"<color=grey>{msg}</color>";
            phText.fontSize = 18;
            phText.alignment = TextAnchor.MiddleCenter;
            phText.color = Color.gray;
        }

        void CreateMiniGrid(InventoryContainer container)
        {
            // 标题行
            var titleObj = new GameObject($"T_{container.containerName}", typeof(Text));
            titleObj.transform.SetParent(overviewGridContainer.transform, false);
            var titleText = titleObj.GetComponent<Text>();
            titleText.text = $"<b>{container.containerName}</b>  {container.UsedCells}/{container.TotalCells}";
            titleText.fontSize = 13;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;

            // 网格容器
            var gridObj = new GameObject($"G_{container.containerName}", typeof(RectTransform));
            gridObj.transform.SetParent(overviewGridContainer.transform, false);

            var grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.childAlignment = TextAnchor.MiddleCenter;

            float containerWidth = overviewGridContainer.GetComponent<RectTransform>().rect.width - 20;
            float spacing = 1f;
            float cell = Mathf.Floor((containerWidth - spacing * (container.gridWidth - 1)) / container.gridWidth);

            // 上衣和裤子缩小
            if (container.equipSlot == EquipSlot.Tops || container.equipSlot == EquipSlot.Pants)
                cell = Mathf.Floor(cell * 0.6f);

            int gw = container.gridWidth;
            int gh = container.gridHeight;

            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = gw;

            float gridW = cell * gw + spacing * (gw - 1);
            float gridH = cell * gh + spacing * (gh - 1);
            var rt = gridObj.GetComponent<RectTransform>();
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, gridW);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, gridH);

            Color fillColor = _inventory.IsHardOverloaded ? overloadColor : occupiedColor;
            for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                {
                    var cellObj = new GameObject($"c_{x}_{y}", typeof(Image));
                    cellObj.transform.SetParent(gridObj.transform, false);
                    var img = cellObj.GetComponent<Image>();

                    bool occupied = false;
                    foreach (var p in container.placedItems)
                        if (x >= p.gridX && x < p.gridX + p.GridWidth &&
                            y >= p.gridY && y < p.gridY + p.GridHeight)
                        { occupied = true; break; }

                    img.color = occupied ? fillColor : emptyColor;
                }
        }

        // ===== 快捷面板（V 循环） =====

        void CycleToNextContainer()
        {
            for (int i = 0; i < CycleOrder.Length; i++)
            {
                _cycleIndex = (_cycleIndex + 1) % CycleOrder.Length;
                var slot = CycleOrder[_cycleIndex];
                var container = _inventory.GetContainer(slot);

                if (container != null && container.TotalCells > 0)
                {
                    _inventory.ActiveContainer = container;
                    _lastQuickSlot = slot;

                    if (quickPanel != null && quickGridContainer != null)
                    {
                        quickPanel.SetActive(true);
                        ShowQuickView();
                    }
                    return;
                }
            }
            ShowToast("没有可用的容器！");
        }

        void OpenContainer(EquipSlot slot)
        {
            var container = _inventory.GetContainer(slot);
            if (container == null || container.TotalCells == 0)
            {
                // 上次记录的装备不见了，按上衣→胸挂→腰带→裤子→背包找第一个可用的
                container = GetFirstFallbackContainer();
                if (container == null)
                {
                    ShowToast("没有可用的容器！");
                    return;
                }
            }

            _inventory.ActiveContainer = container;
            _lastQuickSlot = container.equipSlot;

            if (quickPanel != null && quickGridContainer != null)
            {
                quickPanel.SetActive(true);
                ShowQuickView();
            }
        }

        void ShowQuickView()
        {
            if (_inventory == null) return;
            var container = _inventory.ActiveContainer;
            if (container == null || quickGridContainer == null) return;

            // 清空 + 重建网格
            ClearContainer(quickGridContainer);

            var glg = quickGridContainer.GetComponent<GridLayoutGroup>();
            if (glg != null) DestroyImmediate(glg);
            var vlg = quickGridContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);

            var grid = quickGridContainer.AddComponent<GridLayoutGroup>();
            grid.childAlignment = TextAnchor.MiddleCenter;

            Rect rect = quickGridContainer.GetComponent<RectTransform>().rect;
            float spacing = 2f;
            float cellSize = Mathf.Floor(Mathf.Min(
                (rect.width - spacing * (container.gridWidth - 1)) / container.gridWidth,
                (rect.height - spacing * (container.gridHeight - 1)) / container.gridHeight
            ));

            grid.cellSize = new Vector2(cellSize, cellSize);
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = container.gridWidth;

            Color fillColor = _inventory.IsHardOverloaded ? overloadColor : occupiedColor;

            // 先创建所有背景格子
            for (int y = 0; y < container.gridHeight; y++)
                for (int x = 0; x < container.gridWidth; x++)
                {
                    var cellObj = new GameObject($"Cell_{x}_{y}", typeof(Image));
                    cellObj.transform.SetParent(quickGridContainer.transform, false);

                    bool occupied = false;
                    foreach (var p in container.placedItems)
                        if (x >= p.gridX && x < p.gridX + p.GridWidth &&
                            y >= p.gridY && y < p.gridY + p.GridHeight)
                        { occupied = true; break; }

                    var img = cellObj.GetComponent<Image>();
                    img.color = occupied ? fillColor : emptyColor;
                }

            // 在物品的左上角格子显示图标
            foreach (var p in container.placedItems)
            {
                if (p.itemData == null || p.itemData.icon == null) continue;

                // 找到物品左上角的格子作为父节点
                int iconX = p.gridX;
                int iconY = p.gridY;
                int iconIndex = iconY * container.gridWidth + iconX;
                if (iconIndex >= quickGridContainer.transform.childCount) continue;

                Transform cell = quickGridContainer.transform.GetChild(iconIndex);
                var iconObj = new GameObject("Icon", typeof(Image));
                iconObj.transform.SetParent(cell, false);
                var iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;

                var iconImg = iconObj.GetComponent<Image>();
                iconImg.sprite = p.itemData.icon;
                iconImg.preserveAspect = true;
            }

            // 标题
            if (quickInfoText != null)
            {
                string info = $"<b>{container.containerName}</b>  ({container.UsedCells}/{container.TotalCells})\n";
                info += $"负重: {_inventory.CurrentWeight:F1}/{_inventory.EffectiveMaxWeight}";
                if (_inventory.IsOverloaded)
                    info += $" <color=yellow>({_inventory.OverloadRatio * 100f:F0}%)</color>";
                quickInfoText.text = info;
            }

            if (DragDropManager.Instance != null)
                DragDropManager.Instance.RefreshSelectionBorder();
        }

        // ===== 工具 =====

        void ClearContainer(GameObject container)
        {
            // 用 Destroy（非 Immediate），让 UGUI 事件系统在帧末安全清理
            foreach (Transform child in container.transform)
                Destroy(child.gameObject);
        }

        void RefreshOverview()
        {
            ShowOverview();
        }

        public void ShowToast(string msg)
        {
            if (floatingToastText == null) return;
            floatingToastText.gameObject.SetActive(true);
            floatingToastText.text = msg;
            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), 2.5f);
        }

        InventoryContainer GetFirstFallbackContainer()
        {
            foreach (var slot in FallbackOrder)
            {
                var c = _inventory.GetContainer(slot);
                if (c != null && c.TotalCells > 0) return c;
            }
            return null;
        }

        // ===== 其他UI隐藏/显示 =====

        void SetOtherUIVisible(bool visible)
        {
            // 背包打开时隐藏所有非背包 UI，背包关闭时恢复
            if (_survivalHUDGo != null) _survivalHUDGo.SetActive(visible);
            if (_quickItemBarGo != null) _quickItemBarGo.SetActive(visible);
            // 其他系统UI
            var crafting = ServiceLocator.Get<_Game.Systems.Crafting.CraftingUI>();
            if (crafting != null) crafting.gameObject.SetActive(visible);
            var buildMenu = GetComponent<_Game.Systems.Building.BuildMenuUI>();
            if (buildMenu != null) buildMenu.gameObject.SetActive(visible);
            var prodUI = ServiceLocator.Get<_Game.Systems.Crafting.ProductionDeviceUI>();
            if (prodUI != null) prodUI.gameObject.SetActive(visible);
            var chemUI = ServiceLocator.Get<_Game.Systems.Crafting.ChemicalResearchUI>();
            if (chemUI != null) chemUI.gameObject.SetActive(visible);
        }

        // ===== 固定布局辅助方法 =====

        /// <summary> 在指定位置创建一个容器区块 </summary>
        float CreateContainerAt(Transform parent, InventoryContainer c, EquipSlot slot,
            float parentW, ref float y, bool isGrid, float cellSize, float spacing, float labelH)
        {
            if (c == null) return y;
            string displayName = c.containerName;
            float used = c.UsedCells;
            float total = c.TotalCells;

            // 容器标题
            var nameObj = new GameObject($"N_{displayName}", typeof(Text));
            nameObj.transform.SetParent(parent, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 1);
            nameRt.anchorMax = new Vector2(0, 1);
            nameRt.pivot = new Vector2(0, 1);
            nameRt.sizeDelta = new Vector2(parentW, labelH);
            nameRt.anchoredPosition = new Vector2(0, y);

            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.text = total > 0 ? $"{displayName}  {used}/{total}" : displayName;
            nameTxt.fontSize = 11;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.color = Color.white;

            y -= labelH;

            if (total == 0)
            {
                // 空容器显示占位文字
                var emptyObj = new GameObject("Empty", typeof(Text));
                emptyObj.transform.SetParent(parent, false);
                var emptyRt = emptyObj.GetComponent<RectTransform>();
                emptyRt.anchorMin = new Vector2(0, 1);
                emptyRt.anchorMax = new Vector2(0, 1);
                emptyRt.pivot = new Vector2(0, 1);
                emptyRt.sizeDelta = new Vector2(parentW, cellSize);
                emptyRt.anchoredPosition = new Vector2(0, y);
                var emptyTxt = emptyObj.GetComponent<Text>();
                emptyTxt.text = "未装备";
                emptyTxt.fontSize = 10;
                emptyTxt.color = new Color(0.4f, 0.4f, 0.4f);
                y -= cellSize + spacing;
                return y;
            }

            if (isGrid)
            {
                // 网格显示
                var gridObj = new GameObject($"G_{displayName}", typeof(RectTransform));
                gridObj.transform.SetParent(parent, false);
                var gridRt = gridObj.GetComponent<RectTransform>();
                gridRt.anchorMin = new Vector2(0, 1);
                gridRt.anchorMax = new Vector2(0, 1);
                gridRt.pivot = new Vector2(0, 1);

                float gw = c.gridWidth * cellSize + (c.gridWidth - 1) * spacing;
                float gh = c.gridHeight * cellSize + (c.gridHeight - 1) * spacing;
                gridRt.sizeDelta = new Vector2(gw, gh);
                gridRt.anchoredPosition = new Vector2(0, y);

                var glg = gridObj.AddComponent<GridLayoutGroup>();
                glg.cellSize = new Vector2(cellSize, cellSize);
                glg.spacing = new Vector2(spacing, spacing);
                glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = c.gridWidth;

                Color fillColor = _inventory.IsHardOverloaded ? overloadColor : occupiedColor;
                for (int cy = 0; cy < c.gridHeight; cy++)
                    for (int cx = 0; cx < c.gridWidth; cx++)
                    {
                        var cellObj = new GameObject($"c_{cx}_{cy}", typeof(Image));
                        cellObj.transform.SetParent(gridObj.transform, false);
                        var cellImg = cellObj.GetComponent<Image>();
                        bool occupied = false;
                        PlacedItem? placedAt = null;
                        foreach (var p in c.placedItems)
                            if (cx >= p.gridX && cx < p.gridX + p.GridWidth &&
                                cy >= p.gridY && cy < p.gridY + p.GridHeight)
                            { occupied = true; placedAt = p; break; }
                        cellImg.color = occupied ? fillColor : emptyColor;

                        if (DragDropManager.Instance != null)
                            DragDropManager.Instance.RegisterCellRect(cellObj.GetComponent<RectTransform>(), c, cx, cy);

                        if (occupied && placedAt.HasValue && !placedAt.Value.isGhost
                            && placedAt.Value.gridX == cx && placedAt.Value.gridY == cy)
                        {
                            var drag = cellObj.AddComponent<ItemDragHandler>();
                            drag.Setup(placedAt.Value, c, cx, cy);
                            CreateItemNameOverlay(cellObj, placedAt.Value, cellSize, spacing);
                        }
                    }

                y -= gh + spacing;
            }
            else
            {
                // 横条显示
                var stripObj = new GameObject($"HS_{displayName}", typeof(RectTransform));
                stripObj.transform.SetParent(parent, false);
                var stripRt = stripObj.GetComponent<RectTransform>();
                stripRt.anchorMin = new Vector2(0, 1);
                stripRt.anchorMax = new Vector2(0, 1);
                stripRt.pivot = new Vector2(0, 1);

                int itemCount = c.placedItems.Count;
                int displayCount = Mathf.Max(itemCount, 1);
                float totalW = displayCount * cellSize + (displayCount - 1) * spacing;
                stripRt.sizeDelta = new Vector2(totalW, cellSize);
                stripRt.anchoredPosition = new Vector2(0, y);

                var glg = stripObj.AddComponent<GridLayoutGroup>();
                glg.cellSize = new Vector2(cellSize, cellSize);
                glg.spacing = new Vector2(spacing, 0);
                glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = displayCount;

                Color fillColor = _inventory.IsHardOverloaded ? overloadColor : occupiedColor;
                foreach (var p in c.placedItems)
                {
                    // 跳过幽灵占位格
                    if (p.isGhost) continue;

                    var slotObj = new GameObject("HSlot", typeof(Image));
                    slotObj.transform.SetParent(stripObj.transform, false);
                    var img = slotObj.GetComponent<Image>();
                    img.color = fillColor;

                    if (p.itemData != null && p.itemData.icon != null)
                    {
                        var iconObj = new GameObject("Icon", typeof(Image));
                        iconObj.transform.SetParent(slotObj.transform, false);
                        var iconRt = iconObj.GetComponent<RectTransform>();
                        iconRt.anchorMin = Vector2.zero;
                        iconRt.anchorMax = Vector2.one;
                        iconRt.offsetMin = Vector2.zero;
                        iconRt.offsetMax = Vector2.zero;
                        var iconImg = iconObj.GetComponent<Image>();
                        iconImg.sprite = p.itemData.icon;
                        iconImg.preserveAspect = true;
                    }

                    var drag = slotObj.AddComponent<ItemDragHandler>();
                    drag.Setup(p, c, p.gridX, p.gridY);

                    // 横条物品名称
                    var snObj = new GameObject("ItemName", typeof(Text));
                    snObj.transform.SetParent(slotObj.transform, false);
                    var snRt = snObj.GetComponent<RectTransform>();
                    snRt.anchorMin = Vector2.zero;
                    snRt.anchorMax = Vector2.one;
                    snRt.offsetMin = Vector2.zero;
                    snRt.offsetMax = Vector2.zero;
                    var snTxt = snObj.GetComponent<Text>();
                    snTxt.text = p.itemData != null ? p.itemData.itemName : "";
                    snTxt.fontSize = 9;
                    snTxt.alignment = TextAnchor.LowerCenter;
                    nameTxt.color = Color.white;
                }

                if (itemCount == 0)
                {
                    var emptySlot = new GameObject("Empty", typeof(Image));
                    emptySlot.transform.SetParent(stripObj.transform, false);
                    emptySlot.GetComponent<Image>().color = emptyColor;
                }

                y -= cellSize + spacing;
            }

            return y;
        }

        /// <summary> 在指定坐标创建占位格子（头盔/防弹衣）</summary>
        void CreatePlaceholderSlotAt(Transform parent, string text, Color bgColor,
            float w, float h, float x, float y)
        {
            var obj = new GameObject($"PH_{text}", typeof(Image), typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var img = obj.GetComponent<Image>();
            img.color = bgColor;

            var txtObj = new GameObject("Label", typeof(Text));
            txtObj.transform.SetParent(obj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            var txt = txtObj.GetComponent<Text>();
            txt.text = text;
            txt.fontSize = 12;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.6f, 0.6f, 0.6f);
        }

        /// <summary> 在物品格子上创建名称覆盖（按物品实际占用格子数） </summary>
        void CreateItemNameOverlay(GameObject cellObj, PlacedItem item, float cellSize, float spacing)
        {
            var nameObj = new GameObject("ItemName", typeof(Text));
            nameObj.transform.SetParent(cellObj.transform, false);

            float extraW = (item.GridWidth - 1) * (cellSize + spacing);
            float extraH = (item.GridHeight - 1) * (cellSize + spacing);

            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 1);
            nameRt.anchorMax = new Vector2(0, 1);
            nameRt.pivot = new Vector2(0, 1);
            nameRt.sizeDelta = new Vector2(cellSize + extraW, cellSize + extraH);
            nameRt.anchoredPosition = Vector2.zero;

            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.text = item.itemData.itemName;
            nameTxt.font = GetFont();
            nameTxt.fontSize = 10;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.resizeTextForBestFit = true;
            nameTxt.resizeTextMinSize = 7;
            nameTxt.resizeTextMaxSize = 12;
        }

        /// <summary> 中栏：显示装备名称文本 </summary>
        void CreateEquipNameText(Transform parent, string slotName, string equippedItemName, float w, float h, float x, float y)
        {
            var obj = new GameObject($"EN_{slotName}", typeof(Image), typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var img = obj.GetComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 0.6f);

            var txtObj = new GameObject("Label", typeof(Text));
            txtObj.transform.SetParent(obj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            var txt = txtObj.GetComponent<Text>();
            string display = string.IsNullOrEmpty(equippedItemName) ? slotName + "\n(未装备)" : slotName + "\n" + equippedItemName;
            txt.text = display;
            txt.fontSize = 12;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = string.IsNullOrEmpty(equippedItemName) ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
        }

        /// <summary> 数值展示区：创建一行标签+值（坐标定位，扩展用）</summary>
        void CreateStatRow(Transform parent, string label, string value, float colW, float rowH, float x, float y)
        {
            // 数值标签
            var labelRt = MakeRect("SL_" + label, parent.GetComponent<RectTransform>(),
                x, y, colW * 0.5f, rowH);
            var labelTxt = labelRt.gameObject.AddComponent<Text>();
            labelTxt.text = label;
            labelTxt.font = GetFont();
            labelTxt.fontSize = 12;
            labelTxt.alignment = TextAnchor.MiddleLeft;
            labelTxt.color = new Color(0.7f, 0.7f, 0.7f);

            // 数值
            var valRt = MakeRect("SV_" + label, parent.GetComponent<RectTransform>(),
                x + colW * 0.5f, y, colW * 0.5f, rowH);
            var valTxt = valRt.gameObject.AddComponent<Text>();
            valTxt.text = value;
            valTxt.font = GetFont();
            valTxt.fontSize = 12;
            valTxt.alignment = TextAnchor.MiddleRight;
            valTxt.color = Color.white;
            valTxt.fontStyle = FontStyle.Bold;
        }

        private Font GetFont()
        {
            Font font;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { font = null; }
            if (font == null) font = UGUIBuilder.DefaultFont;
            return font;
        }

        // ═══ 右键物品详情 ═══

        public void ShowItemDetail(ItemData item)
        {
            if (item == null) return;
            HideItemDetail();

            // 找到 overviewPanel 作为父节点
            var parent = overviewPanel != null ? overviewPanel.transform : transform;

            _itemDetailPanel = UGUIBuilder.CreateStretchPanel("ItemDetail", parent,
                new Color(0.08f, 0.08f, 0.1f, 0.95f));
            var rt = _itemDetailPanel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(280, 220);
            rt.anchoredPosition = Vector2.zero;
            rt.SetAsLastSibling();

            var vlg = _itemDetailPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 8);
            vlg.spacing = 3;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 标题
            var title = AddDetailText($"<b>{item.itemName}</b>", 16, Color.white);

            // 基础属性
            string baseInfo = $"重量:{item.weight}  堆叠:{(item.maxStack > 1 ? "×" + item.maxStack : "不可堆叠")}";
            if (item.hasDurability) baseInfo += $"  耐久:{item.maxDurability}";
            AddDetailText(baseInfo, 12, new Color(0.7f, 0.7f, 0.7f));

            // 装备/武器属性
            if (item.equipSlot != EquipSlot.None)
                AddDetailText($"装备位: {item.equipSlot}  护甲:{item.armorValue}  保暖:{item.warmthValue}", 12, Color.white);
            if (item.weaponDamage > 0)
                AddDetailText(item.isFirearm
                    ? $"伤害:{item.weaponDamage}  射速:{item.fireRate}/s  弹匣:{item.magazineSize}"
                    : $"近战伤害:{item.weaponDamage}", 12, Color.white);

            // 从图谱获取产业链信息
            var graph = _Game.Systems.ItemGraph.ItemGraphManager.Instance;
            if (graph != null)
            {
                var node = graph.GetNode(item.itemName);
                if (node != null)
                {
                    AddDetailText($"产业链: {node.primaryChain}  深度: {node.MinDepth}-{node.MaxDepth}", 11, new Color(0.6f, 0.8f, 1f));
                    AddDetailText($"工作台: {node.EffectiveStation}", 11, new Color(0.6f, 0.8f, 1f));

                    if (node.upstreamItemNames != null && node.upstreamItemNames.Length > 0)
                        AddDetailText($"← 上游: {string.Join(", ", node.upstreamItemNames)}", 10, new Color(0.5f, 0.9f, 0.5f));

                    if (node.downstreamItemNames != null && node.downstreamItemNames.Length > 0)
                        AddDetailText($"→ 下游: {string.Join(", ", node.downstreamItemNames)}", 10, new Color(0.9f, 0.7f, 0.3f));

                    if (node.consumerCount > 0)
                        AddDetailText($"热度: {node.consumerCount} 个配方需要此物品", 10, new Color(0.6f, 0.6f, 0.6f));

                    if (node.isRawMaterial)
                        AddDetailText("● 基础原材料", 10, new Color(0.4f, 0.8f, 0.4f));
                    if (node.isDeadEnd)
                        AddDetailText("● 终端物品（无下游配方）", 10, new Color(0.8f, 0.4f, 0.4f));
                }
            }

            // 关闭按钮
            var closeBtn = UGUIBuilder.CreateButton("DetailClose", _itemDetailPanel.transform, "关闭",
                new Color(0.3f, 0.3f, 0.3f), 60, 24);
            closeBtn.onClick.AddListener(HideItemDetail);
            var brt = closeBtn.GetComponent<RectTransform>();
            brt.sizeDelta = new Vector2(60, 24);

            // 点击背包其他地方也关闭
            StartCoroutine(DetailAutoClose());
        }

        Text AddDetailText(string msg, int size, Color c)
        {
            var go = new GameObject("DT", typeof(Text));
            go.transform.SetParent(_itemDetailPanel.transform, false);
            var t = go.GetComponent<Text>();
            t.font = UGUIBuilder.DefaultFont;
            t.text = msg;
            t.fontSize = size;
            t.color = c;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        System.Collections.IEnumerator DetailAutoClose()
        {
            yield return new UnityEngine.WaitForSeconds(0.15f);
            // 简单的点击空白处关闭：在 overviewPanel 上加透明遮罩
        }

        void HideItemDetail()
        {
            if (_itemDetailPanel != null) { Destroy(_itemDetailPanel); _itemDetailPanel = null; }
        }
    }
}
