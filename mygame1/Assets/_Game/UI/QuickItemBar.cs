using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Config;
using _Game.Systems.Inventory;

namespace _Game.UI
{
    /// <summary>
    /// 快捷物品栏（翻页版）
    /// 屏幕中下方固定显示 10 格，滚轮翻页
    /// 显示来自上衣/胸挂/腰带/裤子的物品
    /// 每格对应一个 PlacedItem（不论网格大小）
    /// 数字键 1-0 对应可见的格子
    /// </summary>
    public class QuickItemBar : MonoBehaviour
    {
        [Header("显示设置")]
        [SerializeField] private int maxVisibleSlots = 10;
        [SerializeField] private int slotSize = 50;
        [SerializeField] private int spacing = 3;
        [SerializeField] private float bottomMargin = 20f;

        [Header("颜色")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color occupiedColor = new Color(0.3f, 0.3f, 0.35f, 0.9f);
        [SerializeField] private Color arrowColor = new Color(1f, 1f, 1f, 0.5f);

        private Inventory inventory;
        private GameObject canvasObject;
        private Image[] slotImages;
        private Text[] slotCountTexts;
        private Text[] slotNameTexts;
        private Text arrowLeftText;
        private Text arrowRightText;
        private int scrollIndex = 0;

        // 选中状态
        private int selectedIndex = -1;
        private GameObject[] slotBorders;
        private RectTransform[] slotRects;

        // 使用进度（中央全局UI）
        private GameObject globalProgressGo;
        private Image globalProgressFill;
        private Text globalProgressNameTxt;
        private Coroutine useCoroutine;
        private bool isUsingItem = false;

        // 用于步骤2：外部绑定"使用物品"回调
        public System.Action<int> onItemUseAction;

        // 快捷容器（按显示顺序）
        private static readonly EquipSlot[] QuickSlots = new EquipSlot[]
        {
            EquipSlot.Tops, EquipSlot.Vest, EquipSlot.Belt, EquipSlot.Pants
        };

        private void Start()
        {
            inventory = ServiceLocator.Get<Inventory>();
            if (inventory == null)
            {
                Debug.LogWarning("QuickItemBar: 未找到 Inventory，不显示");
                return;
            }

            CreateCanvas();
            EventBus.Subscribe<InventoryChanged>(OnInventoryChanged);
        }

        void OnEnable()
        {
            for (int i = 0; i < maxVisibleSlots; i++)
            {
                int idx = i;
                InputRouter.BindKey(GetKeyForIndex(idx), InputPriority.Gameplay, () => HandleNumberKey(idx), this);
            }
            InputRouter.BindMouse(0, InputPriority.Gameplay, HandleSlotLeftClick, this);
            InputRouter.BindMouse(1, InputPriority.Gameplay, HandleSlotRightClick, this);
        }

        void OnDisable() { InputRouter.UnbindAll(this); }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<InventoryChanged>(OnInventoryChanged);
        }

        private void Update()
        {
            bool buildVisible = _Game.Systems.Building.BuildMenuUI.IsVisible;
            if (canvasObject != null && canvasObject.activeSelf != !buildVisible)
                canvasObject.SetActive(!buildVisible);
            if (buildVisible) return;
            HandleScroll();
        }

        // ---- 数字键回调 (由 InputRouter 调用) ----

        bool HandleNumberKey(int index)
        {
            if (isUsingItem) return false;
            var items = CollectItems();
            int itemIndex = scrollIndex + index;
            if (itemIndex >= items.Count) return false;

            if (selectedIndex == index)
                SetSelected(-1);
            else
                SetSelected(index);
            return true;
        }

        // ---- 鼠标回调 (由 InputRouter 调用) ----

        bool HandleSlotLeftClick()
        {
            bool hitSlot = false;
            for (int i = 0; i < maxVisibleSlots; i++)
            {
                if (slotRects != null && slotRects[i] != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(slotRects[i], Input.mousePosition))
                {
                    if (isUsingItem) return false;
                    var items = CollectItems();
                    int itemIndex = scrollIndex + i;
                    if (itemIndex >= items.Count) return false;

                    if (selectedIndex == i)
                        SetSelected(-1);
                    else
                        SetSelected(i);
                    hitSlot = true;
                    break;
                }
            }
            if (!hitSlot)
                SetSelected(-1);
            return hitSlot; // 没点到格子就放行给战斗系统
        }

