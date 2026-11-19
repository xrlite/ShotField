#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using ShotField.Core;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static class DirectorTimelineVerifier
    {
        [MenuItem("ShotField/Verifiers/Run Director Timeline Verifier")]
        public static void Run()
        {
            int passCount = 0;
            int totalTests = 5;

            void Assert(string label, bool condition)
            {
                if (!condition)
                {
                    Debug.LogError($"[FAIL] {label}");
                    throw new InvalidOperationException($"Assertion failed: {label}");
                }
                passCount++;
                Debug.Log($"[PASS] {label}");
            }

            Debug.Log("=== [Director Timeline & Presets 全链路核验] ===");

            // 1. 测试 5 种运镜预设完整性与数学有效性
            Vector3 target = new Vector3(1.5f, 0.8f, 2.15f);
            var push = CameraMotionPresets.CreatePreset(CameraMotionType.DollyPush, target, 5f);
            var pull = CameraMotionPresets.CreatePreset(CameraMotionType.DollyPull, target, 5f);
            var pan = CameraMotionPresets.CreatePreset(CameraMotionType.TruckPan, target, 5f);
            var orbit = CameraMotionPresets.CreatePreset(CameraMotionType.ArcOrbit, target, 5f);
            var shake = CameraMotionPresets.CreatePreset(CameraMotionType.HandheldShake, target, 5f);

            bool presetsValid = push.Count >= 10 && pull.Count >= 10 && pan.Count >= 10 &&
                                orbit.Count >= 10 && shake.Count >= 10;
            bool noNaN = !float.IsNaN(push.Evaluate(2.5f).position.x) &&
                         !float.IsNaN(orbit.Evaluate(1.0f).position.y);
            Assert("Test 1: 5 种运镜预设 (推/拉/摇/移/晃) 采样丰富且坐标连续无 NaN", presetsValid && noNaN);

            // 2. 测试 5 种演员姿态预设完整性
            Vector3 basePos = new Vector3(0.8f, 0.86f, 1.25f);
            var idle = ActorPosePresets.CreatePreset(ActorPoseType.Idle, basePos, target, 5f);
            var sit = ActorPosePresets.CreatePreset(ActorPoseType.Sit, basePos, target, 5f);
            var lean = ActorPosePresets.CreatePreset(ActorPoseType.LeanForward, basePos, target, 5f);
            var reach = ActorPosePresets.CreatePreset(ActorPoseType.ReachEnvelope, basePos, target, 5f);
            var stand = ActorPosePresets.CreatePreset(ActorPoseType.StandUp, basePos, target, 5f);

            bool posesValid = idle.Count >= 5 && sit.Count >= 5 && lean.Count >= 5 &&
                              reach.Count >= 5 && stand.Count >= 5;
            bool tagValid = reach.Evaluate(5f).actionTag == "GrabLetter" &&
                            stand.Evaluate(5f).actionTag == "Standing";
            Assert("Test 2: 5 种演员姿态预设 (站/坐/倾/伸手/起立) 动作标签与时序关键帧完备", posesValid && tagValid);

            // 3. 测试 DirectorTimeline 基础时间控制逻辑
            var root = new GameObject("TimelineTestRoot");
            try
            {
                var timeline = root.AddComponent<DirectorTimeline>();
                timeline.duration = 6.0f;
                timeline.Seek(3.0f);
                Assert("Test 3: 时间轴 Seek 寻道与归一化时间比值计算精确 (t=3s/6s => 0.5)",
                    Mathf.Approximately(timeline.NormalizedTime, 0.5f));

                // 4. 测试时间轴对摄影机与演员的联动驱动
                var camGo = new GameObject("TestFilmCamera");
                camGo.transform.SetParent(root.transform);
                var cam = camGo.AddComponent<Camera>();

                var camRecGo = new GameObject("TestCamRecorder");
                camRecGo.transform.SetParent(root.transform);
                var camRecorder = camRecGo.AddComponent<CameraMotionRecorder>();
                camRecorder.filmCamera = cam;
                camRecorder.rawTrajectory = push;

                var actorGo = new GameObject("TestActor");
                actorGo.transform.SetParent(root.transform);
                var actorPlayer = actorGo.AddComponent<ActorTakePlayer>();
                actorPlayer.actorTransform = actorGo.transform;
                actorPlayer.currentTake = reach;

                timeline.cameraRecorder = camRecorder;
                timeline.actorPlayer = actorPlayer;

                // 寻道至起始帧
                timeline.SeekNormalized(0f);
                Vector3 camStart = camGo.transform.position;
                Vector3 actorStart = actorGo.transform.position;

                // 寻道至终点帧
                timeline.SeekNormalized(1f);
                Vector3 camEnd = camGo.transform.position;
                Vector3 actorEnd = actorGo.transform.position;

                bool camMoved = Vector3.Distance(camStart, camEnd) > 0.5f;
                bool actorMoved = Vector3.Distance(actorStart, actorEnd) > 0.15f;
                Assert("Test 4: 拖动时间轴游标能同步驱动摄影机推拉与演员抓信位移", camMoved && actorMoved);

                // 5. 测试边界控制与保护
                timeline.Seek(100f);
                bool clampMax = Mathf.Approximately(timeline.currentTime, 6.0f);
                timeline.Seek(-10f);
                bool clampMin = Mathf.Approximately(timeline.currentTime, 0.0f);
                Assert("Test 5: 时间轴越界寻道保护有效 (0 <= t <= duration)", clampMax && clampMin);

                Debug.Log($"DIRECTOR-TIMELINE-RESULT=PASS ({passCount}/{totalTests})");
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
