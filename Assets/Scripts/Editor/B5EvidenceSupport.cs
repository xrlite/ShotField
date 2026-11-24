#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using ShotField.Core;
using UnityEngine;
namespace ShotField.EditorTools
{
    internal static class B5EvidenceSupport
    {
        [Serializable] internal sealed class EvidenceReport
        {
            public string status, error, templateCamera; public int fps, frames, rawSamples, smoothSamples;
            public float duration, jitterRaw, jitterSmooth, retimeBeforeDuration, retimeAfterDuration, seekNormalized, sampleTime, cameraPositionError;
            public EvidencePaths outputPaths;
        }
        [Serializable] internal sealed class EvidencePaths
        {
            public string framesDirectory, framePattern, rawSmoothedComparison, smoothedFrame, report;
        }
        internal static void SetPose(Transform pose, float t, int i)
        {
            float u = t / 12f;
            float angle = Mathf.Lerp(-1f, 1f, u);
            var target = new Vector3(1.5f, 0.8f, 2.15f);
            var jitter = new Vector3(Mathf.Sin(i * 1.7f), Mathf.Cos(i * 1.3f), Mathf.Sin(i * 0.9f)) * 0.025f;
            pose.position = target + new Vector3(Mathf.Cos(angle) * 1.2f,
                1.35f + Mathf.Sin(u * Mathf.PI * 2f) * 0.12f, Mathf.Sin(angle) * 1.2f) + jitter;
            pose.LookAt(target);
        }
        internal static float Jitter(CameraTrajectory trajectory)
        {
            if (trajectory == null || trajectory.Count < 3) return 0f;
            float sum = 0f;
            for (int i = 2; i < trajectory.Count; i++)
            {
                var second = trajectory.points[i].position - 2f * trajectory.points[i - 1].position + trajectory.points[i - 2].position;
                sum += second.magnitude;
            }
            return sum / (trajectory.Count - 2);
        }
        internal static string CreateOutputDirectory(string stamp)
        {
            string month = Path.Combine(FindEvidenceRoot(), DateTime.Now.ToString("yyyy-MM"));
            Directory.CreateDirectory(month);
            string output = Path.Combine(month, $"B5_frames_{stamp}");
            if (Directory.Exists(output)) throw new InvalidOperationException("Output directory already exists; no files were overwritten.");
            Directory.CreateDirectory(output);
            return output;
        }
        internal static string FindEvidenceRoot()
        {
            var current = new DirectoryInfo(Path.GetFullPath(Application.dataPath));
            while (current != null)
            {
                bool workspace = false;
                foreach (var child in current.GetDirectories())
                    if (child.Name.StartsWith("04_", StringComparison.Ordinal)) workspace = true;
                if (workspace)
                {
                    string found = null;
                    foreach (var match in Directory.GetDirectories(current.FullName, "05_*"))
                        if (File.Exists(Path.Combine(match, "CHANGELOG.md")))
                        {
                            if (found != null) throw new InvalidOperationException("Multiple CHANGELOG evidence roots found.");
                            found = match;
                        }
                    if (found != null) return found;
                }
                current = current.Parent;
            }
            throw new InvalidOperationException("No workspace 05_* evidence root found above Application.dataPath.");
        }
        internal static void TryWriteFailureReport(string directory, string path, string error)
        {
            if (directory == null || path == null) return;
            try
            {
                var report = new EvidenceReport
                {
                    status = "FAIL", error = error,
                    outputPaths = new EvidencePaths { framesDirectory = directory, report = path }
                };
                File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            catch (Exception ex) { Debug.LogError("[B5-EVIDENCE] Could not write failure report: " + ex.Message); }
        }
    }
}
#endif
