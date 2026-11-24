using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>空间时间轴卡：真实分段模型、双轨内容、播放控制与 Edit Mode 编辑接口。</summary>
    [ExecuteAlways]
    public sealed class TimelineCardView : MonoBehaviour
    {
        private const float TrackWidth = 510f;
        private const float TrackCenter = 35f;
        private SpatialBlockCard _card;
        private DirectorTimeline _timeline;
        private ChainTimelineController _controller;
        private RectTransform _track, _actorTrack;
        private Image _playhead;
        private TMPro.TMP_Text _noteLabel;
        private readonly List<RectTransform> _clipRects = new List<RectTransform>();

        public TimelineChainModel Model { get; private set; }
        public int ClipCount => Model?.Count ?? 0;
        public int ReorderCount { get; private set; }
        public int TrimCount { get; private set; }
        public float NormalizedTime { get; private set; }
        public int TrajectoryPointCount => _timeline?.cameraRecorder?.ActiveTrajectory?.Count ?? 0;
        public string LastNote { get; private set; } = "Not bound";

        public void Bind(SpatialBlockCard card, DirectorTimeline timeline, ChainTimelineController controller)
        {
            Unsubscribe();
            _card = card; _timeline = timeline; _controller = controller;
            NormalizedTime = timeline != null ? timeline.NormalizedTime : 0f;
            Model = controller?.model;
            LastNote = Model != null && Model.Count >= 3 ? "Using controller model" : "Created default 7-clip chain";
            if (Model == null || Model.Count < 3) Model = CreateDefaultChain();
            if (controller != null) { controller.timeline = timeline; controller.Bind(Model); }
            BuildContent();
            Subscribe();
        }

        private static TimelineChainModel CreateDefaultChain()
        {
            var chain = new TimelineChainModel();
            for (int i = 0; i < DirectorTimeline.ShotNames.Length; i++)
            {
                int end = Mathf.Min(i + 1, DirectorTimeline.ShotCutTimes.Length - 1);
                string actor = i % 2 == 0 ? "SC01_Performer_A" : "SC01_Performer_B";
                chain.Add(new TimelineClipDto
                {
                    clipId = "clip_" + DirectorTimeline.ShotNames[i], shotId = DirectorTimeline.ShotNames[i],
                    cameraPresetId = i, actorId = actor, actionTag = actor,
                    durationSeconds = Mathf.Max(0.1f, DirectorTimeline.ShotCutTimes[end] - DirectorTimeline.ShotCutTimes[i]),
                    intent = "Timeline block " + (i + 1)
                });
            }
            return chain;
        }

        private void BuildContent()
        {
            if (_card?.ContentRoot == null) { LastNote = "Card content missing"; return; }
            var root = _card.ContentRoot;
            for (int i = root.childCount - 1; i >= 0; i--) Remove(root.GetChild(i).gameObject);
            Button(root, "Play", "播放", -180f, Play); Button(root, "Pause", "暂停", 0f, Pause); Button(root, "Stop", "停止", 180f, Stop);
            Label(root, "CameraLabel", "镜头", new Vector2(-260f, 20f), new Vector2(62f, 56f), 18);
            Label(root, "ActorLabel", "演员", new Vector2(-260f, -70f), new Vector2(62f, 56f), 18);
            BuildRuler(root);
            _track = Panel(root, "ClipTrack", new Vector2(TrackCenter, 20f), new Vector2(TrackWidth, 56f), new Color(0.07f, 0.08f, 0.10f, 0.95f));
            _actorTrack = Panel(root, "ActorTrack", new Vector2(TrackCenter, -70f), new Vector2(TrackWidth, 56f), new Color(0.07f, 0.08f, 0.10f, 0.95f));
            RebuildClips();
            _playhead = Panel(root, "Playhead", Vector2.zero, new Vector2(3f, 130f), new Color(0.95f, 0.76f, 0.25f)).GetComponent<Image>();
            _playhead.raycastTarget = false;
            _noteLabel = Label(root, "TimelineNote", LastNote, new Vector2(0f, -145f), new Vector2(550f, 32f), 16, new Color(0.72f, 0.74f, 0.78f));
            RefreshPlayhead();
        }

        private void BuildRuler(Transform root)
        {
            float total = Model?.TotalDuration ?? 60f;
            for (int i = 0; i <= 4; i++)
            {
                float x = TrackCenter - TrackWidth * 0.5f + TrackWidth * i / 4f;
                Panel(root, "Tick", new Vector2(x, 87f), new Vector2(2f, 10f), new Color(0.5f, 0.52f, 0.56f));
                Label(root, "Time", (total * i / 4f).ToString("0.#"), new Vector2(x, 107f), new Vector2(60f, 22f), 14, new Color(0.7f, 0.72f, 0.76f));
            }
        }

        private void RebuildClips()
        {
            _clipRects.Clear();
            if (_track == null || Model == null) return;
            for (int i = _track.childCount - 1; i >= 0; i--) Remove(_track.GetChild(i).gameObject);
            for (int i = _actorTrack.childCount - 1; i >= 0; i--) Remove(_actorTrack.GetChild(i).gameObject);
            float total = Model.TotalDuration, start = 0f;
            for (int i = 0; i < Model.Count; i++)
            {
                var clip = Model.clips[i];
                float duration = Mathf.Max(0.1f, clip.durationSeconds), width = TrackWidth * duration / total;
                float x = -TrackWidth * 0.5f + start + width * 0.5f;
                var rect = Panel(_track, "Clip_" + i, new Vector2(x, 0f), new Vector2(width, 48f), new Color(0.20f, 0.42f, 0.56f, 0.96f));
                _clipRects.Add((RectTransform)rect.transform);
                rect.gameObject.AddComponent<TimelineClipDrag>().Bind(this, i);
                Label(rect.transform, "Shot", string.IsNullOrEmpty(clip.shotId) ? "Clip" : clip.shotId, Vector2.zero, new Vector2(Mathf.Max(1f, width - 14f), 28f), 14);
                var trim = Panel(rect.transform, "Trim", new Vector2(-4f, 0f), new Vector2(12f, 48f), new Color(0.95f, 0.64f, 0.24f, 0.98f));
                trim.gameObject.AddComponent<TimelineTrimHandle>().Bind(this, i);
                var actor = Panel(_actorTrack, "ActorBlock_" + i, new Vector2(x, 0f), new Vector2(width, 48f), new Color(0.38f, 0.29f, 0.48f, 0.96f));
                Label(actor.transform, "Actor", string.IsNullOrEmpty(clip.actorId) ? "演员" : clip.actorId.Replace("SC01_Performer_", "演员 "), Vector2.zero, new Vector2(Mathf.Max(1f, width - 14f), 28f), 13);
                start += duration;
            }
        }

        private void Button(Transform parent, string name, string text, float x, Action action) =>
            RuntimeUiFactory.CreateTmpButton(parent, name, text, new Vector2(x, 132f), new Vector2(100f, 38f), onClick: action.Invoke);

        private static TMPro.TMP_Text Label(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, Color? color = null) =>
            RuntimeUiFactory.CreateTmpLabel(parent, name, text, fontSize, pos, size, color ?? Color.white);

        private static RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var panel = RuntimeUiFactory.CreatePanel(parent, name, color, pos, size);
            panel.GetComponent<Image>().raycastTarget = true;
            return (RectTransform)panel.transform;
        }

        public bool ReorderClip(int from, int to)
        {
            if (Model?.clips == null || from < 0 || to < 0 || from >= Model.Count || to >= Model.Count || from == to) return Note("Reorder ignored");
            var clip = Model.clips[from];
            if (clip == null) return Note("Reorder ignored: null clip");
            Model.clips.RemoveAt(from); Model.clips.Insert(to, clip); ReorderCount++;
            return Commit("Reordered " + from + " -> " + to);
        }

        public bool TrimClip(int index, float newDuration)
        {
            if (Model?.clips == null || index < 0 || index >= Model.Count || Model.clips[index] == null || float.IsNaN(newDuration) || float.IsInfinity(newDuration)) return Note("Trim ignored");
            Model.clips[index].durationSeconds = Mathf.Max(0.1f, newDuration); TrimCount++;
            return Commit("Trimmed " + index + " to " + Model.clips[index].durationSeconds.ToString("0.##") + "s");
        }

        private bool Commit(string note)
        {
            if (_controller != null) _controller.Bind(Model);
            BuildContent(); // Also refreshes ruler labels and preserves the unchanged camera trajectory.
            Note(note);
            return true;
        }

        private bool Note(string note) { LastNote = note; if (_noteLabel != null) _noteLabel.text = note; return false; }

        public int GetDropIndex(int from, PointerEventData data)
        {
            if (_track == null || data == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, data.position, ResolveCamera(data), out Vector2 point)) return from;
            int nearest = from; float distance = float.MaxValue;
            for (int i = 0; i < _clipRects.Count; i++)
            {
                float d = Mathf.Abs(_clipRects[i].anchoredPosition.x - point.x);
                if (d < distance) { distance = d; nearest = i; }
            }
            return nearest;
        }

        public float GetTrimDuration(int index, float originalDuration, PointerEventData data)
        {
            if (_track == null || data == null || index < 0 || index >= _clipRects.Count) return originalDuration;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, data.position, ResolveCamera(data), out Vector2 point)) return originalDuration;
            var rect = _clipRects[index];
            float edge = rect.anchoredPosition.x + rect.rect.width * 0.5f;
            float scale = Model != null && Model.TotalDuration > 0.001f ? TrackWidth / Model.TotalDuration : 1f;
            return Mathf.Max(0.1f, originalDuration + (point.x - edge) / Mathf.Max(0.001f, scale));
        }

        private Camera ResolveCamera(PointerEventData data) =>
            data.pressEventCamera ?? _card?.Canvas?.worldCamera ?? Camera.main;

        public void Play() { if (_timeline != null) _timeline.Play(); else _controller?.Play(); }
        public void Pause() { if (_timeline != null) _timeline.Pause(); else _controller?.Pause(); }
        public void Stop() { if (_timeline != null) _timeline.Stop(); else _controller?.Stop(); RefreshPlayhead(); }
        private void OnTimeUpdated(float value) { NormalizedTime = Mathf.Clamp01(value); RefreshPlayhead(); }
        private void RefreshPlayhead() { if (_playhead != null) ((RectTransform)_playhead.transform).anchoredPosition = new Vector2(TrackCenter - TrackWidth * 0.5f + TrackWidth * NormalizedTime, -25f); }
        private void Subscribe() { if (_timeline != null) _timeline.OnTimeUpdated += OnTimeUpdated; }
        private void Unsubscribe() { if (_timeline != null) _timeline.OnTimeUpdated -= OnTimeUpdated; }
        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private static void Remove(UnityEngine.Object target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
