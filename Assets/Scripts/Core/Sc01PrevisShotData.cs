using UnityEngine;

namespace ShotField.Core
{
    public enum ShotEasing
    {
        EaseInOutQuad,
        Linear,
        EaseOutCubic,
        EaseInOutSine,
        EaseInQuad,
        FastInSlowOut,
        SmoothStep
    }

    [System.Serializable]
    public struct ShotSpec
    {
        public string id;
        public float startTime;
        public float endTime;
        public Vector3 startCamPos;
        public Vector3 endCamPos;
        public Vector3 lookAtTarget;
        public float fov;
        public ShotEasing easing;
        public string dramaticBeat;

        public float Duration => Mathf.Max(0.001f, endTime - startTime);
    }

    public static class Sc01PrevisShotData
    {
        public const float TotalDuration = 60.0f;
        public const int SampleRate = 30;
        public const int TotalFrames = 1801;

        public static readonly ShotSpec[] Shots = new ShotSpec[]
        {
            new ShotSpec { id = "SH_01", startTime = 0.0f, endTime = 8.0f,
                startCamPos = new Vector3(-2.10f, 1.60f, 0.90f), endCamPos = new Vector3(-1.80f, 1.55f, 1.25f),
                lookAtTarget = new Vector3(0.00f, 1.10f, 2.00f), fov = 62.0f, easing = ShotEasing.EaseInOutQuad,
                dramaticBeat = "The Breach: A enters doorway, B stands waiting" },
            new ShotSpec { id = "SH_02", startTime = 8.0f, endTime = 17.0f,
                startCamPos = new Vector3(-2.00f, 1.45f, 1.50f), endCamPos = new Vector3(-0.90f, 1.40f, 2.10f),
                lookAtTarget = new Vector3(-0.40f, 1.15f, 1.80f), fov = 52.0f, easing = ShotEasing.Linear,
                dramaticBeat = "Cold Advance: A strides toward coffee table" },
            new ShotSpec { id = "SH_03", startTime = 17.0f, endTime = 25.0f,
                startCamPos = new Vector3(-0.20f, 0.65f, 1.70f), endCamPos = new Vector3(0.30f, 0.58f, 1.85f),
                lookAtTarget = new Vector3(0.85f, 0.23f, 2.42f), fov = 42.0f, easing = ShotEasing.EaseOutCubic,
                dramaticBeat = "The Target: Focal lock on sealed letter" },
            new ShotSpec { id = "SH_04", startTime = 25.0f, endTime = 34.0f,
                startCamPos = new Vector3(-0.20f, 1.55f, 1.50f), endCamPos = new Vector3(0.05f, 1.52f, 1.60f),
                lookAtTarget = new Vector3(0.95f, 1.50f, 2.75f), fov = 46.0f, easing = ShotEasing.EaseInOutSine,
                dramaticBeat = "Escalation: Mutual confrontation across table" },
            new ShotSpec { id = "SH_05", startTime = 34.0f, endTime = 43.0f,
                startCamPos = new Vector3(1.35f, 1.55f, 3.10f), endCamPos = new Vector3(1.15f, 1.50f, 2.90f),
                lookAtTarget = new Vector3(0.10f, 1.48f, 1.85f), fov = 44.0f, easing = ShotEasing.EaseInQuad,
                dramaticBeat = "Impasse: B issues warning, A's hand hovers" },
            new ShotSpec { id = "SH_06", startTime = 43.0f, endTime = 52.0f,
                startCamPos = new Vector3(-0.40f, 1.10f, 2.20f), endCamPos = new Vector3(0.15f, 1.00f, 2.30f),
                lookAtTarget = new Vector3(0.85f, 0.40f, 2.42f), fov = 50.0f, easing = ShotEasing.FastInSlowOut,
                dramaticBeat = "The Struggle: Hands clash over envelope, A seizes" },
            new ShotSpec { id = "SH_07", startTime = 52.0f, endTime = 60.0f,
                startCamPos = new Vector3(-0.80f, 1.45f, 1.40f), endCamPos = new Vector3(-1.20f, 1.50f, 1.90f),
                lookAtTarget = new Vector3(0.60f, 0.90f, 2.20f), fov = 56.0f, easing = ShotEasing.SmoothStep,
                dramaticBeat = "Cliffhanger: Tense freeze before seal is broken" }
        };

        public static float EvaluateEasing(float t, ShotEasing easing)
        {
            t = Mathf.Clamp01(t);
            return easing switch
            {
                ShotEasing.EaseInOutQuad => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f,
                ShotEasing.Linear => t,
                ShotEasing.EaseOutCubic => 1f - Mathf.Pow(1f - t, 3f),
                ShotEasing.EaseInOutSine => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f,
                ShotEasing.EaseInQuad => t * t,
                ShotEasing.FastInSlowOut => 1f - (1f - t) * (1f - t),
                ShotEasing.SmoothStep => Mathf.SmoothStep(0f, 1f, t),
                _ => t
            };
        }