        bool HandleSlotRightClick()
        {
            // 右键任意位置都触发使用（不判断 slot rect）
            TryUseSelected();
            return true;
        }

        // ===== UI 创建 =====

        private void CreateCanvas()
        {
            canvasObject = new GameObject("QuickItemBar_Canvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            CreateSlots();
            CreateArrows();
            CreateGlobalProgressBar();
            RefreshAll();
        }

        private void CreateSlots()
        {
            var gridGo = new GameObject("SlotGrid", typeof(RectTransform));
            gridGo.transform.SetParent(canvasObject.transform, false);
            gridGo.transform.localScale = Vector3.one;

            float totalWidth = maxVisibleSlots * slotSize + (maxVisibleSlots - 1) * spacing;

            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.5f, 0);
            gridRt.anchorMax = new Vector2(0.5f, 0);
            gridRt.pivot = new Vector2(0.5f, 0);
            gridRt.sizeDelta = new Vector2(totalWidth, slotSize);
            gridRt.anchoredPosition = new Vector2(0, bottomMargin);

            var glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(slotSize, slotSize);
            glg.spacing = new Vector2(spacing, 0);
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = maxVisibleSlots;

            slotImages = new Image[maxVisibleSlots];
            slotCountTexts = new Text[maxVisibleSlots];
            slotNameTexts = new Text[maxVisibleSlots];
            slotBorders = new GameObject[maxVisibleSlots];
            slotRects = new RectTransform[maxVisibleSlots];

            for (int i = 0; i < maxVisibleSlots; i++)
            {
                var slotObj = new GameObject($"Slot_{i}", typeof(Image));
                slotObj.transform.SetParent(gridGo.transform, false);
                slotObj.transform.localScale = Vector3.one;

                var img = slotObj.GetComponent<Image>();
                img.color = emptyColor;
                slotImages[i] = img;

                // 数字标签（显示 1-0）
                var numObj = new GameObject("KeyNum", typeof(Text));
                numObj.transform.SetParent(slotObj.transform, false);
                var numRt = numObj.GetComponent<RectTransform>();
                numRt.anchorMin = new Vector2(0, 1);
                numRt.anchorMax = new Vector2(0, 1);
                numRt.pivot = new Vector2(0, 1);
                numRt.sizeDelta = new Vector2(18, 16);
                numRt.anchoredPosition = new Vector2(2, -2);

                var numText = numObj.GetComponent<Text>();
                numText.font = GetFont();
                numText.fontSize = 11;
                numText.alignment = TextAnchor.UpperLeft;
                numText.color = new Color(1, 1, 1, 0.5f);
                numText.text = GetKeyLabel(i);

                // 物品名字（顶部居中，slotSize=50）
                var nameObj = new GameObject("ItemName", typeof(Text));
                nameObj.transform.SetParent(slotObj.transform, false);
                var nameRt = nameObj.GetComponent<RectTransform>();
                nameRt.anchorMin = new Vector2(0.5f, 1);
                nameRt.anchorMax = new Vector2(0.5f, 1);
                nameRt.pivot = new Vector2(0.5f, 1);
                nameRt.sizeDelta = new Vector2(slotSize - 4, 16);
                nameRt.anchoredPosition = new Vector2(slotSize / 2f, -2);

                var nameText = nameObj.GetComponent<Text>();
                nameText.font = GetFont();
                nameText.fontSize = 9;
                nameText.alignment = TextAnchor.UpperCenter;
                nameText.color = Color.white;
                nameText.text = "";
                slotNameTexts[i] = nameText;

                // 数量文字（底部居中）
                var countObj = new GameObject("Count", typeof(Text));
                countObj.transform.SetParent(slotObj.transform, false);
                var countRt = countObj.GetComponent<RectTransform>();
                countRt.anchorMin = new Vector2(0.5f, 0);
                countRt.anchorMax = new Vector2(0.5f, 0);
                countRt.pivot = new Vector2(0.5f, 0);
                countRt.sizeDelta = new Vector2(slotSize - 4, 14);
                countRt.anchoredPosition = new Vector2(slotSize / 2f, 2);

                var countText = countObj.GetComponent<Text>();
                countText.font = GetFont();
                countText.fontSize = 11;
                countText.alignment = TextAnchor.LowerCenter;
                countText.color = Color.yellow;
                countText.text = "";
                slotCountTexts[i] = countText;

                // 格子 RectTransform（用于鼠标点击检测）
                slotRects[i] = (RectTransform)slotObj.transform;

                // 选中白色边框（4 条坐标定位的色条）
                var borderGo = new GameObject("Border", typeof(RectTransform));
                borderGo.transform.SetParent(slotObj.transform, false);
                var borderGroup = borderGo.GetComponent<RectTransform>();
                borderGroup.anchorMin = Vector2.zero;
                borderGroup.anchorMax = Vector2.one;
                borderGroup.sizeDelta = Vector2.zero;
                borderGroup.anchoredPosition = Vector2.zero;

                int bw = 2; // border width/height

                // 上
                var topImg = MakeBorderStrip("T", borderGroup, 0, 0, slotSize, bw);
                // 下
                var botImg = MakeBorderStrip("B", borderGroup, 0, -(slotSize - bw), slotSize, bw);
                // 左
                var leftImg = MakeBorderStrip("L", borderGroup, 0, -bw, bw, slotSize - bw * 2);
                // 右
                var rightImg = MakeBorderStrip("R", borderGroup, slotSize - bw, -bw, bw, slotSize - bw * 2);

                borderGo.SetActive(false);
                slotBorders[i] = borderGo;
            }
        }

