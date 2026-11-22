#if UNITY_EDITOR
using System;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// P3 通用提案合同验证器：离线模板标记、在线 JSON 解析、枚举复查、
    /// 旧→新映射与失败异常分支。不发网络请求，不碰场景。
    /// </summary>
    public static class ProposalContractVerifier
    {
        private const int Expected = 8;

        [MenuItem("ShotField/Verifiers/Run Proposal Contract Verifier")]
        public static void Run()
        {
            int passed = 0;
            void Check(string label, bool cond)
            {
                if (!cond) throw new InvalidOperationException("FAIL: " + label);
                passed++;
                Debug.Log("[ProposalContractVerifier] PASS: " + label);
            }

            // 1. 显式离线模板：source=offline-template，不冒充在线结论。
            var offlineParser = new AiScriptParser { IsOfflineMock = true };
            var offline = offlineParser.BuildOfflineContract("内景 小房间 日\n两人低声交谈");
            Check("离线模板标记 source=offline-template", offline.source == "offline-template");
            Check("离线模板给出兜底理由而非空结论", !string.IsNullOrEmpty(offline.modeRationale));

            // 2. 合法提案 JSON 解析：mode / 标题 / 人物 / 镜头归并正确。
            var contract = AiScriptParser.ParseProposalContent(ValidProposalJson);
            Check("合法提案解析出 WalkIn 模式", contract.recommendedMode == ProposalMode.WalkIn);
            Check("合法提案标题与镜头条目齐全",
                !string.IsNullOrEmpty(contract.projectTitle) && contract.shots.Count == 1
                && contract.characters.Count == 2);

            // 3. 坏 JSON：抛 ParseError，消息保留原始片段，不返回空合同。
            bool badJsonThrew = false;
            try { AiScriptParser.ParseProposalContent("这不是合法JSON{{{"); }
            catch (ScriptParseException ex)
            {
                badJsonThrew = ex.ErrorCode == ScriptParseErrorCode.ParseError
                    && ex.Message.Contains("原始内容");
            }
            Check("坏 JSON 显式 ParseError 并保留原始片段", badJsonThrew);

            // 4. 非法 mode：抛 ParseError，不静默落成默认枚举。
            bool badModeThrew = false;
            try { AiScriptParser.ParseProposalContent(InvalidModeJson); }
            catch (ScriptParseException ex) { badModeThrew = ex.ErrorCode == ScriptParseErrorCode.ParseError; }
            Check("非法 recommendedMode 显式报错", badModeThrew);

            // 5. 缺镜头条目：抛 EmptyContent，不伪装成功。
            bool emptyThrew = false;
            try { AiScriptParser.ParseProposalContent(EmptyShotsJson); }
            catch (ScriptParseException ex) { emptyThrew = ex.ErrorCode == ScriptParseErrorCode.EmptyContent; }
            Check("缺 shots 显式 EmptyContent", emptyThrew);

            // 6. 旧→新映射：beats 归 shots，characters 归 characters，不丢字段。
            var legacy = new ScriptAnalysisResult
            {
                sceneTitle = "旧场次",
                conflictSummary = "保持轴线",
                characters = new System.Collections.Generic.List<string> { "男人" },
                beats = new System.Collections.Generic.List<ScriptBeat>
                {
                    new ScriptBeat { character = "男人", action = "推近文件", suggestedShotType = "CloseUp_PropFocus" }
                }
            };
            var mapped = ProposalContractMapper.MapFromLegacy(legacy, "旧剧本");
            Check("旧分析结果映射到新合同：镜头与人物归并",
                mapped.shots.Count == 1 && mapped.characters.Count == 1
                && mapped.constraints.Count == 1);

            VerifyAssert.Finish("PROPOSAL-CONTRACT-RESULT", passed, Expected);
        }

        private const string ValidProposalJson =
            "{\"projectTitle\":\"双人夜谈\",\"recommendedMode\":\"WalkIn\",\"modeRationale\":\"两人近距离细节戏\"," +
            "\"scope\":\"InteriorDetail\"," +
            "\"characters\":[{\"name\":\"女人\",\"role\":\"主角\",\"shotIds\":[\"S01\"]},{\"name\":\"男人\",\"role\":\"对手\",\"shotIds\":[\"S01\"]}]," +
            "\"props\":[{\"name\":\"文件\",\"reuse\":\"Graybox\"}]," +
            "\"shots\":[{\"shotId\":\"S01\",\"summary\":\"推文件\",\"cameraStyle\":\"Fixed\",\"characterCount\":2,\"characters\":[\"女人\",\"男人\"]}]," +
            "\"missingAssets\":[\"墙面材质\"]," +
            "\"cameraSuggestions\":[{\"shotId\":\"S01\",\"style\":\"Fixed\",\"rationale\":\"过肩\"}]," +
            "\"constraints\":[\"保持轴线\"],\"risks\":[{\"type\":\"Axis\",\"detail\":\"过肩越轴\",\"severity\":\"中\"}]}";

        private const string InvalidModeJson =
            "{\"projectTitle\":\"x\",\"recommendedMode\":\"Cinema\",\"modeRationale\":\"r\"," +
            "\"shots\":[{\"shotId\":\"S01\",\"cameraStyle\":\"Fixed\"}]}";

        private const string EmptyShotsJson =
            "{\"projectTitle\":\"x\",\"recommendedMode\":\"Tabletop\",\"modeRationale\":\"r\",\"shots\":[]}";
    }
}
#endif
