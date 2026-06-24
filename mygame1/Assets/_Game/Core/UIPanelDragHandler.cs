using UnityEngine;
using UnityEngine.EventSystems;

namespace _Game.Core
{
    /// <summary>
    /// 面板拖拽 — 挂到标题栏上，拖动整个面板。
    /// </summary>
    public class UIPanelDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private RectTransform _panelRect;
        private Vector2 _offset;
        private bool _dragging;

        void Awake()
        {
            // 标题栏的父级 = 面板的 RectTransform
            _panelRect = transform.parent as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_panelRect == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelRect, eventData.position, eventData.pressEventCamera, out _offset);
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _panelRect == null) return;
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelRect.parent as RectTransform, eventData.position,
                eventData.pressEventCamera, out localPoint))
            {
                _panelRect.anchoredPosition = localPoint - _offset;
            }
        }

        public void OnPointerUp(PointerEventData eventData) => _dragging = false;
    }
}
