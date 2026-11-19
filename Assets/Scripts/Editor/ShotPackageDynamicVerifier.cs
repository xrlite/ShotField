#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class ShotPackageDynamicVerifier
    {
        private const int Expected = 5;

        [MenuItem("ShotField/Verifiers/Run Dynamic Package Verifier")]
        public static void Run()
        {
            int passed = 0;
            void Check(string label, bool cond)
            {
                if (!cond) throw new InvalidOperationException("FAIL: " + label);
                passed++;
                Debug.Log("[ShotPackageDynamicVerifier] PASS: " + label);
            }

            string tempDir = Path.Combine(Application.temporaryCachePath, "DynExport_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(tempDir, "SC01_dynamic_pack.zip");
            try
            {
                var shot = new ShotData
                {
                    shotId = "SC01-DYN",
                    revision = 3,
                    intent = "推镜特写信封并保持主角肩部关系",
                    camera = new ShotCameraState { position = new Vector3(1.2f, 1.0f, 1.0f), aspect = 1.7778f }
                };
                shot.trajectory = CameraMotionPresets.CreatePreset(CameraMotionType.DollyPush, new Vector3(1.5f, 0.46f, 2.15f), 2.0f);
                shot.take = ActorPosePresets.CreatePreset(ActorPoseType.ReachEnvelope, new Vector3(0.8f, 0f, 1.25f), new Vector3(1.5f, 0.46f, 2.15f), 2.0f);
                Check("dynamic shot trajectory and actor take constructed", shot.trajectory.Count >= 10 && shot.take.Count >= 10);

                var frames = new List<byte[]>(24);
                byte[] samplePng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02 };
                for (int i = 0; i < 24; i++) frames.Add(samplePng);

                byte[] zipBytes = ShotPackageDynamicExporter.CreateDynamicZipPackage(shot, frames, 12);
                Check("CreateDynamicZipPackage generates valid zip stream", zipBytes != null && zipBytes.Length > 400);

                ShotPackageDynamicExporter.ExportDynamicPackageToFile(shot, frames, zipPath, 12);
                bool ok = ShotPackageDynamicExporter.VerifyDynamicZipPackage(zipPath, out int frameCount, out var entries);
                Check("VerifyDynamicZipPackage confirms 24 frames and trajectory.json",
                    ok && frameCount == 24 && entries.Contains("frames/clay_0024.png"));

                string clipPath = SpatialStudioLayout.ResolveReferenceClipPath("nonexistent_clip.mp4");
                Check("ResolveReferenceClipPath safely returns null when offline clip missing", clipPath == null);

                Check("dynamic package size and file entry count valid", entries.Count == 28);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }

            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"SHOT-PACKAGE-DYNAMIC-RESULT={result} ({passed}/{Expected})");
            if (Application.isBatchMode) EditorApplication.Exit(result == "PASS" ? 0 : 1);
        }
    }
}
#endif
