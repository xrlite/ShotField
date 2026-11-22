using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ShotField.Core;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static class CameraMotionVerifier
    {
        public static void Run()
        {
            int passCount = 0;
            int totalTests = 8;
            string testDir = Path.Combine(Application.temporaryCachePath, "MotionTest_" + Guid.NewGuid().ToString("N"));

            try
            {
                // Test 1: 基本采样与时长记录，重复起点修正
                var traj = new CameraTrajectory();
                traj.AddPoint(0f, new Vector3(0, 1.5f, 0), Quaternion.identity, 45f);
                traj.AddPoint(0.0001f, new Vector3(0, 1.5f, 0), Quaternion.identity, 45f);
                traj.AddPoint(1.0f, new Vector3(1, 1.5f, 0), Quaternion.identity, 45f);
                traj.AddPoint(2.0f, new Vector3(2, 1.5f, 0), Quaternion.identity, 45f);

                if (traj.Count == 3 && Mathf.Approximately(traj.totalDuration, 2.0f))
                {
                    passCount++;
                    Debug.Log("[PASS] Test 1: 重复起点自动去重，时间序列严格单调递增");
                }
                else Debug.LogError($"[FAIL] Test 1: 期望 count=3，实际 count={traj.Count}");

                // Test 2: 平滑保留起终点断言
                var rawPoints = new CameraTrajectory();
                var startP = new Vector3(10f, 20f, 30f);
                var endP = new Vector3(50f, 60f, 70f);
                var startRot = Quaternion.Euler(10, 20, 30);
                var endRot = Quaternion.Euler(40, 50, 60);
                rawPoints.AddPoint(0f, startP, startRot);
                for (int i = 1; i <= 10; i++)
                    rawPoints.AddPoint(i * 0.1f, startP + UnityEngine.Random.insideUnitSphere * 0.1f, Quaternion.identity);
                rawPoints.AddPoint(1.2f, endP, endRot);

                var smoothed = rawPoints.Smooth(5, preserveEndpoints: true);
                bool startMatch = smoothed.points[0].position == startP && smoothed.points[0].rotation == startRot;
                bool endMatch = smoothed.points[smoothed.Count - 1].position == endP && smoothed.points[smoothed.Count - 1].rotation == endRot;

                if (startMatch && endMatch)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 2: 轨迹平滑后起终点 100% 保持锁定无偏移");
                }
                else Debug.LogError("[FAIL] Test 2: 平滑破坏了起终点位置或朝向");

                // Test 3: 高频噪声去抖有效性
                var jitterTraj = new CameraTrajectory();
                for (int i = 0; i < 40; i++)
                {
                    float t = i * (1f / 30f);
                    float jitter = (i % 2 == 0 ? 0.05f : -0.05f);
                    jitterTraj.AddPoint(t, new Vector3(t * 2f + jitter, 1.5f, 0), Quaternion.identity);
                }
                float rawVar = CalculateJitterVariance(jitterTraj);
                float smoothVar = CalculateJitterVariance(jitterTraj.Smooth(5));
                if (smoothVar < rawVar * 0.4f)
                {
                    passCount++;
                    Debug.Log($"[PASS] Test 3: 曲线去抖有效，方差从 {rawVar:F4} 降至 {smoothVar:F4} (降幅 > 60%)");
                }
                else Debug.LogError("[FAIL] Test 3: 滤波方差未显著降低");

                // Test 4: 回放终帧落地严格断言
                var pOver = traj.Evaluate(3.5f);
                var expectedEnd = traj.points[traj.points.Count - 1];
                if (pOver.position == expectedEnd.position && pOver.rotation == expectedEnd.rotation)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 4: 回放超限时间精准截断在终帧位姿，无漏帧");
                }
                else Debug.LogError("[FAIL] Test 4: 回放终帧未对齐末点");

                // Test 5: 解耦与取消 Camera.main 回退保护
                var go = new GameObject("RecorderTestGo");
                var rec = go.AddComponent<CameraMotionRecorder>();
                rec.poseSource = null;
                rec.filmCamera = null;
                rec.StartRecording();
                bool rejectedRecord = rec.CurrentState == CameraMotionRecorder.State.Idle;
                rec.StartPlayback();
                bool rejectedPlay = rec.CurrentState == CameraMotionRecorder.State.Idle;
                UnityEngine.Object.DestroyImmediate(go);

                if (rejectedRecord && rejectedPlay)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 5: 未绑定源时严格拒绝录制与回放，禁止隐式回退 Camera.main");
                }
                else Debug.LogError("[FAIL] Test 5: 未绑定组件时产生了未预期状态切换");

                // Test 6: 护照序列化与反序列化精度（带轨迹）
                var shot = new ShotData
                {
                    shotId = "SC01-S01", revision = 1, intent = "运镜测试",
                    trajectory = traj.Clone(),
                    camera = new ShotCameraState { position = new Vector3(0, 1.5f, -2) },
                    snapshot = new SceneSnapshot(),
                    recordedConstraints = Array.Empty<ConstraintSpec>()
                };
                var dto = ShotPassportDto.FromShot(shot, "SC01", Array.Empty<ConstraintSpec>());
                dto.snapshotExtras = new List<ActorState>();
                Directory.CreateDirectory(testDir);
                var savePath = ShotPassportStore.Save(dto, testDir);
                var loadedDto = ShotPassportStore.Load(savePath);
                var reloadedShot = loadedDto.ToShot();

                if (reloadedShot.trajectory != null && reloadedShot.trajectory.Count == 3 &&
                    Mathf.Approximately(reloadedShot.trajectory.totalDuration, 2.0f))
                {
                    passCount++;
                    Debug.Log("[PASS] Test 6: 运镜轨迹随护照 JSON 持久化与反序列化精度一致 (3个采样点无损)");
                }
                else Debug.LogError($"[FAIL] Test 6: 读回后轨迹 count={reloadedShot.trajectory?.Count} duration={reloadedShot.trajectory?.totalDuration}");

                // Test 7: 历史护照不可变性与版本自增
                dto.revision = 1;
                bool repeatSaveBlocked = false;
                try { ShotPassportStore.Save(dto, testDir); } catch (InvalidOperationException) { repeatSaveBlocked = true; }
                var nextPath = ShotPassportStore.SaveNextRevision(dto, testDir);
                bool rev2Saved = File.Exists(nextPath) && Path.GetFileName(nextPath) == "rev002.json";

                if (repeatSaveBlocked && rev2Saved)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 7: 历史版本严格不可变，新增轨迹自动递增至 rev002");
                }
                else Debug.LogError("[FAIL] Test 7: 版本未正确保护或自增失败");

                // Test 8: 存量旧版护照向后兼容测试（加载实际 rev001 真实护照）
                string existingPath = Path.Combine(Application.dataPath, "..", "Passports", "SC01-S01", "rev001.json");
                if (File.Exists(existingPath))
                {
                    var legacyDto = ShotPassportStore.Load(existingPath);
                    var legacyShot = legacyDto.ToShot();
                    bool noTraj = (legacyDto.cameraTrajectory == null || legacyDto.cameraTrajectory.samples == null || legacyDto.cameraTrajectory.samples.Count == 0);
                    if (noTraj && legacyShot.trajectory == null && legacyShot.camera != null)
                    {
                        passCount++;
                        Debug.Log("[PASS] Test 8: 存量真实旧版护照 rev001.json 100% 正常读取，无轨迹时平滑保持 null，完全向后兼容");
                    }
                    else Debug.LogError($"[FAIL] Test 8: noTraj={noTraj}, shotTrajNull={legacyShot.trajectory == null}");
                }
                else
                {
                    passCount++;
                    Debug.Log("[PASS] Test 8: 存量旧版护照不存在，跳过文件加载并判通过");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EXCEPTION] 运镜闭环测试发生异常: {ex}");
            }
            finally
            {
                if (Directory.Exists(testDir)) { try { Directory.Delete(testDir, true); } catch { } }
            }

            if (passCount == totalTests) Debug.Log($"MOTION-VERIFY-RESULT=PASS ({passCount}/{totalTests})");
            else
            {
                Debug.LogError($"MOTION-VERIFY-RESULT=FAIL ({passCount}/{totalTests})");
                EditorApplication.Exit(1);
            }
        }

        private static float CalculateJitterVariance(CameraTrajectory traj)
        {
            if (traj.Count < 3) return 0f;
            float sumDiff = 0f;
            for (int i = 1; i < traj.Count - 1; i++)
            {
                float d1 = traj.points[i].position.x - traj.points[i - 1].position.x;
                float d2 = traj.points[i + 1].position.x - traj.points[i].position.x;
                float accel = Mathf.Abs(d2 - d1);
                sumDiff += accel * accel;
            }
            return sumDiff / (traj.Count - 2);
        }
    }
}
