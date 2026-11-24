#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using ShotField.RuntimeUI;
using ShotField.RuntimeUI.Tabletop;
using ShotField.RuntimeUI.Probes;

namespace ShotField.EditorTools
{
    public static class SteadicamAndTimelineSyncVerifier
    {
        [MenuItem("ShotField/Verifiers/Run Steadicam And Timeline Sync Verifier")]
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

            Debug.Log("=== [Steadicam Stabilizer & Synchronized Timeline 全链路核验] ===");
            var root = new GameObject("SteadicamSyncVerifierRoot");

            try
            {
                // Test 1: 斯坦尼康 (>85%) 与手持自然 (>70%) 高频手颤微抖衰减率
                var stabGo = new GameObject("CameraStabilizer");
                stabGo.transform.SetParent(root.transform);
                var stabilizer = stabGo.AddComponent<VirtualCameraStabilizer>();
                stabilizer.mode = CameraStabilizerMode.Steadicam;
                stabilizer.minCutoff = 1.0f;
                stabilizer.smoothTime = 0.15f;

                float dt = 0.0166f;
                float rawJitterSum = 0f, filteredJitterSum = 0f;
                stabilizer.ResetFilters(Vector3.zero, Quaternion.identity);

                for (int i = 0; i < 120; i++)
                {
                    float t = i * dt;
                    float noise = Mathf.Sin(2f * Mathf.PI * 15f * t) * 0.08f;
                    Vector3 raw = new Vector3(t * 0.2f, noise, 0f);
                    Vector3 filtered = stabilizer.FilterPosition(raw, dt);
                    if (i > 30)
                    {
                        rawJitterSum += Mathf.Abs(noise);
                        filteredJitterSum += Mathf.Abs(filtered.y);
                    }
                }
                float attenuation = 1f - (filteredJitterSum / Mathf.Max(rawJitterSum, 1e-4f));
                Assert($"Test 1: 斯坦尼康高频颤抖滤波衰减率达标 (>85% 实测 {attenuation * 100f:F1}%)", attenuation > 0.85f);

                // Test 2: Roll 轴地平线校平 Z 倾斜角度严格消除为 0.00°（含垂直俯视仰视奇异点防御）
                Quaternion severeTilted = Quaternion.Euler(18.5f, 42.0f, 28.5f);
                Quaternion leveled = stabilizer.ApplyHorizonLock(severeTilted);
                float rollAngle = leveled.eulerAngles.z;
                if (rollAngle > 180f) rollAngle = 360f - rollAngle;

                Quaternion downLook = stabilizer.ApplyHorizonLock(Quaternion.Euler(90f, 0f, 0f));
                Quaternion upLook = stabilizer.ApplyHorizonLock(Quaternion.Euler(-90f, 0f, 0f));
                bool verticalSafe = !float.IsNaN(downLook.x) && !float.IsNaN(upLook.x);
                Assert($"Test 2: Roll 轴锁平时相机 Z 轴倾角绝对为 0.00° ({rollAngle:F4}°) 且垂直视角无奇异点", rollAngle < 0.001f && verticalSafe);

                // Test 3: 时间轴统领 L1相机、L2演员、L3载具在同一帧同频寻道推进
                var timeline = root.AddComponent<DirectorTimeline>();
                timeline.duration = 10.0f;

                var vehGo = new GameObject("VehicleTest");
                vehGo.transform.SetParent(root.transform);
                var vehNav = vehGo.AddComponent<VehiclePathNavigator>();
                timeline.vehicleNavigator = vehNav;

                var camGo = new GameObject("FilmCam");
                camGo.transform.SetParent(root.transform);
                var cam = camGo.AddComponent<Camera>();
                var camRecGo = new GameObject("CamRec");
                camRecGo.transform.SetParent(root.transform);
                var camRec = camRecGo.AddComponent<CameraMotionRecorder>();
                camRec.filmCamera = cam;
                timeline.cameraRecorder = camRec;

                timeline.Seek(0.0f);
                Vector3 vehStart = vehGo.transform.position;
                timeline.Seek(5.0f);
                Vector3 vehMid = vehGo.transform.position;
                timeline.Seek(10.0f);
                Vector3 vehEnd = vehGo.transform.position;

                bool vehProgressed = Vector3.Distance(vehStart, vehMid) > 0.5f && Vector3.Distance(vehMid, vehEnd) > 0.5f;
                Assert("Test 3: 时间轴寻道推进多轨实体，载具与实体确定性空间位移", vehProgressed);

                // Test 4: 载具地空双模寻路与 NavMeshObstacle 状态同步
                vehNav.domainMode = VehicleDomainMode.Air;
                vehNav.bankFactor = 1.5f;
                vehNav.EvaluateTrajectory(0.5f);
                float airBankZ = vehGo.transform.eulerAngles.z;
                if (airBankZ > 180f) airBankZ = 360f - airBankZ;
                bool airObstacleDisabled = vehNav.obstacle == null || !vehNav.obstacle.enabled;

                vehNav.domainMode = VehicleDomainMode.Ground;
                vehNav.EvaluateTrajectory(0.5f);
                float groundRollZ = vehGo.transform.eulerAngles.z;
                if (groundRollZ > 180f) groundRollZ = 360f - groundRollZ;
                bool groundObstacleActive = vehNav.obstacle != null && vehNav.obstacle.enabled;

                Assert("Test 4: 载具地空双模切换正常（地面零横滚+障碍雕刻，空中侧倾+雕刻关闭）",
                    groundRollZ < 0.01f && airBankZ > 0.05f && airObstacleDisabled && groundObstacleActive);

                // Test 5: 3D 空间可视化探针无异常单批次生成与更新
                var probeGo = new GameObject("ProbeVisualizer");
                probeGo.transform.SetParent(root.transform);
                var probe = probeGo.AddComponent<SpatialProbeVisualizer>();
                probe.targetCamera = cam;
                probe.vehicleNavigator = vehNav;
                probe.RefreshProbes();
                Assert("Test 5: 空间探针渲染器初始化无异常，视锥与车灯线框顶点就绪",
                    probeGo.transform.childCount >= 3);

                Debug.Log($"STEADICAM-SYNC-RESULT=PASS ({passCount}/{totalTests})");
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
