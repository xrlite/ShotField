using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core.Community
{
        [Serializable]
        public class CommunityCatalogIndex { public List<CommunityCatalogEntry> packages = new List<CommunityCatalogEntry>(); }
        [Serializable]
        public class CommunityCatalogEntry { public string id; public string manifestFileName; public string packageFileName; }

    /// <summary>
    /// 本地先行创作资产仓库：Android 兼容（StreamingAssets 经 catalog_index.json + UnityWebRequest）。
    /// </summary>
    public class LocalCommunityAssetRepository : ICommunityAssetRepository
    {
        private readonly string _officialCatalogDir;
        private readonly string _userCatalogDir;

        public LocalCommunityAssetRepository()
        {
            _officialCatalogDir = Path.Combine(Application.streamingAssetsPath, "CommunityCatalog");
            _userCatalogDir = Path.Combine(Application.persistentDataPath, "CommunityUser");
        }
        public LocalCommunityAssetRepository(string officialDir, string userDir) { _officialCatalogDir = officialDir; _userCatalogDir = userDir; }

        public async Task<IReadOnlyList<CommunityAssetManifest>> ListAsync(CommunityQuery query)
        {
            var results = new List<CommunityAssetManifest>();
            await ScanOfficialCatalogAsync(results);
            ScanUserDirectory(_userCatalogDir, results);

            if (query != null)
            {
                if (query.kind.HasValue) results.RemoveAll(m => m.kind != query.kind.Value);
                if (!string.IsNullOrEmpty(query.keyword))
                    results.RemoveAll(m => !(m.title != null && m.title.Contains(query.keyword)) &&
                                           !(m.tags != null && m.tags.Contains(query.keyword)));
                if (query.limit > 0 && results.Count > query.limit)
                    results = results.GetRange(0, query.limit);
            }
            return results;
        }

        public async Task<CommunityAssetManifest> GetManifestAsync(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return null;
            var list = await ListAsync(null);
            for (int i = 0; i < list.Count; i++) if (list[i].id == assetId) return list[i];
            return null;
        }

        public async Task<string> DownloadPackageAsync(string assetId, string destinationDir)
        {
            var manifest = await GetManifestAsync(assetId);
            if (manifest == null) throw new FileNotFoundException("找不到资产: " + assetId);
            Directory.CreateDirectory(destinationDir);
            string dest = Path.Combine(destinationDir, Path.GetFileName(manifest.packageRelPath));

            if (manifest.isOfficial)
            {
                byte[] data = await ReadStreamingBytesAsync(manifest.packageRelPath);
                if (data != null && data.Length > 0) File.WriteAllBytes(dest, data);
            }
            else if (File.Exists(manifest.packageRelPath))
            {
                File.Copy(manifest.packageRelPath, dest, true);
            }
            return dest;
        }

        public Task<CommunityAssetManifest> PublishPackageAsync(string localPackagePath, CommunityAssetManifest meta)
        {
            if (string.IsNullOrEmpty(localPackagePath) || !File.Exists(localPackagePath))
                throw new FileNotFoundException("发布包文件不存在", localPackagePath);
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            Directory.CreateDirectory(_userCatalogDir);
            string fileName = Path.GetFileName(localPackagePath);
            string targetPkg = Path.Combine(_userCatalogDir, fileName);
            if (!string.Equals(Path.GetFullPath(localPackagePath), Path.GetFullPath(targetPkg), StringComparison.OrdinalIgnoreCase))
                File.Copy(localPackagePath, targetPkg, true);

            meta.packageRelPath = targetPkg;
            meta.sha256 = ComputeSha256(targetPkg);
            meta.createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            string metaPath = Path.ChangeExtension(targetPkg, ".json");
            File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true));
            return Task.FromResult(meta);
        }

        private async Task ScanOfficialCatalogAsync(List<CommunityAssetManifest> list)
        {
            string indexPath = Path.Combine(_officialCatalogDir, "catalog_index.json");
            string indexText = await ReadStreamingTextAsync(indexPath);
            if (string.IsNullOrEmpty(indexText)) return;

            var index = JsonUtility.FromJson<CommunityCatalogIndex>(indexText);
            if (index == null || index.packages == null) return;

            for (int i = 0; i < index.packages.Count; i++)
            {
                var entry = index.packages[i];
                string manifestPath = Path.Combine(_officialCatalogDir, entry.manifestFileName);
                string mText = await ReadStreamingTextAsync(manifestPath);
                if (string.IsNullOrEmpty(mText)) continue;
                var manifest = JsonUtility.FromJson<CommunityAssetManifest>(mText);
                if (manifest != null && !string.IsNullOrEmpty(manifest.id))
                {
                    manifest.isOfficial = true;
                    manifest.packageRelPath = Path.Combine(_officialCatalogDir, entry.packageFileName);
                    list.Add(manifest);
                }
            }
        }

        private void ScanUserDirectory(string dir, List<CommunityAssetManifest> list)
        {
            if (!Directory.Exists(dir)) return;
            var jsonFiles = Directory.GetFiles(dir, "*.json");
            for (int i = 0; i < jsonFiles.Length; i++)
            {
                try
                {
                    string txt = File.ReadAllText(jsonFiles[i]);
                    var m = JsonUtility.FromJson<CommunityAssetManifest>(txt);
                    if (m != null && !string.IsNullOrEmpty(m.id))
                    {
                        m.isOfficial = false;
                        if (string.IsNullOrEmpty(m.packageRelPath))
                            m.packageRelPath = Path.ChangeExtension(jsonFiles[i], ".sfpack.zip");
                        list.Add(m);
                    }
                }
                catch { /* 容错忽略 */ }
            }
        }

        private static async Task<string> ReadStreamingTextAsync(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var req = UnityEngine.Networking.UnityWebRequest.Get(path))
            {
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                return req.result == UnityEngine.Networking.UnityWebRequest.Result.Success ? req.downloadHandler.text : null;
            }
#else
            if (File.Exists(path)) return File.ReadAllText(path);
            return null;
#endif
        }

        private static async Task<byte[]> ReadStreamingBytesAsync(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var req = UnityEngine.Networking.UnityWebRequest.Get(path))
            {
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                return req.result == UnityEngine.Networking.UnityWebRequest.Result.Success ? req.downloadHandler.data : null;
            }
#else
            if (File.Exists(path)) return File.ReadAllBytes(path);
            return null;
#endif
        }

        public static string ComputeSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
