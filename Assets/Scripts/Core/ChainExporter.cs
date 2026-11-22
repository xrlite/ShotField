using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// P1 离线链导出包：JSON 清单 + 每段镜头意图。
    /// 落盘目录复用 <see cref="SceneEditStateStore.ResolveFolder"/>（SHOTFIELD_SAVE_DIR → Saves/），
    /// 文件名仅用 ASCII 时间戳；不产生任何视频产物，在线成片由适配器后续提交。
    /// </summary>
    [Serializable]
    public class ChainExportManifestDto
    {
        public string packageId = "";
        public string exportedAtUtc = "";
        public string sceneTitle = "";
        public string sourceScript = "";
        public List<TimelineClipDto> clips = new List<TimelineClipDto>();
        public List<string> missingAssets = new List<string>();
        public string note = "";
    }

    public static class ChainExporter
    {
        public const string FilePrefix = "chain_export_";
        public const string FileSuffix = ".json";

        public static ChainExportManifestDto BuildManifest(
            TimelineChainModel model, ChainProposalDto proposal, string packageId)
        {
            var manifest = new ChainExportManifestDto
            {
                packageId = string.IsNullOrEmpty(packageId) ? "chain_p1" : packageId,
                exportedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'"),
                sceneTitle = proposal != null ? proposal.sceneTitle : "",
                sourceScript = proposal != null ? proposal.sourceScript : "",
                note = "P1 离线链导出：仅镜头意图与分段清单，无视频产物；在线生成待适配器配置。"
            };

            if (model != null)
            {
                for (int i = 0; i < model.Count; i++)
                {
                    var clip = model.clips[i];
                    if (clip != null) manifest.clips.Add(clip);
                }
            }
            if (proposal != null && proposal.missingAssets != null)
            {
                manifest.missingAssets.AddRange(proposal.missingAssets);
            }
            return manifest;
        }

        /// <summary>写清单 JSON 并返回绝对路径；目录不存在时自动创建。</summary>
        public static string SaveManifest(ChainExportManifestDto manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            string folder = SceneEditStateStore.ResolveFolder();
            Directory.CreateDirectory(folder);

            string fileName = FilePrefix + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + FileSuffix;
            string path = Path.Combine(folder, fileName);

            string json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return path;
        }
    }
}
