#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using ShotField.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 批处理证据生成。把「菜单才能点」的功能变成「命令行能跑」，
    /// 否则 Clay 静帧永远拿不到实拍证据。
    ///
    /// 用法：
    ///   Unity.exe -batchmode -projectPath &lt;prj&gt; -executeMethod ShotField.EditorTools.BatchEvidence.ExportSc01 -logFile &lt;log&gt;
    /// 注意：不要加 -nographics，静帧导出需要真实渲染管线。
    /// </summary>
    public static class BatchEvidence
    {
        private const string EvidenceRelative = "../../../05_验证记录/2026-11";
        private const string AssetRelative = "../../../06_素材/白模";

        // Called in a fresh graphical Editor process to save the camera and capture actual scene views.
        public static void ApplyAndReviewDirector()
        {
            if (Application.isBatchMode) throw new InvalidOperationException("Use a graphical Editor for this entry point.");
            Sc01Blockout.SaveDirectorFraming();
            ExportReviewViews();
        }

        [MenuItem("镜场 ShotField/导出 SC01 三视图")]
        public static void ExportReviewViews()
        {
            const string path = "Assets/Scenes/SC01_Blockout.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != path)
            {
                if (!Application.isBatchMode) throw new InvalidOperationException("请先打开 SC01 场景。");
                scene = EditorSceneManager.OpenScene(path);
            }
            ExportSceneViews(scene);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        internal static void ExportSceneViews(UnityEngine.SceneManagement.Scene scene)
        {
            Camera director = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.name == "Director") director = camera;
            if (!director) throw new InvalidOperationException("SC01 缺少 Director。");
            var folder = Path.Combine(ToProjectPath(EvidenceRelative), "unity_views_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            var holder = new GameObject("Review Camera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, scene);
                var cam = holder.AddComponent<Camera>();
                cam.CopyFrom(director);
                cam.enabled = false;
                cam.aspect = 16f / 9f;
                cam.transform.SetPositionAndRotation(director.transform.position, director.transform.rotation);
                ClayFrameExport.SaveToPath(cam, Path.Combine(folder, "director.png"));
                cam.orthographic = true;
                cam.orthographicSize = 1.85f;
                // Clip from just inside the walls so the elevations show the furniture.
                cam.transform.SetPositionAndRotation(new Vector3(0, 1.1f, 0.065f), Quaternion.identity);
                ClayFrameExport.SaveToPath(cam, Path.Combine(folder, "front.png"));
                cam.transform.SetPositionAndRotation(new Vector3(2.84f, 1.1f, 2), Quaternion.LookRotation(Vector3.left));
                ClayFrameExport.SaveToPath(cam, Path.Combine(folder, "side.png"));
                cam.orthographicSize = 2.1f;
                cam.transform.SetPositionAndRotation(new Vector3(0, 2.75f, 2), Quaternion.LookRotation(Vector3.down, Vector3.forward));
                ClayFrameExport.SaveToPath(cam, Path.Combine(folder, "top.png"));
                Debug.Log("SC01-REVIEW-FILES=" + folder);
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }

        /// <summary>
        /// 建 SC01 白模，然后按 16:9 和 9:16 各导出一张灰度 Clay 静帧，
        /// 最后写一份结果文件把两张图的尺寸和路径记下来。
        /// </summary>
        public static void ExportSc01()
        {
            var evidence = ToProjectPath(EvidenceRelative);
            var output = ToProjectPath(AssetRelative);
            var stamp = DateTime.Now;
            var report = new StringBuilder();
            int failures = 0;

            report.AppendLine("SHOTFIELD CLAY EVIDENCE");
            report.AppendLine("time=" + stamp.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("project=" + Application.dataPath);

            try
            {
                Directory.CreateDirectory(evidence);
                Directory.CreateDirectory(output);

                var scene = EditorSceneManager.OpenScene("Assets/Scenes/SC01_Blockout.unity");
                var director = GameObject.Find("Director");
                var camera = director && director.scene == scene ? director.GetComponent<Camera>() : null;
                report.AppendLine("scene=" + scene.path + " camera=Director");
                if (camera == null)
                {
                    report.AppendLine("RESULT=FAIL reason=no camera in blockout");
                    failures++;
                }
                else
                {
                    report.AppendLine("camera=found fov=" + camera.fieldOfView.ToString("F1"));

                    failures += ExportOne(camera, output, evidence, stamp, ShotCameraState.DefaultAspect, "16x9", report);
                    failures += ExportOne(camera, output, evidence, stamp, ShotCameraState.PortraitAspect, "9x16", report);
                }
            }
            catch (Exception error)
            {
                failures++;
                report.AppendLine("RESULT=FAIL reason=" + error.GetType().Name + ": " + error.Message);
                report.AppendLine("stack=" + error.StackTrace);
                Debug.LogException(error);
            }

            report.AppendLine(failures == 0 ? "RESULT=PASS" : "RESULT=FAIL failures=" + failures);

            var reportPath = Path.Combine(evidence, "clay_evidence_" + stamp.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            try
            {
                File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
                Debug.Log("[CLAY] 结果文件已写入 " + reportPath);
            }
            catch (Exception error)
            {
                Debug.LogError("[CLAY] 结果文件写入失败：" + error.Message);
                failures++;
            }

            foreach (var line in report.ToString().Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line)) Debug.Log("[CLAY] " + line.Trim());
            }

            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int ExportOne(Camera camera, string outputDir, string evidenceDir, DateTime stamp,
            float aspect, string label, StringBuilder report)
        {
            var path = Path.Combine(outputDir, $"clay_SC01_{label}_{stamp:yyyyMMdd_HHmmss}.png");
            try
            {
                camera.aspect = ShotCameraState.NormalizeAspect(aspect);
                var written = ClayFrameExport.SaveToPath(camera, path);
                var file = new FileInfo(written);
                if (!file.Exists || file.Length <= 0)
                {
                    report.AppendLine($"{label}=FAIL reason=file missing");
                    return 1;
                }

                var png = ReadPngSize(file.FullName);
                var expected = ClayFrameExport.MeasureOutputSize(aspect);
                var sizeOk = png.x == expected.x && png.y == expected.y;

                report.AppendLine($"{label}=OK bytes={file.Length} px={png.x}x{png.y} expected={expected.x}x{expected.y} match={sizeOk}");
                if (!sizeOk)
                {
                    report.AppendLine($"{label}=FAIL reason=size mismatch");
                    return 1;
                }

                return 0;
            }
            catch (Exception error)
            {
                report.AppendLine($"{label}=FAIL reason=" + error.GetType().Name + ": " + error.Message);
                Debug.LogException(error);
                return 1;
            }
        }

        /// <summary>从 PNG 头读宽高，避免为了核对尺寸再把整张图载入内存。</summary>
        private static Vector2Int ReadPngSize(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var header = new byte[24];
            if (stream.Read(header, 0, header.Length) < header.Length) throw new InvalidDataException("PNG 头不完整");

            int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return new Vector2Int(width, height);
        }

        private static string ToProjectPath(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, relative));
    }
}
#endif
