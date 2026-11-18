#if UNITY_EDITOR
using System;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class AiAndVideoSystemVerifier
    {
        // 2 条解析 + 2 条交接 + 2 条播放器初始化 + 1 条模式切换 + 1 条模式枚举 = 8。
        private const int ExpectedAssertions = 8;

        [MenuItem("镜场 ShotField/验证/验证 AI 系统与生成回片对照")]
        public static void Run()
        {
            int passCount = 0;
            void Assert(string name, bool condition)
            {
                if (condition)
                {
                    passCount++;
                    Debug.Log($"[AiAndVideoSystemVerifier] PASS: {name}");
                }
                else
                {
                    Debug.LogError($"[AiAndVideoSystemVerifier] FAIL: {name}");
                    throw new Exception("Assertion failed: " + name);
                }
            }

            // Test 1: DeepSeek 剧本结构化服务
            var parser = new AiScriptParser { IsOfflineMock = true };
            var parseTask = parser.ParseScriptAsync("内景 客厅 日\n演员A警惕地走向茶几，试图掩盖桌上的信件。");
            parseTask.Wait();
            var analysis = parseTask.Result;
            Assert("DeepSeek 成功解析剧本角色与动作节拍", analysis != null && analysis.characters.Count > 0 && analysis.beats.Count >= 2);
            Assert("节拍包含分镜类型建议", !string.IsNullOrEmpty(analysis.beats[0].suggestedShotType));

            // Test 2: Seedance 视频生成交接请求
            var seedance = new SeedanceBridge { IsOfflineMock = true };
            var req = new SeedanceTaskRequest
            {
                shotId = "SC01-S01",
                prompt = "神秘低角度镜头，光影渐暗",
                clayReferencePath = "Assets/Exports/SC01-S01_clay.png",
                durationSeconds = 3.5f,
                aspectRatio = "16:9"
            };
            var seedanceTask = seedance.SubmitGenerationAsync(req);
            seedanceTask.Wait();
            var seedanceRes = seedanceTask.Result;
            Assert("Seedance 成功接入交接请求", seedanceRes != null && !string.IsNullOrEmpty(seedanceRes.taskId));
            Assert("Seedance 产生成片视频交接路径", !string.IsNullOrEmpty(seedanceRes.localVideoPath));

            // Test 3: VideoPlayer 片场成片比对组件
            var go = new GameObject("Test_Video_Reviewer");
            try
            {
                var player = go.AddComponent<VideoComparisonPlayer>();
                player.Initialize();
                Assert("VideoPlayer 组件初始化且绑定 RenderTexture", player.videoPlayer != null && player.comparisonOutput != null);
                Assert("RenderTexture 尺寸为 1080P", player.comparisonOutput.width == 1920 && player.comparisonOutput.height == 1080);

                // Test 4: 播放控制与模式切换
                player.LoadVideo("http://localhost/sample.mp4");
                player.SetMode(ComparisonDisplayMode.PictureInPicture);
                Assert("支持画中画与分屏比对模式切换", player.displayMode == ComparisonDisplayMode.PictureInPicture);

                // 无视频源时 SeekNormalized 会安全返回，不能拿「不抛错」当断言。
                // 改为逐个验证三种比对模式都能真正设置成功（可失败的断言）。
                player.SeekNormalized(0.5f);
                bool allModesApplied = true;
                foreach (ComparisonDisplayMode mode in Enum.GetValues(typeof(ComparisonDisplayMode)))
                {
                    player.SetMode(mode);
                    allModesApplied &= player.displayMode == mode;
                }
                Assert("三种比对模式均可设置且状态回读一致",
                    allModesApplied && Enum.GetValues(typeof(ComparisonDisplayMode)).Length == 3);

                if (passCount != ExpectedAssertions)
                {
                    Debug.LogError($"AI-AND-VIDEO-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }
                Debug.Log($"AI-AND-VIDEO-RESULT=PASS ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