        private void CreateArrows()
        {
            float barWidth = maxVisibleSlots * slotSize + (maxVisibleSlots - 1) * spacing;

            // 左箭头
            var leftGo = new GameObject("ArrowLeft", typeof(Text));
            leftGo.transform.SetParent(canvasObject.transform, false);
            var leftRt = leftGo.GetComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0.5f, 0);
            leftRt.anchorMax = new Vector2(0.5f, 0);
            leftRt.pivot = new Vector2(1, 0.5f);
            leftRt.sizeDelta = new Vector2(30, slotSize);
            leftRt.anchoredPosition = new Vector2(-barWidth / 2 - 8, bottomMargin + slotSize / 2);

            arrowLeftText = leftGo.GetComponent<Text>();
            arrowLeftText.font = GetFont();
            arrowLeftText.fontSize = 24;
            arrowLeftText.alignment = TextAnchor.MiddleCenter;
            arrowLeftText.color = arrowColor;
            arrowLeftText.text = "<";

            // 右箭头
            var rightGo = new GameObject("ArrowRight", typeof(Text));
            rightGo.transform.SetParent(canvasObject.transform, false);
            var rightRt = rightGo.GetComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(0.5f, 0);
            rightRt.anchorMax = new Vector2(0.5f, 0);
            rightRt.pivot = new Vector2(0, 0.5f);
            rightRt.sizeDelta = new Vector2(30, slotSize);
            rightRt.anchoredPosition = new Vector2(barWidth / 2 + 8, bottomMargin + slotSize / 2);

            arrowRightText = rightGo.GetComponent<Text>();
            arrowRightText.font = GetFont();
            arrowRightText.fontSize = 24;
            arrowRightText.alignment = TextAnchor.MiddleCenter;
            arrowRightText.color = arrowColor;
            arrowRightText.text = ">";
        }

        // ===== 中央全局进度条 =====

