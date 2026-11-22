#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShotField.Export.EditorTools
{
    /// <summary>
    /// 编辑器内一键静态自测：插件产物在位、帧缓冲格式正确、回执 DTO 文案齐全、
    /// 平台守卫生效。不编码、不产出 mp4；真机/模拟器编码链路待打包后验证。
    /// </summary>
    public static class VideoExportSelfTest
    {
        private const string JarRelative =
            "Assets/Plugins/Android/libs/shotfield-video-encoder.jar";
        private const string JavaRelative =
            "Assets/Plugins/Android/source-java/com/haoyunlai/shotfield/ShotFieldVideoEncoder.java";

        [MenuItem("ShotField/Export/MP4 链路自测（编辑器）")]
        public static void Run()
        {
            var logs = new System.Collections.Generic.List<string>();
            try
            {
                CheckPluginArtifacts(logs);
                CheckFrameBuffer();
                CheckResultDto();
                CheckPlatformGuard();
                logs.Add("SELF-TEST PASS：mp4 导出链路静态检查通过（真机/模拟器编码未验证）");
            }
            catch (Exception ex)
            {
                logs.Add("SELF-TEST FAIL：" + ex.Message);
                Debug.LogError(string.Join("\n", logs));
                throw;
            }
            Debug.Log(string.Join("\n", logs));
        }

        private static void CheckPluginArtifacts(System.Collections.Generic.List<string> logs)
        {
            string jarPath = Path.GetFullPath(JarRelative);
            if (!File.Exists(jarPath)) throw new FileNotFoundException("缺少编码器 jar：" + jarPath);
            var jarInfo = new FileInfo(jarPath);
            if (jarInfo.Length <= 0) throw new InvalidOperationException("编码器 jar 为 0 字节");

            string javaPath = Path.GetFullPath(JavaRelative);
            if (!File.Exists(javaPath)) throw new FileNotFoundException("缺少 Java 源码：" + javaPath);
            string source = File.ReadAllText(javaPath);
            if (!source.Contains("MediaMuxer")) throw new InvalidOperationException("Java 源码未引用 MediaMuxer");
            if (!source.Contains("MIMETYPE_VIDEO_AVC")) throw new InvalidOperationException("Java 源码未配置 AVC");

            logs.Add($"[1] 插件在位：jar {jarInfo.Length} 字节，Java 源码 {source.Length} 字符");
        }

        private static void CheckFrameBuffer()
        {
            const int w = 16;
            const int h = 16;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    float t = (float)i / pixels.Length;
                    pixels[i] = new Color(t, 1f - t, 0.5f, 1f);
                }
                texture.SetPixels(pixels);
                texture.Apply(false);

                byte[] raw = texture.GetRawTextureData<byte>().ToArray();
                if (raw.Length != w * h * 4)
                    throw new InvalidOperationException($"RGBA 缓冲长度异常：{raw.Length}");
                if (raw.Distinct().Count() <= 1)
                    throw new InvalidOperationException("测试帧是纯色，缓冲通路可疑");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void CheckResultDto()
        {
            var done = new VideoExportResult
            {
                State = VideoExportState.Done,
                FileName = "shotfield_test.mp4",
                DurationMs = 5000,
                ByteSize = 123456,
                FrameCount = 120
            };
            if (!done.Ok) throw new InvalidOperationException("有效回执未判定为 Ok");
            if (!done.Summary.Contains("已保存") || !done.Summary.Contains("shotfield_test.mp4"))
                throw new InvalidOperationException("成功回执文案缺文件名");

            var failed = new VideoExportResult
            {
                State = VideoExportState.Failed,
                ErrorMessage = "编码器初始化失败：boom"
            };
            if (failed.Ok) throw new InvalidOperationException("失败回执被误判为 Ok");
            if (!failed.Summary.Contains("导出失败")) throw new InvalidOperationException("失败回执文案缺原因");
        }

        private static void CheckPlatformGuard()
        {
            if (VideoExportSession.PlatformSupported(out string reason))
                throw new InvalidOperationException("编辑器内不应判定为支持编码");
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("平台守卫未给出不支持原因");
        }
    }
}
#endif
