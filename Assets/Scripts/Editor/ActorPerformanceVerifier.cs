#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 1:1 真人表演实录断言验证器：
    /// 校验 3 点 VR 姿态采样、JSON 护照双向往返、IK 驱动还原与回放误差 (<1cm)。
    /// </summary>
    public static class ActorPerformanceVerifier
    {
        public const string Tag = "ACTOR-PERF-RESULT";
        private const int Expected = 5;

        [MenuItem("ShotField/Verifiers/Run Actor Performance Verifier")]
        public static void Run()
        {
            int passed = 0;
            var hostGo = new GameObject("[Test_ActorHost]");
            var headGo = new GameObject("[Test_Head]");
            var lHandGo = new GameObject("[Test_LHand]");
            var rHandGo = new GameObject("[Test_RHand]");

            try
            {
                var clip = new ActorPerformanceClip { clipId = "Perf_Test_01", actorId = "ActorA", scaleRatio = 1.05f };
                clip.AddFrame(0f, Vector3.zero, Quaternion.identity, new Vector3(0, 1.6f, 0), Quaternion.identity,
                    new Vector3(-0.3f, 1.0f, 0.4f), Quaternion.identity, new Vector3(0.3f, 1.0f, 0.4f), Quaternion.identity);
                clip.AddFrame(1.0f, new Vector3(0, 0, 1.0f), Quaternion.identity, new Vector3(0, 1.6f, 1.0f), Quaternion.identity,
                    new Vector3(-0.3f, 1.2f, 1.4f), Quaternion.identity, new Vector3(0.3f, 1.2f, 1.4f), Quaternion.identity);

                Check("片段帧率与时长统计有效", clip.Count == 2 && Mathf.Approximately(clip.duration, 1.0f), ref passed);

                var mid = clip.Evaluate(0.5f);
                float distHead = Vector3.Distance(mid.headPosition, new Vector3(0, 1.6f, 0.5f));
                Check("50% 时间插值精确无损", distHead < 0.001f, ref passed);

                string json = clip.ToJson();
                var clipBack = ActorPerformanceClip.FromJson(json);
                Check("JSON 契约双向往返无丢字段", clipBack != null && clipBack.Count == 2 && Mathf.Abs(clipBack.scaleRatio - 1.05f) < 0.001f, ref passed);

                var take = clip.ToActorTake();
                Check("向后平滑转换至 ActorTake 护照", take != null && take.Count == 2 && take.duration >= 1.0f, ref passed);

                var ik = hostGo.AddComponent<HumanoidThreePointIkDriver>();
                ik.ApplyFrame(mid);
                float error = Vector3.Distance(ik.headPosition, mid.headPosition) + Vector3.Distance(ik.leftHandPosition, mid.leftHandPosition);
                Check("IK 驱动器回放复现位姿误差 < 1cm", error < 0.01f, ref passed);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ACTOR-PERF] 异常: {ex}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostGo);
                UnityEngine.Object.DestroyImmediate(headGo);
                UnityEngine.Object.DestroyImmediate(lHandGo);
                UnityEngine.Object.DestroyImmediate(rHandGo);
            }

            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"{Tag}={result} ({passed}/{Expected})");
            if (result == "FAIL" && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void Check(string label, bool ok, ref int passed)
        {
            if (ok) { passed++; Debug.Log($"[ACTOR-PERF] PASS: {label}"); }
            else Debug.LogError($"[ACTOR-PERF] FAIL: {label}");
        }
    }
}
#endif
