#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 跨进程护照持久化与几何/规则重建核验器。
    /// 进程 A：写入 rev001/rev002、计算 SHA256、落盘元数据后彻底退出。
    /// 进程 B：全新独立进程启动，读取 rev001，核验哈希，重建机位与规则，求值判定。
    /// </summary>
    public static class CrossProcessVerifier
    {
        private static string GetRunDirectory()
        {
            var id = Environment.GetEnvironmentVariable("SHOTFIELD_VERIFY_RUN");
            if (string.IsNullOrEmpty(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, @"\A[A-Za-z0-9_-]+\z"))
                throw new InvalidOperationException("请为 A/B 两次调用设置相同的 SHOTFIELD_VERIFY_RUN。");
            return Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../05_验证记录/2026-11", "PassportCrossProcess-" + id));
        }

        public static void RunProcessA()
        {
            try
            {
                var OutDir = GetRunDirectory();
                if (Directory.Exists(OutDir))
                    throw new IOException("该验收目录已存在，请使用新的运行编号。");
                Directory.CreateDirectory(OutDir);

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Sc01Blockout.BuildIntoScene();

                var dirGo = GameObject.Find("Director");
                var cam = dirGo.GetComponent<Camera>();
                var lib = dirGo.GetComponent<ShotLibrary>();
                var vis = dirGo.GetComponent<VisibilityConstraint>();

                // 设置低角度安全机位
                cam.transform.position = new Vector3(2.35f, 0.42f, 1.20f);
                cam.transform.LookAt(new Vector3(1.50f, 0.451f, 2.15f));
                cam.fieldOfView = 45f;
                var s01 = lib.CreateShot(SceneCaptureMode.Current, "S01 低角度安全机位");
                var r1 = vis.Evaluate(s01, null);

                var rules = UnityEngine.Object.FindObjectsByType<ShotConstraint>(FindObjectsInactive.Exclude);
                var specs = new ConstraintSpec[rules.Length];
                for (int i = 0; i < rules.Length; i++) specs[i] = rules[i].ToSpec();
                s01.recordedConstraints = specs;
                s01.lastCheck.Clear();
                s01.lastCheck.Add(r1);
                s01.checkedAt = DateTimeOffset.UtcNow.ToString("O");

                var dto1 = ShotPassportDto.FromShot(s01, lib.SceneId, specs);
                var path1 = ShotPassportStore.Save(dto1, OutDir);

                var dto2 = ShotPassportDto.FromShot(s01, lib.SceneId, specs);
                var path2 = ShotPassportStore.SaveNextRevision(dto2, OutDir);

                string hash1 = ComputeSha256(path1);
                string meta = $"{{\"shot_id\":\"{dto1.shot_id}\",\"rev1_hash\":\"{hash1}\",\"path1\":\"{path1.Replace('\\', '/')}\",\"path2\":\"{path2.Replace('\\', '/')}\"}}";
                File.WriteAllText(Path.Combine(OutDir, "process_a_meta.json"), meta, new UTF8Encoding(false));

                Debug.Log($"[PROCESS_A] 写入完成. Hash={hash1}, PROCESS_A_RESULT=PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PROCESS_A_FAIL] {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void RunProcessB()
        {
            var sb = new StringBuilder();
            int pass = 0, fail = 0;
            void Assert(string name, bool cond) { if (cond) { pass++; sb.AppendLine("  [PASS] " + name); } else { fail++; sb.AppendLine("  [FAIL] " + name); } }

            try
            {
                sb.AppendLine("=== [跨进程护照恢复独立核验 (Process B)] ===");
                sb.AppendLine("启动时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                var OutDir = GetRunDirectory();
                var metaFile = Path.Combine(OutDir, "process_a_meta.json");
                Assert("META. 进程 A 元数据文件存在", File.Exists(metaFile));

                var metaText = File.ReadAllText(metaFile);
                string expectedHash = ExtractJsonValue(metaText, "rev1_hash");
                string path1 = ExtractJsonValue(metaText, "path1");
                string path2 = ExtractJsonValue(metaText, "path2");

                Assert("FILE_REV1. rev001 护照文件在磁盘存在", File.Exists(path1));
                Assert("FILE_REV2. rev002 护照文件在磁盘存在 (版本链完整)", File.Exists(path2));

                string actualHash = ComputeSha256(path1);
                Assert("HASH_MATCH. 跨进程 SHA-256 强校验一致", string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase));

                // 读入护照并核验字段
                var loadedDto = ShotPassportStore.Load(path1);
                Assert("DTO_ID. 镜头 ID 正确 (SC01-S01)", loadedDto.shot_id == "SC01-S01");
                Assert("DTO_REV. 护照版本为 1", loadedDto.revision == 1);
                Assert("SUMMARY_PASS. 质检摘要 pass=1, violation=0", loadedDto.check_summary != null && loadedDto.check_summary.pass == 1 && loadedDto.check_summary.violation == 0);

                // 启动全新的 SC01 几何场景
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Sc01Blockout.BuildIntoScene();

                var dirGo = GameObject.Find("Director");
                var cam = dirGo.GetComponent<Camera>();
                var lib = dirGo.GetComponent<ShotLibrary>();
                var vis = dirGo.GetComponent<VisibilityConstraint>();

                // 用护照还原机位
                var reloadedShot = loadedDto.ToShot();
                lib.ApplyShot(reloadedShot);

                var expectedPos = new Vector3(2.35f, 0.42f, 1.20f);
                Assert("CAM_RESTORE. 机位三维物理坐标深度还原", Vector3.Distance(cam.transform.position, expectedPos) < 0.001f);
                Assert("CAM_FOV. 视场角恢复 (45°)", Mathf.Approximately(cam.fieldOfView, 45f) && Mathf.Approximately(cam.fieldOfView, loadedDto.camera.fov));
                Assert("CAM_ASPECT. 画幅比恢复 (16:9)", Mathf.Approximately(cam.aspect, ShotCameraState.DefaultAspect));
                Assert("CAM_ROTATION. 相机旋转从护照恢复", Quaternion.Angle(
                    cam.transform.rotation, reloadedShot.camera.CameraRotation) < 0.01f);

                // Keep the generated geometry and replace the active visibility rule with the archived rule.
                var archived = Array.Find(reloadedShot.recordedConstraints, spec => spec != null && spec.type == "prop_hidden");
                if (archived == null || archived.propId != "envelope" || archived.face != "front")
                    throw new FormatException("护照缺少 SC01 的信封正面规则。");
                vis.enabled = false;
                var restoredRule = dirGo.AddComponent<VisibilityConstraint>();
                restoredRule.ruleName = archived.ruleName;
                restoredRule.targetMarkerId = archived.propId;
                restoredRule.occluderMask = archived.occluderMask;
                restoredRule.epsilon = archived.epsilon;
                var reEval = restoredRule.Evaluate(reloadedShot, null);
                Assert("RULE_RE_EVAL. 独立进程重求值结果完全一致 (Pass)", reEval.status == CheckStatus.Pass);

                sb.AppendLine($"=== 结果: 通过 {pass} 项, 失败 {fail} 项 ===");
                string resultTag = fail == 0 ? "CROSS_PROCESS_VERIFY_RESULT=PASS" : "CROSS_PROCESS_VERIFY_RESULT=FAIL";
                sb.AppendLine(resultTag);

                var reportPath = Path.Combine(OutDir, "cross_process_verification.txt");
                using (var stream = new FileStream(reportPath, FileMode.CreateNew))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write(sb.ToString());
                Debug.Log(sb.ToString());

                EditorApplication.Exit(fail == 0 ? 0 : 1);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PROCESS_B_FAIL] {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var bytes = sha.ComputeHash(stream);
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string ExtractJsonValue(string json, string key)
        {
            var pattern = $"\"{key}\":\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return "";
            int start = idx + pattern.Length;
            int end = json.IndexOf('"', start);
            return end > start ? json.Substring(start, end - start) : "";
        }
    }
}
#endif
