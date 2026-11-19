using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    public enum CameraMotionType
    {
        DollyPush,
        DollyPull,
        TruckPan,
        ArcOrbit,
        HandheldShake
    }

    /// <summary>
    /// SC01 经典运镜动作预设库：为导演提供5种工业级机位轨迹。
    /// 严格遵循数学保形，生成标准 CameraTrajectory 供时间轴与录像器采样。
    /// </summary>
    public static class CameraMotionPresets
    {
        public const float DefaultDuration = 5.0f;
        public const int SampleRate = 30;

        public static CameraTrajectory CreatePreset(CameraMotionType type, Vector3 targetPos, float duration = DefaultDuration)
        {
            return type switch
            {
                CameraMotionType.DollyPush => CreateDollyPush(targetPos, duration),
                CameraMotionType.DollyPull => CreateDollyPull(targetPos, duration),
                CameraMotionType.TruckPan => CreateTruckPan(targetPos, duration),
                CameraMotionType.ArcOrbit => CreateArcOrbit(targetPos, duration),
                CameraMotionType.HandheldShake => CreateHandheldShake(targetPos, duration),
                _ => CreateDollyPush(targetPos, duration)
            };
        }

        public static CameraTrajectory CreateDollyPush(Vector3 targetPos, float duration = DefaultDuration)
        {
            var traj = new CameraTrajectory();
            int totalSamples = Mathf.Max(2, Mathf.RoundToInt(duration * SampleRate));
            Vector3 startOffset = new Vector3(0.9f, 0.65f, -1.8f);
            Vector3 endOffset = new Vector3(0.35f, 0.25f, -0.65f);

            for (int i = 0; i <= totalSamples; i++)
            {
                float tNorm = (float)i / totalSamples;
                float time = tNorm * duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, tNorm);
                Vector3 pos = targetPos + Vector3.Lerp(startOffset, endOffset, smoothT);
                Quaternion rot = Quaternion.LookRotation(targetPos - pos, Vector3.up);
                float fov = Mathf.Lerp(50f, 42f, smoothT);
                traj.AddPoint(time, pos, rot, fov);
            }
            return traj;
        }

        public static CameraTrajectory CreateDollyPull(Vector3 targetPos, float duration = DefaultDuration)
        {
            var traj = new CameraTrajectory();
            int totalSamples = Mathf.Max(2, Mathf.RoundToInt(duration * SampleRate));
            Vector3 startOffset = new Vector3(0.25f, 0.20f, -0.55f);
            Vector3 endOffset = new Vector3(1.1f, 0.85f, -2.2f);

            for (int i = 0; i <= totalSamples; i++)
            {
                float tNorm = (float)i / totalSamples;
                float time = tNorm * duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, tNorm);
                Vector3 pos = targetPos + Vector3.Lerp(startOffset, endOffset, smoothT);
                Quaternion rot = Quaternion.LookRotation(targetPos - pos, Vector3.up);
                float fov = Mathf.Lerp(38f, 52f, smoothT);
                traj.AddPoint(time, pos, rot, fov);
            }
            return traj;
        }

        public static CameraTrajectory CreateTruckPan(Vector3 targetPos, float duration = DefaultDuration)
        {
            var traj = new CameraTrajectory();
            int totalSamples = Mathf.Max(2, Mathf.RoundToInt(duration * SampleRate));
            Vector3 startOffset = new Vector3(-1.1f, 0.40f, -1.2f);
            Vector3 endOffset = new Vector3(1.1f, 0.40f, -1.2f);

            for (int i = 0; i <= totalSamples; i++)
            {
                float tNorm = (float)i / totalSamples;
                float time = tNorm * duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, tNorm);
                Vector3 pos = targetPos + Vector3.Lerp(startOffset, endOffset, smoothT);
                Quaternion rot = Quaternion.LookRotation(targetPos - pos, Vector3.up);
                traj.AddPoint(time, pos, rot, 45f);
            }
            return traj;
        }

        public static CameraTrajectory CreateArcOrbit(Vector3 targetPos, float duration = DefaultDuration)
        {
            var traj = new CameraTrajectory();
            int totalSamples = Mathf.Max(2, Mathf.RoundToInt(duration * SampleRate));
            float radius = 1.4f;
            float height = 0.45f;
            float startAngle = -45f * Mathf.Deg2Rad;
            float endAngle = 45f * Mathf.Deg2Rad;

            for (int i = 0; i <= totalSamples; i++)
            {
                float tNorm = (float)i / totalSamples;
                float time = tNorm * duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, tNorm);
                float angle = Mathf.Lerp(startAngle, endAngle, smoothT);
                Vector3 offset = new Vector3(Mathf.Sin(angle) * radius, height, -Mathf.Cos(angle) * radius);
                Vector3 pos = targetPos + offset;
                Quaternion rot = Quaternion.LookRotation(targetPos - pos, Vector3.up);
                traj.AddPoint(time, pos, rot, 45f);
            }
            return traj;
        }

        public static CameraTrajectory CreateHandheldShake(Vector3 targetPos, float duration = DefaultDuration)
        {
            var traj = new CameraTrajectory();
            int totalSamples = Mathf.Max(2, Mathf.RoundToInt(duration * SampleRate));
            Vector3 baseOffset = new Vector3(0.6f, 0.5f, -1.3f);

            for (int i = 0; i <= totalSamples; i++)
            {
                float tNorm = (float)i / totalSamples;
                float time = tNorm * duration;
                float swayX = Mathf.Sin(time * 2.1f) * 0.025f + Mathf.Cos(time * 4.7f) * 0.008f;
                float swayY = Mathf.Cos(time * 1.8f) * 0.02f + Mathf.Sin(time * 3.3f) * 0.006f;
                float swayZ = Mathf.Sin(time * 1.5f) * 0.015f;

                Vector3 pos = targetPos + baseOffset + new Vector3(swayX, swayY, swayZ);
                Quaternion rot = Quaternion.LookRotation(targetPos - pos, Vector3.up) *
                                 Quaternion.Euler(swayY * 15f, swayX * 15f, 0f);
                traj.AddPoint(time, pos, rot, 45f);
            }
            return traj;
        }
    }
}
