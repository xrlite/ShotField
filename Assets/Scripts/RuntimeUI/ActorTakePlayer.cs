using UnityEngine;
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

        private float recordStartTime;
        private float nextSampleTime;
        private float playStartTime;

        void Awake()
        {
            if (actorTransform == null) actorTransform = transform;
        }

        void Update()
        {
            if (CurrentState == PlayState.Recording) TickRecording();
            else if (CurrentState == PlayState.Playing) TickPlayback();
        }

        public void StartRecordingTake(string takeId = "Take_01")
        {
            if (actorTransform == null) return;
            currentTake = new ActorTake { takeId = takeId, actorId = actorId };
            recordStartTime = Time.time;
            nextSampleTime = recordStartTime;
            CurrentState = PlayState.Recording;
            RecordSample(0f);
        }

        public void StopRecordingTake()
        {
            if (CurrentState != PlayState.Recording) return;
            float elapsed = Mathf.Max(0.001f, Time.time - recordStartTime);
            RecordSample(elapsed);
            CurrentState = PlayState.Idle;
        }

        public void StartPlayback(bool loop = false)
        {
            if (currentTake == null || currentTake.Count == 0 || actorTransform == null) return;
            CurrentState = PlayState.Playing;
            playStartTime = Time.time;
        }

        public void StopPlayback()
        {
            if (CurrentState == PlayState.Playing) CurrentState = PlayState.Idle;
        }

        private void TickRecording()
        {
            if (Time.time >= nextSampleTime)
            {
                float elapsed = Time.time - recordStartTime;
                if (elapsed > 0.001f) RecordSample(elapsed);
                nextSampleTime = Time.time + (1f / sampleRate);
            }
        }

        private void TickPlayback()
        {
            if (actorTransform == null || currentTake == null || currentTake.Count == 0)
            {
                CurrentState = PlayState.Idle;
                return;
            }

            float elapsed = Time.time - playStartTime;
            if (elapsed >= currentTake.duration)
            {
                var endK = currentTake.keyframes[currentTake.keyframes.Count - 1];
                actorTransform.SetPositionAndRotation(endK.position, endK.rotation);
                CurrentState = PlayState.Idle;
                return;
            }

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
