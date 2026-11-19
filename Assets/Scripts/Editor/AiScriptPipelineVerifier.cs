#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class AiScriptPipelineVerifier
    {
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run AI Script Pipeline Verifier")]
        public static void Run()
        {
            int passed = 0;
            void Check(string label, bool cond)
            {
                if (!cond) throw new InvalidOperationException("FAIL: " + label);
                passed++;
                Debug.Log("[AiScriptPipelineVerifier] PASS: " + label);
            }

            var pipeline = new FiveActsProductionPipeline();
            bool allActsValid = true;
            foreach (ProductionAct act in Enum.GetValues(typeof(ProductionAct)))
            {
                var res = pipeline.ParseActScript(act);
                allActsValid &= res != null && res.characters.Count > 0 && res.beats.Count > 0;
            }
            Check("all 5 production acts parse into non-empty characters and beats", allActsValid);

            var parser = new AiScriptParser { IsOfflineMock = false, ApiKey = "" };
            string fakeDeepSeekJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"```json\\n{\\\"sceneTitle\\\":\\\"SC01_DeepSeek\\\",\\\"conflictSummary\\\":\\\"hide letter\\\",\\\"characters\\\":[\\\"ActorA\\\"],\\\"beats\\\":[{\\\"character\\\":\\\"ActorA\\\",\\\"action\\\":\\\"step back\\\",\\\"dialogue\\\":\\\"\\\",\\\"suggestedShotType\\\":\\\"Wide\\\"}]}\\n```\"}}]}";
            var parsedApi = parser.ParseDeepSeekResponse(fakeDeepSeekJson);
            Check("AiScriptParser deserializes DeepSeek JSON response with fence stripping",
                parsedApi != null && parsedApi.sceneTitle == "SC01_DeepSeek" && parsedApi.beats[0].action == "step back");

            var customOffline = parser.ParseOfflineStructured("内景 电梯间 夜\n主角迅速按住关门键并遮挡怀中文件");
            Check("AiScriptParser extracts real beats from custom raw script",
                customOffline.sceneTitle == "内景 电梯间 夜" && customOffline.beats[0].action.Contains("关门键"));

            var shot = new ShotData
            {
                shotId = "SC01-S01",
                revision = 2,
                intent = "保持信封正面不泄密",
                camera = new ShotCameraState { aspect = 1.7778f, fieldOfView = 40f }
            };
            var spec = pipeline.BuildShotPromptSpec(ProductionAct.Act2_Shooting, shot, 12, 24);
            Check("AiPromptTemplateEngine formats Seedance @Clay Render 1 prompt with intent",
                spec.seedancePrompt.Contains("@Clay Render 1") && spec.seedancePrompt.Contains("保持信封正面不泄密"));

            string tempDir = Path.Combine(Application.temporaryCachePath, "AiPipeTest_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(tempDir, "ai_bridge_test.zip");
            try
            {
                var frames = new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x01, 0x02 } };
                ShotPackageDynamicExporter.ExportDynamicPackageToFile(shot, frames, zipPath, 12);
                string manifestJson = ReadZipEntryText(zipPath, "manifest.json");
                var req = AiPromptTemplateEngine.FromPackageMetadata(manifestJson, spec.seedancePrompt, zipPath);
                Check("DynamicPackage manifest.json + prompt bridges cleanly to SeedanceTaskRequest",
                    req.shotId == "SC01-S01" && req.prompt.Contains("@Clay Render 1") && req.aspectRatio == "16:9");

                var reqDirect = pipeline.GenerateSeedanceRequest(ProductionAct.Act2_Shooting, shot, zipPath);
                Check("FiveActsProductionPipeline generates complete SeedanceTaskRequest from ShotData",
                    reqDirect.shotId == "SC01-S01" && reqDirect.cameraKeyframeCount == 36);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }

            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"AI-SCRIPT-PIPELINE-RESULT={result} ({passed}/{Expected})");
            if (Application.isBatchMode) EditorApplication.Exit(result == "PASS" ? 0 : 1);
        }

        private static string ReadZipEntryText(string zipPath, string entryName)
        {
            using var fs = File.OpenRead(zipPath);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = archive.GetEntry(entryName);
            if (entry == null) return string.Empty;
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
#endif
