#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>Unity Recorder 5.1.8 的最小公共会话；M1 与 P6 共用，避免两套编码配置漂移。</summary>
    public static class ShotFieldRecorder
    {
        public static RecorderController Start(string outputPath, int width = 1920, int height = 1080,
            float fps = 30f, float bitrateMbps = 8f)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Recorder PrepareRecording 必须在 Play Mode 内调用");
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("outputPath 不能为空");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.FrameRate = fps;
            settings.CapFrameRate = true;
            settings.ExitPlayMode = false;
            settings.SetRecordModeToManual();

            var movie = new MovieRecorderSettings
            {
                Enabled = true,
                RecordMode = RecordMode.Manual,
                OutputFile = Path.ChangeExtension(outputPath, null),
                FrameRate = fps,
                ImageInputSettings = new GameViewInputSettings
                {
                    OutputWidth = width,
                    OutputHeight = height
                },
                EncoderSettings = new CoreEncoderSettings
                {
                    Codec = CoreEncoderSettings.OutputCodec.MP4,
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                    TargetBitRate = bitrateMbps
                }
            };
            settings.AddRecorderSettings(movie);

            var controller = new RecorderController(settings);
            controller.PrepareRecording();
            if (!controller.StartRecording())
                throw new InvalidOperationException("Unity Recorder 启动失败");
            return controller;
        }

        public static void Stop(RecorderController controller)
        {
            if (controller != null && controller.IsRecording()) controller.StopRecording();
        }
    }
}
#endif
