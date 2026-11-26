using System;
using System.Threading.Tasks;
using ShotField.Core;
using TMPro;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public sealed partial class XrAiConnectionView : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        private TMP_InputField _key;
        private TMP_InputField _script;
        private TMP_Text _provider;
        private TMP_Text _status;
        private GameObject _settings;
        private bool _tripo;
        private bool _busy;

        public static bool IsAnyPanelOpen
        {
            get
            {
                var dock = FindAnyObjectByType<DirectorDockPanel>();
                if (dock != null && dock.IsOpen) return true;
                var view = FindAnyObjectByType<XrAiConnectionView>();
                if (view == null) return false;
                return (view._settings != null && view._settings.activeInHierarchy)
                    || (view._workflow != null && view._workflow.activeInHierarchy);
            }
        }

        public static XrAiConnectionView Create(RectTransform parent)
        {
            var panel = RuntimeUiFactory.CreatePanel(parent, "XrAiConnection", PanelColor,
                new Vector2(0f, 174f), new Vector2(526f, 258f));
            var view = parent.gameObject.AddComponent<XrAiConnectionView>();
            view._settings = panel;
            view.BuildWorkflow(parent);
            view.Build();
            panel.SetActive(false);
            return view;
        }

        public void Toggle()
        {
            if (_settings.activeSelf)
            {
                _settings.SetActive(false);
                _key.text = "";
                if (_staged != null) _workflow.SetActive(true);
                return;
            }
            if (_staged != null)
                _workflow.SetActive(!_workflow.activeSelf);
            else OpenSettings();
        }

        private void OpenSettings()
        {
            _workflow.SetActive(false);
            _settings.SetActive(true);
            RefreshProvider();
        }

        private void Build()
        {
            var root = _settings.transform;
            _provider = RuntimeUiFactory.CreateTmpLabel(root, "Provider", "", 18, new Vector2(0, 103));
            RuntimeUiFactory.CreateTmpButton(root, "DeepSeek", "DeepSeek", new Vector2(-93, 68),
                new Vector2(165, 34), onClick: () => SelectProvider(false));
            RuntimeUiFactory.CreateTmpButton(root, "Tripo", "Tripo 3D", new Vector2(93, 68),
                new Vector2(165, 34), onClick: () => SelectProvider(true));
            _key = RuntimeUiFactory.CreateTmpInputField(root, "ApiKey", "", new Vector2(358, 34), 256);
            _key.contentType = TMP_InputField.ContentType.Password;
            ((RectTransform)_key.transform).anchoredPosition = new Vector2(-55, 26);
            RuntimeUiFactory.CreateTmpButton(root, "SaveKey", "保存密钥", new Vector2(190, 26),
                new Vector2(116, 34), onClick: SaveKey);
            _script = RuntimeUiFactory.CreateTmpInputField(root, "Script",
                "客厅里，林宁把信封放到茶几上。周哲走近，两人隔着茶几对话。",
                new Vector2(486, 34), 800);
            ((RectTransform)_script.transform).anchoredPosition = new Vector2(0, -18);
            RuntimeUiFactory.CreateTmpButton(root, "TestConnection", "测试连接", new Vector2(-110, -61),
                new Vector2(190, 36), onClick: () => Run(false));
            RuntimeUiFactory.CreateTmpButton(root, "GenerateProposal", "生成提案（DeepSeek）",
                new Vector2(110, -61), new Vector2(216, 36), onClick: () => Run(true));
            _status = RuntimeUiFactory.CreateTmpLabel(root, "Result", "", 15,
                new Vector2(0, -101), new Vector2(488, 40));
        }

        private void SelectProvider(bool tripo)
        {
            if (_busy) return;
            _tripo = tripo;
            _key.text = "";
            RefreshProvider();
            _status.text = "";
        }

        private void RefreshProvider()
        {
            bool configured = !string.IsNullOrWhiteSpace(_tripo
                ? LocalAiConfig.LoadTripo().ApiKey : DeepSeekConfig.Load().ApiKey);
            _provider.text = (_tripo ? "Tripo 3D" : "DeepSeek") + (configured ? " · 已配置密钥" : " · 输入密钥");
        }

        private void SaveKey()
        {
            if (_busy || string.IsNullOrWhiteSpace(_key.text)) return;
            try
            {
                LocalAiConfig.SaveApiKey(_tripo ? "tripo" : "deepseek", _key.text.Trim());
                _key.text = "";
                RefreshProvider();
                _status.text = "密钥已保存";
            }
            catch (Exception)
            {
                _status.text = "未能保存密钥，请重试";
            }
        }

        private async void Run(bool proposal)
        {
            if (_busy) return;
            _busy = true;
            _status.text = proposal ? "正在生成提案…" : "正在测试连接…";
            try
            {
                string result = proposal ? await GenerateAsync() : await ProbeAsync();
                if (this != null) _status.text = result;
            }
            catch (Exception)
            {
                if (this != null) _status.text = "请求失败，请检查密钥和网络后重试";
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task<string> ProbeAsync()
        {
            var probe = _tripo
                ? await ServiceConnectivityProbe.ProbeTripoAsync(LocalAiConfig.LoadTripo())
                : await ServiceConnectivityProbe.ProbeDeepSeekAsync(DeepSeekConfig.Load());
            return probe.Reason;
        }

        private async Task<string> GenerateAsync()
        {
            if (!DeepSeekConfig.Load().IsConfigured) return "请先配置 DeepSeek 密钥";
            var parser = new AiScriptParser { IsOfflineMock = false };
            string script = _script.text.Trim();
            if (script.Length == 0) return "请先输入短剧本";
            var result = await parser.BuildProposalAsync(script + "\n请为这次预演规划三个镜头。");
            if (this == null) return "";
            return PresentProposal(result, script);
        }
    }
}
