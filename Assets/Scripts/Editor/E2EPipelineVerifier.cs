#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 六阶段端到端全链路一镜到底门禁断言验证器。
    /// 校验 ①剧本解析 -> ②AI四道闸(离线契约) -> ③GLB加载 -> ④沙盘双尺度 -> ⑤斯坦尼康5轨 -> ⑥导出规格/编码器在位(真编码 pending-APK)。
    /// </summary>
    public static class E2EPipelineVerifier
    {
        public const string Tag = "E2E-PIPELINE-RESULT";
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run E2E Pipeline Verifier")]
        public static void Run()
        {
            int passed = 0;
            try
            {
                var reports = E2EWorkflowRunner.ExecuteAllStages();
                if (reports == null || reports.Count != Expected)
                {
                    Debug.LogError($"[E2E-VERIFIER] 阶段数不符合预期，实际={reports?.Count ?? 0}, 预期={Expected}");
                }
                else
                {
                    for (int i = 0; i < reports.Count; i++)
                    {
                        var rep = reports[i];
                        if (rep.success)
                        {
                            passed++;
                            Debug.Log($"[E2E-VERIFIER] PASS: Step {rep.stepIndex} ({rep.stepName}) - {rep.detail}");
                        }
                        else
                        {
                            Debug.LogError($"[E2E-VERIFIER] FAIL: Step {rep.stepIndex} ({rep.stepName}) - {rep.detail}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[E2E-VERIFIER] 执行异常: {ex}");
            }

            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"{Tag}={result} ({passed}/{Expected})");
            if (result == "FAIL" && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
