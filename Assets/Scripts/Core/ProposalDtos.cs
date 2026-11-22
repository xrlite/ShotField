using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    /// <summary>
    /// P1 离线链提案条目类型：镜头 / 演员 / 道具 / 机位。
    /// 位置等空间字段复用 G4-0 的 <see cref="Vector3Dto"/>，不另造平行结构。
    /// </summary>
    public enum ProposalItemKind
    {
        Shot = 0,
        Actor = 1,
        Prop = 2,
        Camera = 3
    }

    /// <summary>提案条目在确认 UI 里的处置状态。</summary>
    public enum ProposalDecision
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        Edited = 3
    }

    /// <summary>
    /// 单条结构化提案：由离线 mock 解析结果转换而来，等待导演在确认 UI 逐条处置。
    /// 序列化字段对齐 JsonUtility（公开字段、无 Dictionary）。
    /// </summary>
    [Serializable]
    public class ProposalItemDto
    {
        public string itemId = "";
        public ProposalItemKind kind;
        public string label = "";
        public string actionText = "";
        public string dialogue = "";

        /// <summary>道具库条目 id（PropLibrary.id），仅 Prop 条目使用。</summary>
        public string propId = "";

        /// <summary>演员标识（场景 GameObject 名或角色 id），仅 Actor 条目使用。</summary>
        public string actorId = "";

        /// <summary>CameraPresetId 枚举值，仅 Camera/Shot 条目使用。</summary>
        public int cameraPresetId = 0;

        public Vector3Dto position;
        public float durationSeconds = 4.0f;
        public string intent = "";

        public ProposalDecision decision = ProposalDecision.Pending;

        /// <summary>导演在确认 UI 里改写参数后的备注。</summary>
        public string note = "";

        public ProposalItemDto() { }

        public bool IsAccepted =>
            decision == ProposalDecision.Accepted || decision == ProposalDecision.Edited;
    }

    /// <summary>
    /// 一整份离线链提案：场次信息 + 条目列表 + 缺资产清单。
    /// 缺资产只登记不静默跳过，应用阶段由编排器逐条回报。
    /// </summary>
    [Serializable]
    public class ChainProposalDto
    {
        public string sceneTitle = "";
        public string conflictSummary = "";
        public string sourceScript = "";
        public List<ProposalItemDto> items = new List<ProposalItemDto>();
        public List<string> missingAssets = new List<string>();

        public List<ProposalItemDto> AcceptedItems
        {
            get
            {
                var result = new List<ProposalItemDto>();
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null && items[i].IsAccepted) result.Add(items[i]);
                }
                return result;
            }
        }

        public ProposalItemDto Find(string itemId)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].itemId == itemId) return items[i];
            }
            return null;
        }
    }
}
