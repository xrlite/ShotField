using System;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 剪映式时间轴播放衔接层：把 <see cref="TimelineChainModel"/> 的分段
    /// 接到既有 <see cref="DirectorTimeline"/> 的播放 / 寻道公开 API 上。
    /// 本文件为新增衔接层，不改 DirectorTimeline 本体；跨段时发布 OnClipChanged。
    /// </summary>
    public class ChainTimelineController : MonoBehaviour
    {
        public TimelineChainModel model;
        public DirectorTimeline timeline;

        private int _currentClip = -1;

        public int CurrentClipIndex => _currentClip;

        /// <summary>当前播放段发生切换时触发，参数为新段下标。</summary>
        public event Action<int> OnClipChanged;

        void Start() => Wire();

        public void Wire()
        {
            if (timeline == null) timeline = FindAnyObjectByType<DirectorTimeline>();
        }

        /// <summary>绑定分段模型，同步总时长并跳到首段。</summary>
        public void Bind(TimelineChainModel chain)
        {
            model = chain;
            _currentClip = -1;
            Wire();
            if (timeline != null && model != null) timeline.SetDuration(model.TotalDuration);
            if (model != null && model.Count > 0) JumpToClip(0);
        }

        public void Play() { Wire(); timeline?.Play(); }

        public void Pause() => timeline?.Pause();

        public void TogglePlay() { Wire(); timeline?.TogglePlay(); }

        public void Stop()
        {
            timeline?.Stop();
            _currentClip = -1;
        }

        /// <summary>跳到指定段开头；越界自动钳制。</summary>
        public void JumpToClip(int index)
        {
            if (model == null || model.Count == 0 || timeline == null) return;
            index = Mathf.Clamp(index, 0, model.Count - 1);
            timeline.Seek(model.StartTime(index));
            if (_currentClip != index)
            {
                _currentClip = index;
                OnClipChanged?.Invoke(index);
            }
        }

        void Update()
        {
            if (timeline == null || model == null || model.Count == 0) return;
            if (!timeline.isPlaying) return;

            int idx = model.ClipAt(timeline.currentTime, out _);
            if (idx != _currentClip)
            {
                _currentClip = idx;
                OnClipChanged?.Invoke(idx);
            }
        }
    }
}
