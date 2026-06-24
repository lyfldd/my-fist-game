using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using _Game.Core;
using _Game.Config;
using _Game.Systems.Inventory;
using Inv = _Game.Systems.Inventory.Inventory;

namespace _Game.UI
{
    /// <summary>
    /// 拖拽管理器（坐标检测版 + 旋转）
    /// </summary>
    public class DragDropManager : MonoBehaviour
    {
        public static DragDropManager Instance { get; private set; }

        private List<CellRegion> _cellRegions = new List<CellRegion>();

        // 拖拽状态
        private bool _pointerDown;
        private PlacedItem? _pendingItem;
        private InventoryContainer _pendingSource;
        private Vector2 _downPos;
        private int _clickCellX, _clickCellY;
        private const float DRAG_THRESHOLD = 8f;

        // 活跃拖拽
        private bool _isDragging;
        public bool IsDragging => _isDragging;
        private PlacedItem? _dragItem;
        private InventoryContainer _dragSource;
        private GameObject _dragVisual;
        private bool _dragRotated; // 拖拽期间的旋转状态
        private bool _clickConsumed;
        private Inv _inventory;

        // 选中状态
        public PlacedItem? SelectedItem { get; private set; }
        public InventoryContainer SelectedContainer { get; private set; }
        public int SelectedCellX { get; private set; }
        public int SelectedCellY { get; private set; }

        struct CellRegion
        {
            public RectTransform rectTransform;
            public InventoryContainer container;
            public int gridX;
            public int gridY;
            public bool isEquipSlot;
            public EquipSlot equipSlot;
        }

        // 持久白框
        private GameObject _selBorderGo;
        private RectTransform _selBorderRt;
        private RectTransform _selBorderCanvasRt; // border 所属 Canvas 的 RectTransform（用于坐标转换）
        private Image[] _selStrips;

        private void Awake()
        {
            if (!UIModeConfig.UseUGUI) { enabled = false; return; }
            Instance = this;
            _inventory = ServiceLocator.Get<Inv>();
            CreateSelectionBorder();
        }

        void CreateSelectionBorder()
        {
            var cGo = new GameObject("DDM_BorderCanvas", typeof(Canvas));
            cGo.transform.SetParent(null, false); // 直接挂场景根，避免父节点偏移
            var crt = cGo.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.sizeDelta = Vector2.zero; crt.anchoredPosition = Vector2.zero;
            var canvas = cGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            _selBorderCanvasRt = crt;
            _selBorderGo = new GameObject("__SelBorder__", typeof(RectTransform));
            _selBorderGo.transform.SetParent(canvas.transform, false);
            _selBorderGo.SetActive(false);
            _selStrips = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var s = new GameObject($"s{i}", typeof(Image));
                s.transform.SetParent(_selBorderGo.transform, false);
                _selStrips[i] = s.GetComponent<Image>();
                _selStrips[i].color = Color.red;
                _selStrips[i].raycastTarget = false;
            }
            _selBorderRt = _selBorderGo.GetComponent<RectTransform>();
            _selBorderRt.anchorMin = _selBorderRt.anchorMax = new Vector2(0, 1);
            _selBorderRt.pivot = new Vector2(0, 1);
        }

