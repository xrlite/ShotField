using UnityEngine;
using UnityEngine.InputSystem;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    public class ActorTakePlayer : MonoBehaviour
    {
        public enum PlayState { Idle, Recording, Playing }

        [Header("目标演员")]
        public Transform actorTransform;
        public string actorId = "actorA";

        [Header("当前表演 Take")]
        public ActorTake currentTake = new ActorTake();

        [Header("采样设置")]
        [Range(10, 60)] public int sampleRate = 30;

        public PlayState CurrentState { get; private set; } = PlayState.Idle;

        private readonly TimedCaptureClock clock = new TimedCaptureClock();

        void Awake()
        {
            if (actorTransform == null) actorTransform = transform;
        }

        private readonly InputAction recordTake = new InputAction("ActorTake", InputActionType.Button,
            "<XRController>{LeftHand}/gripPressed");

        void OnEnable() => recordTake.Enable();
        void OnDisable() => recordTake.Disable();
        void OnDestroy() => recordTake.Dispose();

        void Update()
        {
            if (recordTake.WasPressedThisFrame()) StartRecordingTake();
            if (recordTake.WasReleasedThisFrame()) StopRecordingTake();
            if (CurrentState == PlayState.Recording) TickRecording();
            else if (CurrentState == PlayState.Playing) AdvancePlayback(clock.PlaybackElapsed(Time.time));
        }

        public void StartRecordingTake(string takeId = "Take_01")
        {
            if (actorTransform == null) return;
            currentTake = new ActorTake { takeId = takeId, actorId = actorId };
            clock.BeginRecording(Time.time, sampleRate);
            CurrentState = PlayState.Recording;
            RecordSample(0f);
        }

        public void StopRecordingTake()
        {
            if (CurrentState != PlayState.Recording) return;
            RecordSample(clock.EndRecording(Time.time));
            CurrentState = PlayState.Idle;
        }

        public void StartPlayback()
        {
            if (currentTake == null || currentTake.Count == 0 || actorTransform == null) return;
            CurrentState = PlayState.Playing;
            clock.BeginPlayback(Time.time);
        }

        public void StopPlayback()
        {
            if (CurrentState == PlayState.Playing) CurrentState = PlayState.Idle;
        }

        private void TickRecording()
        {
            if (clock.PollSample(Time.time, sampleRate, out float elapsed))
                RecordSample(elapsed);
        }

        public void AdvancePlayback(float elapsed)
        {
            if (CurrentState != PlayState.Playing) return;
            if (actorTransform == null || currentTake == null || currentTake.Count == 0)
            {
                CurrentState = PlayState.Idle;
                return;
            }

            if (elapsed >= currentTake.duration) CurrentState = PlayState.Idle;
            var sample = currentTake.Evaluate(elapsed);
            actorTransform.SetPositionAndRotation(sample.position, sample.rotation);
        }

        private void RecordSample(float timeOffset, string action = "")
        {
            if (actorTransform == null) return;
            currentTake.AddKeyframe(timeOffset, actorTransform.position, actorTransform.rotation, action);
        }
    }
}
