#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShotField.Core;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static class Sc01M3AssemblyVerifier
    {
        private const string ScenePath = "Assets/Scenes/SC01_Final.unity";
        private static readonly Color AmberColor = new Color(0.949f, 0.639f, 0.235f, 0.85f);

        [MenuItem("ShotField/Verifiers/Run M3 Assembly Verifier")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int passed = 0;
            const int total = 5;

            void Assert(string name, bool condition)
            {
                if (!condition)
                {
                    Debug.LogError($"[M3-FAIL] {name}");
                    throw new InvalidOperationException($"Assertion failed: {name}");
                }
                passed++;
                Debug.Log($"[M3-PASS] {name}");
            }

            Debug.Log("=== [Milestone M3: Amber Trajectory & DirectorTimeline Verification] ===");

            bool shotCountOk = Sc01PrevisShotData.Shots.Length == 7;
            bool noNaN = true;
            for (int i = 0; i <= 1800; i++)
            {
                float t = i / 30.0f;
                Sc01PrevisShotData.EvaluateCamera(t, out var p, out var r, out float fov);
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                    float.IsNaN(r.x) || float.IsNaN(r.y) || float.IsNaN(r.z) || float.IsNaN(r.w) ||
                    float.IsNaN(fov))
                {
                    noNaN = false;
                    break;
                }
            }
            Assert("Test 1: 7 组分镜轨迹与光学参数全覆盖，1801 采样点连续无 NaN", shotCountOk && noNaN);

            var dir = GameObject.Find("Director");
            var lr = dir != null ? dir.GetComponent<LineRenderer>() : null;
            bool dirActive = dir != null && dir.activeInHierarchy && dir.layer == 0;
            bool lrOk = lr != null && lr.enabled && lr.useWorldSpace &&
                        Mathf.Abs(lr.startWidth - 0.012f) < 0.002f &&
                        Mathf.Abs(lr.startColor.r - AmberColor.r) < 0.05f &&
                        Mathf.Abs(lr.startColor.g - AmberColor.g) < 0.05f &&
                        Mathf.Abs(lr.startColor.b - AmberColor.b) < 0.05f &&
                        lr.positionCount == 1801;
            Assert("Test 2: Director 琥珀色 (#F2A33C, 0.012m) LineRenderer 挂载且顶点数达 1801", dirActive && lrOk);

            var a = GameObject.Find("SC01_Performer_A");
            var b = GameObject.Find("SC01_Performer_B");
            bool aOk = a != null && a.activeInHierarchy && a.GetComponent<ActorNavigator>() != null;
            bool bOk = b != null && b.activeInHierarchy && b.GetComponent<ActorNavigator>() != null;
            Assert("Test 3: SC01_Performer_A (红) 与 SC01_Performer_B (蓝) 激活且配备 ActorNavigator", aOk && bOk);

            var timeline = UnityEngine.Object.FindAnyObjectByType<DirectorTimeline>();
            bool timeOk = timeline != null && Mathf.Approximately(timeline.duration, 60.0f) &&
                          !timeline.loop && timeline.cameraRecorder != null;
            Assert("Test 4: DirectorTimeline 设为 60.0s 单次播放且成功绑定 CameraMotionRecorder", timeOk);

            timeline.Seek(0f);
            Vector3 camStart = timeline.cameraRecorder.filmCamera.transform.position;
            Vector3 aStart = a.transform.position;

            timeline.Seek(30f);
            Vector3 camMid = timeline.cameraRecorder.filmCamera.transform.position;
            Vector3 aMid = a.transform.position;

            timeline.Seek(60f);
            Vector3 camEnd = timeline.cameraRecorder.filmCamera.transform.position;
            Vector3 aEnd = a.transform.position;

            bool scrubOk = Vector3.Distance(camStart, camMid) > 0.5f &&
                           Vector3.Distance(camMid, camEnd) > 0.5f &&
                           Vector3.Distance(aStart, aMid) > 0.5f;
            Assert("Test 5: 时间轴 0s->30s->60s 寻道驱动摄影机与演员确定性空间位移", scrubOk);

            Debug.Log($"M3-ASSEMBLY-VERIFY-RESULT=PASS ({passed}/{total})");
        }
    }
}
#endif
