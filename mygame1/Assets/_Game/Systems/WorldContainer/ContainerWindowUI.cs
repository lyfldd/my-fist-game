using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Inventory;
using _Game.UI;

namespace _Game.Systems.WorldContainer
{
    /// <summary>
    /// 容器窗口 UI（可拖拽子窗口）
    /// 显示容器格子，支持点击拾取 + 拖拽移动
    /// </summary>
    public class ContainerWindowUI : MonoBehaviour
    {
        public static ContainerWindowUI Instance { get; private set; }

        [Header("窗口尺寸")]
        public float windowWidth = 320f;
        public float windowHeight = 260f;
        public float cellSize = 50f;
        public float cellGap = 4f;

        private RectTransform _windowRt;
        private RectTransform _titleBar;
        private RectTransform _contentArea;
        private InventoryContainer _container;
        private WorldContainer _worldContainer;
        private Canvas _canvas;
        private bool _isOpen;
        private Text _titleLabel;

        // 双击/拾取检测
        private float _lastClickTime;
        private int _lastClickX = -1;
        private int _lastClickY = -1;
        private const float DOUBLE_CLICK_TIME = 0.3f;

        void Awake()
        {
            Instance = this;
            _canvas = GetComponentInParent<Canvas>();
            gameObject.SetActive(false);
        }

        /// <summary> 打开容器窗口 </summary>
        public void OpenContainer(WorldContainer wc, InventoryContainer container, string title = "容器")
        {
            _worldContainer = wc;
            _container = container;

            // 创建窗口（首次调用）
            if (_windowRt == null)
                CreateWindow();

            // 更新标题
            if (_titleLabel != null)
                _titleLabel.text = title;

            RefreshGrid();
            gameObject.SetActive(true);
            _isOpen = true;
        }

        /// <summary> 关闭窗口 </summary>
        public void CloseWindow()
        {
            gameObject.SetActive(false);
            _isOpen = false;

            // 通知 WorldContainer 检查空状态
            if (_worldContainer != null)
                _worldContainer.OnContainerWindowClosed();

            _worldContainer = null;
            _container = null;
        }

        void CreateWindow()
        {
            var go = new GameObject("ContainerWindow", typeof(RectTransform), typeof(Image));
            _windowRt = go.GetComponent<RectTransform>();
            _windowRt.SetParent(transform, false);
            _windowRt.sizeDelta = new Vector2(windowWidth, windowHeight);
            _windowRt.anchoredPosition = Vector2.zero;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            // 标题栏（可拖拽）
            var titleGo = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            _titleBar = titleGo.GetComponent<RectTransform>();
            _titleBar.SetParent(_windowRt, false);
            _titleBar.sizeDelta = new Vector2(windowWidth, 30f);
            _titleBar.anchoredPosition = new Vector2(0, windowHeight * 0.5f - 15f);
            titleGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // 标题文字（子对象）
            var labelGo = new GameObject("TitleLabel", typeof(RectTransform));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(_titleBar, false);
            labelRt.sizeDelta = new Vector2(windowWidth - 40f, 30f);
            labelRt.anchoredPosition = new Vector2(-10f, 0);
            var labelTxt = labelGo.AddComponent<Text>();
            labelTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            labelTxt.text = "容器";
            labelTxt.fontSize = 14;
            labelTxt.color = Color.white;
            labelTxt.alignment = TextAnchor.MiddleLeft;
            _titleLabel = labelTxt;

            // 拖拽事件
            var dragTrigger = titleGo.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.Drag;
            entry.callback.AddListener((data) => OnDrag((PointerEventData)data));
            dragTrigger.triggers.Add(entry);

            // 关闭按钮
            var closeBtn = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.SetParent(_titleBar, false);
            closeRt.sizeDelta = new Vector2(24f, 24f);
            closeRt.anchoredPosition = new Vector2(windowWidth * 0.5f - 16f, 0);
            closeBtn.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f);

