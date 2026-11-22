using System;
using UnityEngine;
using ShotField.Core;

namespace ShotField.Export
{
    /// <summary>
    /// 固定机位录制成片（首版：720p、24fps、5 秒、无声轨）。
    /// 绑定相机或 RenderTexture，按目标帧率抓 RGBA 帧喂编码器；停止后回执
    /// 文件名 / 绝对路径 / 时长 / 体积。失败显式上报，不产出假文件。
    /// </summary>
    public sealed class VideoRecorderController : MonoBehaviour
    {
        [Header("Source（二选一，优先 RenderTexture）")]
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private RenderTexture sourceTexture;

        [Header("Output")]
        [SerializeField] private int width = 1280;
        [SerializeField] private int height = 720;
        [SerializeField] private int targetFps = 24;
        [SerializeField] private float durationSeconds = 5f;
        [SerializeField] private int bitrate = 4_000_000;

        private VideoExportSession _session;
        private TimedCaptureClock _clock;
        private VideoExportState _state = VideoExportState.Idle;
        private Texture2D _readBuffer;
        private float _startTime;

        public VideoExportResult LastResult { get; private set; }
        public bool IsRecording => _state == VideoExportState.Recording;

        public event Action<VideoExportResult> OnCompleted;
        public event Action<string> OnFailed;

        public void StartVideoRecording()
        {
            if (_state == VideoExportState.Recording || _state == VideoExportState.Processing) return;

            string sourceError = ValidateSource();
            if (sourceError != null) { Fail(sourceError); return; }
            if (!VideoExportSession.PlatformSupported(out string supportReason)) { Fail(supportReason); return; }

            var fileName = VideoExportPaths.BuildFileName(DateTime.Now);
            _session = new VideoExportSession();
            try
            {
                _session.Begin(width, height, targetFps, bitrate, fileName);
            }
            catch (Exception ex)
            {
                _session.Dispose();
                _session = null;
                Fail("编码器初始化失败：" + ex.Message);
                return;
            }

            _clock = new TimedCaptureClock();
            _clock.BeginRecording(Time.unscaledTime, targetFps);
            _startTime = Time.unscaledTime;
            _state = VideoExportState.Recording;
            LastResult = null;
        }

        public void StopVideoRecording()
        {
            if (_state != VideoExportState.Recording) return;
            _state = VideoExportState.Processing;
            FinishSession();
        }

        private void Update()
        {
            if (_state != VideoExportState.Recording) return;
            if (Time.unscaledTime - _startTime >= durationSeconds)
            {
                StopVideoRecording();
                return;
            }
            if (!_clock.PollSample(Time.unscaledTime, targetFps, out _)) return;
            CaptureAndPush();
        }

        private void CaptureAndPush()
        {
            byte[] rgba;
            try { rgba = ReadRgba(); }
            catch (Exception ex) { Fail("抓帧失败：" + ex.Message); return; }

            if (rgba == null || rgba.Length == 0) { Fail("抓帧为空"); return; }

            long us = (long)((Time.unscaledTime - _startTime) * 1_000_000.0);
            try { _session.PushFrame(rgba, us); }
            catch (Exception ex) { Fail("喂帧失败：" + ex.Message); }
        }

        private byte[] ReadRgba()
        {
            RenderTexture rt = sourceTexture;
            RenderTexture temporary = null;
            if (rt == null && sourceCamera != null)
            {
                temporary = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                Camera backup = sourceCamera;
                RenderTexture prevTarget = backup.targetTexture;
                backup.targetTexture = temporary;
                backup.Render();
                backup.targetTexture = prevTarget;
                rt = temporary;
            }
            if (rt == null) return null;

            if (rt.width != width || rt.height != height)
                throw new InvalidOperationException(
                    $"源纹理分辨率 {rt.width}x{rt.height} 与导出尺寸 {width}x{height} 不一致");

            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                EnsureReadBuffer(rt.width, rt.height);
                _readBuffer.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                _readBuffer.Apply(false);
                return _readBuffer.GetRawTextureData<byte>().ToArray();
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private void EnsureReadBuffer(int w, int h)
        {
            if (_readBuffer != null && _readBuffer.width == w && _readBuffer.height == h) return;
            if (_readBuffer != null) Destroy(_readBuffer);
            _readBuffer = new Texture2D(w, h, TextureFormat.RGBA32, false);
        }

        private void FinishSession()
        {
            VideoExportResult result;
            try { result = _session.Finish(); }
            catch (Exception ex)
            {
                result = new VideoExportResult
                {
                    State = VideoExportState.Failed,
                    ErrorMessage = ex.Message
                };
            }
            _session?.Dispose();
            _session = null;
            LastResult = result;

            if (result.Ok)
            {
                _state = VideoExportState.Done;
                OnCompleted?.Invoke(result);
            }
            else
            {
                _state = VideoExportState.Failed;
                OnFailed?.Invoke(result.ErrorMessage);
            }
        }

        private string ValidateSource()
        {
            if (sourceCamera == null && sourceTexture == null) return "未绑定相机或 RenderTexture";
            if (width <= 0 || height <= 0) return "导出分辨率非法";
            if (targetFps <= 0) return "帧率非法";
            if (bitrate <= 0) return "码率非法";
            return null;
        }

        private void Fail(string message)
        {
            _state = VideoExportState.Failed;
            LastResult = new VideoExportResult { State = VideoExportState.Failed, ErrorMessage = message };
            _session?.Dispose();
            _session = null;
            OnFailed?.Invoke(message);
        }

        private void OnDestroy()
        {
            _session?.Dispose();
            _session = null;
            if (_readBuffer != null) Destroy(_readBuffer);
        }
    }
}
