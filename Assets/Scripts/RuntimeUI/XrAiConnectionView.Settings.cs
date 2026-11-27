using ShotField.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    public sealed partial class XrAiConnectionView
    {
        private static readonly string[] ProviderIds = { "deepseek", "openai", "custom" };
        private static readonly string[] ProviderNames = { "DeepSeek", "OpenAI", "自定义" };
        private static readonly string[] ProviderEndpoints =
        {
            "https://api.deepseek.com/chat/completions",
            "https://api.openai.com/v1/chat/completions",
            ""
        };
        private static readonly string[] ProviderModels = { "deepseek-chat", "gpt-4o-mini", "" };

        private TMP_InputField _endpoint, _model, _key, _script;
        private TMP_InputField _tripoEndpoint, _tripoBalance, _tripoKey;
        private TMP_Text _status, _keyHint, _tripoKeyHint, _tripoStatus;
        private GameObject _llmGroup, _tripoGroup;
        private Button _llmTab, _tripoTab, _jsonModeButton;
        private readonly Button[] _providerButtons = new Button[3];
        private int _serviceTab, _providerIndex;
        private bool _jsonMode = true, _clearLlmKey, _clearTripoKey, _busy;

        private void BuildSettings()
        {
            var root = _settings.transform;
            RuntimeUiFactory.CreateTmpLabel(root, "Title", "AI 服务设置", 24, new Vector2(0, 200),
                new Vector2(600, 36));
            _llmTab = RuntimeUiFactory.CreateTmpButton(root, "LlmTab", "大模型", new Vector2(-100, 166),
                new Vector2(180, 34), onClick: () => SetServiceTab(0));
            _tripoTab = RuntimeUiFactory.CreateTmpButton(root, "TripoTab", "3D 资产", new Vector2(100, 166),
                new Vector2(180, 34), onClick: () => SetServiceTab(1));

            _llmGroup = CreateGroup(root, "LlmGroup");
            RuntimeUiFactory.CreateTmpLabel(_llmGroup.transform, "ProviderLabel", "大模型服务商（OpenAI 兼容接口）",
                15, new Vector2(0, 132), new Vector2(600, 24));
            for (int i = 0; i < ProviderIds.Length; i++)
            {
                int idx = i;
                _providerButtons[i] = RuntimeUiFactory.CreateTmpButton(_llmGroup.transform, "Provider_" + ProviderIds[i],
                    ProviderNames[i], new Vector2(-205 + i * 205, 101), new Vector2(190, 32),
                    onClick: () => SelectProvider(idx));
            }
            CreateField(_llmGroup.transform, "接口地址", "Endpoint", "LlmEndpoint", new Vector2(55, 61), 520, ref _endpoint);
            CreateField(_llmGroup.transform, "模型名", "Model", "LlmModel", new Vector2(55, 21), 520, ref _model);
            CreateField(_llmGroup.transform, "API Key", "ApiKey", "LlmKey", new Vector2(35, -19), 430, ref _key, true);
            _key.contentType = TMP_InputField.ContentType.Password;
            RuntimeUiFactory.CreateTmpButton(_llmGroup.transform, "ClearLlmKey", "清除密钥", new Vector2(265, -19),
                new Vector2(90, 30), onClick: () => { _clearLlmKey = true; _key.text = ""; _keyHint.text = "保存后将清除当前密钥"; });
            _keyHint = RuntimeUiFactory.CreateTmpLabel(_llmGroup.transform, "KeyHint", "", 12,
                new Vector2(0, -45), new Vector2(560, 18));
            CreateAction(_llmGroup.transform, "SaveLlm", "保存设置", -205, SaveSettings);
            CreateAction(_llmGroup.transform, "TestLlm", "测试连接", 0, () => Run(false));
            CreateAction(_llmGroup.transform, "GenerateLlm", "生成提案", 205, () => Run(true));
            _jsonModeButton = RuntimeUiFactory.CreateTmpButton(_llmGroup.transform, "JsonMode", "JSON 模式：开",
                new Vector2(-205, -116), new Vector2(200, 30), onClick: ToggleJsonMode);
            RuntimeUiFactory.CreateTmpLabel(_llmGroup.transform, "ScriptLabel", "剧本（用于生成提案）", 14,
                new Vector2(-240, -148), new Vector2(160, 22));
            _script = RuntimeUiFactory.CreateTmpInputField(_llmGroup.transform, "Script",
                "内景 客厅：主角把关键道具放到茶几上，另一人走近，两人隔着茶几对话。",
                new Vector2(520, 36), 800);
            ((RectTransform)_script.transform).anchoredPosition = new Vector2(55, -176);
            _status = RuntimeUiFactory.CreateTmpLabel(_llmGroup.transform, "Status", "", 14,
                new Vector2(0, -211), new Vector2(600, 28));

            _tripoGroup = CreateGroup(root, "TripoGroup");
            RuntimeUiFactory.CreateTmpLabel(_tripoGroup.transform, "TripoTitle", "3D 资产服务", 18,
                new Vector2(0, 126), new Vector2(600, 28));
            CreateField(_tripoGroup.transform, "生成接口", "TripoEndpoint", "TripoEndpoint", new Vector2(55, 79), 520, ref _tripoEndpoint);
            CreateField(_tripoGroup.transform, "余额接口", "TripoBalance", "TripoBalance", new Vector2(55, 39), 520, ref _tripoBalance);
            CreateField(_tripoGroup.transform, "API Key", "TripoKey", "TripoKey", new Vector2(35, -1), 430, ref _tripoKey, true);
            _tripoKey.contentType = TMP_InputField.ContentType.Password;
            RuntimeUiFactory.CreateTmpButton(_tripoGroup.transform, "ClearTripoKey", "清除密钥", new Vector2(265, -1),
                new Vector2(90, 30), onClick: () => { _clearTripoKey = true; _tripoKey.text = ""; _tripoKeyHint.text = "保存后将清除当前密钥"; });
            _tripoKeyHint = RuntimeUiFactory.CreateTmpLabel(_tripoGroup.transform, "TripoKeyHint", "", 12,
                new Vector2(0, -27), new Vector2(560, 18));
            CreateAction(_tripoGroup.transform, "SaveTripo", "保存设置", -120, SaveSettings);
            CreateAction(_tripoGroup.transform, "TestTripo", "测试连接", 120, () => Run(false));
            RuntimeUiFactory.CreateTmpLabel(_tripoGroup.transform, "TripoNote",
                "支持 Tripo 及兼容的 3D 生成接口；云端社区资产接口保持开放契约。", 13,
                new Vector2(0, -94), new Vector2(580, 40));
            _tripoStatus = RuntimeUiFactory.CreateTmpLabel(_tripoGroup.transform, "TripoStatus", "", 14,
                new Vector2(0, -151), new Vector2(600, 28));
            SetServiceTab(0);
        }

        private static GameObject CreateGroup(Transform root, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(640, 460);
            return go;
        }

        private static void CreateField(Transform root, string caption, string name, string fieldName,
            Vector2 pos, float width, ref TMP_InputField field, bool secret = false)
        {
            RuntimeUiFactory.CreateTmpLabel(root, name + "Label", caption, 13, new Vector2(-240, pos.y),
                new Vector2(150, 24));
            field = RuntimeUiFactory.CreateTmpInputField(root, fieldName, "", new Vector2(width, 30), secret ? 256 : 512);
            ((RectTransform)field.transform).anchoredPosition = pos;
        }

        private static void CreateAction(Transform root, string name, string caption, float x,
            UnityEngine.Events.UnityAction click) =>
            RuntimeUiFactory.CreateTmpButton(root, name, caption, new Vector2(x, -80), new Vector2(180, 34), onClick: click);

        private void SetServiceTab(int index)
        {
            _serviceTab = index;
            _llmGroup.SetActive(index == 0);
            _tripoGroup.SetActive(index == 1);
            UpdateTabButtons();
        }

        private void SelectProvider(int index)
        {
            if (_busy) return;
            _providerIndex = index;
            _endpoint.text = ProviderEndpoints[index];
            _model.text = ProviderModels[index];
            _key.text = "";
            _clearLlmKey = false;
            _keyHint.text = "当前密钥：" + LocalAiConfig.Mask(DeepSeekConfig.Load().ApiKey) + "（留空不修改）";
            _status.text = index == 2 ? "请填写自定义接口地址与模型名" : "已填入预设，可修改后保存";
            UpdateProviderButtons();
        }

        private void LoadDrafts()
        {
            var llm = DeepSeekConfig.Load();
            _providerIndex = llm.ProviderId == "openai" ? 1 : llm.ProviderId == "deepseek" ? 0 : 2;
            _endpoint.text = llm.Endpoint;
            _model.text = llm.Model;
            _key.text = "";
            _clearLlmKey = false;
            _jsonMode = llm.UseJsonMode;
            _keyHint.text = "当前密钥：" + LocalAiConfig.Mask(llm.ApiKey) + "（留空不修改）";
            var tripo = LocalAiConfig.LoadTripo();
            _tripoEndpoint.text = tripo.Endpoint;
            _tripoBalance.text = tripo.BalanceEndpoint;
            _tripoKey.text = "";
            _clearTripoKey = false;
            _tripoKeyHint.text = "当前密钥：" + LocalAiConfig.Mask(tripo.ApiKey) + "（留空不修改）";
            _status.text = "选择服务商或直接编辑开放接口参数";
            UpdateProviderButtons();
            UpdateJsonModeButton();
            SetServiceTab(_serviceTab);
        }

        private void UpdateProviderButtons()
        {
            for (int i = 0; i < _providerButtons.Length; i++)
            {
                if (_providerButtons[i] == null) continue;
                _providerButtons[i].GetComponentInChildren<TMP_Text>().text =
                    (i == _providerIndex ? "● " : "") + ProviderNames[i];
                _providerButtons[i].GetComponent<Image>().color = i == _providerIndex
                    ? new Color(0.36f, 0.29f, 0.18f, 0.98f) : new Color(0.24f, 0.26f, 0.30f, 0.95f);
            }
        }

        private void UpdateTabButtons()
        {
            if (_llmTab != null) _llmTab.GetComponent<Image>().color = _serviceTab == 0
                ? new Color(0.36f, 0.29f, 0.18f, 0.98f) : new Color(0.24f, 0.26f, 0.30f, 0.95f);
            if (_tripoTab != null) _tripoTab.GetComponent<Image>().color = _serviceTab == 1
                ? new Color(0.36f, 0.29f, 0.18f, 0.98f) : new Color(0.24f, 0.26f, 0.30f, 0.95f);
        }

        private void ToggleJsonMode()
        {
            _jsonMode = !_jsonMode;
            UpdateJsonModeButton();
        }

        private void UpdateJsonModeButton()
        {
            if (_jsonModeButton != null)
                _jsonModeButton.GetComponentInChildren<TMP_Text>().text = "JSON 模式：" + (_jsonMode ? "开" : "关");
        }
    }
}
