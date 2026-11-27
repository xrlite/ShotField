using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    [Serializable]
    public class ScriptBeat
    {
        public string character;
        public string action;
        public string dialogue;
        public string suggestedShotType;
    }

    [Serializable]
    public class ScriptAnalysisResult
    {
        public string sceneTitle;
        public string conflictSummary;
        public List<string> characters = new List<string>();
        public List<ScriptBeat> beats = new List<ScriptBeat>();
    }

    public partial class AiScriptParser
    {
        /// <summary>旧契约解析（向后兼容）；场景约束由 <paramref name="context"/> 运行时传入，可空。</summary>
        public async Task<ScriptAnalysisResult> ParseScriptAsync(string rawScript, SceneConstraintContext context = null)
        {
            if (string.IsNullOrWhiteSpace(rawScript))
                throw new ScriptParseException(ScriptParseErrorCode.ParseError, "剧本文本为空，无法解析");

            if (IsOfflineMock) return ParseOfflineStructured(rawScript);

            using var client = OpenLiveClient();
            DeepSeekChatResponse response;
            try
            {
                response = await client.CompleteAsync(BuildLegacySystemPrompt(context), rawScript).ConfigureAwait(false);
            }
            catch (ScriptParseException) { throw; }
            catch (Exception ex)
            {
                throw new ScriptParseException(ScriptParseErrorCode.Network, $"AI 未接通: {ex.Message}", ex);
            }

            LastUsage = response?.usage;
            var result = ParseContent(PickContent(response));
            if (result == null || result.beats == null || result.beats.Count == 0)
                throw new ScriptParseException(ScriptParseErrorCode.EmptyContent, "大模型返回内容不可解析或无镜头条目");
            return result;
        }

        /// <summary>解析 OpenAI 兼容响应包装体（编辑器验证器复用）；不可解析返回 null，不造 mock 产物。</summary>
        public ScriptAnalysisResult ParseDeepSeekResponse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return null;

            DeepSeekChatResponse wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<DeepSeekChatResponse>(rawJson);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AiScriptParser] 响应包装解析失败: {ex.Message}");
                return null;
            }

            return ParseContent(PickContent(wrapper));
        }

        private static ScriptAnalysisResult ParseContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            string cleaned = StripJsonFence(content);
            try
            {
                var result = JsonUtility.FromJson<ScriptAnalysisResult>(cleaned);
                if (result != null && result.beats != null && result.beats.Count > 0) return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AiScriptParser] 结构化 JSON 解析失败: {ex.Message}");
            }
            return null;
        }
    }
}
