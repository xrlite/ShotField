using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    public enum ActorPoseType
    {
        Idle,
        Sit,
        LeanForward,
        ReachEnvelope,
        StandUp
    }

    /// <summary>
    /// SC01 演员动作预设库：为主角提供5种剧情核心表演Take。
    /// 生成标准 ActorTake，通过时间轴精确驱动位置、旋转与动作标签。
    /// </summary>
    public static class ActorPosePresets
    {
        public const float DefaultDuration = 5.0f;

        public static ActorTake CreatePreset(ActorPoseType type, Vector3 basePos, Vector3 targetPos, float duration = DefaultDuration)
        {
            return type switch
            {
                ActorPoseType.Idle => CreateIdle(basePos, targetPos, duration),
                ActorPoseType.Sit => CreateSit(basePos, targetPos, duration),
                ActorPoseType.LeanForward => CreateLeanForward(basePos, targetPos, duration),
                ActorPoseType.ReachEnvelope => CreateReachEnvelope(basePos, targetPos, duration),
                ActorPoseType.StandUp => CreateStandUp(basePos, targetPos, duration),
                _ => CreateIdle(basePos, targetPos, duration)
            };
        }

        public static ActorTake CreateIdle(Vector3 basePos, Vector3 targetPos, float duration = DefaultDuration)
        {
            var take = new ActorTake { takeId = "Pose_Idle", actorId = "ActorA", duration = duration };
            Quaternion lookRot = Quaternion.LookRotation(targetPos - basePos, Vector3.up);
            int samples = 5;
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples * duration;
                float breathOffset = Mathf.Sin(t * 1.5f) * 0.005f;
                take.AddKeyframe(t, basePos + new Vector3(0f, breathOffset, 0f), lookRot, "Idle");
            }
            return take;
        }

        public static ActorTake CreateSit(Vector3 seatPos, Vector3 targetPos, float duration = DefaultDuration)
        {
            var take = new ActorTake { takeId = "Pose_Sit", actorId = "ActorA", duration = duration };
            Quaternion lookRot = Quaternion.LookRotation(targetPos - seatPos, Vector3.up);
            Vector3 sittingPos = seatPos + new Vector3(0f, -0.35f, 0f);
            int samples = 5;
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples * duration;
                take.AddKeyframe(t, sittingPos, lookRot, "Sit");
            }
            return take;
        }

        public static ActorTake CreateLeanForward(Vector3 seatPos, Vector3 targetPos, float duration = DefaultDuration)
        {
            var take = new ActorTake { takeId = "Pose_LeanForward", actorId = "ActorA", duration = duration };
            Vector3 startPos = seatPos + new Vector3(0f, -0.35f, 0f);
            Vector3 toTarget = (targetPos - seatPos).normalized;
            Vector3 endPos = startPos + toTarget * 0.22f + new Vector3(0f, -0.06f, 0f);

            Quaternion startRot = Quaternion.LookRotation(targetPos - seatPos, Vector3.up);
            Quaternion endRot = startRot * Quaternion.Euler(12f, 0f, 0f);

            int samples = 10;
            for (int i = 0; i <= samples; i++)
            {
                float tNorm = (float)i / samples;
                float time = tNorm * duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, tNorm);
                Vector3 p = Vector3.Lerp(startPos, endPos, smoothT);
                Quaternion r = Quaternion.Slerp(startRot, endRot, smoothT);
                take.AddKeyframe(time, p, r, smoothT > 0.5f ? "Leaning" : "Sit");
            }
            return take;
        }

        public static ActorTake CreateReachEnvelope(Vector3 seatPos, Vector3 envelopePos, float duration = DefaultDuration)
        {
            var take = new ActorTake { takeId = "Pose_ReachEnvelope", actorId = "ActorA", duration = duration };
            Vector3 startPos = seatPos + new Vector3(0f, -0.35f, 0f);
            Vector3 toEnv = (envelopePos - seatPos).normalized;
            Vector3 peakPos = startPos + toEnv * 0.28f + new Vector3(0f, -0.08f, 0f);

            Quaternion startRot = Quaternion.LookRotation(envelopePos - seatPos, Vector3.up);
            Quaternion reachRot = startRot * Quaternion.Euler(15f, 5f, 0f);

            int samples = 12;
            for (int i = 0; i <= samples; i++)
            {
                float tNorm = (float)i / samples;
                float time = tNorm * duration;
                float arc = Mathf.Sin(tNorm * Mathf.PI);
                Vector3 p = Vector3.Lerp(startPos, peakPos, tNorm) + new Vector3(0f, arc * 0.03f, 0f);
                Quaternion r = Quaternion.Slerp(startRot, reachRot, tNorm);
                string act = tNorm > 0.7f ? "GrabLetter" : (tNorm > 0.3f ? "Reach" : "Sit");
                take.AddKeyframe(time, p, r, act);
            }
            return take;
        }

        public static ActorTake CreateStandUp(Vector3 seatPos, Vector3 standPos, float duration = DefaultDuration)
        {
            var take = new ActorTake { takeId = "Pose_StandUp", actorId = "ActorA", duration = duration };
            Vector3 sitPos = seatPos + new Vector3(0f, -0.35f, 0f);
            Quaternion sitRot = Quaternion.LookRotation(standPos - seatPos, Vector3.up);
            Quaternion standRot = sitRot;

            int samples = 10;
            for (int i = 0; i <= samples; i++)
            {
                float tNorm = (float)i / samples;
                float time = tNorm * duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, tNorm);
                Vector3 p = Vector3.Lerp(sitPos, standPos, smoothT);
                Quaternion r = Quaternion.Slerp(sitRot, standRot, smoothT);
                take.AddKeyframe(time, p, r, smoothT > 0.6f ? "Standing" : "StandingUp");
            }
            return take;
        }
    }
}
