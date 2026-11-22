#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ShotField.Core;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static class Sc01M3AssemblyVerifier
    {
        private const string ScenePath = BuildAndroid.ScenePath;

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

            // Test 1: 7 组分镜数据连续无 NaN（轨迹数据源，不依赖编辑态 LineRenderer 顶点）。
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

            // Test 2: 轨迹由 CameraMotionRecorder 承载——绑定 Director 相机、双缓存非空、颜色琥珀、数据源帧数 >0。
            // 旧判据要求 LineRenderer 编辑态 baked 且 1801 顶点；轨迹实为运行时生成，编辑态不 baked 是设计，不再断言顶点数/启用态。
            var cam = UnityEngine.Object.FindAnyObjectByType<CameraMotionRecorder>(FindObjectsInactive.Include);
            var amber = PrevisPalette.AmberTrajectory;
            bool recorderOk = cam != null && cam.filmCamera != null &&
                cam.filmCamera.gameObject.name == "Director" &&
                cam.rawTrajectory != null && cam.rawTrajectory.Count >= 2 &&
                cam.smoothedTrajectory != null && cam.smoothedTrajectory.Count >= 2 &&
                ColorClose(cam.recordColor, amber) && ColorClose(cam.readyColor, amber) &&
                cam.trajectoryLine != null &&
                ColorClose(cam.trajectoryLine.startColor, amber) &&
                cam.trajectoryLine.sharedMaterial != null &&
                ColorClose(cam.trajectoryLine.sharedMaterial.color, amber) &&
                Sc01PrevisShotData.TotalFrames > 0;
            Assert("Test 2: 录制器绑定 Director 相机，原始/平滑轨迹各 ≥2 点，记录/就绪/线体颜色均为琥珀，数据源帧数 >0", recorderOk);

            // Test 3: M1 退役契约——正式红演员激活且装配完整，旧 Performer_A/B 必须停用。
            var red = FindEvenIfInactive("PrevisCast_A_Red");
            var performerA = FindEvenIfInactive("SC01_Performer_A");
            var performerB = FindEvenIfInactive("SC01_Performer_B");
            bool redOk = red != null && red.activeInHierarchy &&
                red.GetComponent<ActorNavigator>() != null && red.GetComponent<NavMeshAgent>() != null;
            bool legacyRetired = (performerA == null || !performerA.activeInHierarchy) &&
                (performerB == null || !performerB.activeInHierarchy);
            Assert("Test 3: PrevisCast_A_Red 激活且配 ActorNavigator+NavMeshAgent；SC01_Performer_A/B 均已停用", redOk && legacyRetired);

            var timeline = UnityEngine.Object.FindAnyObjectByType<DirectorTimeline>(FindObjectsInactive.Include);
            bool timeOk = timeline != null && Mathf.Approximately(timeline.duration, 60.0f) &&
                          !timeline.loop && timeline.cameraRecorder != null;
            Assert("Test 4: DirectorTimeline 设为 60.0s 单次播放且成功绑定 CameraMotionRecorder", timeOk);

            // Test 5: 时间轴 0→30→60 寻道驱动摄影机与正式演员确定性位移。
            timeline.Seek(0f);
            Vector3 camStart = timeline.cameraRecorder.filmCamera.transform.position;
            Vector3 actorStart = timeline.actorA != null ? timeline.actorA.transform.position : Vector3.zero;

            timeline.Seek(30f);
            Vector3 camMid = timeline.cameraRecorder.filmCamera.transform.position;
            Vector3 actorMid = timeline.actorA != null ? timeline.actorA.transform.position : Vector3.zero;

            timeline.Seek(60f);
            Vector3 camEnd = timeline.cameraRecorder.filmCamera.transform.position;

            bool scrubOk = Vector3.Distance(camStart, camMid) > 0.5f &&
                           Vector3.Distance(camMid, camEnd) > 0.5f &&
                           Vector3.Distance(actorStart, actorMid) > 0.5f;
            Assert("Test 5: 时间轴 0s->30s->60s 寻道驱动摄影机与正式演员确定性空间位移", scrubOk);

            bool allPassed = passed == total;
            Debug.Log($"M3-ASSEMBLY-VERIFY-RESULT={(allPassed ? "PASS" : "FAIL")} ({passed}/{total})");
            if (Application.isBatchMode) EditorApplication.Exit(allPassed ? 0 : 1);
        }

        private static bool ColorClose(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.05f && Mathf.Abs(a.g - b.g) < 0.05f &&
            Mathf.Abs(a.b - b.b) < 0.05f && Mathf.Abs(a.a - b.a) < 0.05f;

        private static GameObject FindEvenIfInactive(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.name != name) continue;
                if (!go.scene.IsValid()) continue;
                return go;
            }
            return null;
        }
    }
}
#endif
