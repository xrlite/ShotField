using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    /// <summary>
    /// 旧提案 DTO → 新 <see cref="ProposalContractDto"/> 单向映射。
    /// 只做字段归并，不发请求、不碰场景；旧字段名不改，新合同不回写旧结构。
    /// </summary>
    public static class ProposalContractMapper
    {
        /// <summary>旧离线/在线分析结果归并到新通用合同；缺省字段保持空列表，不编造。</summary>
        public static ProposalContractDto MapFromLegacy(ScriptAnalysisResult legacy, string rawScript)
        {
            var contract = new ProposalContractDto
            {
                projectTitle = legacy != null ? legacy.sceneTitle ?? "" : "",
                source = "live"
            };

            if (legacy == null) return contract;

            if (!string.IsNullOrEmpty(legacy.conflictSummary))
                contract.constraints.Add(legacy.conflictSummary);

            if (legacy.characters != null)
            {
                foreach (var name in legacy.characters)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    contract.characters.Add(new ProposalCharacterDto { name = name });
                }
            }

            if (legacy.beats != null)
            {
                for (int i = 0; i < legacy.beats.Count; i++)
                {
                    var beat = legacy.beats[i];
                    if (beat == null) continue;
                    var shotId = $"S{i + 1:00}";
                    var shot = new ProposalShotDto
                    {
                        shotId = shotId,
                        summary = beat.action ?? "",
                        cameraStyle = MapCameraStyle(beat.suggestedShotType),
                        characterCount = string.IsNullOrEmpty(beat.character) ? 0 : 1
                    };
                    if (!string.IsNullOrEmpty(beat.character)) shot.characters.Add(beat.character);
                    if (!string.IsNullOrEmpty(beat.dialogue)) shot.note = beat.dialogue;
                    contract.shots.Add(shot);
                }
            }

            var scene = new ProposalSceneDto { title = contract.projectTitle, description = rawScript ?? "" };
            contract.scenes.Add(scene);
            return contract;
        }

        /// <summary>旧链提案条目归并到新合同；条目Kind只做归类，处置状态不搬。</summary>
        public static ProposalContractDto MapFromChain(ChainProposalDto chain)
        {
            var contract = new ProposalContractDto
            {
                projectTitle = chain != null ? chain.sceneTitle ?? "" : "",
                source = "live"
            };
            if (chain == null) return contract;

            if (!string.IsNullOrEmpty(chain.conflictSummary))
                contract.constraints.Add(chain.conflictSummary);
            if (chain.missingAssets != null) contract.missingAssets.AddRange(chain.missingAssets);

            if (chain.items == null) return contract;
            foreach (var item in chain.items)
            {
                if (item == null) continue;
                switch (item.kind)
                {
                    case ProposalItemKind.Shot:
                        contract.shots.Add(new ProposalShotDto
                        {
                            shotId = item.itemId,
                            summary = item.actionText,
                            note = item.intent
                        });
                        break;
                    case ProposalItemKind.Actor:
                        contract.characters.Add(new ProposalCharacterDto { name = item.actorId });
                        break;
                    case ProposalItemKind.Prop:
                        contract.props.Add(new ProposalPropDto { name = item.propId, existingRef = item.propId });
                        break;
                    case ProposalItemKind.Camera:
                        contract.cameraSuggestions.Add(new ProposalCameraSuggestionDto
                        {
                            shotId = item.itemId,
                            rationale = item.actionText
                        });
                        break;
                }
            }
            return contract;
        }

        private static CameraStyle MapCameraStyle(string legacyShotType)
        {
            string s = legacyShotType ?? "";
            if (s.Contains("Wide") || s.Contains("Establishing")) return CameraStyle.Rail;
            if (s.Contains("CloseUp") || s.Contains("Medium") || s.Contains("Low")) return CameraStyle.Fixed;
            return CameraStyle.Undecided;
        }
    }
}
