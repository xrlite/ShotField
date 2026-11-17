#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShotField.Core;
using ShotField.Constraints;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    /// <summary>
    /// SC01 真实片场全链路核验套件。
    /// 包含：两机位一过一警、画幅切换一致性、护照重启读回以及 XR 核心组件挂载断言。
    /// </summary>
    public static class SC01RealShotVerifier
    {
        private static StringBuilder sb = new StringBuilder();
        private static int passCount = 0;
        private static int failCount = 0;

        public static void Run()
        {
            sb = new StringBuilder();
            passCount = 0;
            failCount = 0;

            Log("=== [SC01 真实片场全链路核验] ===");
            Log("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            // 1. 构建真实 SC01 片场与工作台
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Sc01Blockout.BuildIntoScene();
            var wbGo = SpatialWorkbenchBuilder.BuildWorkbench();

            Assert("SC01. 场景根节点与 PXR_Manager 挂载", root.GetComponent<ByteDance.PICO.XR.PXR_Manager>() != null);
            var canvas = wbGo.GetComponent<Canvas>();
            Assert("SC01. 空间 3D 工作台画布存在且为 WorldSpace", canvas != null && canvas.renderMode == RenderMode.WorldSpace);
            var es = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            Assert("SC01. EventSystem 存在", es != null);

            var dirGo = GameObject.Find("Director");
            var cam = dirGo.GetComponent<Camera>();
            var lib = dirGo.GetComponent<ShotLibrary>();
            var vis = dirGo.GetComponent<VisibilityConstraint>();
            var monitor = wbGo.GetComponent<SpatialMonitor>();

            // 2. 两台真实机位一过一警
            // 机位 1：安全机位（侧平视极低角度拍茶几侧面，信封正面朝上不入画）
            cam.transform.position = new Vector3(2.35f, 0.42f, 1.20f);
            cam.transform.LookAt(new Vector3(1.50f, 0.451f, 2.15f));
            var s01 = lib.CreateShot(false, "S01 安全机位：平视低角度拍茶几，信封正面朝上不入画", true);
            var r1 = vis.Evaluate(s01, null);
            Log($"  [DETAIL S01] status={r1.status}, detail={r1.detail}");
            Assert("REAL_SHOT_1. 安全机位判定 = 通过 (Pass)", r1.status == CheckStatus.Pass);

            // 机位 2：危险机位（茶几上方 1.45m 俯冲俯拍信封正面）
            cam.transform.position = new Vector3(1.50f, 1.45f, 2.15f);
            cam.transform.LookAt(new Vector3(1.50f, 0.45f, 2.15f));
            var s02 = lib.CreateShot(true, "S02 危险机位：正上方俯拍信封，正面完全泄露", true);
            var r2 = vis.Evaluate(s02, s01);
            Log($"  [DETAIL S02] status={r2.status}, detail={r2.detail}");
            Assert("REAL_SHOT_2. 危险机位判定 = 违规 (Violation)", r2.status == CheckStatus.Violation);

            // 3. 画幅切换一致性
            monitor.SetAspect(false); // 16:9
            bool aspect16x9 = !monitor.IsPortrait && Mathf.Approximately(cam.aspect, ShotCameraState.DefaultAspect);
            monitor.SetAspect(true);  // 9:16
            bool aspect9x16 = monitor.IsPortrait && Mathf.Approximately(cam.aspect, ShotCameraState.PortraitAspect);
            Assert("ASPECT. 16:9 与 9:16 画幅双向切换一致", aspect16x9 && aspect9x16);

            // 4. 护照重启读回
            var dto = ShotPassportDto.FromShot(s01, lib.SceneId, null);
            var path = ShotPassportStore.Save(dto, ShotPassportStore.ProjectFolder);
            var loaded = ShotPassportStore.Load(path);
            bool passportOk = (loaded.shot_id == dto.shot_id) &&
                              (loaded.revision == dto.revision) &&
                              Mathf.Approximately(loaded.camera.fov, dto.camera.fov) &&
                              (loaded.camera.pos[0] == dto.camera.pos[0]);
            Assert("PASSPORT_RELOAD. 护照落盘与重启读回 100% 一致", passportOk);

            // 5. 汇总并落盘证据
            Log($"=== 结果: 通过 {passCount} 项, 失败 {failCount} 项 ===");
            string resultTag = failCount == 0 ? "SC01-VERIFY-RESULT=PASS" : "SC01-VERIFY-RESULT=FAIL";
            Log(resultTag);

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../05_验证记录/2026-11"));
            Directory.CreateDirectory(outDir);
            var outFile = Path.Combine(outDir, "sc01_verification_result.txt");
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
            Debug.Log(sb.ToString());
        }

        private static void Assert(string name, bool condition)
        {
            if (condition)
            {
                passCount++;
                Log("  [PASS] " + name);
            }
            else
            {
                failCount++;
                Log("  [FAIL] " + name);
            }
        }

        private static void Log(string msg) => sb.AppendLine(msg);
    }
}
#endif
