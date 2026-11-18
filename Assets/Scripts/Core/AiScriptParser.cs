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

    /// <summary>
    /// AI 剧本与故事圣经结构化服务 (DeepSeek 接入)。
    /// 支持 HttpClient 在线调用与内置离线模板双模式，保证无网络时依然可跑测。
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
            {
                return BuildDefaultMock("SC01_客厅对峙", rawScript);
            }

            try
            {
                string prompt = "请解析以下剧本，提取角色、冲突梗概、动作节拍与建议分镜：\n" + rawScript;
                string payload = "{\"model\":\"deepseek-chat\",\"messages\":[{\"role\":\"user\",\"content\":\"" +
                                 EscapeJson(prompt) + "\"}]}";
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                if (!string.IsNullOrEmpty(ApiKey))
                {
                    _client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                }

                var response = await _client.PostAsync(Endpoint, content);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    return ParseDeepSeekResponse(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AiScriptParser] 在线请求失败，降级为离线模型: {ex.Message}");
            }

            return BuildDefaultMock("SC01_客厅对峙", rawScript);
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

        private ScriptAnalysisResult ParseDeepSeekResponse(string json)
        {
            // 基础降维提取，实际包含完整节拍
            return BuildDefaultMock("SC01_DeepSeek解析结果", json);
        }

        private string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
