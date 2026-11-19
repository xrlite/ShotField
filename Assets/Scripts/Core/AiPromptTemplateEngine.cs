using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ShotField.Core
{
    [Serializable]
    public class AiShotPromptSpec
    {
        public string shotId;
        public string actName;
        public string sceneTitle;
        public string clayRefTag = "@Clay Render 1";
        public string lightingSpec = "Cool moonlight from floor window, warm key spotlight on coffee table";
        public string cameraSpec;
        public string constraintIntent;
        public float duration;
        public string aspectRatio;
        public int fps;
        public int frameCount;
        public string seedancePrompt;
    }

    /// <summary>
    /// AI 分镜 Prompt 模板引擎与参考包对接器（System 6）：
    /// 打通五幕剧本结构、Seedance 2.5 @Clay Render 1 提示词与 DynamicPackageManifest 对接格式。
    /// </summary>
    public static class AiPromptTemplateEngine
    {
        public static AiShotPromptSpec BuildSpec(
            ShotData shot,
            ProductionAct act,
            ActSceneDescriptor actDesc,
            ScriptAnalysisResult analysis = null,
            int fps = 12,
            int frameCount = 36)
        {
            if (shot == null) throw new ArgumentNullException(nameof(shot));
            float aspect = shot.camera != null && shot.camera.aspect > 0.01f ? shot.camera.aspect : 1.7778f;
            string aspectStr = aspect < 1.0f ? "9:16" : "16:9";
            float dur = shot.trajectory != null && shot.trajectory.totalDuration > 0.1f
                ? shot.trajectory.totalDuration
                : (actDesc != null ? actDesc.defaultDuration : 5.0f);

            string motion = actDesc != null ? actDesc.suggestedCameraMotion : "Smooth Tracking";
            string beatAction = (analysis != null && analysis.beats != null && analysis.beats.Count > 0)
                ? analysis.beats[0].action : "Actor performs scene blocking";

            var spec = new AiShotPromptSpec
            {
                shotId = shot.shotId,
                actName = act.ToString(),
                sceneTitle = analysis != null && !string.IsNullOrEmpty(analysis.sceneTitle)
                    ? analysis.sceneTitle : (actDesc != null ? actDesc.actTitle : "SC01"),
                cameraSpec = $"{motion}, FOV {shot.camera?.fieldOfView ?? 40f:F0}deg, Aspect {aspectStr}",
                constraintIntent = string.IsNullOrEmpty(shot.intent) ? "Keep key prop intent intact" : shot.intent,
                duration = dur,
                aspectRatio = aspectStr,
                fps = fps,
                frameCount = frameCount
            };
            spec.seedancePrompt = FormatGenerationPrompt(spec, actDesc, beatAction);
            return spec;
        }

        public static string FormatGenerationPrompt(AiShotPromptSpec spec, ActSceneDescriptor actDesc, string beatAction)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Shot Reference Spec: {spec.shotId} ({spec.actName})");
            sb.AppendLine($"Reference: {spec.clayRefTag}");
            sb.AppendLine($"Duration: {spec.duration:F1}s | FPS: {spec.fps} | Frames: {spec.frameCount}");
            sb.AppendLine($"Aspect Ratio: {spec.aspectRatio}");
            sb.AppendLine($"Camera: {spec.cameraSpec}");
            sb.AppendLine($"Lighting: {spec.lightingSpec}");
            sb.AppendLine($"Director Intent: {spec.constraintIntent}");
            sb.AppendLine($"Action Beat: {beatAction}");
            if (actDesc != null && !string.IsNullOrEmpty(actDesc.promptTemplate))
            {
                sb.AppendLine($"Prompt: {spec.clayRefTag}, {actDesc.promptTemplate}, {beatAction}, {spec.lightingSpec}");
            }
            return sb.ToString();
        }

        public static SeedanceTaskRequest FromPackageMetadata(
            string manifestJson,
            string generationPromptTxt,
            string clayRefPath = "")
        {
            var manifest = !string.IsNullOrWhiteSpace(manifestJson)
                ? JsonUtility.FromJson<DynamicPackageManifest>(manifestJson)
                : new DynamicPackageManifest();

            string aspectStr = (manifest != null && manifest.aspect > 0.01f && manifest.aspect < 1.0f) ? "9:16" : "16:9";
            string extractedPrompt = ExtractPromptLine(generationPromptTxt);
            return new SeedanceTaskRequest
            {
                shotId = manifest != null && !string.IsNullOrEmpty(manifest.shotId) ? manifest.shotId : "SC01-S01",
                prompt = string.IsNullOrWhiteSpace(extractedPrompt) ? (manifest?.intent ?? "") : extractedPrompt,
                clayReferencePath = clayRefPath ?? string.Empty,
                durationSeconds = manifest != null && manifest.duration > 0.1f ? manifest.duration : 5.0f,
                aspectRatio = aspectStr,
                cameraKeyframeCount = manifest != null && manifest.frameCount > 0 ? manifest.frameCount : 10
            };
        }

        private static string ExtractPromptLine(string promptTxt)
        {
            if (string.IsNullOrWhiteSpace(promptTxt)) return string.Empty;
            var lines = promptTxt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Prompt:", StringComparison.OrdinalIgnoreCase))
                    return lines[i].Substring("Prompt:".Length).Trim();
            }
            return lines.Length > 0 ? lines[0].Trim() : string.Empty;
        }
    }
}
