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
        private void ApplyActor(ProposalItemDto item, ChainApplyReport report)
        {
            var actor = ResolveActor(item.actorId);
            if (actor == null)
            {
                report.placementFailures.Add($"演员 {item.actorId}：场景内无已登记演员");
                return;
            }
            Vector3 pos = item.position.ToUnityVector3();
            if (actor.TryPlace(pos, SampleRadius, out Vector3 snapped))
                report.placedActorIds.Add(actor.name);
            else
                report.placementFailures.Add($"演员 {actor.name}：落点不可行走，未落位");
        }

        // 解析器角色名（ActorA / Director / 中文称谓）与场景 GameObject 名（PrevisCast_*）不一致，
        // 在此做别名映射；未命中再按名字包含匹配，最后回退 Current。键名忽略大小写与首尾空白。
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

        private ActorNavigator ResolveActor(string actorId)
        {
            var all = ActorSelection.All;
            if (all != null)
            {
                string raw = (actorId ?? "").Trim();
                string sceneName = ActorAliases.TryGetValue(raw, out var mapped) ? mapped : raw;
                foreach (var nav in all)
                {
                    if (nav == null) continue;
                    if (nav.name == sceneName) return nav;
                }
                foreach (var nav in all)
                {
                    if (nav != null && !string.IsNullOrEmpty(raw) && nav.name.Contains(raw)) return nav;
                }
            }
            return ActorSelection.Current;
        }

        private Vector3 ResolveAnchor(ActorNavigator fallbackActor)
        {
            if (anchorTransform != null) return anchorTransform.position;
            var marker = SemanticMarker.Find("envelope");
            if (marker != null) return marker.transform.position;
            if (fallbackActor != null) return fallbackActor.transform.position;
            return new Vector3(0.8f, 1.25f, 1.25f);
        }
    }
}
