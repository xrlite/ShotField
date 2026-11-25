using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 3 点 VR 关键帧（Root + 头部 + 左右手局部相对位姿）。
    /// </summary>
    [Serializable]
    public struct PerformancePoseFrame
    {
        public float time;
        public Vector3 rootPosition;
        public Quaternion rootRotation;
        public Vector3 headPosition;
        public Quaternion headRotation;
        public Vector3 leftHandPosition;
        public Quaternion leftHandRotation;
        public Vector3 rightHandPosition;
        public Quaternion rightHandRotation;

        public static PerformancePoseFrame Lerp(PerformancePoseFrame a, PerformancePoseFrame b, float t) =>
            new PerformancePoseFrame
            {
                time = Mathf.Lerp(a.time, b.time, t),
                rootPosition = Vector3.Lerp(a.rootPosition, b.rootPosition, t),
                rootRotation = Quaternion.Slerp(a.rootRotation, b.rootRotation, t),
                headPosition = Vector3.Lerp(a.headPosition, b.headPosition, t),
                headRotation = Quaternion.Slerp(a.headRotation, b.headRotation, t),
                leftHandPosition = Vector3.Lerp(a.leftHandPosition, b.leftHandPosition, t),
                leftHandRotation = Quaternion.Slerp(a.leftHandRotation, b.leftHandRotation, t),
                rightHandPosition = Vector3.Lerp(a.rightHandPosition, b.rightHandPosition, t),
                rightHandRotation = Quaternion.Slerp(a.rightHandRotation, b.rightHandRotation, t)
            };
    }

    [Serializable]
    public class ActorPerformanceClipDto
    {
        public string clipId;
        public string actorId;
        public float duration;
        public float scaleRatio;
        public List<PerformancePoseFrame> frames = new List<PerformancePoseFrame>();
    }

    /// <summary>
    /// 1:1 真人表演实录数据载体：存储 30Hz 骨骼与肢体动作采样。
    /// 向后兼容现有 ActorTake，支持多机位复拍与回放。
    /// </summary>
    [Serializable]
    public class ActorPerformanceClip
    {
        public string clipId = "Perf_01";
        public string actorId = "actorA";
        public float duration = 0f;
        public float scaleRatio = 1.0f;
        public List<PerformancePoseFrame> frames = new List<PerformancePoseFrame>();

        public int Count => frames.Count;

        public void AddFrame(float t, Vector3 rootPos, Quaternion rootRot,
            Vector3 headPos, Quaternion headRot,
            Vector3 lHandPos, Quaternion lHandRot,
            Vector3 rHandPos, Quaternion rHandRot)
        {
            frames.Add(new PerformancePoseFrame
            {
                time = t,
                rootPosition = rootPos, rootRotation = rootRot,
                headPosition = headPos, headRotation = headRot,
                leftHandPosition = lHandPos, leftHandRotation = lHandRot,
                rightHandPosition = rHandPos, rightHandRotation = rHandRot
            });
            if (t > duration) duration = t;
        }

        public PerformancePoseFrame Evaluate(float t)
        {
            if (frames == null || frames.Count == 0) return default;
            if (t >= duration) return frames[frames.Count - 1];
            KeyframeTrack.FindSegment(frames, f => f.time, t, out int idxA, out int idxB, out float alpha);
            if (idxA < 0) return frames[0];
            return idxA == idxB ? frames[idxA] : PerformancePoseFrame.Lerp(frames[idxA], frames[idxB], alpha);
        }

        public ActorTake ToActorTake()
        {
            var take = new ActorTake { takeId = clipId, actorId = actorId, duration = duration };
            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                take.AddKeyframe(f.time, f.rootPosition, f.rootRotation, "performance");
            }
            return take;
        }

        public string ToJson()
        {
            var dto = new ActorPerformanceClipDto
            {
                clipId = clipId, actorId = actorId, duration = duration,
                scaleRatio = scaleRatio, frames = new List<PerformancePoseFrame>(frames)
            };
            return JsonUtility.ToJson(dto);
        }

        public static ActorPerformanceClip FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var dto = JsonUtility.FromJson<ActorPerformanceClipDto>(json);
            if (dto == null) return null;
            return new ActorPerformanceClip
            {
                clipId = dto.clipId, actorId = dto.actorId,
                duration = dto.duration, scaleRatio = dto.scaleRatio,
                frames = dto.frames ?? new List<PerformancePoseFrame>()
            };
        }
    }
}
