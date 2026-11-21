using System;
using UnityEngine;
using UnityEditor;
using ShotField.Core;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static class Phase2IntegrationVerifier
    {
        public static void Run()
        {
            int passCount = 0;
            int totalTests = 5;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BuildAndroid.ScenePath);
            var bench = UnityEngine.Object.FindAnyObjectByType<SpatialWorkbench>();
            if (!bench || bench.gameObject.scene != scene) throw new InvalidOperationException("Missing saved workbench");
            var motion = bench.GetComponent<CameraMotionRecorder>();
            var board = bench.GetComponent<TabletopDirector>();
            var actor = bench.GetComponent<ActorTakePlayer>();
            var audience = bench.GetComponent<AudienceLab>();
            if (!motion || !motion.poseSource || !motion.filmCamera || !motion.hmdObserver ||
                motion.filmCamera == motion.hmdObserver || !board || !board.sceneRoot1to1 ||
                !board.tabletopProxyRoot || !board.observerCamera || !actor ||
                actor.actorTransform != SemanticMarker.Find("actorA").transform || !audience ||
                audience.audienceHead != motion.hmdObserver.transform)
                throw new InvalidOperationException("Invalid SC01 studio bindings");
            var testRoot = new GameObject("Phase2TestRoot");
            try
            {
                // Test 1: 微缩沙盘坐标正反变换可逆性 (1:20 精度 < 0.001m)
                var dirGo = new GameObject("TabletopDirectorGo");
                dirGo.transform.SetParent(testRoot.transform);
                var dir = dirGo.AddComponent<TabletopDirector>();
                var anchorGo = new GameObject("Anchor");
                anchorGo.transform.SetParent(testRoot.transform);
                dir.tabletopAnchor = anchorGo.transform;
                dir.miniScale = 0.05f;

                var worldPos = new Vector3(2.5f, 1.4f, 3.8f);
                var tabletopPos = dir.WorldToTabletop(worldPos);
                var restoredWorldPos = dir.TabletopToWorld(tabletopPos);

                if (Vector3.Distance(worldPos, restoredWorldPos) < 0.001f)
                {
                    passCount++;
                    Debug.Log($"[PASS] Test 1: 沙盘 1:20 坐标正反变换可逆精度高 (误差 < 0.001m)");
                }
                else Debug.LogError($"[FAIL] Test 1: 逆变换误差过大 dist={Vector3.Distance(worldPos, restoredWorldPos)}");

                // Test 2: 双尺度沙盘模式切换状态与视点切换
                var camGo = new GameObject("ObserverCam");
                camGo.transform.SetParent(testRoot.transform);
                dir.observerCamera = camGo.transform;
                dir.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
                bool isMini = dir.currentMode == TabletopDirector.ScaleMode.TabletopMini;
                dir.SwitchMode(TabletopDirector.ScaleMode.Immersive1to1);
                bool isImmersive = dir.currentMode == TabletopDirector.ScaleMode.Immersive1to1;

                if (isMini && isImmersive && camGo.transform.position == Vector3.zero)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 2: 1:1 片场与 1:20 微缩沙盘双尺度模式切换正常");
                }
                else Debug.LogError("[FAIL] Test 2: 沙盘尺度切换状态异常");

                // Test 3: 虚拟演员表演 Take 时序采样与插值精度
                var take = new ActorTake { takeId = "Take_SC01_01", actorId = "actorA" };
                take.AddKeyframe(0f, new Vector3(0, 0, 0), Quaternion.identity, "Stand");
                take.AddKeyframe(1.0f, new Vector3(1, 0, 1), Quaternion.Euler(0, 45, 0), "Walk");
                take.AddKeyframe(2.0f, new Vector3(2, 0, 2), Quaternion.Euler(0, 90, 0), "Sit");

                var midKey = take.Evaluate(0.5f);
                if (take.Count == 3 && Mathf.Approximately(take.duration, 2.0f) &&
                    Mathf.Approximately(midKey.position.x, 0.5f) && Mathf.Approximately(midKey.position.z, 0.5f))
                {
                    passCount++;
                    Debug.Log("[PASS] Test 3: 演员 Take 时序动作采样与半路点插值精确");
                }
                else Debug.LogError($"[FAIL] Test 3: 演员 Take 插值异常 x={midKey.position.x} z={midKey.position.z}");

                // Test 4: 演员 Take 多机位拍摄复用独立性
                var actorGo = new GameObject("ActorA_Proxy");
                actorGo.transform.SetParent(testRoot.transform);
                var takePlayer = actorGo.AddComponent<ActorTakePlayer>();
                takePlayer.actorTransform = actorGo.transform;
                takePlayer.currentTake = take.Clone();

                takePlayer.StartPlayback();
                takePlayer.AdvancePlayback(0.5f);
                var midpoint = actorGo.transform.position;
                takePlayer.AdvancePlayback(3f);
                if (midpoint == new Vector3(0.5f, 0, 0.5f) &&
                    actorGo.transform.position == new Vector3(2, 0, 2) &&
                    takePlayer.CurrentState == ActorTakePlayer.PlayState.Idle)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 4: 演员播放器实际应用中间帧和终帧");
                }
                else Debug.LogError("[FAIL] Test 4: 演员播放器未正确应用轨迹");

                // Test 5: 观众实验室视线采样与注视热度统计
                var labGo = new GameObject("AudienceLabGo");
                labGo.transform.SetParent(testRoot.transform);
                var lab = labGo.AddComponent<AudienceLab>();
                lab.audienceHead = labGo.transform;
                lab.focusTargetId = "envelope";

                var target = new GameObject("Screening prop");
                target.transform.SetParent(testRoot.transform);
                labGo.transform.position = new Vector3(200, 200, 200);
                target.transform.position = labGo.transform.position + Vector3.forward * 3f;
                target.AddComponent<BoxCollider>();
                target.AddComponent<SemanticMarker>().markerId = "phase2-screening-prop";
                Physics.SyncTransforms();
                lab.StartScreeningSession(null, "phase2-screening-prop");
                lab.CaptureSample(0f);
                labGo.transform.rotation = Quaternion.Euler(0, 90, 0);
                lab.CaptureSample(0.7f);
                lab.CaptureSample(1f);
                float focusRatio = lab.GetFocusRatio();

                if (Mathf.Approximately(focusRatio, 0.7f) && lab.gazeSamples.Count == 3)
                {
                    passCount++;
                    Debug.Log($"[PASS] Test 5: 观众实验室视线注意力采样与关键道具注视占比统计准确 ({focusRatio * 100f:F0}%)");
                }
                else Debug.LogError($"[FAIL] Test 5: 观众视线统计比例异常 ratio={focusRatio}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EXCEPTION] 第二阶段集成验证异常: {ex}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testRoot);
            }

            if (passCount == totalTests)
            {
                Debug.Log($"PHASE2-VERIFY-RESULT=PASS ({passCount}/{totalTests})");
            }
            else
            {
                Debug.LogError($"PHASE2-VERIFY-RESULT=FAIL ({passCount}/{totalTests})");
                EditorApplication.Exit(1);
            }
        }
    }
}
