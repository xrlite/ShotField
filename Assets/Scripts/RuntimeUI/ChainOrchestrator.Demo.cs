using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 明确的演示入口：使用内置示例剧本走真实解析、提案、四道闸与时间轴，
    /// 不伪造 AI 返回；在线/离线模式仍由既有配置决定。
    /// </summary>
    public partial class ChainOrchestrator
    {
        public const string DemoScript =
            "内景 客厅 日\nActorA 在茶几旁放置关键道具，走到门口，拿起桌上的茶杯。";

        public void ImportDemoAndApply()
        {
            ImportScript(DemoScript, "DemoCard");
            if (Proposal == null || Proposal.items == null) return;
            foreach (var item in Proposal.items)
                if (item != null && item.decision != ProposalDecision.Rejected)
                    item.decision = ProposalDecision.Accepted;
            ConfirmAndApply();
        }
    }
}
