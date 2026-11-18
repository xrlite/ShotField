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
            EditorSceneManager.OpenScene("Assets/Scenes/SC01_Blockout.unity", OpenSceneMode.Single);
            var wbGo = GameObject.Find("Spatial_Workbench_Canvas");

            Assert("SC01. 工作台引用已保存", wbGo != null && wbGo.GetComponent<SpatialWorkbench>() != null);
            var hmdGo = GameObject.Find("XR_Headset");
            var tpd = hmdGo != null ? hmdGo.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>() : null;
            Assert("SC01. XR_Headset 头显观察机位存在且挂载 TrackedPoseDriver", tpd != null);
            var canvas = wbGo.GetComponent<Canvas>();
            Assert("SC01. 空间 3D 工作台画布存在且为 WorldSpace", canvas != null && canvas.renderMode == RenderMode.WorldSpace);
            var es = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            Assert("SC01. EventSystem 存在", es != null);

            var dirGo = GameObject.Find("Director");
            var cam = dirGo.GetComponent<Camera>();
            var lib = dirGo.GetComponent<ShotLibrary>();
            var vis = dirGo.GetComponent<VisibilityConstraint>();
            var monitor = wbGo.GetComponent<SpatialMonitor>();
            var envelope = SemanticMarker.Find("envelope");
            Assert("SC01. 信封语义标记与关键面已接线", envelope != null &&
                envelope.criticalFaceMustStayHidden && envelope.criticalFaceNormal == Vector3.forward);
            var envelopeCenter = envelope.CriticalFaceCenter;

            // 2. 两台真实机位一过一警
            // 机位 1：安全机位（侧平视极低角度拍茶几侧面，信封正面朝上不入画）
            cam.transform.position = envelopeCenter + new Vector3(0.85f, -0.39f, -0.95f);
            cam.transform.LookAt(envelopeCenter);
            var s01 = lib.CreateShot(SceneCaptureMode.Current, "S01 安全机位：平视低角度拍茶几，信封正面朝上不入画");
            var r1 = vis.Evaluate(s01, null);
            Log($"  [DETAIL S01] status={r1.status}, detail={r1.detail}");
            Assert("REAL_SHOT_1. 安全机位判定 = 通过 (Pass)", r1.status == CheckStatus.Pass);

            // 机位 2：危险机位（茶几上方 1.45m 俯冲俯拍信封正面）
            cam.transform.position = envelopeCenter + envelope.CriticalFaceNormalWorld * 1f;
            cam.transform.LookAt(envelopeCenter);
            var s02 = lib.CreateShot(SceneCaptureMode.InheritLatestOrCapture, "S02 危险机位：正上方俯拍信封，正面完全泄露");
            var r2 = vis.Evaluate(s02, s01);
            Log($"  [DETAIL S02] status={r2.status}, detail={r2.detail}");
            Assert("REAL_SHOT_2. 危险机位判定 = 违规 (Violation)", r2.status == CheckStatus.Violation);

            var occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            occluder.transform.position = envelopeCenter + envelope.CriticalFaceNormalWorld * 0.35f;
            occluder.transform.localScale = new Vector3(0.4f, 0.05f, 0.4f);
            Physics.SyncTransforms();
            Assert("REAL_SHOT_OCCLUSION. 同一正面机位加挡板后判定通过",
                vis.Evaluate(s02, s01).status == CheckStatus.Pass);
            occluder.SetActive(false);
            Physics.SyncTransforms();

            // 3. 画幅切换一致性
            monitor.SetAspect(false); // 16:9
            bool aspect16x9 = !monitor.IsPortrait && Mathf.Approximately(cam.aspect, ShotCameraState.DefaultAspect);
            monitor.SetAspect(true);  // 9:16
            bool aspect9x16 = monitor.IsPortrait && Mathf.Approximately(cam.aspect, ShotCameraState.PortraitAspect);
            Assert("ASPECT. 16:9 与 9:16 画幅双向切换一致", aspect16x9 && aspect9x16);

            // 4. 护照历史保护与重启深度读回核验
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../05_验证记录/2026-11"));
            var verifyRun = System.Environment.GetEnvironmentVariable("SHOTFIELD_VERIFY_RUN");
            var folderTag = string.IsNullOrEmpty(verifyRun)
                ? DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss")
                : verifyRun;
            var verifyFolder = Path.Combine(outDir, "PassportVerify-" + folderTag);
            if (Directory.Exists(verifyFolder))
                verifyFolder = Path.Combine(outDir, "PassportVerify-" + folderTag + "-" + DateTimeOffset.Now.ToString("HHmmss"));
            var rules = UnityEngine.Object.FindObjectsByType<ShotConstraint>(FindObjectsInactive.Exclude);
            var specs = new ConstraintSpec[rules.Length];
            for (int i = 0; i < rules.Length; i++) specs[i] = rules[i].ToSpec();
            s01.recordedConstraints = specs;
            s01.lastCheck.Clear();
            s01.lastCheck.Add(r1);
            s01.checkedAt = DateTimeOffset.UtcNow.ToString("O");

            // 保存 rev001
            var dto1 = ShotPassportDto.FromShot(s01, lib.SceneId, specs);
            var path1 = ShotPassportStore.Save(dto1, verifyFolder);

            // 递增版本保存 rev002，验证版本链并存、历史不可篡改
            var dto2 = ShotPassportDto.FromShot(s01, lib.SceneId, specs);
            var path2 = ShotPassportStore.SaveNextRevision(dto2, verifyFolder);
            bool historyPreserved = File.Exists(path1) && File.Exists(path2) &&
                                    dto2.revision == dto1.revision + 1 && path1 != path2;
            Assert("PASSPORT_HISTORY. 不可变版本链 (rev001 与 rev002 并存未被覆写)", historyPreserved);

            // 尝试同版本重复保存，断言必须抛出异常阻断
            bool overwriteBlocked = false;
            try { ShotPassportStore.Save(dto1, verifyFolder); }
            catch (InvalidOperationException) { overwriteBlocked = true; }
            Assert("PASSPORT_IMMUTABLE. 同版本重复保存被严格拦截 (防历史篡改)", overwriteBlocked);

            // 从磁盘读回 rev001，深度恢复机位并核验规则一致性与质检摘要
            var loadedDto = ShotPassportStore.Load(path1);
            var reloadedShot = loadedDto.ToShot();
            lib.ApplyShot(reloadedShot);
            var reEval = vis.Evaluate(reloadedShot, null);
            bool reloadOk = (loadedDto.shot_id == dto1.shot_id) &&
                            (loadedDto.revision == 1) &&
                            (loadedDto.check_summary != null && loadedDto.check_summary.pass == 1) &&
                            (loadedDto.constraints != null && loadedDto.constraints.Length == specs.Length) &&
                            (reEval.status == CheckStatus.Pass) &&
                            Vector3.Distance(cam.transform.position, s01.camera.position) < 0.001f;
            Assert("PASSPORT_RELOAD. 读回后机位物理恢复、质检摘要与规则求值完全一致", reloadOk);

            // 5. 汇总并落盘证据
            Log($"=== 结果: 通过 {passCount} 项, 失败 {failCount} 项 ===");
            string resultTag = failCount == 0 ? "SC01-REAL-RESULT=PASS" : "SC01-REAL-RESULT=FAIL";
            Log(resultTag);
            Log(resultTag.Replace("SC01-REAL", "SC01-VERIFY"));

            Directory.CreateDirectory(outDir);
            var outFile = Path.Combine(verifyFolder, "sc01_verification_result.txt");
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(failCount == 0 ? 0 : 1);
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
