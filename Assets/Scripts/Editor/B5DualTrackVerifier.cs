#if UNITY_EDITOR
using System;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;
namespace ShotField.EditorTools
{
    /// <summary>B5：真实录制状态机产 12s raw/smoothed 双轨，并验证重定时与 50% 寻址。</summary>
    public static class B5DualTrackVerifier
    {
        private const int Expected = 8;
        private const float Duration = 12f;
        private const int Rate = 30;
        [MenuItem("ShotField/Verifiers/Run B5 Dual Track Verifier")]
        public static void Run()
        {
            int passed = 0;
            var host = new GameObject("[B5DualTrackVerifier]");
            var pose = new GameObject("Pose").transform;
            pose.SetParent(host.transform);
            var cameraGo = new GameObject("FilmCamera");
            var camera = cameraGo.AddComponent<Camera>();
            var recorder = host.AddComponent<CameraMotionRecorder>();
            recorder.poseSource = pose; recorder.filmCamera = camera; recorder.sampleRate = Rate;
            recorder.smoothEnabled = true; recorder.smoothWindow = 5; recorder.useSmoothedForPlayback = true;
            try
            {
                recorder.StartRecording(0f);
                bool recording = recorder.CurrentState == CameraMotionRecorder.State.Recording;
                for (int i = 1; i <= (int)(Duration * Rate); i++)
                {
                    float t = i / (float)Rate;
                    SetPose(pose, t, i);
                    recorder.TickRecordingAt(t);
                }
                recorder.StopRecording(Duration);
                Check("录制状态机可进入并退出 Recording", recording && recorder.CurrentState == CameraMotionRecorder.State.Idle, ref passed);
                var raw = recorder.rawTrajectory;
                var smooth = recorder.smoothedTrajectory;
                Check("实录时长达到 12 秒（>=10s）", raw != null && Mathf.Abs(raw.totalDuration - Duration) < 0.001f, ref passed);
                Check("30Hz 实录采样数达到 360", raw != null && raw.Count >= 360, ref passed);
                Check("raw/smoothed 双轨采样数一致", raw != null && smooth != null && raw.Count == smooth.Count, ref passed);
                bool endpoints = raw != null && smooth != null &&
                    Vector3.Distance(raw.points[0].position, smooth.points[0].position) < 0.0001f &&
                    Vector3.Distance(raw.points[raw.Count - 1].position, smooth.points[smooth.Count - 1].position) < 0.0001f;
                Check("平滑保持起终点位置", endpoints, ref passed);
                float rawJitter = Jitter(raw); float smoothJitter = Jitter(smooth);
                Debug.Log($"[B5-DUAL-TRACK] jitter raw={rawJitter:F6} smooth={smoothJitter:F6} ratio={(smoothJitter / Mathf.Max(0.000001f, rawJitter)):F3}");
                Check("平滑削弱真实轨迹的高频分量", rawJitter > 0.00001f && smoothJitter < rawJitter * 0.8f, ref passed);
                var timeline = host.AddComponent<DirectorTimeline>(); timeline.cameraRecorder = recorder;
                var trajectoryRef = recorder.ActiveTrajectory; int pointCount = trajectoryRef.Count;
                timeline.SetDuration(Duration); timeline.SeekNormalized(0.5f); Vector3 before = camera.transform.position;
                timeline.SetDuration(Duration * 0.5f); timeline.SeekNormalized(0.5f); Vector3 after = camera.transform.position;
                Check("12s→6s 重定时不换轨迹且 50% 画面不动", ReferenceEquals(trajectoryRef, recorder.ActiveTrajectory) &&
                    trajectoryRef.Count == pointCount && Vector3.Distance(before, after) < 0.0001f, ref passed);
                Check("50% 寻址精确落在 6 秒采样", Mathf.Abs(timeline.NormalizedTime - 0.5f) < 0.0001f &&
                    Vector3.Distance(after, trajectoryRef.Evaluate(Duration * 0.5f).position) < 0.0001f, ref passed);
            }
            catch (Exception ex) { Debug.LogError("[B5-DUAL-TRACK] " + ex); }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(cameraGo); }
            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"B5-DUAL-TRACK-RESULT={result} ({passed}/{Expected})");
            if (result == "FAIL" && Application.isBatchMode) EditorApplication.Exit(1);
        }
        private static void SetPose(Transform pose, float t, int i)
        {
            float u = t / Duration;
            float angle = Mathf.Lerp(-1f, 1f, u);
            var target = new Vector3(1.5f, 0.8f, 2.15f);
            var jitter = new Vector3(Mathf.Sin(i * 1.7f), Mathf.Cos(i * 1.3f), Mathf.Sin(i * 0.9f)) * 0.025f;
            pose.position = target + new Vector3(Mathf.Cos(angle) * 1.2f, 1.35f + Mathf.Sin(u * Mathf.PI * 2f) * 0.12f, Mathf.Sin(angle) * 1.2f) + jitter;
            pose.LookAt(target);
        }
        private static float Jitter(CameraTrajectory trajectory)
        {
            if (trajectory == null || trajectory.Count < 3) return 0f;
            float sum = 0f;
            for (int i = 2; i < trajectory.Count; i++)
            {
                var second = trajectory.points[i].position - 2f * trajectory.points[i - 1].position + trajectory.points[i - 2].position;
                sum += second.magnitude;
            }
            return sum / (trajectory.Count - 2);
        }
        private static void Check(string label, bool ok, ref int passed)
        {
            if (ok) { passed++; Debug.Log($"[B5-DUAL-TRACK] PASS: {label}"); }
            else Debug.LogError($"[B5-DUAL-TRACK] FAIL: {label}");
        }
    }
}
#endif