        private void CreateGlobalProgressBar()
        {
            var go = new GameObject("UseProgress_Canvas", typeof(Canvas));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;

            // 背景面板
            var panel = new GameObject("Panel", typeof(Image), typeof(RectTransform));
            panel.transform.SetParent(go.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(200, 36);
            panelRt.anchoredPosition = new Vector2(0, 180);
            var panelImg = panel.GetComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.7f);

            // 进度填充条（加一个纯白 Sprite 确保 Filled 模式工作）
            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(panel.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2, 2);
            fillRt.offsetMax = new Vector2(-2, -2);
            var fillImg = fill.GetComponent<Image>();
            var whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
            fillImg.sprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            fillImg.color = new Color(0.2f, 0.8f, 0.3f, 0.6f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0;

            // 物品名称
            var name = new GameObject("Name", typeof(Text));
            name.transform.SetParent(panel.transform, false);
            var nameRt = name.GetComponent<RectTransform>();
            nameRt.anchorMin = Vector2.zero;
            nameRt.anchorMax = Vector2.one;
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameTxt = name.GetComponent<Text>();
            nameTxt.font = GetFont();
            nameTxt.fontSize = 14;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;
            nameTxt.text = "";

            globalProgressGo = go;
            globalProgressFill = fillImg;
            globalProgressNameTxt = nameTxt;
            go.SetActive(false);
        }

        // ===== 物品收集 =====

        private List<PlacedItem> CollectItems()
        {
            var items = new List<PlacedItem>();
            foreach (var slot in QuickSlots)
            {
                var c = inventory.GetContainer(slot);
                if (c == null || c.TotalCells == 0) continue;

                for (int y = c.gridHeight - 1; y >= 0; y--)
                    for (int x = 0; x < c.gridWidth; x++)
                    {
                        var placed = FindItemAt(c, x, y);
                        if (placed.HasValue && placed.Value.itemData != null)
                            if (placed.Value.gridX == x && placed.Value.gridY == y)
                                items.Add(placed.Value);
                    }
            }
            return items;
        }

        private PlacedItem? FindItemAt(InventoryContainer container, int x, int y)
        {
            foreach (var p in container.placedItems)
                if (x >= p.gridX && x < p.gridX + p.GridWidth &&
                    y >= p.gridY && y < p.gridY + p.GridHeight)
                    return p;
            return null;
        }

        // ===== 交互 =====

        private void HandleScroll()
        {
            // 建造菜单打开时快捷栏不抢滚轮（两个UI底部重叠）
            if (_Game.Systems.Building.BuildMenuUI.IsVisible) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll == 0) return;

            // 仅鼠标在快捷栏区域内时响应滚轮
            if (!IsMouseOverBar()) return;

            var items = CollectItems();
            int itemCount = items.Count;
            if (itemCount <= maxVisibleSlots) return;

            int maxScroll = itemCount - maxVisibleSlots;
            if (scroll > 0) scrollIndex = Mathf.Max(0, scrollIndex - 1);
            else scrollIndex = Mathf.Min(maxScroll, scrollIndex + 1);

            RefreshAll();
        }

        private void TryUseSelected()
        {
            if (isUsingItem || selectedIndex < 0) return;

            var items = CollectItems();
            int itemIndex = scrollIndex + selectedIndex;
            if (itemIndex >= items.Count) return;

            SetSelected(selectedIndex);
            onItemUseAction?.Invoke(selectedIndex);
            useCoroutine = StartCoroutine(UseItemCoroutine(selectedIndex));
        }

        private IEnumerator UseItemCoroutine(int slotIndex)
        {
            var items = CollectItems();
            int itemIndex = scrollIndex + slotIndex;
            if (itemIndex >= items.Count) yield break;

            var item = items[itemIndex];
            if (item.itemData == null) yield break;

            float useTime = item.itemData.useTime;
            isUsingItem = true;

            // 显示全局进度条
            if (globalProgressGo != null)
            {
                globalProgressGo.SetActive(true);
                globalProgressFill.fillAmount = 0;
                globalProgressNameTxt.text = item.itemData.itemName;
                float elapsed = 0;
                while (elapsed < useTime)
                {
                    elapsed += Time.deltaTime;
                    globalProgressFill.fillAmount = elapsed / useTime;
                    yield return null;
                }
                globalProgressFill.fillAmount = 1;
                yield return new WaitForSeconds(0.15f);
                globalProgressGo.SetActive(false);
            }

            // 使用完成 → 直接扣物品 + 发布事件（SurvivalSystem 自己处理效果）
            inventory.RemoveItem(item.itemData, 1);
            EventBus.Publish(new ItemUsedEvent(item.itemData, 1));

            isUsingItem = false;
            SetSelected(-1);
            RefreshAll();
        }

