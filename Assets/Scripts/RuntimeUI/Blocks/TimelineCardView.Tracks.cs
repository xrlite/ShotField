using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 1~5 层时间轴架构扩展：机位、演员、道具/载具、音频、灯光分层与空间探针状态绑定。
    /// </summary>
    public sealed partial class TimelineCardView
    {
        public static readonly string[] LayerNames = { "机位", "演员", "道具/载具", "音频", "灯光" };
        public static readonly Color[] LayerColors =
        {
            new Color(0.20f, 0.42f, 0.56f, 0.96f), // Layer 0: 机位 (蓝灰)
            new Color(0.38f, 0.29f, 0.48f, 0.96f), // Layer 1: 演员 (紫)
            new Color(0.25f, 0.45f, 0.35f, 0.96f), // Layer 2: 道具/载具 (绿)
            new Color(0.55f, 0.38f, 0.22f, 0.96f), // Layer 3: 音频 (橙褐)
            new Color(0.52f, 0.48f, 0.22f, 0.96f)  // Layer 4: 灯光 (金黄)
        };

        private readonly RectTransform[] _layerTracks = new RectTransform[5];
        private TimelineTrackLayer _activeLayer = TimelineTrackLayer.Camera;

        public TimelineTrackLayer ActiveLayer => _activeLayer;

        public void SelectTrackLayer(TimelineTrackLayer layer)
        {
            _activeLayer = layer;
            Note($"切换轨道: {LayerNames[(int)layer]}");
        }

        public bool BindSpatialProbe(int clipIndex, TimelineTrackLayer layer, string targetId, Vector3 worldPos)
        {
            if (Model == null || clipIndex < 0 || clipIndex >= Model.Count) return false;
            var clip = Model.clips[clipIndex];
            if (clip == null) return false;
            clip.BindProbe(layer, targetId, worldPos, true);
            Commit($"探针绑定: {LayerNames[(int)layer]} -> {targetId}");
            return true;
        }

        public bool TryGetSpatialProbe(int clipIndex, TimelineTrackLayer layer, out SpatialProbeBindingDto probe)
        {
            probe = null;
            if (Model == null || clipIndex < 0 || clipIndex >= Model.Count) return false;
            return Model.clips[clipIndex].TryGetProbe(layer, out probe);
        }

        public void SyncSpatialProbesFromContext(SceneContextDto context)
        {
            if (context == null || Model == null) return;
            for (int i = 0; i < Model.Count; i++)
            {
                var clip = Model.clips[i];
                if (clip == null) continue;
                if (!string.IsNullOrEmpty(clip.actorId))
                {
                    var actorObj = context.objects?.Find(o => o.objectId == clip.actorId || o.displayName == clip.actorId);
                    if (actorObj != null) clip.BindProbe(TimelineTrackLayer.Actor, clip.actorId, actorObj.position.ToUnityVector3(), true);
                }
                var camObj = context.objects?.Find(o => o.objectId == SceneContextCapture.DirectorCameraId);
                if (camObj != null) clip.BindProbe(TimelineTrackLayer.Camera, SceneContextCapture.DirectorCameraId, camObj.position.ToUnityVector3(), true);
            }
        }

        public string GetTrackLayerSummary(TimelineTrackLayer layer)
        {
            int boundCount = 0;
            if (Model != null)
                for (int i = 0; i < Model.Count; i++)
                    if (Model.clips[i].TryGetProbe(layer, out _)) boundCount++;
            return $"{LayerNames[(int)layer]}轨 (绑定探针: {boundCount}/{ClipCount})";
        }

        private void BuildLayerTracks(Transform root)
        {
            _layerTracks[0] = _track;
            _layerTracks[1] = _actorTrack;
            // 轨道层级指示标签与状态
            Label(root, "LayersHeader", "1~5层时间轴", new Vector2(-255f, 132f), new Vector2(100f, 32f), 13, new Color(0.65f, 0.68f, 0.75f));
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
                var rect = Panel(_track, "Clip_" + i, new Vector2(x, 0f), new Vector2(width, 48f), LayerColors[0]);
                _clipRects.Add((RectTransform)rect.transform);
                rect.gameObject.AddComponent<TimelineClipDrag>().Bind(this, i);
                Label(rect.transform, "Shot", string.IsNullOrEmpty(clip.shotId) ? "Clip" : clip.shotId, Vector2.zero, new Vector2(Mathf.Max(1f, width - 14f), 28f), 14);
                var trim = Panel(rect.transform, "Trim", new Vector2(-4f, 0f), new Vector2(12f, 48f), new Color(0.95f, 0.64f, 0.24f, 0.98f));
                trim.gameObject.AddComponent<TimelineTrimHandle>().Bind(this, i);
                var actor = Panel(_actorTrack, "ActorBlock_" + i, new Vector2(x, 0f), new Vector2(width, 48f), LayerColors[1]);
                Label(actor.transform, "Actor", string.IsNullOrEmpty(clip.actorId) ? "演员" : clip.actorId.Replace("SC01_Performer_", "演员 "), Vector2.zero, new Vector2(Mathf.Max(1f, width - 14f), 28f), 13);
                start += duration;
            }
        }
    }
}