            // 关闭按钮文字（子对象）
            var closeLabel = new GameObject("XLabel", typeof(RectTransform));
            closeLabel.transform.SetParent(closeRt, false);
            var closeLabelRt = closeLabel.GetComponent<RectTransform>();
            closeLabelRt.sizeDelta = new Vector2(24f, 24f);
            var closeTxt = closeLabel.AddComponent<Text>();
            closeTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            closeTxt.text = "X";
            closeTxt.fontSize = 14;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;

            closeBtn.GetComponent<Button>().onClick.AddListener(() => CloseWindow());

            // 内容区域
            var contentGo = new GameObject("Content", typeof(RectTransform));
            _contentArea = contentGo.GetComponent<RectTransform>();
            _contentArea.SetParent(_windowRt, false);
            _contentArea.sizeDelta = new Vector2(windowWidth - 20f, windowHeight - 50f);
            _contentArea.anchoredPosition = new Vector2(0, -10f);
        }

        void RefreshGrid()
        {
            if (_container == null) return;

            // 清除旧覆盖层（保留格子背景和拖拽注册）
            for (int i = _contentArea.childCount - 1; i >= 0; i--)
            {
                var child = _contentArea.GetChild(i);
                if (child.name.StartsWith("OV_"))
                    Destroy(child.gameObject);
            }

            float startX = -_contentArea.sizeDelta.x * 0.5f + cellSize * 0.5f;
            float startY = _contentArea.sizeDelta.y * 0.5f - cellSize * 0.5f;
            Color emptyColor = new Color(0.2f, 0.2f, 0.2f);
            Color fillColor = new Color(0.3f, 0.7f, 0.3f);

            // 第一遍：更新格子背景（保留已有格子，不销毁）
            for (int y = 0; y < _container.gridHeight; y++)
            {
                for (int x = 0; x < _container.gridWidth; x++)
                {
                    int idx = y * _container.gridWidth + x;
                    RectTransform cellRt;

                    if (idx < _contentArea.childCount)
                    {
                        cellRt = _contentArea.GetChild(idx) as RectTransform;
                    }
                    else
                    {
                        var cellGo = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
                        cellRt = cellGo.GetComponent<RectTransform>();
                        cellRt.SetParent(_contentArea, false);
                        cellRt.sizeDelta = new Vector2(cellSize, cellSize);
                        if (DragDropManager.Instance != null)
                            DragDropManager.Instance.RegisterCellRect(cellRt, _container, x, y);
                    }

                    float px = startX + x * (cellSize + cellGap);
                    float py = startY - y * (cellSize + cellGap);
                    cellRt.anchoredPosition = new Vector2(px, py);
                    cellRt.GetComponent<Image>().raycastTarget = true;

                    var placed = _container.GetItemAt(x, y);
                    bool occupied = placed.HasValue && placed.Value.itemData != null;
                    cellRt.GetComponent<Image>().color = occupied ? fillColor : emptyColor;
                }
            }

            // 第二遍：为每个物品绘制覆盖层（名称+数量）
            foreach (var p in _container.placedItems)
            {
                if (p.itemData == null) continue;

                int gx = p.gridX, gy = p.gridY;
                float overlayW = p.GridWidth * cellSize + (p.GridWidth - 1) * cellGap;
                float overlayH = p.GridHeight * cellSize + (p.GridHeight - 1) * cellGap;
                float ox = startX + gx * (cellSize + cellGap);
                float oy = startY - gy * (cellSize + cellGap);

                var overlay = new GameObject("OV_" + p.itemData.itemName, typeof(RectTransform));
                var overlayRt = overlay.GetComponent<RectTransform>();
                overlayRt.SetParent(_contentArea, false);
                overlayRt.sizeDelta = new Vector2(overlayW, overlayH);
                overlayRt.anchoredPosition = new Vector2(ox, oy);
                overlayRt.SetAsLastSibling();

                // 物品名称
                float nameH = overlayH * 0.55f;
                var nameRt = MakeChildRect("Name", overlayRt, 0, 0, overlayW, nameH);
                var nameTxt = nameRt.gameObject.AddComponent<Text>();
                nameTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
                nameTxt.text = p.itemData.itemName;
                nameTxt.fontSize = Mathf.FloorToInt(Mathf.Clamp(overlayW * 0.18f, 8f, 16f));
                nameTxt.alignment = TextAnchor.MiddleCenter;
                nameTxt.color = Color.white;
                nameTxt.resizeTextForBestFit = true;
                nameTxt.resizeTextMinSize = 7;
                nameTxt.resizeTextMaxSize = nameTxt.fontSize;
                nameTxt.raycastTarget = false;

                // 数量 xN
                if (p.count > 1)
                {
                    float cntW = overlayW * 0.5f;
                    float cntH = overlayH * 0.35f;
                    var cntRt = MakeChildRect("Count", overlayRt, overlayW - cntW, -(overlayH - cntH), cntW, cntH);
                    var cntTxt = cntRt.gameObject.AddComponent<Text>();
                    cntTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
                    cntTxt.text = $"x{p.count}";
                    cntTxt.fontSize = Mathf.FloorToInt(Mathf.Clamp(overlayH * 0.25f, 8f, 14f));
                    cntTxt.alignment = TextAnchor.LowerRight;
                    cntTxt.color = Color.yellow;
                    cntTxt.resizeTextForBestFit = true;
                    cntTxt.resizeTextMinSize = 7;
                    cntTxt.resizeTextMaxSize = cntTxt.fontSize;
                    cntTxt.raycastTarget = false;
                }

                // 双击拾取（复用已有格子不再重复AddComponent）
                int cellIdx = gy * _container.gridWidth + gx;
                if (cellIdx < _contentArea.childCount)
                {
                    var cellGo = _contentArea.GetChild(cellIdx).gameObject;
                    if (cellGo.GetComponent<Button>() == null)
                    {
                        var btn = cellGo.AddComponent<Button>();
                        btn.onClick.AddListener(() => HandleCellClick(gx, gy));
                    }
                }
            }
        }