        void ShowSelectionBorder(RectTransform cellRect, float w, float h)
        {
            if (cellRect == null || _selBorderGo == null) return;
            Vector3[] corners = new Vector3[4];
            cellRect.GetWorldCorners(corners);
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _selBorderCanvasRt, screenPos, null, out local);
            _selBorderRt.anchoredPosition = local;
            _selBorderRt.sizeDelta = new Vector2(w, h);
            int bw = 3;
            SetStrip(_selStrips[0], 0, 0, w, bw);
            SetStrip(_selStrips[1], 0, h - bw, w, bw);
            SetStrip(_selStrips[2], 0, 0, bw, h);
            SetStrip(_selStrips[3], w - bw, 0, bw, h);
            _selBorderGo.SetActive(true);
        }

        void HideSelectionBorder() { if (_selBorderGo != null) _selBorderGo.SetActive(false); }

        void SetStrip(Image img, float x, float y, float w, float h)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        void OnEnable() { InputRouter.BindKey(KeyCode.T, InputPriority.UI, HandleRotate, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }

        bool HandleRotate()
        {
            if (_isDragging && _dragItem.HasValue)
                ToggleDragRotation();
            else if (_pointerDown && _pendingItem.HasValue)
                RotateInPlace();
            else if (SelectedItem.HasValue && SelectedContainer != null)
                RotateSelectedItem();
            else
                return false;
            return true;
        }

        private void Update()
        {
            // 兜底：鼠标已松开但状态未清
            if ((_pointerDown || _isDragging) && !Input.GetMouseButton(0))
            {
                if (_isDragging) EndDrag(Input.mousePosition);
                ResetState();
            }

            if (_pointerDown && !_isDragging)
            {
                float dist = Vector2.Distance(Input.mousePosition, _downPos);
                if (dist >= DRAG_THRESHOLD)
                    BeginDrag();
            }

            if (_isDragging && _dragVisual != null)
                _dragVisual.transform.position = Input.mousePosition;
        }

        void LateUpdate()
        {
            // 点击空白处取消选中
            if (Input.GetMouseButtonDown(0) && SelectedItem.HasValue && !_clickConsumed)
                DeselectItem();
            _clickConsumed = false;
        }

        // ===== 格子注册 =====

        public void RegisterCellRect(RectTransform rt, InventoryContainer container, int gridX, int gridY)
        {
            RegisterCellRect(rt, container, gridX, gridY, false);
        }

        public void RegisterEquipSlot(RectTransform rt, InventoryContainer container, int gridX, int gridY, EquipSlot slot)
        {
            if (rt == null) return;
            _cellRegions.Add(new CellRegion
            {
                rectTransform = rt,
                container = container,
                gridX = gridX,
                gridY = gridY,
                isEquipSlot = true,
                equipSlot = slot
            });
        }

        void RegisterCellRect(RectTransform rt, InventoryContainer container, int gridX, int gridY, bool isEquipSlot)
        {
            if (rt == null) return;
            _cellRegions.Add(new CellRegion
            {
                rectTransform = rt,
                container = container,
                gridX = gridX,
                gridY = gridY,
                isEquipSlot = isEquipSlot
            });
            // 每个格子挂点击处理器（空格子也能接收点击）
            if (!isEquipSlot)
            {
                var img = rt.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = true;
                if (rt.GetComponent<CellClickHandler>() == null)
                {
                    var h = rt.gameObject.AddComponent<CellClickHandler>();
                    h.container = container; h.gridX = gridX; h.gridY = gridY;
                }
            }
        }

        /// <summary> 由 CellClickHandler 回调——点击移动 </summary>
        public void HandleCellClick(InventoryContainer container, int gridX, int gridY)
        {
            if (!SelectedItem.HasValue || SelectedContainer == null) return;
            if (_isDragging) return;
            var item = SelectedItem.Value;
            if (container == SelectedContainer && gridX == SelectedCellX && gridY == SelectedCellY) return;

            if (container == SelectedContainer)
            {
                bool free = container.IsSpaceFreeFor(gridX, gridY, item.GridWidth, item.GridHeight,
                    item.gridX, item.gridY, item.GridWidth, item.GridHeight);
                Debug.Log($"[DDM] ClickMove same src=({item.gridX},{item.gridY}) dst=({gridX},{gridY}) {item.GridWidth}x{item.GridHeight} free={free}");
                if (free)
                {
                    container.MoveItem(item.gridX, item.gridY, gridX, gridY, item.rotated);
                    DeselectItem();
                    EventBus.Publish(new InventoryChanged("moved", item.itemData.itemName, item.count));
                }
            }
            else
            {
                bool free = container.IsSpaceFree(gridX, gridY, item.GridWidth, item.GridHeight);
                Debug.Log($"[DDM] ClickMove cross dst=({gridX},{gridY}) free={free}");
                if (free)
                {
                    SelectedContainer.RemoveItemAt(item.gridX, item.gridY, item.count);
                    container.placedItems.Add(PlacedItem.CloneWithPosition(item, gridX, gridY, item.rotated));
                    DeselectItem();
                    EventBus.Publish(new InventoryChanged("moved", item.itemData.itemName, item.count));
                }
            }
        }

        public void ClearCells()
        {
            HideSelectionBorder();
            _cellRegions.Clear();
        }

        // ===== ItemDragHandler 回调 =====

        public void OnPointerDown(PlacedItem item, InventoryContainer source, int cellX, int cellY, RectTransform overlayRt = null)
        {
            if (_isDragging) CancelDrag();

            _clickConsumed = true;
            _pointerDown = true;
            _pendingItem = item;
            _pendingSource = source;
            _downPos = Input.mousePosition;
            _clickCellX = cellX;
            _clickCellY = cellY;

            // 记录点击前是否已选中（用于 OnPointerUp 判断是否取消选中）
            bool wasAlreadySelected = SelectedItem.HasValue
                && SelectedContainer == source
                && SelectedItem.Value.gridX == cellX
                && SelectedItem.Value.gridY == cellY;

            SelectItem(item, source, cellX, cellY, overlayRt);

            // 如果点击前就已选中 → 取消选中；否则保持选中
            if (wasAlreadySelected)
                DeselectItem();
        }

        public void OnPointerUp(Vector2 position)
        {
            if (_isDragging) EndDrag(position);
            ResetState();
        }

        // ===== 旋转 =====

        /// <summary> 原地旋转（拖拽开始前），在源容器内更新位置/朝向 </summary>
        void RotateInPlace()
        {
            if (!_pendingItem.HasValue || _pendingItem.Value.itemData == null) return;
            var item = _pendingItem.Value;

            bool newRotated = !item.rotated;
            int newW = newRotated ? item.OriginalHeight : item.OriginalWidth;
            int newH = newRotated ? item.OriginalWidth : item.OriginalHeight;

            // 旋转公式：(rx, ry) → (ry, rx)，原点不动
            int curW = item.GridWidth;
            int curH = item.GridHeight;
            int rx = _clickCellX - item.gridX;
            int ry = _clickCellY - item.gridY;
            int newGridX = _clickCellX - ry;
            int newGridY = _clickCellY - rx;

            bool fits = _pendingSource.IsSpaceFreeFor(newGridX, newGridY, newW, newH, item.gridX, item.gridY, curW, curH);

            if (fits)
            {
                _pendingSource.RemoveItemAt(item.gridX, item.gridY, item.count);
                var rotated = PlacedItem.CloneWithPosition(item, newGridX, newGridY, newRotated);
                _pendingSource.placedItems.Add(rotated);
                _pendingItem = rotated;
                SelectedItem = rotated;
                EventBus.Publish(new InventoryChanged("rotated", item.itemData.itemName, item.count));
            }
        }

        /// <summary> 拖拽中旋转 — 只 toggle 标记，落点时校验 </summary>
        void ToggleDragRotation()
        {
            if (!_dragItem.HasValue) return;
            _dragRotated = !_dragRotated;
            var item = _dragItem.Value;
            int newW = _dragRotated ? item.OriginalHeight : item.OriginalWidth;
            int newH = _dragRotated ? item.OriginalWidth : item.OriginalHeight;

            if (_dragVisual != null)
            {
                var rt = _dragVisual.GetComponent<RectTransform>();
                float iconSize = 22f * Mathf.Max(newW, newH);
                rt.sizeDelta = new Vector2(iconSize, iconSize);
            }
        }

        // ===== 选中状态 =====

        RectTransform FindCellRect(InventoryContainer container, int gx, int gy)
        {
            foreach (var cr in _cellRegions)
                if (cr.container == container && cr.gridX == gx && cr.gridY == gy && cr.rectTransform != null)
                    return cr.rectTransform;
            return null;
        }

        void SelectItem(PlacedItem item, InventoryContainer container, int cellX, int cellY, RectTransform overlayRt = null)
        {
            SelectedItem = item;
            SelectedContainer = container;
            SelectedCellX = cellX;
            SelectedCellY = cellY;
            var cellRt = overlayRt ?? FindCellRect(container, cellX, cellY);
            Debug.Log($"[DDM] SelectItem id={item.instanceId} cellRt={cellRt != null}");
            if (cellRt != null)
                ShowSelectionBorder(cellRt, cellRt.sizeDelta.x, cellRt.sizeDelta.y);
        }

        public void DeselectItem()
        {
            HideSelectionBorder();
            SelectedItem = null;
            SelectedContainer = null;
        }

        /// <summary> 供 InventoryUI 在重建完UI后调用，恢复选中白框 </summary>
        public void RefreshSelectionBorder()
        {
            if (!SelectedItem.HasValue || SelectedContainer == null) return;
            var cellRt = FindCellRect(SelectedContainer, SelectedCellX, SelectedCellY);
            if (cellRt != null)
                ShowSelectionBorder(cellRt, cellRt.sizeDelta.x, cellRt.sizeDelta.y);
        }

        /// <summary> 选中状态下旋转物品 </summary>
        void RotateSelectedItem()
        {
            if (!SelectedItem.HasValue || SelectedContainer == null) return;
            var item = SelectedItem.Value;

            bool newRotated = !item.rotated;
            int newW = newRotated ? item.OriginalHeight : item.OriginalWidth;
            int newH = newRotated ? item.OriginalWidth : item.OriginalHeight;

            int curW = item.GridWidth;
            int curH = item.GridHeight;
            int rx = SelectedCellX - item.gridX;
            int ry = SelectedCellY - item.gridY;
            int newGridX = SelectedCellX - ry;
            int newGridY = SelectedCellY - rx;

            bool fits = SelectedContainer.IsSpaceFreeFor(newGridX, newGridY, newW, newH, item.gridX, item.gridY, curW, curH);

            if (fits)
            {
                SelectedContainer.RemoveItemAt(item.gridX, item.gridY, item.count);
                var rotated = PlacedItem.CloneWithPosition(item, newGridX, newGridY, newRotated);
                SelectedContainer.placedItems.Add(rotated);
                SelectedItem = rotated;
                EventBus.Publish(new InventoryChanged("rotated", item.itemData.itemName, item.count));
            }
        }

        // ===== 拖拽生命周期 =====

        private void BeginDrag()
        {
            if (!_pendingItem.HasValue || _pendingItem.Value.itemData == null) return;

            // 开始拖拽时清除选中
            if (SelectedItem.HasValue)
                DeselectItem();

            _isDragging = true;
            var pd = _pendingItem.Value;
            _dragItem = pd;
            _dragSource = _pendingSource;
            _dragRotated = pd.rotated;

            int visW = _dragRotated ? pd.OriginalHeight : pd.OriginalWidth;
            int visH = _dragRotated ? pd.OriginalWidth : pd.OriginalHeight;

            _dragVisual = new GameObject("DragIcon", typeof(Image));
            _dragVisual.transform.SetParent(transform, false);
            _dragVisual.transform.SetAsLastSibling();

            var img = _dragVisual.GetComponent<Image>();
            if (pd.itemData.icon != null)
                img.sprite = pd.itemData.icon;
            img.color = new Color(1, 1, 1, 0.7f);
            img.preserveAspect = true;
            img.raycastTarget = false;

            var rt = _dragVisual.GetComponent<RectTransform>();
            float iconSize = 22f * Mathf.Max(visW, visH);
            rt.sizeDelta = new Vector2(iconSize, iconSize);
            rt.position = Input.mousePosition;
        }

        private void EndDrag(Vector2 screenPos)
        {
            if (!_dragItem.HasValue || _dragItem.Value.itemData == null)
            {
                CancelDrag();
                return;
            }

            var target = FindCellAtPosition(screenPos);
            if (target.HasValue)
                TryMoveToContainer(target.Value);
            else
                TryEquipFromDirectDrop(screenPos);

            CancelDrag();
        }

        /// <summary> 松手在装备槽上时直接触发装备 </summary>
        void TryEquipFromDirectDrop(Vector2 screenPos)
        {
            if (_inventory == null || !_dragItem.HasValue) return;
            var itemData = _dragItem.Value.itemData;
            if (itemData == null || itemData.equipSlot == EquipSlot.None) return;

            var ui = ServiceLocator.Get<InventoryUI>();
            if (ui == null) return;
            var canvas = ui.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            if (Inv.IsWeaponSlot(itemData.equipSlot))
            {
                var weaponSlots = new[] { EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.KnifeBelt, EquipSlot.SidearmBelt };
                foreach (var ws in weaponSlots)
                {
                    string slotName = ws.ToString() + "_slot";
                    var slotRt = FindEquipSlotRecursive(canvas.transform, slotName);
                    if (slotRt != null && RectTransformUtility.RectangleContainsScreenPoint(slotRt, screenPos, canvas.worldCamera))
                    {
                        if (_inventory.EquipItem(itemData, ws, _dragItem.Value.instanceId))
                        {
                            _dragSource.RemoveItemAt(_dragItem.Value.gridX, _dragItem.Value.gridY, _dragItem.Value.count);
                            return;
                        }
                        CancelDrag();
                        return;
                    }
                }
            }

            string itemSlotName = itemData.equipSlot.ToString() + "_slot";
            var itemSlotRt = FindEquipSlotRecursive(canvas.transform, itemSlotName);
            if (itemSlotRt != null && RectTransformUtility.RectangleContainsScreenPoint(itemSlotRt, screenPos, canvas.worldCamera))
            {
                if (_inventory.EquipItem(itemData, _dragItem.Value.instanceId))
                {
                    _dragSource.RemoveItemAt(_dragItem.Value.gridX, _dragItem.Value.gridY, _dragItem.Value.count);
                    return;
                }
            }
        }

        RectTransform FindEquipSlotRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child as RectTransform;
                var found = FindEquipSlotRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        public void CancelDrag()
        {
            _isDragging = false;
            _dragRotated = false;
            if (_dragVisual != null)
            {
                Destroy(_dragVisual);
                _dragVisual = null;
            }
            ResetState();
        }

        private void ResetState()
        {
            _pointerDown = false;
            _pendingItem = null;
            _pendingSource = null;
            if (!_isDragging)
            {
                _dragItem = null;
                _dragSource = null;
            }
        }

        // ===== 坐标检测 =====

        private CellRegion? FindCellAtPosition(Vector2 screenPos)
        {
            for (int i = _cellRegions.Count - 1; i >= 0; i--)
            {
                var cr = _cellRegions[i];
                if (cr.rectTransform == null) continue;

                var canvas = cr.rectTransform.GetComponentInParent<Canvas>();
                if (canvas == null) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(cr.rectTransform, screenPos, canvas.worldCamera))
                {
                    return cr;
                }
            }
            return null;
        }

        // ===== 放置逻辑 =====

        /// <summary> 根据拖拽偏移和旋转状态计算放置位置 </summary>
        void ComputeDropPosition(CellRegion cr, out int dropX, out int dropY)
        {
            var pd = _dragItem.Value;
            int origRx = _clickCellX - pd.gridX;
            int origRy = _clickCellY - pd.gridY;

            if (_dragRotated != pd.rotated)
            {
                // 旋转被切换：(rx, ry) → (ry, rx)，原点不动
                dropX = cr.gridX - origRy;
                dropY = cr.gridY - origRx;
            }
            else
            {
                dropX = cr.gridX - origRx;
                dropY = cr.gridY - origRy;
            }
        }

        private void TryMoveToContainer(CellRegion cr)
        {
            if (_inventory == null || !_dragItem.HasValue || _dragItem.Value.itemData == null) return;

            var pd = _dragItem.Value;
            var itemData = pd.itemData;
            int count = pd.count;
            var target = cr.container;
            ComputeDropPosition(cr, out int gridX, out int gridY);

            int itemW = _dragRotated ? pd.OriginalHeight : pd.OriginalWidth;
            int itemH = _dragRotated ? pd.OriginalWidth : pd.OriginalHeight;

            if (target == _dragSource && gridX == pd.gridX && gridY == pd.gridY && _dragRotated == pd.rotated)
                return;

            // 装备槽格子触发自动装备
            if (itemData.equipSlot != EquipSlot.None && cr.isEquipSlot)
            {
                bool matchEquip = (target != null && target.equipSlot == itemData.equipSlot)
                               || (target == null && Inv.IsWeaponSlot(itemData.equipSlot));
                if (matchEquip)
                {
                    if (_inventory.EquipItem(itemData, cr.equipSlot, _dragItem.Value.instanceId))
                    {
                        _dragSource.RemoveItemAt(pd.gridX, pd.gridY, count);
                        return;
                    }
                    CancelDrag();
                    return;
                }
            }

            if (itemData.equipSlot != EquipSlot.None)
            {
                if (target != null && target.equipSlot == itemData.equipSlot)
                {
                    ShowToast("请在左边装备栏穿戴");
                    return;
                }
            }

            if (target.IsSpaceFree(gridX, gridY, itemW, itemH))
            {
                _dragSource.RemoveItemAt(pd.gridX, pd.gridY, count);
                var placed = PlacedItem.CloneWithPosition(pd, gridX, gridY, _dragRotated);
                target.placedItems.Add(placed);
                EventBus.Publish(new InventoryChanged("moved", itemData.itemName, count));
                return;
            }

            if (target == _dragSource)
            {
                _dragSource.RemoveItemAt(pd.gridX, pd.gridY, count);
                bool free = target.IsSpaceFree(gridX, gridY, itemW, itemH);
                if (free)
                {
                    var placed = PlacedItem.CloneWithPosition(pd, gridX, gridY, _dragRotated);
                    target.placedItems.Add(placed);
                    EventBus.Publish(new InventoryChanged("moved", itemData.itemName, count));
                    return;
                }
                var back = PlacedItem.CloneWithPosition(pd, pd.gridX, pd.gridY, pd.rotated);
                _dragSource.placedItems.Add(back);
            }

            ShowToast("放不下！");
        }

        private void ShowToast(string msg)
        {
            var ui = ServiceLocator.Get<InventoryUI>();
            if (ui != null) ui.ShowToast(msg);
        }
    }

    /// <summary> 格子点击处理器 — 空格子也能接收点击，驱动点击移动 </summary>
    public class CellClickHandler : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler
    {
        public InventoryContainer container;
        public int gridX;
        public int gridY;

        void Awake()
        {
            var img = GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.raycastTarget = true;
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Debug.Log($"[CellClick] grid=({gridX},{gridY}) btn={eventData.button}");
            if (eventData.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left) return;
            DragDropManager.Instance?.HandleCellClick(container, gridX, gridY);
        }
    }
}
