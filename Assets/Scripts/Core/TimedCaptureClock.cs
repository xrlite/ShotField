using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 通用时序采集与回放时钟（消除 CameraMotionRecorder 与 ActorTakePlayer 的状态机重复）。
    /// </summary>
    public sealed class TimedCaptureClock
    {
        private float _recordStartTime;
        private float _nextSampleTime;
        private float _playStartTime;

        public void BeginRecording(float now, int sampleRate)
        {
            int rate = Mathf.Max(1, sampleRate);
            _recordStartTime = now;
            _nextSampleTime = now + 1f / rate;
        }

        public bool PollSample(float now, int sampleRate, out float elapsed)
        {
            if (now < _nextSampleTime)
            {
                elapsed = 0f;
                return false;
            }
            int rate = Mathf.Max(1, sampleRate);
            elapsed = now - _recordStartTime;
            _nextSampleTime = _recordStartTime + (Mathf.Floor(elapsed * rate) + 1f) / rate;
            return elapsed > 0.001f;
        }

        public float EndRecording(float now) => Mathf.Max(0.001f, now - _recordStartTime);

        public void BeginPlayback(float now) => _playStartTime = now;

        public float PlaybackElapsed(float now) => Mathf.Max(0f, now - _playStartTime);
    }
}
