using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    public enum ProductionAct
    {
        Act1_Connection,
        Act2_Shooting,
        Act3_Verification,
        Act4_Screening,
        Act5_FinalCut
    }

    [Serializable]
    public class ActSceneDescriptor
    {
        public ProductionAct act;
        public string actTitle;
        public string rawScript;
        public string promptTemplate;
        public string suggestedCameraMotion;
        public float defaultDuration;
    }

    /// <summary>
    /// 《镜场》五幕故事圣经与 AI 影视生成管线。
    /// 承接剧本结构化、机位提示词生成，并与 Seedance 视频交接打通。
    /// </summary>
    public class FiveActsProductionPipeline
    {
        private readonly Dictionary<ProductionAct, ActSceneDescriptor> _acts = new Dictionary<ProductionAct, ActSceneDescriptor>();
        private readonly AiScriptParser _parser;
        private readonly SeedanceBridge _seedance;

        public FiveActsProductionPipeline(AiScriptParser parser = null, SeedanceBridge seedance = null)
        {
            _parser = parser ?? new AiScriptParser { IsOfflineMock = true };
            _seedance = seedance ?? new SeedanceBridge { IsOfflineMock = true };
            InitializeActs();
        }

        private void InitializeActs()
        {
            _acts[ProductionAct.Act1_Connection] = new ActSceneDescriptor
            {
                act = ProductionAct.Act1_Connection,
                actTitle = "第一幕 · 连接（多人空间入场）",
                rawScript = "内景 空间影视工作台\n导演戴上头显，主创团队在网络同房间内同步加载SC01场景白模。",
                promptTemplate = "Cinematic wide establishing shot, virtual film studio, futuristic spatial workbench, ambient lighting, 8k",
                suggestedCameraMotion = "Slow Pan Right (10 deg/s)",
                defaultDuration = 4.0f
            };

            _acts[ProductionAct.Act2_Shooting] = new ActSceneDescriptor
            {
                act = ProductionAct.Act2_Shooting,
                actTitle = "第二幕 · 拍摄（身体找机位 + 手持运镜）",
                rawScript = "内景 客厅 日\n演员A缓步走近茶几，导演移动身体寻找极低视角，机位S01安全入画，信封正面朝上不泄密。",
                promptTemplate = "Cinematic low angle tracking shot, push in dolly, shallow depth of field, warm indoor shadows, table edge focus",
                suggestedCameraMotion = "Low Dolly Push (0.35m/s)",
                defaultDuration = 3.5f
            };

            _acts[ProductionAct.Act3_Verification] = new ActSceneDescriptor
            {
                act = ProductionAct.Act3_Verification,
                actTitle = "第三幕 · 检查（约束报警 + 空间修镜）",
                rawScript = "内景 客厅 日\n机位俯冲触发视线违规报警，导演调整机位并引入物理挡板，护照版本升至rev002。",
                promptTemplate = "Dramatic high angle view, critical prop occlusion, wooden partition, sharp rim light, tension",
                suggestedCameraMotion = "Static High Angle",
                defaultDuration = 3.0f
            };

            _acts[ProductionAct.Act4_Screening] = new ActSceneDescriptor
            {
                act = ProductionAct.Act4_Screening,
                actTitle = "第四幕 · 试看（观众视线与注意力分析）",
                rawScript = "内景 虚拟试看席\n观众试看S01分镜，头部朝向与视线聚焦在茶几区域，生成注意力热图。",
                promptTemplate = "Audience perspective preview, dark screening studio, focused gaze point, cinematic atmosphere",
                suggestedCameraMotion = "Stationary Eyeline",
                defaultDuration = 4.5f
            };

            _acts[ProductionAct.Act5_FinalCut] = new ActSceneDescriptor
            {
                act = ProductionAct.Act5_FinalCut,
                actTitle = "第五幕 · 成片（Seedance 渲染回片场对照）",
                rawScript = "内景 片场分屏监视器\nSeedance根据白模参考帧渲染出真实质感成片，在片场虚拟屏幕上与白模左右分屏同步播放。",
                promptTemplate = "Masterpiece cinematic film clip, photorealistic texture, final color grading, split screen review",
                suggestedCameraMotion = "Synchronized Playback",
                defaultDuration = 5.0f
            };
        }

        public ActSceneDescriptor GetActDescriptor(ProductionAct act) => _acts[act];

        public SeedanceTaskRequest GenerateSeedanceRequest(ProductionAct act, string clayRefPath, string aspect = "16:9")
        {
            var desc = _acts[act];
            return new SeedanceTaskRequest
            {
                shotId = $"SC01-{act}",
                prompt = $"{desc.promptTemplate}, Camera: {desc.suggestedCameraMotion}",
                clayReferencePath = clayRefPath,
                durationSeconds = desc.defaultDuration,
                aspectRatio = aspect,
                cameraKeyframeCount = 10
            };
        }

        public ScriptAnalysisResult ParseActScript(ProductionAct act)
        {
            var desc = _acts[act];
            var task = _parser.ParseScriptAsync(desc.rawScript);
            task.Wait();
            return task.Result;
        }
    }
}
