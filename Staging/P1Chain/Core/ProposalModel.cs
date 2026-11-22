using System;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 离线提案构建器：把 <see cref="ScriptAnalysisResult"/>（AiScriptParser 离线 mock 产物）
    /// 转成 <see cref="ChainProposalDto"/>。纯数据转换，不碰场景、不发网络请求。
    /// 机位预设映射复用 <see cref="CameraPresetId"/>；资产存在性核对复用 <see cref="PropLibrary"/>。
    /// </summary>
    public static class ProposalModel
    {
        // SC01 白模基准坐标（制作包 §2），仅作离线提案的默认落点，集成后可被场景锚点覆盖。
        private static readonly Vector3 DefaultActorAnchor = new Vector3(0.8f, 0f, 1.25f);
        private static readonly Vector3 DefaultPropAnchor = new Vector3(1.5f, 0.46f, 2.15f);

        public static ChainProposalDto BuildFromAnalysis(ScriptAnalysisResult result, string rawScript)
        {
            var proposal = new ChainProposalDto
            {
                sourceScript = rawScript ?? "",
                sceneTitle = result != null ? result.sceneTitle : "SC01_Default",
                conflictSummary = result != null ? result.conflictSummary : ""
            };

            if (result == null || result.beats == null || result.beats.Count == 0) return proposal;

            for (int i = 0; i < result.beats.Count; i++)
            {
                var beat = result.beats[i];
                if (beat == null) continue;

                string shotId = $"SC01-S{i + 1:00}";
                proposal.items.Add(BuildShotItem(shotId, beat));
                proposal.items.Add(BuildActorItem(i, shotId, beat));

                string propId = DetectPropId(beat.action);
                if (!string.IsNullOrEmpty(propId))
                {
                    proposal.items.Add(BuildPropItem(i, propId, beat.action));
                    RegisterMissing(proposal, propId);
                }
            }

            return proposal;
        }

        private static ProposalItemDto BuildShotItem(string shotId, ScriptBeat beat)
        {
            return new ProposalItemDto
            {
                itemId = shotId,
                kind = ProposalItemKind.Shot,
                label = shotId,
                actionText = beat.action ?? "",
                dialogue = beat.dialogue ?? "",
                cameraPresetId = (int)MapShotType(beat.suggestedShotType),
                durationSeconds = 4.0f,
                intent = beat.action ?? ""
            };
        }

        private static ProposalItemDto BuildActorItem(int index, string shotId, ScriptBeat beat)
        {
            string actorId = string.IsNullOrEmpty(beat.character) ? "actorA" : beat.character;
            return new ProposalItemDto
            {
                itemId = shotId + "-Actor",
                kind = ProposalItemKind.Actor,
                label = actorId,
                actorId = actorId,
                actionText = beat.action ?? "",
                dialogue = beat.dialogue ?? "",
                position = Vector3Dto.FromUnityVector3(DefaultActorAnchor),
                intent = beat.action ?? ""
            };
        }

        private static ProposalItemDto BuildPropItem(int index, string propId, string action)
        {
            return new ProposalItemDto
            {
                itemId = $"P{index + 1:00}-{propId}",
                kind = ProposalItemKind.Prop,
                label = propId,
                propId = propId,
                actionText = action ?? "",
                position = Vector3Dto.FromUnityVector3(DefaultPropAnchor)
            };
        }

        /// <summary>解析器输出的景别字符串 → 既有机位预设枚举；未命中走正面中景。</summary>
        public static CameraPresetId MapShotType(string suggestedShotType)
        {
            string s = suggestedShotType ?? "";
            if (s.Contains("CloseUp") || s.Contains("Detail")) return CameraPresetId.OverShoulder;
            if (s.Contains("Wide") || s.Contains("Establishing")) return CameraPresetId.Top;
            if (s.Contains("Low")) return CameraPresetId.Low;
            if (s.Contains("Side")) return CameraPresetId.Side;
            return CameraPresetId.Front;
        }

        private static string DetectPropId(string action)
        {
            string a = action ?? "";
            if (a.Contains("信")) return "envelope";
            if (a.Contains("沙发")) return "sofa";
            if (a.Contains("茶几")) return "coffeeTable";
            if (a.Contains("鞋柜")) return "shoeCabinet";
            if (a.Contains("杯")) return "cup";
            if (a.Contains("拖鞋")) return "slipper";
            if (a.Contains("门")) return "door";
            return null;
        }

        /// <summary>道具库无条目或条目无真实 prefabPath 时登记进缺资产清单，不静默跳过。</summary>
        private static void RegisterMissing(ChainProposalDto proposal, string propId)
        {
            var entry = PropLibrary.Find(propId);
            bool missing = entry == null || string.IsNullOrEmpty(entry.prefabPath) || entry.isProcedural;
            if (missing && !proposal.missingAssets.Contains(propId))
            {
                proposal.missingAssets.Add(propId);
            }
        }
    }
}
