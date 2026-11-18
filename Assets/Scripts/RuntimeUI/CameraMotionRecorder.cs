using UnityEngine;
using UnityEngine.InputSystem;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    public class CameraMotionRecorder : MonoBehaviour
    {
        public enum State { Idle, Recording, Playing }

        [Header("三路明确绑定（禁止Camera.main自动回退）")]
        [Tooltip("录制位姿源：手柄/摄影机手持Rig/位姿采集器")]
        public Transform poseSource;
        [Tooltip("影片回放相机：片场电影机镜头")]
        public Camera filmCamera;
        [Tooltip("头显观察相机：导演VR自由视角（独立观察，不强制视角绑架）")]
        public Camera hmdObserver;

        [Header("采样与滤波设置")]
        [Range(10, 60)] public int sampleRate = 30;
        public bool smoothEnabled = true;
        [Range(3, 9)] public int smoothWindow = 5;
        public bool useSmoothedForPlayback = true;

        [Header("轨迹渲染")]
        public LineRenderer trajectoryLine;
        public Color recordColor = new Color(1f, 0.2f, 0.2f, 0.8f);
        public Color readyColor = new Color(0.2f, 0.9f, 0.3f, 0.8f);

        [Header("轨迹双缓存（原始 + 平滑）")]
        public CameraTrajectory rawTrajectory = new CameraTrajectory();
        public CameraTrajectory smoothedTrajectory = new CameraTrajectory();

        public CameraTrajectory ActiveTrajectory =>
            (useSmoothedForPlayback && smoothedTrajectory != null && smoothedTrajectory.Count > 0)
                ? smoothedTrajectory : rawTrajectory;

        public State CurrentState { get; private set; } = State.Idle;

        private float recordStartTime;
        private float nextSampleTime;
        private float playStartTime;
        private readonly InputAction recordAction = new InputAction("RecordMotion", InputActionType.Button);

        void Awake()
        {
            recordAction.AddBinding("<XRController>{RightHand}/gripPressed");
            recordAction.AddBinding("<Keyboard>/r");
            EnsureLineRenderer();
        }

        void OnEnable() => recordAction.Enable();
        void OnDisable() => recordAction.Disable();
        void OnDestroy() => recordAction.Dispose();

        void Update()
        {
            if (recordAction.WasPressedThisFrame() && CurrentState == State.Idle) StartRecording();
            else if (recordAction.WasReleasedThisFrame() && CurrentState == State.Recording) StopRecording();

            if (CurrentState == State.Recording) TickRecording();
            else if (CurrentState == State.Playing) TickPlayback();
        }

        public void StartRecording()
        {
            if (poseSource == null)
            {
                Debug.LogWarning("[运镜录制] poseSource 未绑定！已禁止 Camera.main 自动回退，拒绝录制。");
                return;
            }
            CurrentState = State.Recording;
            rawTrajectory = new CameraTrajectory();
            smoothedTrajectory = new CameraTrajectory();
            recordStartTime = Time.time;
            nextSampleTime = recordStartTime;
            UpdateLineStyle(recordColor);
            RecordSample(0f);
        }

        public void StopRecording()
        {
            if (CurrentState != State.Recording) return;
            float totalElapsed = Mathf.Max(0.001f, Time.time - recordStartTime);
            RecordSample(totalElapsed);
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
            if (filmCamera == null)
            {
                Debug.LogWarning("[运镜回放] filmCamera 未绑定！已禁止 Camera.main 自动回退，拒绝回放。");
                return;
            }
            var traj = ActiveTrajectory;
            if (traj == null || traj.Count == 0) return;
            CurrentState = State.Playing;
            playStartTime = Time.time;
        }

        public void StopPlayback()
        {
            if (CurrentState == State.Playing) CurrentState = State.Idle;
        }

        private void TickRecording()
        {
            if (Time.time >= nextSampleTime)
            {
                float elapsed = Time.time - recordStartTime;
                if (elapsed > 0.001f) RecordSample(elapsed);
                nextSampleTime = Time.time + (1f / sampleRate);
                RefreshTrajectoryLine();
            }
        }

        private void TickPlayback()
        {
            var traj = ActiveTrajectory;
            if (traj == null || traj.Count == 0 || filmCamera == null)
            {
                CurrentState = State.Idle;
                return;
            }
            float elapsed = Time.time - playStartTime;
            if (elapsed >= traj.totalDuration)
            {
                var endPoint = traj.points[traj.points.Count - 1];
                filmCamera.transform.SetPositionAndRotation(endPoint.position, endPoint.rotation);
                filmCamera.fieldOfView = endPoint.fieldOfView;
                CurrentState = State.Idle;
                return;
            }
            var sample = traj.Evaluate(elapsed);
            filmCamera.transform.SetPositionAndRotation(sample.position, sample.rotation);
            filmCamera.fieldOfView = sample.fieldOfView;
        }

        private void RecordSample(float timeOffset)
        {
            if (poseSource == null) return;
            float fov = filmCamera != null ? filmCamera.fieldOfView : 45f;
            rawTrajectory.AddPoint(timeOffset, poseSource.position, poseSource.rotation, fov);
        }

        public void ApplyToShot(ShotData shot)
        {
            if (shot == null) return;
            shot.trajectory = ActiveTrajectory?.Clone();
        }

        private void EnsureLineRenderer()
        {
            if (trajectoryLine == null) trajectoryLine = GetComponent<LineRenderer>();
            if (trajectoryLine == null)
            {
                trajectoryLine = gameObject.AddComponent<LineRenderer>();
                trajectoryLine.startWidth = 0.012f;
                trajectoryLine.endWidth = 0.012f;
                trajectoryLine.useWorldSpace = true;
                trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private void UpdateLineStyle(Color color)
        {
            if (trajectoryLine == null) return;
            trajectoryLine.startColor = color;
            trajectoryLine.endColor = color;
        }

        public void RefreshTrajectoryLine()
        {
            var traj = ActiveTrajectory;
            if (trajectoryLine == null || traj == null) return;
            int count = traj.points.Count;
            trajectoryLine.positionCount = count;
            for (int i = 0; i < count; i++)
                trajectoryLine.SetPosition(i, traj.points[i].position);
        }
    }
}
