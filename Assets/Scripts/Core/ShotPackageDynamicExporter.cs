using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace ShotField.Core
{
    [Serializable]
    public class DynamicPackageManifest
    {
        public string shotId;
        public int revision;
        public string exportedAt;
        public string intent;
        public float aspect;
        public float duration;
        public int fps;
        public int frameCount;
        public List<string> files = new List<string>();
    }

    /// <summary>
    /// 动态参考包导出器（Task B）：
    /// 打包护照 JSON、AI 提示词、相机与演员轨迹 JSON、以及 Clay 序列帧 PNG 进 .zip。
    /// </summary>
    public static class ShotPackageDynamicExporter
    {
        public static byte[] CreateDynamicZipPackage(
            ShotData shot,
            IReadOnlyList<byte[]> clayFrames,
            int fps = 12)
        {
            if (shot == null) throw new ArgumentNullException(nameof(shot));
            int safeFps = Mathf.Clamp(fps, 1, 60);
            int count = clayFrames != null ? clayFrames.Count : 0;

            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                float duration = shot.trajectory != null && shot.trajectory.totalDuration > 0f
                    ? shot.trajectory.totalDuration
                    : (shot.take != null && shot.take.duration > 0f ? shot.take.duration : 5.0f);

                var manifest = new DynamicPackageManifest
                {
                    shotId = shot.shotId,
                    revision = shot.revision,
                    exportedAt = DateTimeOffset.UtcNow.ToString("O"),
                    intent = shot.intent,
                    aspect = shot.camera != null ? shot.camera.aspect : 1.7778f,
                    duration = duration,
                    fps = safeFps,
                    frameCount = count
                };

                var dto = ShotPassportMapper.FromShot(shot, "SC01", shot.recordedConstraints);
                ShotPackageExporter.AddTextEntry(archive, "passport.json", JsonUtility.ToJson(dto, true));
                manifest.files.Add("passport.json");

                string prompt = $"# Dynamic Shot Spec: {shot.shotId} rev{shot.revision}\n" +
                                $"Duration: {duration:F1}s | FPS: {safeFps} | Frames: {count}\n" +
                                $"Intent: {shot.intent}\n";
                ShotPackageExporter.AddTextEntry(archive, "generation_prompt.txt", prompt);
                manifest.files.Add("generation_prompt.txt");

                string trajJson = JsonUtility.ToJson(shot.trajectory ?? new CameraTrajectory(), true);
                ShotPackageExporter.AddTextEntry(archive, "trajectory.json", trajJson);
                manifest.files.Add("trajectory.json");

                for (int i = 0; i < count; i++)
                {
                    byte[] frame = clayFrames[i];
                    if (frame == null || frame.Length == 0) continue;
                    string entryName = $"frames/clay_{i + 1:D4}.png";
                    var imgEntry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
                    using (var s = imgEntry.Open()) s.Write(frame, 0, frame.Length);
                    manifest.files.Add(entryName);
                }

                ShotPackageExporter.AddTextEntry(archive, "manifest.json", JsonUtility.ToJson(manifest, true));
                manifest.files.Add("manifest.json");
            }
            return memoryStream.ToArray();
        }

        public static string ExportDynamicPackageToFile(
            ShotData shot,
            IReadOnlyList<byte[]> clayFrames,
            string outputZipPath,
            int fps = 12)
        {
            if (string.IsNullOrEmpty(outputZipPath)) throw new ArgumentNullException(nameof(outputZipPath));
            string dir = Path.GetDirectoryName(outputZipPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            byte[] zipData = CreateDynamicZipPackage(shot, clayFrames, fps);
            File.WriteAllBytes(outputZipPath, zipData);
            return outputZipPath;
        }

        public static bool VerifyDynamicZipPackage(string zipPath, out int frameCount, out List<string> entries)
        {
            frameCount = 0;
            entries = new List<string>();
            if (!File.Exists(zipPath)) return false;
            try
            {
                using var fs = File.OpenRead(zipPath);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
                foreach (var e in archive.Entries)
                {
                    entries.Add(e.FullName);
                    if (e.FullName.StartsWith("frames/clay_") && e.FullName.EndsWith(".png"))
                        frameCount++;
                }
                return entries.Contains("passport.json")
                    && entries.Contains("manifest.json")
                    && entries.Contains("trajectory.json")
                    && frameCount > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
