using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    /// <summary>提案推荐的片场形态：沙盘 / 1:1 走进片场 / 混合。</summary>
    public enum ProposalMode { Tabletop = 0, WalkIn = 1, Hybrid = 2 }

    /// <summary>场景体量判断：室内细节戏 / 宏大场景 / 未定。</summary>
    public enum SpaceScope { Undecided = 0, InteriorDetail = 1, GrandScene = 2 }

    /// <summary>资产处置建议：复用现有 / 灰盒替代 / Tripo 生成 / 未定。</summary>
    public enum ReuseStrategy { Unknown = 0, Reuse = 1, Graybox = 2, Tripo = 3 }

    /// <summary>镜头机位运动：手持 / 固定 / 轨道 / 未定。</summary>
    public enum CameraStyle { Undecided = 0, Handheld = 1, Fixed = 2, Rail = 3 }

    /// <summary>排演风险类型：越轴 / 穿墙 / 遮挡 / 空间不足 / 其他。</summary>
    public enum RiskType { Other = 0, Axis = 1, WallThrough = 2, Occlusion = 3, SpaceTight = 4 }

    /// <summary>一份通用提案中的场次描述。</summary>
    [Serializable]
    public class ProposalSceneDto
    {
        public string sceneId = "";
        public string title = "";
        public SpaceScope scope;
        public string description = "";
        public string sizeHint = "";
    }

    /// <summary>提案角色条目：只登记镜头真正需要的人，不按槽位填满。</summary>
    [Serializable]
    public class ProposalCharacterDto
    {
        public string name = "";
        public string role = "";
        public List<string> shotIds = new List<string>();
        public string note = "";
    }

    /// <summary>提案资产条目及处置策略。</summary>
    [Serializable]
    public class ProposalPropDto
    {
        public string name = "";
        public ReuseStrategy reuse;
        public string existingRef = "";
        public string note = "";
    }

    /// <summary>提案单镜：动作摘要、机位运动、出场人数。</summary>
    [Serializable]
    public class ProposalShotDto
    {
        public string shotId = "";
        public string summary = "";
        public CameraStyle cameraStyle;
        public int characterCount;
        public List<string> characters = new List<string>();
        public string note = "";
    }

    /// <summary>排演风险条目。</summary>
    [Serializable]
    public class ProposalRiskDto
    {
        public RiskType type;
        public string detail = "";
        public string severity = "";
    }

    /// <summary>单镜机位建议及理由。</summary>
    [Serializable]
    public class ProposalCameraSuggestionDto
    {
        public string shotId = "";
        public CameraStyle style;
        public string rationale = "";
    }

    /// <summary>
    /// 通用提案契约（P3）。与旧 <see cref="ScriptAnalysisResult"/> / <see cref="ChainProposalDto"/>
    /// 并存，不改既有字段名；旧链路映射见 <c>ProposalContractMapper</c>。
    /// source=offline-template 表示离线规则模板产物，必须由调用方显式选择，不冒充在线结果。
    /// </summary>
    [Serializable]
    public class ProposalContractDto
    {
        public string projectTitle = "";
        public ProposalMode recommendedMode;
        public string modeRationale = "";
        public SpaceScope scope;

        public List<ProposalSceneDto> scenes = new List<ProposalSceneDto>();
        public List<ProposalCharacterDto> characters = new List<ProposalCharacterDto>();
        public List<ProposalPropDto> props = new List<ProposalPropDto>();
        public List<ProposalShotDto> shots = new List<ProposalShotDto>();
        public List<string> missingAssets = new List<string>();
        public List<ProposalCameraSuggestionDto> cameraSuggestions = new List<ProposalCameraSuggestionDto>();
        public List<string> constraints = new List<string>();
        public List<ProposalRiskDto> risks = new List<ProposalRiskDto>();

        /// <summary>live = 在线 DeepSeek；offline-template = 显式选择的离线规则模板。</summary>
        public string source = "live";
    }

    /// <summary>
    /// 运行时传入的可选场景约束上下文：有清单就注入提示词，没有就不注入。
    /// 不再把「单一客厅 / 固定尺寸 / 固定演员槽」写进提示词常量。
    /// </summary>
    [Serializable]
    public class SceneConstraintContext
    {
        public string sceneTitle = "";
        public List<string> availableActorSlots = new List<string>();
        public List<PropEntry> availableProps;
        public List<string> existingConstraints = new List<string>();

        public bool HasActorSlots => availableActorSlots != null && availableActorSlots.Count > 0;
        public bool HasProps => availableProps != null && availableProps.Count > 0;
    }
}
