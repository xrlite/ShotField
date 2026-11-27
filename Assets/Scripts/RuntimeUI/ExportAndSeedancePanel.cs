using System;
using System.IO;
using System.Threading.Tasks;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 导出与字节火山方舟 Seedance 2.5 对接控制器：
    /// 1) 导出本地 1080p 渲染参考包与成片清单；
    /// 2) 预留字节方舟 API Key / Model 配置与一键提交/轮询入口（Key 仅存本地安全路径）。
    /// </summary>
    public sealed class ExportAndSeedancePanel : MonoBehaviour
    {
        private const string ArkKeyPref = "ShotField.ArkTempKey.Session";
        public string ModelId { get; set; } = "doubao-seedance-2-5-260628";
        public string LastStatus { get; private set; } = "就绪 · 可导出本地参考包或配置字节视频接口";
        public string LastTaskId { get; private set; } = "";

        private readonly SeedanceBridge _bridge = new SeedanceBridge();

        public void SaveArkKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) { LastStatus = "请输入有效的方舟 API Key"; return; }
            string path = Path.Combine(Application.persistentDataPath, "ark_seedance.local.txt");
            File.WriteAllText(path, key.Trim());
            LastStatus = "已安全保存字节方舟临时 Key（仅本地，不进工程包）";
        }

        public string LoadArkKey()
        {
            string path = Path.Combine(Application.persistentDataPath, "ark_seedance.local.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }

        public string ExportLocalReferencePackage()
        {
            var workbench = FindAnyObjectByType<SpatialWorkbench>();
            var shot = workbench != null ? workbench.SelectedShot : null;
            if (shot == null)
                return LastStatus = "导出失败：请先在拍摄页保存至少一个镜头";
            if (workbench.monitor == null)
                return LastStatus = "导出失败：找不到监看相机，无法生成参考帧";

            string outDir = Path.Combine(Application.persistentDataPath, "Exports");
            Directory.CreateDirectory(outDir);
            string path = Path.Combine(outDir,
                $"ShotField_{shot.shotId}_{DateTime.Now:yyyyMMdd_HHmm_ssfff}.zip");
            try
            {
                string message = SpatialStudioLayout.ExportDynamicOrStatic(workbench, shot, workbench.monitor, path);
                if (!ShotPackageExporter.VerifyZipPackage(path, out var entries) ||
                    !File.Exists(path) || new FileInfo(path).Length == 0 || entries.Count < 3)
                    return LastStatus = "导出失败：参考包校验未通过（缺少护照、清单或参考帧）";
                return LastStatus = $"{message}\n镜头 {shot.shotId} / 包内 {entries.Count} 项 / 1080p 监看源";
            }
            catch (System.Exception ex)
            {
                return LastStatus = $"导出失败：{ex.Message}";
            }
        }

        public async Task<string> SubmitOrPreviewSeedanceAsync(string prompt)
        {
            string key = LoadArkKey();
            _bridge.ApiKey = key;
            _bridge.IsOfflineMock = string.IsNullOrEmpty(key);
            var req = new SeedanceTaskRequest
            {
                prompt = string.IsNullOrWhiteSpace(prompt) ? "电影感室内双人对话推镜，暖调台灯光影，16:9" : prompt,
                modelId = ModelId,
                resolution = "1080p",
                aspectRatio = "16:9",
                durationSeconds = 5f
            };
            var res = await _bridge.SubmitGenerationAsync(req);
            LastTaskId = res.taskId ?? "";
            return LastStatus = $"{res.status}: {res.message} (task={LastTaskId})";
        }
    }
}
