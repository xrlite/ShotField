using UnityEngine;
using UnityEngine.EventSystems;

namespace ShotField.RuntimeUI
{
    /// <summary>镜头 Clip 的指针拖动适配器；排序完全委托给 TimelineCardView。</summary>
    public sealed class TimelineClipDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private TimelineCardView _view;
        private int _index;
        private int _dropIndex;
        private bool _dragging;

        public void Bind(TimelineCardView view, int index)
        {
            _view = view;
            _index = index;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = false;
            if (!CanDrag(eventData)) return;
            _dropIndex = _index;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !CanDrag(eventData)) return;
            _dropIndex = _view.GetDropIndex(_index, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            if (CanDrag(eventData)) _view.ReorderClip(_index, _dropIndex);
            _dragging = false;
        }

        private bool CanDrag(PointerEventData eventData)
        {
            if (_view == null || eventData == null || eventData.pointerPress != gameObject) return false;
            if (EventSystem.current == null)
            {
                if (Application.isPlaying) Debug.LogWarning("[TimelineCardView] Drag unavailable: EventSystem missing", this);
                return false;
            }
            return true;
        }

        private void OnDisable() => _dragging = false;
    }
}
