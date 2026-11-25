using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShotField.Core.Community
{
    [Serializable]
    public class CommunityServerConfig
    {
        public string baseUrl = "https://api.shotfield.com/v1";
        public string apiToken = "";
        public int timeoutSeconds = 15;
    }

    /// <summary>
    /// 云端创作资产社区服务桩：预留未来 RESTful 服务端接口与鉴权契约。
    /// </summary>
    public class RemoteHttpCommunityRepository : ICommunityAssetRepository
    {
        public CommunityServerConfig config;

        public RemoteHttpCommunityRepository(CommunityServerConfig cfg = null)
        {
            config = cfg ?? new CommunityServerConfig();
        }

        public Task<IReadOnlyList<CommunityAssetManifest>> ListAsync(CommunityQuery query)
        {
            // 待云端上线对接 HTTP GET /v1/assets?kind=...
            var fallback = new List<CommunityAssetManifest>();
            return Task.FromResult<IReadOnlyList<CommunityAssetManifest>>(fallback);
        }

        public Task<CommunityAssetManifest> GetManifestAsync(string assetId)
        {
            // 待云端上线对接 HTTP GET /v1/assets/{id}
            return Task.FromResult<CommunityAssetManifest>(null);
        }

        public Task<string> DownloadPackageAsync(string assetId, string destinationDir)
        {
            // 待云端上线对接 HTTP GET /v1/assets/{id}/download
            throw new NotImplementedException("云端下载服务规划中，请切换 LocalCommunityAssetRepository 使用本地案例");
        }

        public Task<CommunityAssetManifest> PublishPackageAsync(string localPackagePath, CommunityAssetManifest meta)
        {
            // 待云端上线对接 HTTP POST /v1/assets/publish
            throw new NotImplementedException("云端发布服务规划中，请切换 LocalCommunityAssetRepository 使用本地案例");
        }
    }
}
