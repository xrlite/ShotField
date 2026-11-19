using System;
using UnityEngine;
using UnityEngine.Video;

namespace ShotField.Core
{
    public enum ComparisonDisplayMode { FullVideo, SideBySideSplit, PictureInPicture }

    /// <summary>
    /// 片场成片回看与白模对照播放器 (VideoPlayer 驱动)。
    /// 允许导演在 VR 片场内实时播放 Seedance 生产出的成片视频，并与白模机位进行分屏比对。
    /// </summary>
    [ExecuteAlways]
    public class VideoComparisonPlayer : MonoBehaviour
    {
        [Header("视频源")]
        public VideoPlayer videoPlayer;
        public Camera referenceCamera;

        [Header("比对输出")]
        public RenderTexture comparisonOutput;
        public ComparisonDisplayMode displayMode = ComparisonDisplayMode.SideBySideSplit;

        [Range(0f, 1f)] public float splitRatio = 0.5f;

        public bool IsPlaying => videoPlayer != null && videoPlayer.isPlaying;
        public double CurrentTime => videoPlayer != null ? videoPlayer.time : 0.0;
        public double Duration => videoPlayer != null ? videoPlayer.length : 0.0;
        public bool HasSource => videoPlayer != null &&
            (videoPlayer.clip != null || !string.IsNullOrWhiteSpace(videoPlayer.url));

        void Awake() => Initialize();

        public void Initialize()
        {
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
                if (videoPlayer == null) videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            if (comparisonOutput == null)
            {
                comparisonOutput = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                comparisonOutput.name = "VideoComparison_RT";
            }
            videoPlayer.targetTexture = comparisonOutput;
        }

        public void LoadVideo(string pathOrUrl)
        {
            if (string.IsNullOrEmpty(pathOrUrl)) return;
            videoPlayer.url = pathOrUrl;
            videoPlayer.Prepare();
        }

        public void Play()
        {
            if (videoPlayer != null) videoPlayer.Play();
        }

        public void Pause()
        {
            if (videoPlayer != null) videoPlayer.Pause();
        }

        public void Stop()
        {
            if (videoPlayer != null) videoPlayer.Stop();
        }

        public void SeekNormalized(float t)
        {
            if (videoPlayer == null || Duration <= 0.0) return;
            double targetTime = Math.Clamp(t, 0f, 1f) * Duration;
            videoPlayer.time = targetTime;
        }

        public void SetMode(ComparisonDisplayMode mode)
        {
            displayMode = mode;
        }
    }
}
