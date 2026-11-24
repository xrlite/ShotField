using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>1~5 层时间轴架构分层枚举：机位、演员、道具/载具、音频、灯光。</summary>
    public enum TimelineTrackLayer
    {
        Camera = 0,      // 第 1 层：机位
        Actor = 1,       // 第 2 层：演员
        PropVehicle = 2, // 第 3 层：道具/载具
        Audio = 3,       // 第 4 层：音频
        Light = 4        // 第 5 层：灯光
    }

    /// <summary>空间探针状态绑定骨架：时间切片下的空间坐标、探针置信度与绑定对象。</summary>
    [Serializable]
    public class SpatialProbeBindingDto
    {
        public TimelineTrackLayer layer = TimelineTrackLayer.Camera;
        public string targetId = "";
        public Vector3Dto position;
        public Vector3Dto rotationEuler;
        public bool isBound;
        public bool isOccluded;
        public float confidence = 1.0f;
        public string probeSummary = "";
    }

    /// <summary>
    /// 剪映式时间轴分段：一段 clip = 机位预设 + 时长 + 演员动作引用 + 空间探针绑定。
    /// 纯数据模型，不驱动播放；播放寻道由 RuntimeUI 层 ChainTimelineController 衔接 DirectorTimeline。
    /// </summary>
    [Serializable]
    public class TimelineClipDto
    {
        public string clipId = "";
        public string shotId = "";
        public int cameraPresetId = 0;
        public Vector3Dto anchor;
        public string actorId = "";
        public string actionTag = "";
        public float durationSeconds = 4.0f;
        public string intent = "";
        public TimelineTrackLayer primaryLayer = TimelineTrackLayer.Camera;
        public List<SpatialProbeBindingDto> probeBindings = new List<SpatialProbeBindingDto>();

        public void BindProbe(TimelineTrackLayer layer, string targetId, Vector3 pos, bool bound = true)
        {
            if (probeBindings == null) probeBindings = new List<SpatialProbeBindingDto>();
            var existing = probeBindings.Find(p => p.layer == layer);
            if (existing == null)
            {
                existing = new SpatialProbeBindingDto { layer = layer };
                probeBindings.Add(existing);
            }
            existing.targetId = targetId ?? "";
            existing.position = pos;
            existing.isBound = bound;
            existing.confidence = bound ? 1.0f : 0f;
            existing.probeSummary = $"{layer}:{existing.targetId}@{pos}";
        }

        public bool TryGetProbe(TimelineTrackLayer layer, out SpatialProbeBindingDto probe)
        {
            var found = probeBindings?.Find(p => p.layer == layer);
            if (found != null && found.isBound)
            {
                probe = found;
                return true;
            }
            probe = null;
            return false;
        }
    }

    /// <summary>
    /// clip 有序列表：拼接 / 排序 / 删段 / 按时间定位分段。
    /// 时间区间按顺序累计时长，不维护独立时间轴关键帧（机位轨迹仍归 CameraTrajectory）。
    /// </summary>
    [Serializable]
    public class TimelineChainModel
    {
        public List<TimelineClipDto> clips = new List<TimelineClipDto>();

        public int Count => clips != null ? clips.Count : 0;

        public float TotalDuration
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Count; i++) total += Mathf.Max(0.1f, clips[i].durationSeconds);
                return total;
            }
        }

        public void Add(TimelineClipDto clip)
        {
            if (clip != null) clips.Add(clip);
        }

        public bool RemoveAt(int index)
        {
            if (clips == null || index < 0 || index >= clips.Count) return false;
            clips.RemoveAt(index);
            return true;
        }

        /// <summary>上下移动排序；delta 为 -1 前移、+1 后移，越界返回 false。</summary>
        public bool Move(int index, int delta)
        {
            if (clips == null || index < 0 || index >= clips.Count) return false;
            int target = index + delta;
            if (target < 0 || target >= clips.Count) return false;
            var item = clips[index];
            clips.RemoveAt(index);
            clips.Insert(target, item);
            return true;
        }

        public void Clear() => clips.Clear();

        /// <summary>第 index 段的起始秒数。</summary>
        public float StartTime(int index)
        {
            float t = 0f;
            for (int i = 0; i < index && i < Count; i++) t += Mathf.Max(0.1f, clips[i].durationSeconds);
            return t;
        }

        /// <summary>按当前时间定位分段；越界返回最近端分段，localTime 钳制在本段内。</summary>
        public int ClipAt(float time, out float localTime)
        {
            localTime = 0f;
            if (Count == 0) return -1;
            for (int i = 0; i < Count; i++)
            {
                float dur = Mathf.Max(0.1f, clips[i].durationSeconds);
                if (time < StartTime(i) + dur)
                {
                    localTime = Mathf.Clamp(time - StartTime(i), 0f, dur);
                    return i;
                }
            }
            localTime = Mathf.Max(0.1f, clips[Count - 1].durationSeconds);
            return Count - 1;
        }

        /// <summary>
        /// 从已确认提案生成 clip 列表：接受的 Shot 条目成段，
        /// 同序号接受的 Actor 条目提供 actorId 与动作引用；拒绝条目不进入时间轴。
        /// </summary>
        public static TimelineChainModel FromProposal(ChainProposalDto proposal)
        {
            var model = new TimelineChainModel();
            if (proposal == null || proposal.items == null) return model;

            var acceptedShots = new List<ProposalItemDto>();
            var acceptedActors = new List<ProposalItemDto>();
            foreach (var item in proposal.items)
            {
                if (item == null || !item.IsAccepted) continue;
                if (item.kind == ProposalItemKind.Shot) acceptedShots.Add(item);
                else if (item.kind == ProposalItemKind.Actor) acceptedActors.Add(item);
            }

            for (int i = 0; i < acceptedShots.Count; i++)
            {
                var shot = acceptedShots[i];
                var actor = i < acceptedActors.Count ? acceptedActors[i] : null;
                var clip = new TimelineClipDto
                {
                    clipId = shot.itemId,
                    shotId = shot.itemId,
                    cameraPresetId = shot.cameraPresetId,
                    anchor = shot.position,
                    actorId = actor != null ? actor.actorId : "",
                    actionTag = actor != null ? actor.actionText : "",
                    durationSeconds = Mathf.Max(0.1f, shot.durationSeconds),
                    intent = shot.intent
                };
                clip.BindProbe(TimelineTrackLayer.Camera, shot.itemId, shot.position.ToUnityVector3(), true);
                if (actor != null && !string.IsNullOrEmpty(actor.actorId))
                    clip.BindProbe(TimelineTrackLayer.Actor, actor.actorId, actor.position.ToUnityVector3(), true);
                model.Add(clip);
            }
            return model;
        }
    }
}
