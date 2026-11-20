using System.Collections.Generic;
using System.IO;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 空间双屏人体工学排布与离线样片兜底工具（Task C & Task B 采样器）。
    /// </summary>
    public static class SpatialStudioLayout
    {
        public static string ResolveReferenceClipPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            string path = Path.Combine(Application.streamingAssetsPath, "ReferenceClips", fileName);
            return File.Exists(path) ? path : null;
        }

        public static void ApplyDualScreenLayout(SpatialWorkbench wb, bool expandReview)
        {
            if (wb == null || wb.monitor == null || wb.monitor.monitorDisplay == null) return;
            var review = wb.GetComponent<VideoReviewPanel>();
            if (review != null)
            {
                review.ApplyMode(expandReview
                    ? ComparisonDisplayMode.SideBySideSplit
                    : ComparisonDisplayMode.PictureInPicture);
            }
        }

        public static List<byte[]> SampleTimelineFrames(
            SpatialMonitor monitor,
            DirectorTimeline timeline,
            int fps = 12,
            int maxFrames = 60)
        {
            var frames = new List<byte[]>();
            if (monitor == null || monitor.sourceCamera == null || timeline == null) return frames;

            int total = Mathf.Clamp(Mathf.RoundToInt(timeline.duration * fps), 2, maxFrames);
            float prevTime = timeline.currentTime;
            bool wasPlaying = timeline.isPlaying;
            timeline.Pause();

            try
            {
                for (int i = 0; i < total; i++)
                {
                    float norm = total > 1 ? (float)i / (total - 1) : 0f;
                    timeline.SeekNormalized(norm);
                    frames.Add(RuntimeClayCapture.Capture(monitor));
                }
            }
            finally
            {
                timeline.Seek(prevTime);
                if (wasPlaying) timeline.Play();
            }
            return frames;
        }

        public static string ExportDynamicOrStatic(
            SpatialWorkbench wb,
            ShotData shot,
            SpatialMonitor monitor,
            string outputZipPath)
        {
            var timeline = wb != null ? wb.GetComponent<DirectorTimeline>() : null;
            if (timeline != null && timeline.duration > 0.1f &&
                (timeline.cameraRecorder?.ActiveTrajectory?.Count > 1 || timeline.actorPlayer?.currentTake?.Count > 1))
            {
                var frames = SampleTimelineFrames(monitor, timeline, 12, 36);
                if (frames.Count > 1)
                {
                    ShotPackageDynamicExporter.ExportDynamicPackageToFile(shot, frames, outputZipPath, 12);
                    return $"动态参考包已导出 ({frames.Count}帧): {outputZipPath}";
                }
            }
            var clayPng = RuntimeClayCapture.Capture(monitor);
            ShotPackageExporter.ExportPackageToFile(shot, clayPng, outputZipPath);
            return "参考包已导出: " + outputZipPath;
        }
    }
}
