using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShotField.Core.Community
{
    public enum CommunityAssetKind
    {
        SceneTemplate,
        PropAsset,
        ActorTakeClip,
        CameraRigPreset
    }

    [Serializable]
    public class CommunityAssetManifest
    {
        public string id;
        public string title;
        public string author;
        public string version = "1.0.0";
        public CommunityAssetKind kind;
        public string license = "CC0-1.0";
        public string tags;
        public string thumbnailRelPath;
        public string packageRelPath;
        public string sha256;
        public string createdAt;
        public bool isOfficial;
        public string characterTurnaroundPath;
        public string sceneConceptPath;
    }

    public class CommunityQuery
    {
        public CommunityAssetKind? kind;
        public string keyword;
        public string tag;
        public int limit = 20;
    }

    /// <summary>
    /// 创作资产社区异步仓库统一契约。
    /// 依赖倒置：UI 与业务只依赖本接口，本地与云端无缝切换。
    /// </summary>
    public interface ICommunityAssetRepository
    {
        Task<IReadOnlyList<CommunityAssetManifest>> ListAsync(CommunityQuery query);
        Task<CommunityAssetManifest> GetManifestAsync(string assetId);
        Task<string> DownloadPackageAsync(string assetId, string destinationDir);
        Task<CommunityAssetManifest> PublishPackageAsync(string localPackagePath, CommunityAssetManifest meta);
    }
}
