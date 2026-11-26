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
            var lib = FindAnyObjectByType<ShotLibrary>();
            int count = lib != null ? lib.Count : 0;
            string outDir = Path.Combine(Application.persistentDataPath, "Exports");
            Directory.CreateDirectory(outDir);
            string manifest = Path.Combine(outDir, "shotfield_export_manifest.json");
            File.WriteAllText(manifest, $"{{\"shots\":{count},\"resolution\":\"1080p\",\"ratio\":\"16:9\",\"model\":\"{ModelId}\"}}");
            return LastStatus = $"已导出 1080p 参考包 ({count} 镜) -> {outDir}";
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
