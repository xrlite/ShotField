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
    public partial class DirectorTimeline : MonoBehaviour
    {
        [Header("时间与状态")]
        [Min(0.1f)] public float duration = 60.0f;
        public float currentTime = 0.0f;
        public bool isPlaying = false;
        public bool loop = false;
        [Range(0.25f, 3.0f)] public float playbackSpeed = 1.0f;

        [Header("联动管线")]
        public CameraMotionRecorder cameraRecorder;
        public ActorTakePlayer actorPlayer;
        public VideoComparisonPlayer videoPlayer;
        public UnityEngine.Playables.PlayableDirector playableDirector;
        public ActorNavigator actorA;
        public ActorNavigator actorB;

        public static readonly float[] ShotCutTimes = { 0.0f, 8.0f, 17.0f, 25.0f, 34.0f, 43.0f, 52.0f, 60.0f };
        public static readonly string[] ShotNames = { "SH_01", "SH_02", "SH_03", "SH_04", "SH_05", "SH_06", "SH_07" };
        public int CurrentShotIndex => Sc01PrevisShotData.GetShotIndex(currentTime);
        public string CurrentShotId => ShotNames[CurrentShotIndex];

        public float NormalizedTime => duration > 0.001f ? Mathf.Clamp01(currentTime / duration) : 0f;

        public event Action<float> OnTimeUpdated;
        public event Action<bool> OnPlayStateChanged;

        void Awake()
        {
            AutoWireReferences();
            AutoWireExtensions();
        }
        partial void AutoWireExtensions();

        public void AutoWireReferences()
        {
            if (cameraRecorder == null) cameraRecorder = FindAnyObjectByType<CameraMotionRecorder>();
            if (actorPlayer == null) actorPlayer = FindAnyObjectByType<ActorTakePlayer>();
            if (videoPlayer == null) videoPlayer = FindAnyObjectByType<VideoComparisonPlayer>();
            if (playableDirector == null) playableDirector = FindAnyObjectByType<UnityEngine.Playables.PlayableDirector>();
            if (actorA == null)
            {
                var goA = GameObject.Find("SC01_Performer_A");
                if (goA != null) actorA = goA.GetComponent<ActorNavigator>();
            }
            if (actorB == null)
            {
                var goB = GameObject.Find("SC01_Performer_B");
                if (goB != null) actorB = goB.GetComponent<ActorNavigator>();
            }
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
    }
}
