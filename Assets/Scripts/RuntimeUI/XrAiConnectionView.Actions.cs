using System;
using System.Threading.Tasks;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public sealed partial class XrAiConnectionView
    {
        private void SaveSettings()
        {
            if (_busy) return;
            if (_serviceTab == 0) SaveLlmDrafts();
            else SaveTripoDrafts();
        }

        private bool SaveLlmDrafts()
        {
            if (!IsValidEndpoint(_endpoint.text))
            {
                _status.text = "接口地址无效，请填写完整的 http/https 地址";
                return false;
            }
            if (string.IsNullOrWhiteSpace(_model.text))
            {
                _status.text = "请填写模型名";
                return false;
            }
            string key = _clearLlmKey && string.IsNullOrWhiteSpace(_key.text) ? "" : string.IsNullOrWhiteSpace(_key.text) ? (DeepSeekConfig.Load().Endpoint == _endpoint.text.Trim() ? null : "") : _key.text.Trim();
            try
            {
                LocalAiConfig.SaveLlmSettings(ProviderIds[_providerIndex], ProviderNames[_providerIndex],
                    _endpoint.text, _model.text, key, _jsonMode);
                _key.text = "";
                _clearLlmKey = false;
                _keyHint.text = "当前密钥：" + LocalAiConfig.Mask(DeepSeekConfig.Load().ApiKey) + "（留空不修改）";
                _status.text = "大模型设置已保存";
                return true;
            }
            catch (Exception)
            {
                _status.text = "保存失败，请检查本地存储权限";
                return false;
            }
        }

        private bool SaveTripoDrafts()
        {
            if (!IsValidEndpoint(_tripoEndpoint.text) || !IsValidEndpoint(_tripoBalance.text))
            {
                _tripoStatus.text = "接口地址无效，请填写完整的 http/https 地址";
                return false;
            }
            string key = _clearTripoKey ? "" : string.IsNullOrWhiteSpace(_tripoKey.text) ? null : _tripoKey.text.Trim();
            try
            {
                LocalAiConfig.SaveTripoSettings(_tripoEndpoint.text, _tripoBalance.text, key);
                _tripoKey.text = "";
                _clearTripoKey = false;
                _tripoKeyHint.text = "当前密钥：" + LocalAiConfig.Mask(LocalAiConfig.LoadTripo().ApiKey) + "（留空不修改）";
                _tripoStatus.text = "3D 服务设置已保存";
                return true;
            }
            catch (Exception)
            {
                _tripoStatus.text = "保存失败，请检查本地存储权限";
                return false;
            }
        }

        private async void Run(bool proposal)
        {
            if (_busy) return;
            if (proposal && _serviceTab != 0)
            {
                _tripoStatus.text = "请先切到大模型页生成提案";
                return;
            }
            _busy = true;
            SetStatus(proposal ? "正在生成提案…" : "正在测试连接…");
            try
            {
                string result = proposal ? await GenerateAsync() : await TestConnectionAsync();
                if (this != null) SetStatus(result);
            }
            catch (Exception ex)
            {
                if (this != null) SetStatus("请求失败：" + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task<string> TestConnectionAsync()
        {
            if (_serviceTab == 0)
            {
                if (!IsValidEndpoint(_endpoint.text) || string.IsNullOrWhiteSpace(_model.text))
                    return "请先填写有效的接口地址与模型名";
                var config = BuildLlmConfigFromDrafts();
                var probe = await ServiceConnectivityProbe.ProbeLlmAsync(config);
                return probe.Reason;
            }

            if (!IsValidEndpoint(_tripoEndpoint.text) || !IsValidEndpoint(_tripoBalance.text))
                return "请先填写有效的生成接口与余额接口";
            var current = LocalAiConfig.LoadTripo();
            string key = _clearTripoKey ? "" : string.IsNullOrWhiteSpace(_tripoKey.text)
                ? current.ApiKey : _tripoKey.text.Trim();
            var tripo = new TripoConfig(_tripoEndpoint.text.Trim(), _tripoBalance.text.Trim(), key,
                current.TextToModelVersion, current.RigModelVersion, current.FaceLimit,
                current.PollIntervalSeconds, current.PollTimeoutSeconds);
            var tripoProbe = await ServiceConnectivityProbe.ProbeTripoAsync(tripo);
            return tripoProbe.Reason;
        }

        private async Task<string> GenerateAsync()
        {
            if (!SaveLlmDrafts()) return _status.text;
            var config = DeepSeekConfig.Load();
            if (!config.IsConfigured) return "请先配置大模型密钥、接口地址与模型名";
            string script = _script.text.Trim();
            if (script.Length == 0) return "请先输入短剧本";
            var parser = new AiScriptParser { IsOfflineMock = false };
            var result = await parser.BuildProposalAsync(script + "\n请为这次预演规划三个镜头。");
            if (this == null) return "";
            return PresentProposal(result, script);
        }

        private DeepSeekConfig BuildLlmConfigFromDrafts()
        {
            var current = DeepSeekConfig.Load();
            string key = _clearLlmKey && string.IsNullOrWhiteSpace(_key.text) ? "" : string.IsNullOrWhiteSpace(_key.text)
                ? (current.Endpoint == _endpoint.text.Trim() ? current.ApiKey : "") : _key.text.Trim();
            return new DeepSeekConfig(_endpoint.text.Trim(), _model.text.Trim(), key,
                current.TimeoutSeconds, current.MaxRetries, ProviderIds[_providerIndex],
                ProviderNames[_providerIndex], _jsonMode);
        }

        private void SetStatus(string message)
        {
            if (_serviceTab == 0) _status.text = message;
            else _tripoStatus.text = message;
        }

        private static bool IsValidEndpoint(string value)
        {
            return Uri.TryCreate((value ?? "").Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
