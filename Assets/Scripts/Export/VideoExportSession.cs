using System;
using System.IO;
using UnityEngine;

namespace ShotField.Export
{
    /// <summary>
    /// Android MediaCodec / MediaMuxer 编码器的 C# 包装（路线 1-B，buffer 回读）。
    /// 非 Android 运行时不创建原生对象；所有失败都显式抛异常或写进回执，
    /// 不允许静默产出空文件/黑屏文件。
    /// </summary>
    public sealed class VideoExportSession : IDisposable
    {
        private const string JavaClassName = "com.haoyunlai.shotfield.ShotFieldVideoEncoder";

        private AndroidJavaObject _encoder;
        private bool _configured;
        private int _frameCount;

        public bool IsConfigured => _configured;
        public int FrameCount => _frameCount;

        public static bool PlatformSupported(out string reason)
        {
            if (Application.isEditor)
            {
                reason = "编辑器内不编码；请打包到 Android 后验证导出";
                return false;
            }
            if (Application.platform != RuntimePlatform.Android)
            {
                reason = "当前平台不支持应用内 mp4 导出";
                return false;
            }
            reason = null;
            return true;
        }

        public void Begin(int width, int height, int fps, int bitrate, string fileName)
        {
            if (!PlatformSupported(out string supportReason))
                throw new PlatformNotSupportedException(supportReason);
            if (_configured) throw new InvalidOperationException("session already started");
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("file name is empty");

            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null) throw new InvalidOperationException("Android activity unavailable");

            _encoder = new AndroidJavaObject(JavaClassName, activity);
            _encoder.Call("configure", width, height, fps, bitrate, fileName);
            _configured = true;
            _frameCount = 0;
        }

        public void PushFrame(byte[] rgba, long timestampUs)
        {
            if (!_configured || _encoder == null) throw new InvalidOperationException("session not started");
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            _encoder.Call("pushFrame", rgba, timestampUs);
            _frameCount++;
        }

        /// <summary>停止编码、封装 mp4，回收原生对象；回执始终返回，不向外抛。</summary>
        public VideoExportResult Finish()
        {
            var result = new VideoExportResult();
            if (!_configured)
            {
                result.State = VideoExportState.Failed;
                result.ErrorMessage = "会话未开始";
                return result;
            }

            try
            {
                _encoder.Call("stop");
                string absolutePath = SafeCallString("getOutputPath");
                result.AbsolutePath = absolutePath ?? string.Empty;
                result.FileName = string.IsNullOrEmpty(absolutePath)
                    ? string.Empty
                    : Path.GetFileName(absolutePath);
                result.DurationMs = SafeCallLong("getDurationMs");
                result.ByteSize = SafeCallLong("getFileSizeBytes");
                result.Width = SafeCallInt("getWidth");
                result.Height = SafeCallInt("getHeight");
                result.FrameCount = SafeCallInt("getFrameCount");

                string javaError = SafeCallString("getError");
                if (result.ByteSize <= 0 || result.FrameCount <= 0)
                {
                    result.State = VideoExportState.Failed;
                    result.ErrorMessage = string.IsNullOrEmpty(javaError)
                        ? "编码器未产出有效画面（0 帧或 0 字节）"
                        : javaError;
                }
                else
                {
                    result.State = VideoExportState.Done;
                }
            }
            catch (Exception ex)
            {
                result.State = VideoExportState.Failed;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                Dispose();
            }
            return result;
        }

        public void Dispose()
        {
            if (_encoder != null)
            {
                try { _encoder.Call("release"); } catch { /* 回收失败不掩盖原错误 */ }
                _encoder.Dispose();
                _encoder = null;
            }
            _configured = false;
        }

        private string SafeCallString(string method)
        {
            try { return _encoder.Call<string>(method); } catch { return null; }
        }

        private long SafeCallLong(string method)
        {
            try { return _encoder.Call<long>(method); } catch { return 0L; }
        }

        private int SafeCallInt(string method)
        {
            try { return _encoder.Call<int>(method); } catch { return 0; }
        }
    }
}
