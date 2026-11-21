#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShotField.EditorTools
{
    /// <summary>场景观感批处理抓图：彩色、保留材质/灯光/后处理，四机位对齐（不加 -nographics）。</summary>
    public static class Sc01LookCapture
    {
        private const string ScenePath = "Assets/Scenes/SC01_Blockout.unity";
        private const string EvidenceRelative = "../../../05_验证记录/2026-11";
        private const int Width = 1280;
        private const int Height = 720;

        private struct Pose
        {
            public string name;
            public Vector3 position, lookAt;
            public float fov, orthoSize;
            public bool orthographic, hideCeiling;
        }

        private static Pose[] BuildPoses() => new[]
        {
            new Pose { name = "门洞", position = new Vector3(-2.20f, 1.60f, -1.70f), lookAt = new Vector3(-2.20f, 1.45f, 2.40f), fov = 57f },
            new Pose { name = "棚内广角", position = new Vector3(5.30f, 2.30f, -0.95f), lookAt = new Vector3(-2.00f, 1.40f, -0.60f), fov = 53f },
            new Pose { name = "俯视全景", position = new Vector3(1.00f, 8.60f, 2.60f), lookAt = new Vector3(1.00f, 0f, 2.60f), orthographic = true, orthoSize = 4.40f, hideCeiling = true },
            new Pose { name = "抬头桁架", position = new Vector3(0.50f, 1.60f, -1.50f), lookAt = new Vector3(0.50f, 4.00f, 1.00f), fov = 53f }
        };

        [MenuItem("镜场 ShotField/抓场景观感四图 (Look Capture)")]
        public static void CaptureFourMenu() => CaptureFour();

        public static void CaptureFour()
        {
            var report = new StringBuilder();
            int failures = 0;
            var stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, EvidenceRelative));
            Directory.CreateDirectory(folder);

            report.AppendLine("SHOTFIELD LOOK CAPTURE");
            report.AppendLine("time=" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("scene=" + ScenePath);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            report.AppendLine("activeScene=" + scene.path);

            Camera template = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                if (cam.name == "Director") { template = cam; break; }

            var holder = new GameObject("LookCapture Camera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, scene);
                var camera = holder.AddComponent<Camera>();
                camera.enabled = false;
                var additionalCameraType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (additionalCameraType != null)
                {
                    var cameraData = new SerializedObject(holder.AddComponent(additionalCameraType));
                    var renderPostProcessing = cameraData.FindProperty("m_RenderPostProcessing");
                    if (renderPostProcessing != null) { renderPostProcessing.boolValue = true; cameraData.ApplyModifiedPropertiesWithoutUndo(); }
                }
                if (template != null) { camera.CopyFrom(template); report.AppendLine("templateCamera=Director hdr=" + template.allowHDR); }
                else report.AppendLine("templateCamera=NONE (使用默认相机参数)");

                var hidden = new List<GameObject>();
                foreach (var pose in BuildPoses())
                {
                    try
                    {
                        if (pose.hideCeiling)
                        {
                            hidden.Clear();
                            foreach (var root in scene.GetRootGameObjects())
                            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                            {
                                if (!t.gameObject.activeSelf) continue;
                                var n = t.name;
                                if (!n.Contains("CeilPanel") && !n.Contains("Ceiling") && !n.Contains("Ceil")) continue;
                                t.gameObject.SetActive(false);
                                hidden.Add(t.gameObject);
                            }
                        }

                        Apply(camera, pose);
                        var path = Path.Combine(folder, stamp + "_unity_look_" + pose.name + ".png");
                        var bytes = Render(camera);
                        File.WriteAllBytes(path, bytes);
                        var info = new FileInfo(path);
                        report.AppendLine(pose.name + "=OK bytes=" + info.Length + " " + Width + "x" + Height);
                        Debug.Log("[LOOK] " + pose.name + " -> " + path);
                    }
                    catch (System.Exception error)
                    {
                        failures++;
                        report.AppendLine(pose.name + "=FAIL " + error.GetType().Name + ": " + error.Message);
                        Debug.LogException(error);
                    }
                    finally
                    {
                        foreach (var go in hidden) if (go) go.SetActive(true);
                        hidden.Clear();
                    }
                }
            }
            catch (System.Exception error)
            {
                failures++;
                report.AppendLine("FATAL=" + error.GetType().Name + ": " + error.Message);
                Debug.LogException(error);
            }
            finally
            {
                if (holder) Object.DestroyImmediate(holder);
            }

            report.AppendLine(failures == 0 ? "RESULT=PASS 4/4" : "RESULT=FAIL failures=" + failures);
            var reportPath = Path.Combine(folder, "look_capture_" + stamp + ".txt");
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
            foreach (var line in report.ToString().Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) Debug.Log("[LOOK] " + line.Trim());
            Debug.Log("[LOOK] 结果文件 " + reportPath);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static void Apply(Camera camera, Pose pose)
        {
            camera.orthographic = pose.orthographic;
            if (pose.orthographic) camera.orthographicSize = pose.orthoSize;
            else camera.fieldOfView = pose.fov;
            camera.aspect = (float)Width / Height;
            var dir = pose.lookAt - pose.position;
            camera.transform.SetPositionAndRotation(pose.position, Quaternion.LookRotation(dir.normalized, Vector3.up));
        }

        private static byte[] Render(Camera camera)
        {
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new System.InvalidOperationException("当前渲染管线不支持静帧导出。");
                RenderPipeline.SubmitRenderRequest(camera, request);

                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply(false);
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
#endif
