#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using ShotField.Core.Community;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 创作资产社区断言验证器：
    /// 校验官方内置包读取、分类与关键词过滤、用户资产发布、SHA-256 计算与 JSON 契约。
    /// </summary>
    public static class CommunityAssetVerifier
    {
        public const string Tag = "COMMUNITY-ASSET-RESULT";
        private const int Expected = 5;

        [MenuItem("ShotField/Verifiers/Run Community Asset Verifier")]
        public static void Run()
        {
            int passed = 0;
            string tempDir = Path.Combine(Application.temporaryCachePath, "CommunityTest_" + Guid.NewGuid().ToString("N"));

            try
            {
                var repo = new LocalCommunityAssetRepository();
                var list = repo.ListAsync(null).Result;
                Check("扫描官方内置资产包数量 >= 3", list != null && list.Count >= 3, ref passed);

                var first = list != null && list.Count > 0 ? list[0] : null;
                Check("官方资产包元数据完整且标为官方", first != null && first.isOfficial && !string.IsNullOrEmpty(first.title) && first.sha256 != null && first.sha256.Length == 64, ref passed);

                var queryKind = new CommunityQuery { kind = CommunityAssetKind.SceneTemplate };
                var filteredByKind = repo.ListAsync(queryKind).Result;
                Check("按资产类型 SceneTemplate 过滤成功", filteredByKind != null && filteredByKind.Count >= 3, ref passed);

                var queryKeyword = new CommunityQuery { keyword = "夜戏" };
                var filteredByKw = repo.ListAsync(queryKeyword).Result;
                Check("按关键词 '夜戏' 检索命中目标模板", filteredByKw != null && filteredByKw.Count == 1 && filteredByKw[0].id == "sfpack_official_sc01_night", ref passed);

                Directory.CreateDirectory(tempDir);
                string srcDir = Path.Combine(tempDir, "src");
                string userDir = Path.Combine(tempDir, "user");
                Directory.CreateDirectory(srcDir);
                string testPackFile = Path.Combine(srcDir, "user_custom_test.sfpack.zip");
                File.WriteAllText(testPackFile, "SHOTFIELD_TEST_USER_PACK_PAYLOAD");
                var userRepo = new LocalCommunityAssetRepository(Path.Combine(Application.streamingAssetsPath, "CommunityCatalog"), userDir);

                var meta = new CommunityAssetManifest
                {
                    id = "sfpack_user_test_01",
                    title = "用户自制夜景",
                    author = "UserDirector",
                    kind = CommunityAssetKind.SceneTemplate
                };
                var pubResult = userRepo.PublishPackageAsync(testPackFile, meta).Result;
                Check("用户包发布成功并自动计算 SHA-256", pubResult != null && !pubResult.isOfficial && !string.IsNullOrEmpty(pubResult.sha256) && File.Exists(Path.Combine(userDir, "user_custom_test.sfpack.json")), ref passed);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[COMMUNITY-ASSET] 异常: {ex}");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { /* 忽略清理错误 */ }
                }
            }

            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"{Tag}={result} ({passed}/{Expected})");
            if (result == "FAIL" && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void Check(string label, bool ok, ref int passed)
        {
            if (ok) { passed++; Debug.Log($"[COMMUNITY-ASSET] PASS: {label}"); }
            else Debug.LogError($"[COMMUNITY-ASSET] FAIL: {label}");
        }
    }
}
#endif
