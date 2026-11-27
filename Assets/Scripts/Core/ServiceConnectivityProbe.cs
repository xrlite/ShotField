using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>单次连通性自检结果；<see cref="Reason"/> 面向用户，不含 Key 明文。</summary>
    public sealed class ProbeResult
    {
        public bool Ok { get; }
        public string Reason { get; }

        public ProbeResult(bool ok, string reason)
        {
            Ok = ok;
            Reason = string.IsNullOrEmpty(reason) ? (ok ? "已连接" : "未连接") : reason;
        }
    }

    /// <summary>
    /// 服务连通性自检：大模型走一次最小补全，Tripo 走余额接口。
    /// 探针不缓存、不写配置；报错只给人话原因，绝不把 Key 带进日志或文案。
    /// </summary>
    public static class ServiceConnectivityProbe
    {
        public static Task<ProbeResult> ProbeDeepSeekAsync(DeepSeekConfig config) => ProbeLlmAsync(config);

        public static async Task<ProbeResult> ProbeLlmAsync(DeepSeekConfig config)
        {
            if (config == null || !config.IsConfigured)
                return new ProbeResult(false, "未配置密钥、接口地址或模型名");

            try
            {
                using var client = new DeepSeekClient(config);
                var reply = await client.CompleteAsync("Reply with JSON: {\"ok\":true}", "ping");
                if (reply?.choices == null || reply.choices.Count == 0 || string.IsNullOrWhiteSpace(reply.choices[0]?.message?.content)) return new ProbeResult(false, "服务返回空响应");
                return new ProbeResult(true, $"已连接 {config.DisplayName}");
            }
            catch (ScriptParseException ex)
            {
                return new ProbeResult(false, HumanizeLlm(ex));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Probe] 大模型自检异常: {ex.GetType().Name}");
                return new ProbeResult(false, "网络异常，连不上服务");
            }
        }

        public static async Task<ProbeResult> ProbeTripoAsync(TripoConfig config)
        {
            if (config == null || !config.IsConfigured)
                return new ProbeResult(false, "未配置 Key 或余额接口");

            // 自检用独立 15s 超时客户端，不沿用生成链路 300s 轮询超时，避免卡死主线程。
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
            try
            {
                using var resp = await http.GetAsync(config.BalanceEndpoint).ConfigureAwait(false);
                string raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                // 余额接口在本机被 Cloudflare 边缘拦截（403 / error 1010），不代表 Key 或生成域名不可用；
                // 降级为「已连接（额度未知）」。401 才是 Key 无效，判未连接。
                if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return new ProbeResult(true, "已连接（额度未知）· 余额接口被网络边缘拦截");
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return new ProbeResult(false, "Key 无效（401）");
                if (!resp.IsSuccessStatusCode)
                    return new ProbeResult(false, $"服务返回 HTTP {(int)resp.StatusCode}");

                var parsed = JsonUtility.FromJson<TripoBalanceResponse>(raw);
                if (parsed == null || parsed.code != 0)
                    return new ProbeResult(false, $"接口返回 code={parsed?.code ?? -1}");
                double balance = parsed.data != null ? parsed.data.balance : 0;
                return new ProbeResult(true, $"已连接，余额 {balance:0}");
            }
            catch (TaskCanceledException)
            {
                return new ProbeResult(false, "连接超时");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Probe] Tripo 自检异常: {ex.GetType().Name}");
                return new ProbeResult(false, "网络异常，连不上服务");
            }
        }

        private static string HumanizeLlm(ScriptParseException ex)
        {
            switch (ex.ErrorCode)
            {
                case ScriptParseErrorCode.NotConfigured: return "未配置密钥、接口地址或模型名";
                case ScriptParseErrorCode.Network: return "网络异常，连不上服务";
                case ScriptParseErrorCode.Timeout: return "连接超时";
                case ScriptParseErrorCode.HttpError: return $"服务返回 HTTP {ex.HttpStatus}";
                case ScriptParseErrorCode.ParseError: return "响应格式不对";
                case ScriptParseErrorCode.EmptyContent: return "返回内容为空";
                default: return "未连接";
            }
        }
    }
}
