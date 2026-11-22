using System;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 运行时通用连接中心：选服务商 → 填 Key → 测试连接 → 显示「已连接 / 未连接 + 原因」。
    /// 不依赖 Editor 或 WindowsPlayer，头显内可用；Key 走 <see cref="LocalAiConfig"/> 同一本地配置口径，
    /// 界面掩码显示，日志与报错绝不出现 Key 明文。
    /// </summary>
    public sealed class ConnectionCenterPanel : MonoBehaviour
    {
        private static readonly string[] ProviderIds = { "deepseek", "tripo" };
        private static readonly string[] ProviderLabels = { "DeepSeek 文本", "Tripo 3D" };

        public bool Visible { get; set; }

        /// <summary>供外部触发一次异步自检；不在调用线程阻塞等待。</summary>
        public void TestConnection() => RunProbe();

        private int _providerIndex;
        private string _keyDraft = "";
        private bool _editingKey;
        private bool _probing;
        private ProbeResult _pending;
        private string _status = "尚未测试连接";

        private void Start()
        {
            _keyDraft = LoadCurrentKey();
        }

        private void Update()
        {
            if (_pending == null) return;
            _status = _pending.Reason;
            _probing = false;
            _pending = null;
        }

        void OnGUI()
        {
            if (!Visible) return;

            var area = new Rect(Screen.width - 352f, 12f, 340f, 320f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("连接设置");

            DrawProviderRow();
            DrawKeyRow();
            DrawProbeRow();
            DrawStatus();

            GUILayout.EndArea();
        }

        private void DrawProviderRow()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < ProviderIds.Length; i++)
            {
                bool selected = i == _providerIndex;
                if (GUILayout.Toggle(selected, ProviderLabels[i]))
                {
                    if (!selected) SelectProvider(i);
                }
            }
            GUILayout.EndHorizontal();
        }

        private void SelectProvider(int index)
        {
            _providerIndex = index;
            _editingKey = false;
            _keyDraft = LoadCurrentKey();
            _status = "尚未测试连接";
        }

        private void DrawKeyRow()
        {
            if (_editingKey)
            {
                _keyDraft = GUILayout.TextField(_keyDraft ?? "");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("保存 Key")) SaveKey();
                if (GUILayout.Button("取消修改")) { _editingKey = false; _keyDraft = LoadCurrentKey(); }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Key：" + LocalAiConfig.Mask(LoadCurrentKey()));
                if (GUILayout.Button("修改 Key"))
                {
                    _editingKey = true;
                    _keyDraft = LoadCurrentKey();
                }
            }
        }

        private void DrawProbeRow()
        {
            if (GUILayout.Button(_probing ? "正在测试…" : "测试连接")) RunProbe();
        }

        private void DrawStatus()
        {
            bool ok = _status.StartsWith("已连接");
            GUI.color = ok ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.7f, 0.7f);
            GUILayout.Label(_status);
            GUI.color = Color.white;
        }

        private void SaveKey()
        {
            LocalAiConfig.SaveApiKey(ProviderIds[_providerIndex], _keyDraft?.Trim());
            _editingKey = false;
            _status = "Key 已保存";
        }

        private async void RunProbe()
        {
            if (_probing) return;
            _probing = true;
            _status = "正在测试连接…";
            try
            {
                if (ProviderIds[_providerIndex] == "tripo")
                    _pending = await ServiceConnectivityProbe.ProbeTripoAsync(LocalAiConfig.LoadTripo());
                else
                    _pending = await ServiceConnectivityProbe.ProbeDeepSeekAsync(DeepSeekConfig.Load());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConnectionCenter] 自检异常: {ex.GetType().Name}");
                _pending = new ProbeResult(false, "自检异常，请稍后再试");
            }
        }

        private string LoadCurrentKey()
        {
            return ProviderIds[_providerIndex] == "tripo"
                ? LocalAiConfig.LoadTripo().ApiKey
                : DeepSeekConfig.Load().ApiKey;
        }
    }
}
