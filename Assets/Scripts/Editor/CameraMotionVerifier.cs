using System;
using UnityEngine;
using UnityEditor;
using ShotField.Core;

namespace ShotField.EditorTools
{
    public static class CameraMotionVerifier
    {
        public static void Run()
        {
            int passCount = 0;
            int totalTests = 5;

            try
            {
                // Test 1: 基本采样与时长记录
                var traj = new CameraTrajectory();
                traj.AddPoint(0f, new Vector3(0, 1.5f, 0), Quaternion.identity, 45f);
                traj.AddPoint(1.0f, new Vector3(1, 1.5f, 0), Quaternion.identity, 45f);
                traj.AddPoint(2.0f, new Vector3(2, 1.5f, 0), Quaternion.identity, 45f);

                if (traj.Count == 3 && Mathf.Approximately(traj.totalDuration, 2.0f))
                {
                    passCount++;
                    Debug.Log("[PASS] Test 1: 基本采样点与总时长记录正常");
                }
                else
                {
                    Debug.LogError($"[FAIL] Test 1: 期望 count=3 duration=2.0，实际 count={traj.Count} duration={traj.totalDuration}");
                }

                // Test 2: 时间插值平滑精度 (Evaluate 二分查找与 Slerp/Lerp)
                var midPoint = traj.Evaluate(0.5f);
                if (Mathf.Approximately(midPoint.position.x, 0.5f) && Mathf.Approximately(midPoint.position.y, 1.5f))
                {
                    passCount++;
                    Debug.Log("[PASS] Test 2: 时间插值二分定位与 Lerp 精度正常");
                }
                else
                {
                    Debug.LogError($"[FAIL] Test 2: t=0.5 插值期望 x=0.5，实际 x={midPoint.position.x}");
                }

                // Test 3: 高频噪声去抖平滑 (Smooth 降噪方差比验证)
                var jitterTraj = new CameraTrajectory();
                int sampleCount = 60;
                float dt = 1f / 30f;
                for (int i = 0; i < sampleCount; i++)
                {
                    float t = i * dt;
                    float basePos = t * 2f;
                    float jitter = (i % 2 == 0 ? 0.05f : -0.05f); // 5cm 高频交替抖动
                    jitterTraj.AddPoint(t, new Vector3(basePos + jitter, 1.5f, 0), Quaternion.identity);
                }

                float rawAccelVariance = CalculateJitterVariance(jitterTraj);
                var smoothed = jitterTraj.Smooth(5);
                float smoothedVariance = CalculateJitterVariance(smoothed);

                if (smoothedVariance < rawAccelVariance * 0.4f)
                {
                    passCount++;
                    Debug.Log($"[PASS] Test 3: 高频手抖滤波有效，方差从 {rawAccelVariance:F4} 降至 {smoothedVariance:F4} (降幅 > 60%)");
                }
                else
                {
                    Debug.LogError($"[FAIL] Test 3: 滤波方差未显著降低: raw={rawAccelVariance} smoothed={smoothedVariance}");
                }

                // Test 4: 边界条件测试 (0点、单点、超出上限)
                var emptyTraj = new CameraTrajectory();
                var p0 = emptyTraj.Evaluate(1.0f);
                var pOver = traj.Evaluate(5.0f);
                var pUnder = traj.Evaluate(-1.0f);

                if (p0.position == Vector3.zero &&
                    Mathf.Approximately(pOver.position.x, 2.0f) &&
                    Mathf.Approximately(pUnder.position.x, 0.0f))
                {
                    passCount++;
                    Debug.Log("[PASS] Test 4: 空轨迹、负时间与超限时间钳制边界测试通过");
                }
                else
                {
                    Debug.LogError("[FAIL] Test 4: 边界条件处理异常");
                }

                // Test 5: 深拷贝与独立性
                var cloned = traj.Clone();
                cloned.AddPoint(3.0f, new Vector3(3, 1.5f, 0), Quaternion.identity);
                if (traj.Count == 3 && cloned.Count == 4)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 5: Trajectory 深拷贝隔离性正常");
                }
                else
                {
                    Debug.LogError("[FAIL] Test 5: Clone 修改污染了原始轨迹");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EXCEPTION] 运镜验证发生异常: {ex}");
            }

            if (passCount == totalTests)
            {
                Debug.Log($"MOTION-VERIFY-RESULT=PASS ({passCount}/{totalTests})");
            }
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
