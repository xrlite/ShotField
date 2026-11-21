using System;
using UnityEngine;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 最小可用导演时间轴组件：统领片场摄影机轨迹、演员Take时序与回片视频的统一Scrubber。
    /// 位于 RuntimeUI 层，协调底层 Core 数据与运行时 Actor/Camera 播放器。
    /// </summary>
    [ExecuteAlways]
    public class DirectorTimeline : MonoBehaviour
    {
        [Header("时间与状态")]
        [Min(0.1f)] public float duration = 5.0f;
        public float currentTime = 0.0f;
        public bool isPlaying = false;
        public bool loop = true;
        [Range(0.25f, 3.0f)] public float playbackSpeed = 1.0f;

        [Header("联动管线")]
        public CameraMotionRecorder cameraRecorder;
        public ActorTakePlayer actorPlayer;
        public VideoComparisonPlayer videoPlayer;

        public float NormalizedTime => duration > 0.001f ? Mathf.Clamp01(currentTime / duration) : 0f;

        public event Action<float> OnTimeUpdated;
        public event Action<bool> OnPlayStateChanged;

        void Awake() => AutoWireReferences();

        public void AutoWireReferences()
        {
            if (cameraRecorder == null) cameraRecorder = FindAnyObjectByType<CameraMotionRecorder>();
            if (actorPlayer == null) actorPlayer = FindAnyObjectByType<ActorTakePlayer>();
            if (videoPlayer == null) videoPlayer = FindAnyObjectByType<VideoComparisonPlayer>();
        }

        void Update()
        {
            if (!isPlaying) return;

            currentTime += Time.deltaTime * playbackSpeed;
            if (currentTime >= duration)
            {
                if (loop)
                {
                    currentTime = Mathf.Repeat(currentTime, duration);
                }
                else
                {
                    currentTime = duration;
                    isPlaying = false;
                    OnPlayStateChanged?.Invoke(false);
                }
            }
            ApplyCurrentFrame();
            OnTimeUpdated?.Invoke(NormalizedTime);
        }

        public void Play()
        {
            if (duration <= 0.001f) return;
            if (currentTime >= duration && !loop) currentTime = 0f;
            isPlaying = true;
            OnPlayStateChanged?.Invoke(true);
        }

        public void Pause()
        {
            isPlaying = false;
            OnPlayStateChanged?.Invoke(false);
        }

        public void TogglePlay()
        {
            if (isPlaying) Pause();
            else Play();
        }

        public void Stop()
        {
            isPlaying = false;
            currentTime = 0f;
            ApplyCurrentFrame();
            OnPlayStateChanged?.Invoke(false);
            OnTimeUpdated?.Invoke(0f);
        }

        public void Seek(float timeInSeconds)
        {
            currentTime = Mathf.Clamp(timeInSeconds, 0f, duration);
            ApplyCurrentFrame();
            OnTimeUpdated?.Invoke(NormalizedTime);
        }

        public void SeekNormalized(float normT)
        {
            Seek(Mathf.Clamp01(normT) * duration);
        }

        public void SetDuration(float newDuration)
        {
            duration = Mathf.Max(0.1f, newDuration);
            if (currentTime > duration) Seek(duration);
        }

        public void ApplyCurrentFrame()
        {
            // 1. 驱动摄影机轨迹回放
            if (cameraRecorder != null && cameraRecorder.filmCamera != null)
            {
                var traj = cameraRecorder.ActiveTrajectory;
                if (traj != null && traj.Count > 0)
                {
                    float sampleTime = NormalizedTime * traj.totalDuration;
                    var sample = traj.Evaluate(sampleTime);
                    cameraRecorder.filmCamera.transform.SetPositionAndRotation(sample.position, sample.rotation);
                    cameraRecorder.filmCamera.fieldOfView = sample.fieldOfView;
                }
            }

            // 2. 驱动演员位姿回放
            if (actorPlayer != null && actorPlayer.actorTransform != null && actorPlayer.currentTake != null)
            {
                var take = actorPlayer.currentTake;
                if (take.Count > 0)
                {
                    float sampleTime = NormalizedTime * take.duration;
                    var sample = take.Evaluate(sampleTime);
                    actorPlayer.actorTransform.SetPositionAndRotation(sample.position, sample.rotation);
                }
            }

            // 3. 驱动成片比对播放器寻道
            if (videoPlayer != null && videoPlayer.HasSource)
            {
                videoPlayer.SeekNormalized(NormalizedTime);
            }
        }
    }
}