        void HandleCellClick(int gridX, int gridY)
        {
            float now = UnityEngine.Time.time;
            if (gridX == _lastClickX && gridY == _lastClickY && now - _lastClickTime < DOUBLE_CLICK_TIME)
            {
                // 双击 → 拾取
                PickupItem(gridX, gridY);
                _lastClickX = -1;
                _lastClickY = -1;
            }
            else
            {
                // 第一次单击，等待第二次
                _lastClickTime = now;
                _lastClickX = gridX;
                _lastClickY = gridY;
            }
        }

        void PickupItem(int gridX, int gridY)
        {
            if (_container == null) return;

            var placed = _container.GetItemAt(gridX, gridY);
            if (!placed.HasValue || placed.Value.itemData == null) return;

            var pItem = placed.Value;
            var inventory = PlayerRegistry.Get<_Game.Systems.Inventory.Inventory>();
            if (inventory == null) return;

            int count = pItem.count;
            int added = inventory.AddItem(pItem.itemData, count);
            if (added > 0)
            {
                _container.RemoveItemAt(gridX, gridY, added);
                RefreshGrid();
            }
            else
            {
                Debug.Log("背包已满");
            }
        }

        // 窗口拖拽
        private Vector2 _dragOffset;
        public void OnDrag(PointerEventData eventData)
        {
            if (_windowRt != null && _canvas != null)
            {
                Vector2 pos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas.transform as RectTransform,
                    eventData.position, _canvas.worldCamera, out pos);
                _windowRt.anchoredPosition = pos - new Vector2(0, 0);
            }
        }

        void OnEnable() { InputRouter.BindKey(KeyCode.Escape, InputPriority.UI, HandleEsc, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }

        bool HandleEsc()
        {
            if (!_isOpen) return false;
            CloseWindow();
            return true;
        }

        /// <summary> 创建子RectTransform（同总面板的MakeRect风格）</summary>
        RectTransform MakeChildRect(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            rt.pivot = new Vector2(0, 1);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            return rt;
        }
    }
}
