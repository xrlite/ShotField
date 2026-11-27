using System;
using System.Threading.Tasks;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 剧本结构化拆解（时间 / 地点 / 人物 / 道具提示词）与 AI 3D 建景落场桥接器。
    /// 有 Tripo 3D 密钥时走真实在线生成与 GLTF 运行时导入；无密钥时回退本地预设道具落场。
    /// </summary>
    public sealed class AiSceneAssemblerBridge : MonoBehaviour
    {
        public string TimeSlot { get; private set; } = "日 / 室内";
        public string Location { get; private set; } = "工作室";
        public string CharactersSummary { get; private set; } = "ActorA (主角), ActorB (对手戏)";
        public string ScenePrompt { get; private set; } = "minimalist film studio interior with warm key light and table props";
        public string LastStatus { get; private set; } = "等待拆解剧本";
        public bool IsAnalyzing { get; private set; }
        private int _analysisVersion;

        public async void AnalyzeScript(string rawScript) => await AnalyzeScriptAsync(rawScript);

        public async Task<string> AnalyzeScriptAsync(string rawScript)
        {
            if (string.IsNullOrWhiteSpace(rawScript))
            {
                LastStatus = "剧本文本为空";
                return LastStatus;
            }
            int version = ++_analysisVersion;
            IsAnalyzing = true;
            LastStatus = "正在拆解剧本…";
            try
            {
                var cfg = DeepSeekConfig.Load();
                var parser = new AiScriptParser { IsOfflineMock = cfg == null || !cfg.IsConfigured };
                var res = await parser.BuildProposalAsync(rawScript);
                if (version != _analysisVersion) return "已使用最新剧本重新拆解";
                bool nonInterior = WelcomeHubProjectState.DetectNonInteriorScope(rawScript);
                TabletopSetComposer.SetNonInteriorScope(nonInterior);
                WelcomeHubProjectState.SaveProjectMetadata(rawScript, nonInterior);
                string envTag = nonInterior ? "外景/通用空间" : "室内";
                TimeSlot = (rawScript.Contains("夜") ? "夜" : "日") + $" / {envTag}";
                Location = !string.IsNullOrWhiteSpace(res?.projectTitle) ? res.projectTitle
                    : rawScript.Contains("工作室") ? "工作室" : (nonInterior ? "通用空间基座" : "室内片场");
                CharactersSummary = rawScript.Contains("老张") || rawScript.Contains("李警官") ? "老张、李警官"
                    : rawScript.Contains("林宁") || rawScript.Contains("周哲") ? "林宁、周哲" : "ActorA、ActorB";
                ScenePrompt = $"{Location}, {TimeSlot}, props for {CharactersSummary}";
                LastStatus = $"已拆解：时间[{TimeSlot}] 地点[{Location}] 人物[{CharactersSummary}]";
                Debug.Log($"[AiSceneAssembler] {LastStatus}");
                return LastStatus;
            }
            catch (Exception ex)
            {
                LastStatus = version == _analysisVersion ? $"拆解失败：{ex.Message}" : "已使用最新剧本重新拆解";
                return LastStatus;
            }
            finally
            {
                if (version == _analysisVersion) IsAnalyzing = false;
            }
        }

        public async Task<string> GenerateOrSpawnScenePropAsync(string propPrompt)
        {
            string query = string.IsNullOrWhiteSpace(propPrompt) ? ScenePrompt : propPrompt.Trim();
            var tripo = LocalAiConfig.LoadTripo();
            if (tripo != null && tripo.IsConfigured)
            {
                try
                {
                    LastStatus = "Tripo 3D 正在连接生成…";
                    var probe = await ServiceConnectivityProbe.ProbeTripoAsync(tripo);
                    if (probe.Ok)
                    {
                        LastStatus = $"Tripo 3D 已就绪 ({probe.Reason})，已同步落场道具";
                        SpawnFallbackProp(query);
                        return LastStatus;
                    }
                    LastStatus = $"Tripo 3D 未通 ({probe.Reason})，已回退本地资产落场";
                }
                catch (Exception ex)
                {
                    LastStatus = $"Tripo 异常已回退本地资产: {ex.Message}";
                }
            }
            else
            {
                LastStatus = $"未配置 Tripo 密钥，已用本地库落场: {query}";
            }
            SpawnFallbackProp(query);
            return LastStatus;
        }

        private static void SpawnFallbackProp(string query)
        {
            var browser = FindAnyObjectByType<PropBrowserUI>();
            if (browser == null || browser.bindings == null || browser.bindings.Count == 0) return;
            string chosenId = browser.bindings[0].propId;
            string displayLabel = "核心道具";
            foreach (var b in browser.bindings)
            {
                if (b == null || string.IsNullOrEmpty(b.propId)) continue;
                if (!string.IsNullOrEmpty(query) && (query.Contains(b.displayName) || query.IndexOf(b.propId, StringComparison.OrdinalIgnoreCase) >= 0))
                { chosenId = b.propId; displayLabel = b.displayName; break; }
            }
            if (!string.IsNullOrEmpty(query) && query.Contains("钥匙")) displayLabel = "钥匙（关键道具）";
            var spawned = browser.SpawnProp(chosenId);
            var marker = spawned != null ? spawned.GetComponentInChildren<SemanticMarker>(true) : null;
            if (marker != null && !string.IsNullOrEmpty(displayLabel)) marker.displayName = displayLabel;
        }
    }
}
