using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    public class CameraMotionRecorder : MonoBehaviour
    {
        public enum State { Idle, Recording, Playing }

        [Header("目标相机")]
        public Camera targetCamera;

        [Header("轨迹设置")]
        [Range(10, 60)] public int sampleRate = 30;
        public bool autoSmoothOnStop = true;
        [Range(3, 9)] public int smoothWindow = 5;

        [Header("轨迹渲染")]
        public LineRenderer trajectoryLine;
        public Color recordColor = new Color(1f, 0.2f, 0.2f, 0.8f);
        public Color readyColor = new Color(0.2f, 0.9f, 0.3f, 0.8f);

        [Header("当前轨迹数据")]
        public CameraTrajectory currentTrajectory = new CameraTrajectory();

        public State CurrentState { get; private set; } = State.Idle;

        private float recordStartTime;
        private float nextSampleTime;
        private float playStartTime;
        private readonly InputAction recordAction = new InputAction("RecordMotion", InputActionType.Button);

        void Awake()
        {
            recordAction.AddBinding("<XRController>{RightHand}/gripPressed");
            recordAction.AddBinding("<Keyboard>/r");
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) targetCamera = Camera.main;
            EnsureLineRenderer();
        }

        void OnEnable() => recordAction.Enable();
        void OnDisable() => recordAction.Disable();
        void OnDestroy() => recordAction.Dispose();

        void Update()
        {
            if (recordAction.WasPressedThisFrame() && CurrentState == State.Idle)
                StartRecording();
            else if (recordAction.WasReleasedThisFrame() && CurrentState == State.Recording)
                StopRecording();

            if (CurrentState == State.Recording)
                TickRecording();
            else if (CurrentState == State.Playing)
                TickPlayback();
        }

        public void StartRecording()
        {
            if (targetCamera == null) return;
            CurrentState = State.Recording;
            currentTrajectory = new CameraTrajectory();
            recordStartTime = Time.time;
            nextSampleTime = recordStartTime;
            UpdateLineStyle(recordColor);
            RecordSample(0f);
        }

        public void StopRecording()
        {
            if (CurrentState != State.Recording) return;
            RecordSample(Time.time - recordStartTime);
            CurrentState = State.Idle;
            if (autoSmoothOnStop && currentTrajectory.Count > 2)
                currentTrajectory = currentTrajectory.Smooth(smoothWindow);
            UpdateLineStyle(readyColor);
            RefreshTrajectoryLine();
        }

        public void StartPlayback()
        {
            if (currentTrajectory == null || currentTrajectory.Count == 0 || targetCamera == null) return;
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
                RecordSample(Time.time - recordStartTime);
                nextSampleTime = Time.time + (1f / sampleRate);
                RefreshTrajectoryLine();
            }
        }

        private void TickPlayback()
        {
            float elapsed = Time.time - playStartTime;
            if (elapsed > currentTrajectory.totalDuration)
            {
                CurrentState = State.Idle;
                return;
            }
            var sample = currentTrajectory.Evaluate(elapsed);
            targetCamera.transform.SetPositionAndRotation(sample.position, sample.rotation);
            targetCamera.fieldOfView = sample.fieldOfView;
        }

        private void RecordSample(float timeOffset)
        {
            if (targetCamera == null) return;
            var t = targetCamera.transform;
            currentTrajectory.AddPoint(timeOffset, t.position, t.rotation, targetCamera.fieldOfView);
        }

        private void EnsureLineRenderer()
        {
            if (trajectoryLine == null) trajectoryLine = GetComponent<LineRenderer>();
            if (trajectoryLine == null)
            {
                trajectoryLine = gameObject.AddComponent<LineRenderer>();
                trajectoryLine.startWidth = 0.015f;
                trajectoryLine.endWidth = 0.015f;
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
            if (trajectoryLine == null || currentTrajectory == null) return;
            int count = currentTrajectory.points.Count;
            trajectoryLine.positionCount = count;
            for (int i = 0; i < count; i++)
                trajectoryLine.SetPosition(i, currentTrajectory.points[i].position);
        }
    }
}
