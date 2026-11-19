using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
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

    [Serializable]
    internal class DeepSeekChatResponse
    {
        public List<DeepSeekChoice> choices = new List<DeepSeekChoice>();
    }

    [Serializable]
    internal class DeepSeekChoice
    {
        public DeepSeekMessage message;
    }

    [Serializable]
    internal class DeepSeekMessage
    {
        public string role;
        public string content;
    }

    /// <summary>
    /// AI 剧本与故事圣经结构化服务 (DeepSeek 接入)。
    /// 支持 HttpClient 在线调用（真实 JSON 解析）与离线文本结构化提取双模式。
    /// </summary>
    public class AiScriptParser
    {
        public string ApiKey { get; set; } = "";
        public string Endpoint { get; set; } = "https://api.deepseek.com/v1/chat/completions";
        public bool IsOfflineMock { get; set; } = true;

        private readonly HttpClient _client;

        public AiScriptParser(HttpClient customClient = null)
        {
            _client = customClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        public async Task<ScriptAnalysisResult> ParseScriptAsync(string rawScript)
        {
            if (string.IsNullOrWhiteSpace(rawScript))
                return BuildDefaultMock("SC01_Default", "未提供剧本文本");

            if (IsOfflineMock || string.IsNullOrEmpty(ApiKey))
                return ParseOfflineStructured(rawScript);

            try
            {
                string payload = BuildDeepSeekPayload(rawScript);
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                _client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);

                var response = await _client.PostAsync(Endpoint, content);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var parsed = ParseDeepSeekResponse(json);
                    if (parsed != null && parsed.beats != null && parsed.beats.Count > 0)
                        return parsed;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AiScriptParser] 在线请求失败，降级为离线解析: {ex.Message}");
            }

            return ParseOfflineStructured(rawScript);
        }

        public string BuildDeepSeekPayload(string rawScript)
        {
            string sys = "你是影视预演结构化助手。仅返回合法JSON，字段为: " +
                         "{\"sceneTitle\":\"\",\"conflictSummary\":\"\",\"characters\":[\"\"]," +
                         "\"beats\":[{\"character\":\"\",\"action\":\"\",\"dialogue\":\"\",\"suggestedShotType\":\"\"}]}";
            return "{\"model\":\"deepseek-chat\",\"temperature\":0.2,\"messages\":[" +
                   "{\"role\":\"system\",\"content\":\"" + EscapeJson(sys) + "\"}," +
                   "{\"role\":\"user\",\"content\":\"" + EscapeJson(rawScript) + "\"}]}";
        }

        public ScriptAnalysisResult ParseDeepSeekResponse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return null;
            try
            {
                var wrapper = JsonUtility.FromJson<DeepSeekChatResponse>(rawJson);
                string content = wrapper?.choices != null && wrapper.choices.Count > 0
                    ? wrapper.choices[0]?.message?.content
                    : rawJson;
                if (string.IsNullOrWhiteSpace(content)) return null;

                string cleaned = StripJsonFence(content);
                var result = JsonUtility.FromJson<ScriptAnalysisResult>(cleaned);
                if (result != null && result.beats != null && result.beats.Count > 0)
                    return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AiScriptParser] DeepSeek JSON 解析回退: {ex.Message}");
            }
            return BuildDefaultMock("SC01_DeepSeekFallback", rawJson);
        }

        public ScriptAnalysisResult ParseOfflineStructured(string rawScript)
        {
            if (string.IsNullOrWhiteSpace(rawScript)) return BuildDefaultMock("SC01_Default", rawScript);
            var lines = rawScript.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string title = lines.Length > 0 ? lines[0].Trim() : "SC01_客厅对峙";
            var res = new ScriptAnalysisResult { sceneTitle = title, conflictSummary = lines.Length > 1 ? lines[1].Trim() : title };
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
                    res.beats.Add(new ScriptBeat { character = p.Contains("导演") ? "Director" : "ActorA", action = p, dialogue = string.Empty, suggestedShotType = shotType });
                }
            }
            return res.beats.Count == 0 ? BuildDefaultMock(title, rawScript) : res;
        }

        public ScriptAnalysisResult BuildDefaultMock(string title, string sourceScript)
        {
            var res = new ScriptAnalysisResult
            {
                sceneTitle = title,
                conflictSummary = "关键信件在茶几上，演员A企图隐藏秘密，摄影机需避免正面泄露关键内容。"
            };
            res.characters.Add("ActorA");
            res.beats.Add(new ScriptBeat
            {
                character = "ActorA",
                action = "缓步走向茶几，余光警惕地扫视门口",
                dialogue = "（喃喃自语）绝对不能让他看到这封信……",
                suggestedShotType = "MediumShot_LowAngle"
            });
            res.beats.Add(new ScriptBeat
            {
                character = "ActorA",
                action = "伸手试图遮挡信封，但动作骤然停滞",
                dialogue = "",
                suggestedShotType = "CloseUp_PropFocus"
            });
            return res;
        }

        private static string StripJsonFence(string text)
        {
            string s = text.Trim();
            if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            else if (s.StartsWith("```")) s = s.Substring(3);
            if (s.EndsWith("```")) s = s.Substring(0, s.Length - 3);
            return s.Trim();
        }

        private string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}
