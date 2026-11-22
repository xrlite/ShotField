using System.Text;

namespace ShotField.Core
{
    public partial class AiScriptParser
    {
        /// <summary>
        /// 新通用提案契约的系统提示词：通用 JSON 契约 + 六个必答问题。
        /// 场景清单、演员槽、道具清单只在 <paramref name="context"/> 非空时注入；
        /// 不内置「单一客厅 / 固定尺寸 / 固定槽位」等任何场景常量。
        /// </summary>
        internal static string BuildProposalSystemPrompt(SceneConstraintContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("你是影视预演（previs）导演助手。只返回合法 JSON，不要 Markdown 代码块，不要多余解释。");
            sb.AppendLine("顶层字段：");
            sb.AppendLine("\"projectTitle\": 项目或场次标题；");
            sb.AppendLine("\"recommendedMode\": 只能取 Tabletop（沙盘）/ WalkIn（1:1 走进片场）/ Hybrid（混合）；");
            sb.AppendLine("\"modeRationale\": 推荐理由；");
            sb.AppendLine("\"scope\": 只能取 InteriorDetail（室内细节戏）/ GrandScene（宏大场景）/ Undecided；");
            sb.AppendLine("\"scenes\": [{\"sceneId\":\"\",\"title\":\"\",\"scope\":\"\",\"description\":\"\",\"sizeHint\":\"\"}]；");
            sb.AppendLine("\"characters\": [{\"name\":\"\",\"role\":\"\",\"shotIds\":[],\"note\":\"\"}]；");
            sb.AppendLine("\"props\": [{\"name\":\"\",\"reuse\":\"Reuse|Graybox|Tripo|Unknown\",\"existingRef\":\"\",\"note\":\"\"}]；");
            sb.AppendLine("\"shots\": [{\"shotId\":\"\",\"summary\":\"\",\"cameraStyle\":\"Handheld|Fixed|Rail|Undecided\",\"characterCount\":0,\"characters\":[],\"note\":\"\"}]；");
            sb.AppendLine("\"missingAssets\": []；");
            sb.AppendLine("\"cameraSuggestions\": [{\"shotId\":\"\",\"style\":\"Handheld|Fixed|Rail|Undecided\",\"rationale\":\"\"}]；");
            sb.AppendLine("\"constraints\": []；");
            sb.AppendLine("\"risks\": [{\"type\":\"Axis|WallThrough|Occlusion|SpaceTight|Other\",\"detail\":\"\",\"severity\":\"\"}]。");
            sb.AppendLine();
            sb.AppendLine("模式判别参考（按剧本主导特征选最贴合的一种，不要默认 Hybrid 兜底）：");
            sb.AppendLine("- WalkIn（1:1 走进片场）：室内细节戏、1~2 人近距离表演，靠眼神 / 手部 / 微动作，需要真人尺度走位与近身机位。");
            sb.AppendLine("- Tabletop（沙盘）：3 人以上围聚，重点是站位、队形、视线关系与路线调度，俯视看全局。");
            sb.AppendLine("- Hybrid（混合）：同一戏里既有全局调度又有近身跟随 / 细节表演；或大场景分段，一部分上沙盘、一部分 1:1。");
            sb.AppendLine();
            sb.AppendLine("必须明确回答以下六个问题，答案落到对应字段：");
            sb.AppendLine("1. 这是室内细节戏还是宏大场景？→ scope。");
            sb.AppendLine("2. 推荐沙盘 / 1:1 走进片场 / 混合中的哪一种？→ recommendedMode，并在 modeRationale 给理由。");
            sb.AppendLine("3. 当前每个镜头真正需要几个人？→ characters 只列实际出场角色，shots[].characterCount 按镜头给人数，不要把槽位填满。");
            sb.AppendLine("4. 哪些资产可复用（Reuse）、哪些可灰盒替代（Graybox）、哪些值得 Tripo 生成（Tripo）？→ props[].reuse；清单外资产进 missingAssets。");
            sb.AppendLine("5. 每镜是手持（Handheld）/ 固定（Fixed）/ 轨道（Rail）？→ shots[].cameraStyle 与 cameraSuggestions。");
            sb.AppendLine("6. 有哪些越轴（Axis）/ 穿墙（WallThrough）/ 遮挡（Occlusion）/ 空间不足（SpaceTight）风险？→ risks 与 constraints。");
            sb.AppendLine();
            AppendContextConstraints(sb, context);
            return sb.ToString();
        }

        /// <summary>旧契约提示词：去掉场景尺寸与槽位硬绑定；清单只来自运行时上下文。</summary>
        internal static string BuildLegacySystemPrompt(SceneConstraintContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("你是影视预演结构化助手。仅返回合法JSON，字段为:");
            sb.AppendLine("{\"sceneTitle\":\"\",\"conflictSummary\":\"\",\"characters\":[\"\"]," +
                "\"beats\":[{\"character\":\"\",\"action\":\"\",\"dialogue\":\"\",\"suggestedShotType\":\"\"}]}");
            sb.AppendLine("suggestedShotType 只能取 MediumShot_LowAngle / CloseUp_PropFocus / Wide_Establishing。");
            sb.AppendLine();
            AppendContextConstraints(sb, context);
            return sb.ToString();
        }

        private static void AppendContextConstraints(StringBuilder sb, SceneConstraintContext context)
        {
            if (context == null)
            {
                sb.AppendLine("当前未提供既有场景清单：可按剧本自行估算出场人数与所需资产，不要假设固定房间尺寸或固定演员槽位。");
                return;
            }

            if (!string.IsNullOrEmpty(context.sceneTitle))
                sb.AppendLine($"既有场次：{context.sceneTitle}");

            if (context.HasActorSlots)
            {
                sb.AppendLine("可用演员槽（character 字段填槽位 ID）：");
                foreach (var slot in context.availableActorSlots) sb.AppendLine($"- {slot}");
            }
            else
            {
                sb.AppendLine("未提供固定演员槽：按剧本实际出场角色登记，不要预设人数。");
            }

            if (context.HasProps)
            {
                sb.AppendLine("可用道具（道具名只能取自以下清单）：");
                foreach (var p in context.availableProps) sb.AppendLine($"- {p.id}：{p.displayName}");
                sb.AppendLine("剧本里出现但清单没有的人物或道具，在动作末尾注明「缺：名称」，不要编造资产 ID。");
            }
            else
            {
                sb.AppendLine("未提供既有道具清单：道具与资产处置按复用 / 灰盒 / Tripo 三类评估。");
            }

            if (context.existingConstraints != null && context.existingConstraints.Count > 0)
            {
                sb.AppendLine("既有约束（不得违反）：");
                foreach (var c in context.existingConstraints) sb.AppendLine($"- {c}");
            }
        }
    }
}