        private void SetSelected(int index)
        {
            if (selectedIndex == index) return;
            selectedIndex = index;
            for (int i = 0; i < maxVisibleSlots; i++)
            {
                if (slotBorders != null && slotBorders[i] != null)
                    slotBorders[i].SetActive(i == index);
            }
        }

        /// <summary> 在格子内创建一个白色边框色条（anchor(0,1) 绝对坐标）</summary>
        private Image MakeBorderStrip(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }


        // ===== 刷新 =====

        private void RefreshAll()
        {
            if (slotImages == null) return;

            var items = CollectItems();
            int itemCount = items.Count;

            // 限制滚动范围
            int maxScroll = Mathf.Max(0, itemCount - maxVisibleSlots);
            scrollIndex = Mathf.Clamp(scrollIndex, 0, maxScroll);

            // 显示箭头
            if (arrowLeftText != null) arrowLeftText.gameObject.SetActive(scrollIndex > 0);
            if (arrowRightText != null) arrowRightText.gameObject.SetActive(scrollIndex < maxScroll);

            // 填充可见格子
            for (int i = 0; i < maxVisibleSlots; i++)
            {
                int itemIdx = scrollIndex + i;
                var img = slotImages[i];
                var txt = slotCountTexts[i];
                var nameTxt = slotNameTexts[i];

                if (itemIdx < itemCount)
                {
                    var item = items[itemIdx];
                    if (item.itemData.icon != null)
                    {
                        img.sprite = item.itemData.icon;
                        img.type = Image.Type.Simple;
                        img.preserveAspect = true;
                    }
                    img.color = occupiedColor;
                    txt.text = "x" + item.count;
                    nameTxt.text = item.itemData.itemName;
                }
                else
                {
                    img.sprite = null;
                    img.color = emptyColor;
                    txt.text = "";
                    nameTxt.text = "";
                }
            }

            // 物品变动 → 强制取消选中 + 重置进度
            if (isUsingItem && useCoroutine != null)
            {
                StopCoroutine(useCoroutine);
                isUsingItem = false;
            }
            if (globalProgressGo != null) globalProgressGo.SetActive(false);
            SetSelected(-1);
        }

        private void OnInventoryChanged(InventoryChanged evt)
        {
            RefreshAll();
        }

        /// <summary> 鼠标是否在快捷栏区域（含左右箭头）内 </summary>
        private bool IsMouseOverBar()
        {
            float totalWidth = maxVisibleSlots * slotSize + (maxVisibleSlots - 1) * spacing;
            float barLeft = Screen.width / 2f - totalWidth / 2f - 38f;  // 含左箭头宽度
            float barRight = Screen.width / 2f + totalWidth / 2f + 38f; // 含右箭头宽度
            float barBottom = bottomMargin - 4f;
            float barTop = bottomMargin + slotSize + 4f;

            Vector2 mousePos = Input.mousePosition;
            return mousePos.x >= barLeft && mousePos.x <= barRight
                && mousePos.y >= barBottom && mousePos.y <= barTop;
        }

        // ===== 工具 =====

        /// <summary> 第 i 格对应的按键 </summary>
        private KeyCode GetKeyForIndex(int i)
        {
            return i switch
            {
                0 => KeyCode.Alpha1,
                1 => KeyCode.Alpha2,
                2 => KeyCode.Alpha3,
                3 => KeyCode.Alpha4,
                4 => KeyCode.Alpha5,
                5 => KeyCode.Alpha6,
                6 => KeyCode.Alpha7,
                7 => KeyCode.Alpha8,
                8 => KeyCode.Alpha9,
                9 => KeyCode.Alpha0,
                _ => KeyCode.None
            };
        }

        /// <summary> 第 i 格的按键标签文字 </summary>
        private string GetKeyLabel(int i)
        {
            return i == 9 ? "0" : (i + 1).ToString();
        }

        private Font GetFont()
        {
            Font font;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { font = null; }
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            return font;
        }
    }
}
