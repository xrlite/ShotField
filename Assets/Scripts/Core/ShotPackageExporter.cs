using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 镜头生成参考包导出器（系统 9 影视生成与生态基础服务）。
    /// 将镜头护照 JSON、白模静帧 PNG、AI 提示词清单打包为标准 .zip 参考包，供 Seedance 等模型消费。
    /// 纯 C# 实现，基于 System.IO.Compression.ZipArchive，无平台特定依赖。
    /// </summary>
    public static class ShotPackageExporter
    {
        [Serializable]
        public class PackageManifest
        {
            public string shotId;
            public int revision;
            public string exportedAt;
            public string intent;
            public float aspect;
            public float duration;
            public List<string> files = new List<string>();
        }

        public static byte[] CreateZipPackage(ShotData shot, byte[] imageBytes = null, string imageName = "clay_frame.png")
        {
            if (shot == null) throw new ArgumentNullException(nameof(shot));

            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                var manifest = new PackageManifest
                {
                    shotId = shot.shotId,
                    revision = shot.revision,
                    exportedAt = DateTimeOffset.UtcNow.ToString("O"),
                    intent = shot.intent,
                    aspect = shot.camera != null ? shot.camera.aspect : 1.7778f,
                    duration = shot.trajectory != null ? shot.trajectory.totalDuration : (shot.take != null ? shot.take.duration : 3.0f)
                };

                // 1. 护照核心数据 passport.json
                var dto = ShotPassportMapper.FromShot(shot, "SC01", shot.recordedConstraints);
                string passportJson = JsonUtility.ToJson(dto, true);
                AddTextEntry(archive, "passport.json", passportJson);
                manifest.files.Add("passport.json");

                // 2. AI 生成提示词描述 generation_prompt.txt
                string prompt = BuildGenerationPrompt(shot, manifest.duration);
                AddTextEntry(archive, "generation_prompt.txt", prompt);
                manifest.files.Add("generation_prompt.txt");

                // 3. 静帧/参考图片（可选）
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    string safeImageName = string.IsNullOrEmpty(imageName) ? "clay_frame.png" : imageName;
                    var imgEntry = archive.CreateEntry(safeImageName, System.IO.Compression.CompressionLevel.Optimal);
                    using (var entryStream = imgEntry.Open())
                    {
                        entryStream.Write(imageBytes, 0, imageBytes.Length);
                    }
                    manifest.files.Add(safeImageName);
                }

                // 4. 清单清单 manifest.json
                string manifestJson = JsonUtility.ToJson(manifest, true);
                AddTextEntry(archive, "manifest.json", manifestJson);
                manifest.files.Add("manifest.json");
            }

            return memoryStream.ToArray();
        }

        public static string ExportPackageToFile(ShotData shot, byte[] imageBytes, string outputZipPath, string imageName = "clay_frame.png")
        {
            if (string.IsNullOrEmpty(outputZipPath)) throw new ArgumentNullException(nameof(outputZipPath));
            string dir = Path.GetDirectoryName(outputZipPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            byte[] zipData = CreateZipPackage(shot, imageBytes, imageName);
            File.WriteAllBytes(outputZipPath, zipData);
            return outputZipPath;
        }

        public static bool VerifyZipPackage(string zipPath, out List<string> entries)
        {
            entries = new List<string>();
            if (!File.Exists(zipPath)) return false;

            try
            {
                using var fileStream = File.OpenRead(zipPath);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
                foreach (var entry in archive.Entries)
                {
                    entries.Add(entry.FullName);
                }
                return entries.Contains("passport.json") && entries.Contains("manifest.json");
            }
            catch
            {
                return false;
            }
        }

        internal static void AddTextEntry(ZipArchive archive, string entryName, string textContent)
        {
            var entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            byte[] bytes = Encoding.UTF8.GetBytes(textContent ?? "");
            entryStream.Write(bytes, 0, bytes.Length);
        }

        private static string BuildGenerationPrompt(ShotData shot, float duration)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Shot Reference Spec: {shot.shotId} (Revision {shot.revision})");
            sb.AppendLine($"Duration: {duration:F1}s");
            sb.AppendLine($"Aspect Ratio: {shot.camera?.aspect:F4}");
            sb.AppendLine($"Field of View: {shot.camera?.fieldOfView:F1} deg");
            sb.AppendLine($"Camera Position: {shot.camera?.position}");
            sb.AppendLine($"Director Intent: {shot.intent}");
            if (shot.trajectory != null && shot.trajectory.Count > 0)
            {
                sb.AppendLine($"Motion Type: Handheld 6DoF Camera Trajectory ({shot.trajectory.Count} key points)");
            }
            if (shot.take != null && shot.take.Count > 0)
            {
                sb.AppendLine($"Actor Action: {shot.take.takeId} for {shot.take.actorId} ({shot.take.Count} keyframes)");
            }
            return sb.ToString();
        }
    }
}
