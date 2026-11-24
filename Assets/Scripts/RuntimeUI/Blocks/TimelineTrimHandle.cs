using UnityEngine;
using UnityEngine.EventSystems;

namespace ShotField.RuntimeUI
{
    /// <summary>Clip 右缘手柄：把指针位移换算为时长，松手提交真实裁切。</summary>
    public sealed class TimelineTrimHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private TimelineCardView _view;
        private int _index;
        private float _originalDuration;
        private float _candidateDuration;
        private bool _dragging;

        public void Bind(TimelineCardView view, int index)
        {
            _view = view;
            _index = index;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = false;
            if (!CanTrim(eventData) || _view.Model?.clips == null || _index < 0 || _index >= _view.ClipCount) return;
            _originalDuration = _view.Model.clips[_index].durationSeconds;
            _candidateDuration = _originalDuration;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !CanTrim(eventData)) return;
            _candidateDuration = _view.GetTrimDuration(_index, _originalDuration, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            if (CanTrim(eventData)) _view.TrimClip(_index, _candidateDuration);
            _dragging = false;
        }

        private bool CanTrim(PointerEventData eventData)
        {
            if (_view == null || eventData == null || eventData.pointerPress != gameObject) return false;
            if (EventSystem.current == null)
            {
                if (Application.isPlaying) Debug.LogWarning("[TimelineCardView] Trim unavailable: EventSystem missing", this);
                return false;
            }
            return true;
        }

        private void OnDisable() => _dragging = false;
    }
}
