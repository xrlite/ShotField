#if UNITY_EDITOR
using System;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class FiveActsPipelineVerifier
    {
        // 1 条枚举数量 + 5 条逐幕描述符 + 3 条请求/解析 = 9。
        private const int ExpectedAssertions = 9;

        [MenuItem("镜场 ShotField/验证/验证五幕剧本与 Seedance 管线")]
        public static void Run()
        {
            int passCount = 0;
            void Assert(string name, bool condition)
            {
                if (condition)
                {
                    passCount++;
                    Debug.Log($"[FiveActsPipelineVerifier] PASS: {name}");
                }
                else
                {
                    Debug.LogError($"[FiveActsPipelineVerifier] FAIL: {name}");
                    throw new Exception("Assertion failed: " + name);
                }
            }

            var pipeline = new FiveActsProductionPipeline();

            // Test 1: 五幕结构完整性检查
            var acts = (ProductionAct[])Enum.GetValues(typeof(ProductionAct));
            Assert("五幕结构枚举数量为 5", acts.Length == 5);

            foreach (var act in acts)
            {
                var desc = pipeline.GetActDescriptor(act);
                Assert($"Act {act} 描述符有效且包含完整 Prompt 模板",
                    desc != null && !string.IsNullOrEmpty(desc.promptTemplate) && !string.IsNullOrEmpty(desc.suggestedCameraMotion));
            }

            // Test 2: 第二幕（拍摄/运镜）Prompt 组装
            var reqAct2 = pipeline.GenerateSeedanceRequest(ProductionAct.Act2_Shooting, "Assets/Exports/SC01-S01_clay.png");
            Assert("第二幕生成请求携带运镜参数与 Prompt",
                reqAct2.prompt.Contains("Low Dolly Push") && reqAct2.shotId == "SC01-Act2_Shooting");

            // Test 3: 第五幕（成片分屏）生成请求
            var reqAct5 = pipeline.GenerateSeedanceRequest(ProductionAct.Act5_FinalCut, "Assets/Exports/SC01-S05_clay.png", "16:9");
            Assert("第五幕成片比对请求生成就绪",
                reqAct5.durationSeconds >= 5.0f && reqAct5.prompt.Contains("Synchronized Playback"));

            // Test 4: 剧本结构化解析打通
            var analysis = pipeline.ParseActScript(ProductionAct.Act2_Shooting);
            Assert("剧本解析器成功为第二幕提取角色与节拍",
                analysis != null && analysis.beats.Count > 0);

            if (passCount != ExpectedAssertions)
            {
                Debug.LogError($"FIVE-ACTS-PIPELINE-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"FIVE-ACTS-PIPELINE-RESULT=PASS ({passCount}/{ExpectedAssertions})");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
