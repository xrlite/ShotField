using UnityEngine;
using UnityEngine.InputSystem;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    public class CameraMotionRecorder : MonoBehaviour
    {
        public enum State { Idle, Recording, Playing }

        [Header("三路明确绑定（禁止Camera.main自动回退）")]
        [Tooltip("录制位姿源")] public Transform poseSource;
        [Tooltip("影片回放相机")] public Camera filmCamera;
        [Tooltip("头显观察相机")] public Camera hmdObserver;
        public bool useDeviceInput = true;

        [Header("采样与滤波设置")]
        [Range(10, 60)] public int sampleRate = 30;
        public bool smoothEnabled = true;
        [Range(3, 9)] public int smoothWindow = 5;
        public bool useSmoothedForPlayback = true;

        [Header("轨迹渲染")]
        public LineRenderer trajectoryLine;
        public static Color AmberColor => PrevisPalette.AmberTrajectory;
        public Color recordColor = PrevisPalette.AmberTrajectory;
        public Color readyColor = PrevisPalette.AmberTrajectory;

        [Header("轨迹双缓存（原始 + 平滑）")]
        public CameraTrajectory rawTrajectory = new CameraTrajectory();
        public CameraTrajectory smoothedTrajectory = new CameraTrajectory();

        public CameraTrajectory ActiveTrajectory =>
            (useSmoothedForPlayback && smoothedTrajectory != null && smoothedTrajectory.Count > 0)
                ? smoothedTrajectory : rawTrajectory;

        public State CurrentState { get; private set; } = State.Idle;

        private Material ownedLineMaterial;
        private readonly TimedCaptureClock clock = new TimedCaptureClock();
        private readonly InputAction recordAction = new InputAction("RecordMotion", InputActionType.Button);

        void Awake()
        {
            recordAction.AddBinding("<XRController>{RightHand}/gripPressed");
            recordAction.AddBinding("<Keyboard>/r");
            EnsureLineRenderer();
        }

        void OnEnable() => recordAction.Enable();
        void OnDisable() => recordAction.Disable();
        void OnDestroy() { recordAction.Dispose(); if (ownedLineMaterial != null) Destroy(ownedLineMaterial); }

        public void LoadAndPlay(CameraTrajectory trajectory)
        {
            rawTrajectory = trajectory.Clone();
            smoothedTrajectory = null;
            RefreshTrajectoryLine();
            StartPlayback();
        }

        void Update()
        {
            if (useDeviceInput && recordAction.WasPressedThisFrame() && CurrentState == State.Idle) StartRecording();
            else if (useDeviceInput && recordAction.WasReleasedThisFrame() && CurrentState == State.Recording) StopRecording();

            if (CurrentState == State.Recording) TickRecording();
            else if (CurrentState == State.Playing) TickPlayback();
        }

        public void StartRecording() => StartRecording(Time.time);

        public void StartRecording(float startTime)
        {
            if (poseSource == null || CurrentState != State.Idle) return;
            CurrentState = State.Recording;
            rawTrajectory = new CameraTrajectory();
            smoothedTrajectory = new CameraTrajectory();
            clock.BeginRecording(startTime, sampleRate);
            UpdateLineStyle(recordColor);
            RecordSample(0f);
            RefreshTrajectoryLine();
        }

        public void StopRecording() => StopRecording(Time.time);

        public void StopRecording(float endTime)
        {
            if (CurrentState != State.Recording) return;
            RecordSample(clock.EndRecording(endTime));
            CurrentState = State.Idle;

            if (smoothEnabled && rawTrajectory.Count > 2)
                smoothedTrajectory = rawTrajectory.Smooth(smoothWindow, preserveEndpoints: true);
            else
                smoothedTrajectory = rawTrajectory.Clone();

            UpdateLineStyle(readyColor);
            RefreshTrajectoryLine();
        }

        public void StartPlayback()
        {
            if (filmCamera == null || filmCamera == hmdObserver || CurrentState == State.Recording) return;
            var traj = ActiveTrajectory;
            if (traj == null || traj.Count == 0) return;
            CurrentState = State.Playing;
            clock.BeginPlayback(Time.time);
        }

        public void StopPlayback() { if (CurrentState == State.Playing) CurrentState = State.Idle; }

        private void TickRecording() => TickRecordingAt(Time.time);

        public bool TickRecordingAt(float now)
        {
            if (!clock.PollSample(now, sampleRate, out float elapsed)) return false;
            RecordSample(elapsed);
            if (trajectoryLine != null)
            {
                trajectoryLine.positionCount = rawTrajectory.Count;
                trajectoryLine.SetPosition(rawTrajectory.Count - 1, rawTrajectory.points[rawTrajectory.Count - 1].position);
            }
            return true;
        }

        private void TickPlayback()
        {
            var traj = ActiveTrajectory;
            if (traj == null || traj.Count == 0 || filmCamera == null)
            {
                CurrentState = State.Idle;
                return;
            }
            float elapsed = clock.PlaybackElapsed(Time.time);
            var sample = traj.Evaluate(elapsed);
            if (elapsed >= traj.totalDuration) CurrentState = State.Idle;
            filmCamera.transform.SetPositionAndRotation(sample.position, sample.rotation);
            filmCamera.fieldOfView = sample.fieldOfView;
        }

        private void RecordSample(float timeOffset)
        {
            if (poseSource == null) return;
            float fov = filmCamera != null ? filmCamera.fieldOfView : 45f;
            rawTrajectory.AddPoint(timeOffset, poseSource.position, poseSource.rotation, fov);
        }

        public void ApplyToShot(ShotData shot) { if (shot != null) shot.trajectory = ActiveTrajectory?.Clone(); }

        public void EnsureLineRenderer()
        {
            if (trajectoryLine == null) trajectoryLine = GetComponent<LineRenderer>();
            if (trajectoryLine == null) trajectoryLine = gameObject.AddComponent<LineRenderer>();
            trajectoryLine.startWidth = 0.012f;
            trajectoryLine.endWidth = 0.012f;
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.alignment = LineAlignment.View;
            trajectoryLine.numCornerVertices = 4;
            trajectoryLine.numCapVertices = 4;
            trajectoryLine.gameObject.layer = 0;
            if (ownedLineMaterial == null)
            {
                var s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                ownedLineMaterial = new Material(s) { color = PrevisPalette.AmberTrajectory };
            }
            trajectoryLine.sharedMaterial = ownedLineMaterial;
            UpdateLineStyle(PrevisPalette.AmberTrajectory);
        }

        private void UpdateLineStyle(Color color)
        {
            if (trajectoryLine == null) return;
            trajectoryLine.startColor = color;
            trajectoryLine.endColor = color;
            if (trajectoryLine.sharedMaterial != null) trajectoryLine.sharedMaterial.color = color;
        }

        public void RefreshTrajectoryLine()
        {
            var traj = ActiveTrajectory;
            if (trajectoryLine == null || traj == null) return;
            int count = traj.points.Count;
            trajectoryLine.positionCount = count;
            for (int i = 0; i < count; i++)
                trajectoryLine.SetPosition(i, traj.points[i].position);
            UpdateLineStyle(PrevisPalette.AmberTrajectory);
        }
    }
}
