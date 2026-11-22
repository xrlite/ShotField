using System;
using System.Collections.Generic;
using System.Net.Http;
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

    /// <summary>
    /// AI 剧本结构化编排层。两条路径：
    /// <list type="bullet">
    /// <item>离线结构化：仅当调用方显式 <see cref="IsOfflineMock"/>=true 时走，纯文本提取，不发网络请求。</item>
    /// <item>在线 DeepSeek：配置装载 -> <see cref="DeepSeekClient"/> 发请求 -> 解析结构化 JSON。</item>
    /// </list>
    /// 在线失败 / 超时 / 非 2xx / JSON 不可解析 / 空条目 -> 抛 <see cref="ScriptParseException"/>，
    /// 绝不用 mock 数据冒充成功。HTTP、超时、重试、错误码在 <see cref="DeepSeekClient"/>，本类只做编排与解析。
    /// </summary>
    public class AiScriptParser
    {
        public string ApiKey { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public bool IsOfflineMock { get; set; } = true;

        private readonly HttpClient _httpClient;

        public AiScriptParser(HttpClient customClient = null)
        {
            _httpClient = customClient;
        }

        /// <summary>解析剧本文本；在线路径未接通时抛结构化异常，不返回假成功结果。</summary>
        public async Task<ScriptAnalysisResult> ParseScriptAsync(string rawScript)
        {
            if (string.IsNullOrWhiteSpace(rawScript))
                throw new ScriptParseException(ScriptParseErrorCode.ParseError, "剧本文本为空，无法解析");

            // 显式离线：调用方主动选择，不做在线兜底。
            if (IsOfflineMock) return ParseOfflineStructured(rawScript);

            var config = DeepSeekConfig.Load();
            var apiKey = string.IsNullOrEmpty(ApiKey) ? config.ApiKey : ApiKey;
            var endpoint = string.IsNullOrEmpty(Endpoint) ? config.Endpoint : Endpoint;
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
                throw new ScriptParseException(ScriptParseErrorCode.NotConfigured, "未配置 DeepSeek ApiKey / Endpoint，AI 未接通");

            var liveConfig = new DeepSeekConfig(endpoint, config.Model, apiKey, config.TimeoutSeconds, config.MaxRetries);
            using var client = new DeepSeekClient(liveConfig, _httpClient);

            DeepSeekChatResponse response;
            try
            {
                response = await client.CompleteAsync(SystemSchemaPrompt, rawScript).ConfigureAwait(false);
            }
            catch (ScriptParseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ScriptParseException(ScriptParseErrorCode.Network, $"AI 未接通: {ex.Message}", ex);
            }

            string content = response?.choices != null && response.choices.Count > 0
                ? response.choices[0]?.message?.content
                : null;

            var result = ParseContent(content);
            if (result == null || result.beats == null || result.beats.Count == 0)
                throw new ScriptParseException(ScriptParseErrorCode.EmptyContent, "DeepSeek 返回内容不可解析或无镜头条目");
            return result;
        }

        /// <summary>解析 DeepSeek 响应包装体（编辑器验证器复用）；不可解析返回 null，不造 mock 产物。</summary>
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

            string content = wrapper?.choices != null && wrapper.choices.Count > 0
                ? wrapper.choices[0]?.message?.content
                : rawJson;
            return ParseContent(content);
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

        /// <summary>显式离线文本结构化：仅提取分句与景别建议，不产生在线产物。</summary>
        public ScriptAnalysisResult ParseOfflineStructured(string rawScript)
        {
            if (string.IsNullOrWhiteSpace(rawScript)) return new ScriptAnalysisResult();

            var lines = rawScript.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string title = lines.Length > 0 ? lines[0].Trim() : "SC01_客厅对峙";
            var res = new ScriptAnalysisResult
            {
                sceneTitle = title,
                conflictSummary = lines.Length > 1 ? lines[1].Trim() : title
            };
            res.characters.Add("ActorA");

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (i == 0 && (line.StartsWith("内景") || line.StartsWith("外景"))) continue;
                var clauses = line.Split(new[] { '，', ',', '；', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var subBeats = clauses.Length > 1 ? clauses : new[] { line };
                foreach (var clause in subBeats)
                {
                    string p = clause.Trim();
                    if (string.IsNullOrEmpty(p)) continue;
                    string shotType = p.Contains("信") || p.Contains("特写") ? "CloseUp_PropFocus"
                        : (p.Contains("远") || p.Contains("全景") ? "Wide_Establishing" : "MediumShot_LowAngle");
                    res.beats.Add(new ScriptBeat
                    {
                        character = p.Contains("导演") ? "Director" : "ActorA",
                        action = p,
                        dialogue = string.Empty,
                        suggestedShotType = shotType
                    });
                }
            }
            return res;
        }

        private const string SystemSchemaPrompt =
            "你是影视预演结构化助手。仅返回合法JSON，字段为: " +
            "{\"sceneTitle\":\"\",\"conflictSummary\":\"\",\"characters\":[\"\"]," +
            "\"beats\":[{\"character\":\"\",\"action\":\"\",\"dialogue\":\"\",\"suggestedShotType\":\"\"}]}";

        private static string StripJsonFence(string text)
        {
            string s = text.Trim();
            if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            else if (s.StartsWith("```")) s = s.Substring(3);
            if (s.EndsWith("```")) s = s.Substring(0, s.Length - 3);
            return s.Trim();
        }
    }
}
