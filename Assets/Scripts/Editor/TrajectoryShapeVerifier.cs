using System;
using UnityEngine;
using UnityEditor;
using ShotField.Core;

namespace ShotField.EditorTools
{
    public static class TrajectoryShapeVerifier
    {
        public static void Run()
        {
            int passCount = 0;
            int totalTests = 5;

            try
            {
                // Test 1: 构建含抖动轨迹并执行平滑
                var raw = new CameraTrajectory();
                int sampleCount = 30;
                for (int i = 0; i < sampleCount; i++)
                {
                    float t = i / (float)(sampleCount - 1);
                    Vector3 basePos = new Vector3(t * 2.0f, Mathf.Sin(t * Mathf.PI), t * 0.5f);
                    // 内部点加入高频抖动噪声
                    if (i > 0 && i < sampleCount - 1)
                    {
                        float jitter = Mathf.Sin(i * 17.3f) * 0.08f;
                        basePos.y += jitter;
                    }
                    Quaternion rot = Quaternion.Euler(0, t * 45f, 0);
                    raw.AddPoint(t, basePos, rot, 45f + t * 5f);
                }

                var smooth = raw.Smooth(5, preserveEndpoints: true);
                if (smooth != null && smooth.Count == raw.Count)
                {
                    passCount++;
                    Debug.Log($"[TrajectoryShapeVerifier] Test 1 PASS: 平滑生成采样点数一致 ({smooth.Count}/{raw.Count})");
                }

                // Test 2 (断言 a): 起终点位移平滑前后变化率 < 2%
                float dispRaw = Vector3.Distance(raw.points[0].position, raw.points[raw.Count - 1].position);
                float dispSmooth = Vector3.Distance(smooth.points[0].position, smooth.points[smooth.Count - 1].position);
                float dispDiffRate = Mathf.Abs(dispRaw - dispSmooth) / Mathf.Max(0.0001f, dispRaw);
                if (dispDiffRate < 0.02f)
                {
                    passCount++;
                    Debug.Log($"[TrajectoryShapeVerifier] Test 2 PASS (断言 a): 起终位移变化率 {dispDiffRate * 100f:F4}% < 2% (raw={dispRaw:F4}, smooth={dispSmooth:F4})");
                }
                else
                {
                    Debug.LogError($"[TrajectoryShapeVerifier] Test 2 FAIL: 位移变化率超标 {dispDiffRate * 100f:F4}%");
                }

                // Test 3 (断言 b): 总时长严格不变
                float durDiff = Mathf.Abs(raw.totalDuration - smooth.totalDuration);
                if (durDiff < 0.0001f)
                {
                    passCount++;
                    Debug.Log($"[TrajectoryShapeVerifier] Test 3 PASS (断言 b): 总时长严格一致 (raw={raw.totalDuration:F4}s, smooth={smooth.totalDuration:F4}s)");
                }
                else
                {
                    Debug.LogError($"[TrajectoryShapeVerifier] Test 3 FAIL: 总时长不一致 (diff={durDiff})");
                }

                // Test 4 (断言 c): 合成抖动噪声被显著削弱 (方差比值)
                float varRaw = CalculateJitterVariance(raw);
                float varSmooth = CalculateJitterVariance(smooth);
                float varRatio = varRaw > 0.00001f ? (varSmooth / varRaw) : 1.0f;
                if (varRatio < 0.5f)
                {
                    passCount++;
                    Debug.Log($"[TrajectoryShapeVerifier] Test 4 PASS (断言 c): 抖动方差显著削弱 (rawVar={varRaw:F6}, smoothVar={varSmooth:F6}, ratio={varRatio * 100f:F2}%)");
                }
                else
                {
                    Debug.LogError($"[TrajectoryShapeVerifier] Test 4 FAIL: 抖动方差削弱不足 (ratio={varRatio})");
                }

                // Test 5 (断言 d): 单调时间序列保持与边界条件
                bool timeMonotonic = true;
                for (int i = 1; i < smooth.Count; i++)
                {
                    if (smooth.points[i].time < smooth.points[i - 1].time)
                    {
                        timeMonotonic = false;
                        break;
                    }
                }
                var emptyTraj = new CameraTrajectory();
                var emptySmooth = emptyTraj.Smooth(5, true);
                bool emptyOk = emptySmooth != null && emptySmooth.Count == 0;

                if (timeMonotonic && emptyOk)
                {
                    passCount++;
                    Debug.Log("[TrajectoryShapeVerifier] Test 5 PASS (断言 d): 时间序列严格单调且空边界安全");
                }
                else
                {
                    Debug.LogError($"[TrajectoryShapeVerifier] Test 5 FAIL: 单调性={timeMonotonic}, 空边界={emptyOk}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TrajectoryShapeVerifier] EXCEPTION: {ex}");
            }

            if (passCount == totalTests)
            {
                Debug.Log($"TRAJECTORY-SHAPE-RESULT=PASS ({passCount}/{totalTests})");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"TRAJECTORY-SHAPE-RESULT=FAIL ({passCount}/{totalTests})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static float CalculateJitterVariance(CameraTrajectory traj)
        {
            if (traj.Count < 3) return 0f;
            float sumDiff = 0f;
            for (int i = 1; i < traj.Count - 1; i++)
            {
                Vector3 d1 = traj.points[i].position - traj.points[i - 1].position;
                Vector3 d2 = traj.points[i + 1].position - traj.points[i].position;
                Vector3 accel = d2 - d1;
                sumDiff += accel.sqrMagnitude;
            }
            return sumDiff / (traj.Count - 2);
        }
    }
}
