using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using _Game.Systems.Inventory;

namespace _Game.UI
{
    /// <summary>
    /// 物品拖拽处理器
    /// 只负责接收 Pointer 事件并上报 DragDropManager
    /// </summary>
    public class ItemDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private PlacedItem _item;
        private InventoryContainer _container;
        private int _cellX;
        private int _cellY;
        private RectTransform _overlayRt;

        public void Setup(PlacedItem item, InventoryContainer container, int cellX, int cellY, RectTransform overlayRt = null)
        {
            _item = item;
            _container = container;
            _cellX = cellX;
            _cellY = cellY;
            _overlayRt = overlayRt;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_item.itemData == null) return;

            var mgr = DragDropManager.Instance;
            if (mgr != null)
                mgr.OnPointerDown(_item, _container, _cellX, _cellY, _overlayRt);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            var mgr = DragDropManager.Instance;
            if (mgr != null)
                mgr.OnPointerUp(eventData.position);
        }
    }
}
