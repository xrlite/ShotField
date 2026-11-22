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
    /// AI 剧本结构化编排层。两条路径：
    /// <list type="bullet">
    /// <item>离线结构化：仅当调用方显式 <see cref="IsOfflineMock"/>=true 时走，纯文本提取，不发网络请求。</item>
    /// <item>在线 DeepSeek：配置装载 -> <see cref="DeepSeekClient"/> 发请求 -> 解析结构化 JSON。</item>
    /// </list>
    /// 在线失败 / 超时 / 非 2xx / JSON 不可解析 / 空条目 -> 抛 <see cref="ScriptParseException"/>，
    /// 绝不用 mock 数据冒充成功。HTTP、超时、重试、错误码在 <see cref="DeepSeekClient"/>，本类只做编排与解析。
    /// </summary>
    public partial class AiScriptParser
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
                response = await client.CompleteAsync(BuildSystemPrompt(), rawScript).ConfigureAwait(false);
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

        private static readonly (string Id, string Label)[] Cast =
        {
            ("PrevisCast_A_Red", "红 · 主角"),
            ("PrevisCast_B_Blue", "蓝 · 对手"),
            ("PrevisCast_C_Yellow", "黄"),
            ("PrevisCast_D_Green", "绿"),
            ("PrevisCast_E_Violet", "紫"),
            ("PrevisCast_F_Magenta", "品红")
        };

        /// <summary>
        /// 构造系统提示词：JSON 契约 + 当前片场硬约束（场景尺寸、可用演员、道具清单）。
        /// 道具条目实时读 <see cref="PropLibrary"/>，不硬编码猜名称；清单外资产要求显式标「缺」。
        /// </summary>
        private static string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("你是影视预演结构化助手。仅返回合法JSON，字段为:");
            sb.AppendLine("{\"sceneTitle\":\"\",\"conflictSummary\":\"\",\"characters\":[\"\"]," +
                "\"beats\":[{\"character\":\"\",\"action\":\"\",\"dialogue\":\"\",\"suggestedShotType\":\"\"}]}");
            sb.AppendLine();
            sb.AppendLine("场景约束：当前只有 SC01 室内客厅，6m×4m，家具沿墙摆放；不要安排室外、车内或大空间动作。");
            sb.AppendLine("可用演员（character 字段填括号内 ID）：");
            foreach (var c in Cast) sb.AppendLine($"- {c.Id}（{c.Label}）");
            sb.AppendLine("可用道具（道具名只能取自以下清单）：");
            foreach (var p in PropLibrary.All) sb.AppendLine($"- {p.id}：{p.displayName}");
            sb.AppendLine("剧本里出现但清单没有的人物或道具，在 action 末尾注明「缺：名称」，不要编造资产 ID。");
            sb.AppendLine("suggestedShotType 只能取 MediumShot_LowAngle / CloseUp_PropFocus / Wide_Establishing。");
            return sb.ToString();
        }
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