        public static int GetShotIndex(float time)
        {
            for (int i = Shots.Length - 1; i >= 0; i--)
                if (time >= Shots[i].startTime) return i;
            return 0;
        }

        public static void EvaluateCamera(float time, out Vector3 pos, out Quaternion rot, out float fov)
        {
            time = Mathf.Clamp(time, 0f, TotalDuration);
            int idx = GetShotIndex(time);
            var shot = Shots[idx];
            float u = Mathf.Clamp01((time - shot.startTime) / shot.Duration);
            float e = EvaluateEasing(u, shot.easing);
            pos = Vector3.Lerp(shot.startCamPos, shot.endCamPos, e);
            Vector3 forward = (shot.lookAtTarget - pos).normalized;
            rot = Quaternion.LookRotation(forward, Vector3.up);
            fov = shot.fov;
        }

        public static Vector3 EvaluateActorA(float time)
        {
            time = Mathf.Clamp(time, 0f, TotalDuration);
            if (time < 8.0f)
            {
                float u = Mathf.Clamp01(time / 5.0f);
                return Vector3.Lerp(new Vector3(-2.20f, 0f, 0.15f), new Vector3(-1.85f, 0f, 0.85f), u);
            }
            if (time < 17.0f)
            {
                float u = Mathf.Clamp01((time - 8.0f) / 6.5f);
                return Vector3.Lerp(new Vector3(-1.85f, 0f, 0.85f), new Vector3(-0.40f, 0f, 1.80f), u);
            }
            if (time < 25.0f) return new Vector3(-0.40f, 0f, 1.80f);
            if (time < 34.0f)
            {
                float u = Mathf.Clamp01((time - 25.0f) / 5.0f);
                return Vector3.Lerp(new Vector3(-0.40f, 0f, 1.80f), new Vector3(0.10f, 0f, 1.85f), u);
            }
            if (time < 43.0f) return new Vector3(0.10f, 0f, 1.85f);
            if (time < 52.0f)
            {
                float u = Mathf.Clamp01((time - 43.0f) / 4.0f);
                return Vector3.Lerp(new Vector3(0.10f, 0f, 1.85f), new Vector3(0.15f, 0f, 1.80f), u);
            }
            float u7 = Mathf.Clamp01((time - 52.0f) / 4.0f);
            return Vector3.Lerp(new Vector3(0.15f, 0f, 1.80f), new Vector3(0.20f, 0f, 1.70f), u7);
        }

        public static Quaternion EvaluateActorARotation(float time)
        {
            Vector3 pos = EvaluateActorA(time);
            Vector3 lookTarget = (time < 8.0f) ? new Vector3(-1.85f, 0f, 0.85f)
                : (time < 17.0f) ? new Vector3(-0.40f, 0f, 1.80f)
                : new Vector3(0.85f, 0f, 2.42f);
            Vector3 dir = lookTarget - pos;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
        }

        public static Vector3 EvaluateActorB(float time)
        {
            time = Mathf.Clamp(time, 0f, TotalDuration);
            if (time < 25.0f) return new Vector3(1.18f, 0f, 3.05f);
            if (time < 34.0f)
            {
                float u = Mathf.Clamp01((time - 25.0f) / 4.5f);
                return Vector3.Lerp(new Vector3(1.18f, 0f, 3.05f), new Vector3(0.95f, 0f, 2.75f), u);
            }
            if (time < 43.0f) return new Vector3(0.95f, 0f, 2.75f);
            if (time < 52.0f)
            {
                float u = Mathf.Clamp01((time - 43.0f) / 3.0f);
                return Vector3.Lerp(new Vector3(0.95f, 0f, 2.75f), new Vector3(0.92f, 0f, 2.72f), u);
            }
            float u7 = Mathf.Clamp01((time - 52.0f) / 3.0f);
            return Vector3.Lerp(new Vector3(0.92f, 0f, 2.72f), new Vector3(0.90f, 0f, 2.70f), u7);
        }

        public static Quaternion EvaluateActorBRotation(float time)
        {
            if (time < 8.0f) return Quaternion.Euler(0f, 198f, 0f);
            Vector3 posB = EvaluateActorB(time);
            Vector3 posA = EvaluateActorA(time);
            Vector3 dir = posA - posB;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir.normalized) : Quaternion.Euler(0f, 198f, 0f);
        }

        public static CameraTrajectory CreateSequenceTrajectory()
        {
            var traj = new CameraTrajectory();
            for (int i = 0; i < TotalFrames; i++)
            {
                float t = (float)i / (TotalFrames - 1) * TotalDuration;
                EvaluateCamera(t, out var pos, out var rot, out float fov);
                traj.AddPoint(t, pos, rot, fov);
            }
            return traj;
        }
    }
}
