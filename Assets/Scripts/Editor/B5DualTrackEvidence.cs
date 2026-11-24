#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShotField.EditorTools
{
    /// <summary>One-shot B5 proof: real recording state machine, dual-track frames, and retime/seek evidence.</summary>
    public static class B5DualTrackEvidence
    {
        private const float Duration = 12f;
        private const int Rate = 30;
        private const int Frames = 360;
        private const int Width = 960;
        private const int Height = 540;
        private const float Seek = 0.5f;
        private const float SampleTime = Duration * Seek;
        private const string TimeLabel = "t=6.00s / 12.00s (50%)";
        [MenuItem("ShotField/Evidence/Run B5 Dual Track Evidence")]
        public static void Run()
        {
            string outputDirectory = null, reportPath = null, error = null;
            bool passed = false;
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            try
            {
                outputDirectory = B5EvidenceSupport.CreateOutputDirectory(stamp);
                reportPath = Path.Combine(outputDirectory, $"B5_evidence_{stamp}.json");
                Generate(outputDirectory, reportPath);
                passed = true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                Debug.LogException(ex);
                B5EvidenceSupport.TryWriteFailureReport(outputDirectory, reportPath, error);
            }
            string detail = passed ? $"frames={Frames} report={reportPath}" : error;
            Debug.Log($"B5-EVIDENCE-RESULT={(passed ? "PASS" : "FAIL")} {detail}");
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }
        private static void Generate(string outputDirectory, string reportPath)
        {
            var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid() && scene.path == BuildAndroid.ScenePath, "BuildAndroid scene did not open cleanly.");
            var template = FindTemplateCamera(scene);
            var host = new GameObject("[B5DualTrackEvidence]") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                SceneManager.MoveGameObjectToScene(host, scene);
                var camera = ConfigureCamera(host, template);
                var pose = new GameObject("Pose").transform;
                pose.SetParent(host.transform, false);
                var recorder = host.AddComponent<CameraMotionRecorder>();
                recorder.poseSource = pose;
                recorder.filmCamera = camera;
                recorder.sampleRate = Rate;
                recorder.smoothEnabled = true;
                recorder.smoothWindow = 5;
                recorder.useSmoothedForPlayback = true;
                recorder.useDeviceInput = false;
                Record(recorder, pose);
                var raw = recorder.rawTrajectory;
                var smooth = recorder.smoothedTrajectory;
                Require(raw != null && smooth != null, "Raw or smoothed trajectory is missing.");
                Require(raw.Count == Frames + 1 && smooth.Count == raw.Count, "Unexpected dual-track sample counts.");
                Require(Mathf.Abs(raw.totalDuration - Duration) < 0.001f &&
                    Mathf.Abs(smooth.totalDuration - Duration) < 0.001f, "Recorded duration is not 12 seconds.");
                Require(Vector3.Distance(raw.points[0].position, smooth.points[0].position) < 0.0001f &&
                    Vector3.Distance(raw.points[raw.Count - 1].position, smooth.points[smooth.Count - 1].position) < 0.0001f,
                    "Smoothing did not preserve endpoints.");
                float jitterRaw = B5EvidenceSupport.Jitter(raw), jitterSmooth = B5EvidenceSupport.Jitter(smooth);
                Require(jitterRaw > 0.00001f && jitterSmooth < jitterRaw * 0.8f, "Smoothing did not reduce high-frequency jitter.");

                var timeline = host.AddComponent<DirectorTimeline>();
                timeline.cameraRecorder = recorder;
                timeline.actorPlayer = null;
                timeline.videoPlayer = null;
                timeline.playableDirector = null;
                timeline.actorA = null;
                timeline.actorB = null;
                var trajectory = recorder.ActiveTrajectory;
                int pointCount = trajectory.Count;
                timeline.SetDuration(Duration);
                timeline.SeekNormalized(Seek);
                float beforeDuration = timeline.duration, beforeNormalized = timeline.NormalizedTime;
                Vector3 before = camera.transform.position;
                timeline.SetDuration(Duration * Seek);
                timeline.SeekNormalized(Seek);
                float afterDuration = timeline.duration, afterNormalized = timeline.NormalizedTime;
                Vector3 after = camera.transform.position;
                float cameraPositionError = Vector3.Distance(before, after);
                Require(Mathf.Abs(beforeDuration - Duration) < 0.001f && Mathf.Abs(afterDuration - SampleTime) < 0.001f &&
                    Mathf.Abs(beforeNormalized - Seek) < 0.0001f && Mathf.Abs(afterNormalized - Seek) < 0.0001f &&
                    ReferenceEquals(trajectory, recorder.ActiveTrajectory) && trajectory.Count == pointCount &&
                    cameraPositionError < 0.0001f &&
                    Vector3.Distance(after, trajectory.Evaluate(SampleTime).position) < 0.0001f,
                    "Retime or normalized 50% seek changed the camera sample.");

                for (int i = 0; i < Frames; i++)
                {
                    B5EvidenceRenderer.Apply(camera, smooth.Evaluate(i / (float)Rate));
                    B5EvidenceRenderer.RenderJpg(camera, Path.Combine(outputDirectory, $"frame_{i:D4}.jpg"), Width, Height);
                }
                string comparisonPath = Path.Combine(outputDirectory, "raw_smoothed_t06.png");
                B5EvidenceRenderer.RenderSideBySide(camera, raw.Evaluate(SampleTime), smooth.Evaluate(SampleTime), comparisonPath);
                string singlePath = Path.Combine(outputDirectory, "smoothed_t06.png");
                B5EvidenceRenderer.Apply(camera, smooth.Evaluate(SampleTime));
                B5EvidenceRenderer.RenderPng(camera, singlePath, Width, Height, TimeLabel);
                ValidateOutputs(outputDirectory, comparisonPath, singlePath, Frames);

                var report = new B5EvidenceSupport.EvidenceReport
                {
                    status = "PASS", duration = Duration, fps = Rate, frames = Frames,
                    rawSamples = raw.Count, smoothSamples = smooth.Count, jitterRaw = jitterRaw, jitterSmooth = jitterSmooth,
                    retimeBeforeDuration = beforeDuration, retimeAfterDuration = afterDuration, seekNormalized = Seek,
                    sampleTime = SampleTime, cameraPositionError = cameraPositionError,
                    templateCamera = template == null ? "NONE" : template.name,
                    outputPaths = new B5EvidenceSupport.EvidencePaths
                    {
                        framesDirectory = outputDirectory,
                        framePattern = Path.Combine(outputDirectory, "frame_0000.jpg .. frame_0359.jpg"),
                        rawSmoothedComparison = comparisonPath, smoothedFrame = singlePath, report = reportPath
                    }
                };
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            finally { Object.DestroyImmediate(host); }
        }
        private static void Record(CameraMotionRecorder recorder, Transform pose)
        {
            B5EvidenceSupport.SetPose(pose, 0f, 0);
            if (recorder.trajectoryLine != null) recorder.trajectoryLine.enabled = false;
            recorder.StartRecording(0f);
            Require(recorder.CurrentState == CameraMotionRecorder.State.Recording, "Recorder did not enter Recording.");
            int accepted = 0;
            for (int i = 1; i <= Frames; i++)
            {
                float t = i / (float)Rate;
                B5EvidenceSupport.SetPose(pose, t, i);
                if (recorder.TickRecordingAt(t)) accepted++;
            }
            recorder.StopRecording(Duration);
            Require(accepted == Frames && recorder.CurrentState == CameraMotionRecorder.State.Idle,
                "Recorder did not accept 360 ticks and return to Idle.");
        }
        private static Camera ConfigureCamera(GameObject host, Camera template)
        {
            var camera = host.AddComponent<Camera>();
            if (template != null) camera.CopyFrom(template);
            else
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            }
            camera.enabled = false;
            camera.orthographic = false;
            camera.allowHDR = false;
            camera.cullingMask &= ~(1 << 5);
            var type = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (type != null && host.GetComponent(type) == null)
            {
                host.AddComponent(type);
                var data = new SerializedObject(host.GetComponent(type));
                var post = data.FindProperty("m_RenderPostProcessing");
                if (post != null) { post.boolValue = true; data.ApplyModifiedPropertiesWithoutUndo(); }
            }
            return camera;
        }
        private static Camera FindTemplateCamera(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                if (camera.name == "Director") return camera;
            return null;
        }
        private static void ValidateOutputs(string directory, string comparisonPath, string singlePath, int frames)
        {
            if (Directory.GetFiles(directory, "frame_*.jpg").Length != frames) throw new InvalidOperationException("JPG frame count is not 360.");
            for (int i = 0; i < frames; i++) if (new FileInfo(Path.Combine(directory, $"frame_{i:D4}.jpg")).Length == 0) throw new InvalidOperationException($"JPG frame {i} is empty.");
            if (new FileInfo(comparisonPath).Length == 0 || new FileInfo(singlePath).Length == 0) throw new InvalidOperationException("A PNG output is empty.");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
