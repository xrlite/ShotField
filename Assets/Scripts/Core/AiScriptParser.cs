using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// AI 剧本结构化编排层。在线 DeepSeek：配置装载 -> <see cref="DeepSeekClient"/> 发请求 -> 解析 JSON。
    /// 在线失败 / 超时 / 非 2xx / JSON 不可解析 / 空条目 -> 抛 <see cref="ScriptParseException"/>，
    /// 绝不用 mock 数据冒充成功。旧契约链路见 <see cref="AiScriptParser"/> 的 Legacy 分部。
    /// </summary>
    public partial class AiScriptParser
    {
        public string ApiKey { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public bool IsOfflineMock { get; set; } = true;

        /// <summary>上一次在线响应的 token 用量；离线或失败时为 null。</summary>
        public DeepSeekUsage LastUsage { get; private set; }

        private readonly HttpClient _httpClient;

        public AiScriptParser(HttpClient customClient = null)
        {
            _httpClient = customClient;
        }

        /// <summary>新通用提案契约解析；场景约束可空，失败抛结构化异常，不伪装成功。</summary>
        public async Task<ProposalContractDto> BuildProposalAsync(string rawScript, SceneConstraintContext context = null)
        {
            if (string.IsNullOrWhiteSpace(rawScript))
                throw new ScriptParseException(ScriptParseErrorCode.ParseError, "剧本文本为空，无法解析");

            if (IsOfflineMock) return BuildOfflineContract(rawScript);

            using var client = OpenLiveClient();
            DeepSeekChatResponse response;
            try
            {
                response = await client.CompleteAsync(BuildProposalSystemPrompt(context), rawScript).ConfigureAwait(false);
            }
            catch (ScriptParseException) { throw; }
            catch (Exception ex)
            {
                throw new ScriptParseException(ScriptParseErrorCode.Network, $"AI 未接通: {ex.Message}", ex);
            }

            LastUsage = response?.usage;
            return ParseProposalContent(PickContent(response));
        }

        public static ProposalContractDto ParseProposalContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ScriptParseException(ScriptParseErrorCode.EmptyContent, "DeepSeek 返回提案内容为空");

            string cleaned = StripJsonFence(content);
            ProposalContractDto contract;
            try
            {
                contract = JsonUtility.FromJson<ProposalContractDto>(cleaned);
            }
            catch (Exception ex)
            {
                throw new ScriptParseException(ScriptParseErrorCode.ParseError,
                    $"提案 JSON 解析失败: {ex.Message}；原始内容: {ProposalContractValidator.Preview(cleaned)}", ex);
            }

            if (contract == null)
                throw new ScriptParseException(ScriptParseErrorCode.ParseError,
                    $"提案 JSON 不可解析；原始内容: {ProposalContractValidator.Preview(cleaned)}");

            if (!ProposalContractValidator.TryParseMode(cleaned, out var mode))
                throw new ScriptParseException(ScriptParseErrorCode.ParseError,
                    $"recommendedMode 缺失或非法；原始内容: {ProposalContractValidator.Preview(cleaned)}");
            contract.recommendedMode = mode;

            if (string.IsNullOrWhiteSpace(contract.projectTitle))
                throw new ScriptParseException(ScriptParseErrorCode.EmptyContent, "提案缺少 projectTitle");
            if (string.IsNullOrWhiteSpace(contract.modeRationale))
                throw new ScriptParseException(ScriptParseErrorCode.EmptyContent, "提案缺少推荐理由 modeRationale");
            if (contract.shots == null || contract.shots.Count == 0)
                throw new ScriptParseException(ScriptParseErrorCode.EmptyContent, "提案缺少镜头条目 shots");
            return contract;
        }

        private static string PickContent(DeepSeekChatResponse response)
        {
            return response?.choices != null && response.choices.Count > 0
                ? response.choices[0]?.message?.content
                : null;
        }

        private DeepSeekClient OpenLiveClient()
        {
            var config = DeepSeekConfig.Load();
            var apiKey = string.IsNullOrEmpty(ApiKey) ? config.ApiKey : ApiKey;
            var endpoint = string.IsNullOrEmpty(Endpoint) ? config.Endpoint : Endpoint;
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
                throw new ScriptParseException(ScriptParseErrorCode.NotConfigured, "未配置 DeepSeek ApiKey / Endpoint，AI 未接通");

            var live = new DeepSeekConfig(endpoint, config.Model, apiKey, config.TimeoutSeconds, config.MaxRetries);
            return new DeepSeekClient(live, _httpClient);
        }

        internal static string StripJsonFence(string text)
        {
            string s = text.Trim();
            if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            else if (s.StartsWith("```")) s = s.Substring(3);
            if (s.EndsWith("```")) s = s.Substring(0, s.Length - 3);
            return s.Trim();
        }
    }
}
