using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// ChainOrchestrator 演员/锚点解析分部：角色别名映射、演员落位、锚点回退。
    /// 从主文件拆出以守住单文件 200 行红线。
    /// </summary>
    public partial class ChainOrchestrator
    {
        public bool StageOnlineProposal(ProposalContractDto contract, string script)
        {
            if (contract?.shots == null || contract.shots.Count != 3 ||
                contract.shots.Exists(shot => shot == null) || (_pipeline != null && _pipeline.IsRunning))
                return false;

            var proposal = new ChainProposalDto
            {
                sceneTitle = contract.projectTitle,
                conflictSummary = contract.modeRationale,
                sourceScript = script
            };
            if (contract.missingAssets != null) proposal.missingAssets.AddRange(contract.missingAssets);
            var presets = new[] { CameraPresetId.Front, CameraPresetId.Side, CameraPresetId.OverShoulder };
            for (int i = 0; i < contract.shots.Count; i++)
            {
                var shot = contract.shots[i];
                proposal.items.Add(new ProposalItemDto
                {
                    itemId = $"SC01-S{i + 1:00}",
                    kind = ProposalItemKind.Shot,
                    label = string.IsNullOrWhiteSpace(shot.shotId) ? $"镜头 {i + 1}" : shot.shotId,
                    actionText = shot.summary,
                    intent = shot.summary,
                    cameraPresetId = (int)presets[i]
                });
            }
            PublishProposal(proposal, false);
            return true;
        }

        // 解析器角色名（ActorA / Director / 中文称谓）与场景 GameObject 名（PrevisCast_*）不一致，
        // 只允许显式别名或精确场景名；不再子串模糊匹配，也不回退 Current。
        private static readonly Dictionary<string, string> ActorAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ActorA"] = "PrevisCast_A_Red",
            ["actorA"] = "PrevisCast_A_Red",
            ["actorRed"] = "PrevisCast_A_Red",
            ["ActorB"] = "PrevisCast_B_Blue",
            ["actorB"] = "PrevisCast_B_Blue",
            ["actorBlue"] = "PrevisCast_B_Blue",
            ["Director"] = "PrevisCast_B_Blue",
            ["actorYellow"] = "PrevisCast_C_Yellow",
            ["actorGreen"] = "PrevisCast_D_Green",
            ["actorViolet"] = "PrevisCast_E_Violet",
            ["actorMagenta"] = "PrevisCast_F_Magenta",
            // 中文称谓 → 演员槽位：男主/男主一类归红，女主一类归蓝。
            ["男人"] = "PrevisCast_A_Red",
            ["男主"] = "PrevisCast_A_Red",
            ["男主角"] = "PrevisCast_A_Red",
            ["主角"] = "PrevisCast_A_Red",
            ["男的"] = "PrevisCast_A_Red",
            ["少年"] = "PrevisCast_A_Red",
            ["青年男子"] = "PrevisCast_A_Red",
            ["女人"] = "PrevisCast_B_Blue",
            ["女主"] = "PrevisCast_B_Blue",
            ["女主角"] = "PrevisCast_B_Blue",
            ["女的"] = "PrevisCast_B_Blue",
            ["少女"] = "PrevisCast_B_Blue",
            ["青年女子"] = "PrevisCast_B_Blue",
            ["黄衣"] = "PrevisCast_C_Yellow",
            ["绿衣"] = "PrevisCast_D_Green",
            ["紫衣"] = "PrevisCast_E_Violet",
            ["粉衣"] = "PrevisCast_F_Magenta",
            ["品红"] = "PrevisCast_F_Magenta"
        };

        internal bool TryResolveActorStrict(string actorId, out ActorNavigator actor)
        {
            actor = null;
            string raw = (actorId ?? "").Trim();
            if (raw.Length == 0) return false;
            string sceneName = ActorAliases.TryGetValue(raw, out var mapped) ? mapped : raw;
            var all = ActorSelection.All;
            if (all != null)
                foreach (var nav in all)
                {
                    if (nav == null) continue;
                    var marker = nav.GetComponent<SemanticMarker>();
                    if (nav.name == sceneName || (marker != null && marker.markerId == raw))
                    { actor = nav; return true; }
                }
            foreach (var nav in UnityEngine.Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include))
            {
                if (nav == null) continue;
                var marker = nav.GetComponent<SemanticMarker>();
                if (nav.name == sceneName || (marker != null && marker.markerId == raw))
                { actor = nav; return true; }
            }
            return false;
        }

        internal bool TryResolveAnchor(ActorNavigator fallbackActor, out Vector3 anchor)
        {
            if (anchorTransform != null) { anchor = anchorTransform.position; return true; }
            var marker = SemanticMarker.Find("envelope", FindObjectsInactive.Include);
            if (marker != null) { anchor = marker.transform.position; return true; }
            if (fallbackActor != null) { anchor = fallbackActor.transform.position; return true; }
            anchor = Vector3.zero; return false;
        }
    }
}
