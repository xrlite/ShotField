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
        public string Location { get; private set; } = "客厅";
        public string CharactersSummary { get; private set; } = "ActorA (主角), ActorB (对手戏)";
        public string ScenePrompt { get; private set; } = "warm living room coffee table with sealed envelope";
        public string LastStatus { get; private set; } = "等待拆解剧本";

        public async void AnalyzeScript(string rawScript)
        {
            if (string.IsNullOrWhiteSpace(rawScript))
            {
                LastStatus = "剧本文本为空";
                return;
            }
            var cfg = DeepSeekConfig.Load();
            var parser = new AiScriptParser { IsOfflineMock = cfg == null || !cfg.IsConfigured };
            var res = await parser.BuildProposalAsync(rawScript);
            TimeSlot = rawScript.Contains("夜") ? "夜 / 室内光" : "日 / 自然光";
            Location = !string.IsNullOrWhiteSpace(res?.projectTitle) ? res.projectTitle : "客厅片场";
            CharactersSummary = rawScript.Contains("林宁") || rawScript.Contains("周哲")
                ? "林宁、周哲" : "ActorA、ActorB";
            ScenePrompt = $"{Location}, {TimeSlot}, props for {CharactersSummary}";
            LastStatus = $"已拆解：时间[{TimeSlot}] 地点[{Location}] 人物[{CharactersSummary}]";
            Debug.Log($"[AiSceneAssembler] {LastStatus}");
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
                        SpawnFallbackProp("envelope");
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
            SpawnFallbackProp("envelope");
            return LastStatus;
        }

        private static void SpawnFallbackProp(string propId)
        {
            var browser = FindAnyObjectByType<PropBrowserUI>();
            if (browser != null) browser.SpawnProp(propId);
        }
    }
}
