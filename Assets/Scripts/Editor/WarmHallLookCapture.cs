#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>Evidence capture for the warm director hall, with UI excluded.</summary>
    internal static class WarmHallLookCapture
    {
        private struct Pose
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Target;
            public float Fov;
        }

        [MenuItem("ShotField/Visual/Capture Warm Hall")]
        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            var hall = GameObject.Find("[WarmHall]");
            if (hall == null) throw new InvalidOperationException("Run Build Warm Director Hall first.");
            var center = hall.transform.position;
            var poses = new[]
            {
                new Pose { Name = "主视角", Position = new Vector3(0f, 0.90f, 0.00f), Target = new Vector3(0f, 0.30f, 1.20f), Fov = 66f },
                new Pose { Name = "创作台特写", Position = new Vector3(1.55f, 1.42f, -0.45f), Target = new Vector3(0f, 0.82f, 1.20f), Fov = 48f },
                new Pose { Name = "拱门对称", Position = new Vector3(0f, 1.58f, -2.10f), Target = new Vector3(0f, 1.42f, 3.20f), Fov = 59f }
            };
            poses[0].Position += new Vector3(center.x, 0f, center.z - 1.20f);
            poses[0].Target += new Vector3(center.x, 0f, center.z - 1.20f);
            poses[1].Position += new Vector3(center.x, 0f, center.z - 1.20f);
            poses[1].Target += new Vector3(center.x, 0f, center.z - 1.20f);
            poses[2].Position += new Vector3(center.x, 0f, center.z - 1.20f);
            poses[2].Target += new Vector3(center.x, 0f, center.z - 1.20f);

            Camera template = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                if (cam.name == "Director") { template = cam; break; }

            var holder = new GameObject("WarmHallEvidenceCamera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = holder.AddComponent<Camera>();
            camera.enabled = false;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.047f, 0.039f);
            if (template != null) camera.CopyFrom(template);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.cullingMask = ~(1 << 5);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.047f, 0.039f);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            var cameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (cameraDataType != null)
            {
                var data = new SerializedObject(holder.AddComponent(cameraDataType));
                var post = data.FindProperty("m_RenderPostProcessing");
                if (post != null) { post.boolValue = true; data.ApplyModifiedPropertiesWithoutUndo(); }
            }

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../05_验证记录/2026-11"));
            Directory.CreateDirectory(folder);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var report = new StringBuilder("WARM-HALL-CAPTURE\n");
            report.AppendLine("hallActive=" + hall.activeInHierarchy + " position=" + hall.transform.position);
            foreach (var renderer in hall.GetComponentsInChildren<Renderer>(true))
                report.AppendLine("renderer=" + renderer.name + " active=" + renderer.gameObject.activeInHierarchy +
                    " enabled=" + renderer.enabled + " material=" + (renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "null") +
                    " center=" + renderer.bounds.center);
            foreach (var light in hall.GetComponentsInChildren<Light>(true))
                report.AppendLine("light=" + light.name + " active=" + light.gameObject.activeInHierarchy +
                    " enabled=" + light.enabled + " intensity=" + light.intensity);
            int failures = 0;
            try
            {
                foreach (var pose in poses)
                {
                    camera.orthographic = false;
                    camera.fieldOfView = pose.Fov;
                    camera.aspect = 16f / 9f;
                    camera.transform.SetPositionAndRotation(pose.Position,
                        Quaternion.LookRotation((pose.Target - pose.Position).normalized, Vector3.up));
                    var planes = GeometryUtility.CalculateFrustumPlanes(camera);
                    var tableRenderer = hall.transform.Find("DirectorTable/Table_IvorySlab").GetComponent<Renderer>();
                    report.AppendLine("pose=" + pose.Name + " camera=" + camera.transform.position +
                        " forward=" + camera.transform.forward + " near=" + camera.nearClipPlane +
                        " far=" + camera.farClipPlane + " mask=" + camera.cullingMask +
                        " tableInFrustum=" + GeometryUtility.TestPlanesAABB(planes, tableRenderer.bounds));
                    string path = Path.Combine(folder, stamp + "_暗厅_" + pose.Name + ".png");
                    byte[] bytes = FrameExportUtility.RenderCameraToPng(camera, 1920, 1080);
                    File.WriteAllBytes(path, bytes);
                    report.AppendLine(pose.Name + "=OK bytes=" + bytes.Length + " path=" + path);
                    Debug.Log("[WarmHallCapture] " + pose.Name + " -> " + path);
                }
            }
            catch (Exception error)
            {
                failures++;
                report.AppendLine("FATAL=" + error);
                Debug.LogException(error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
            report.AppendLine(failures == 0 ? "RESULT=PASS 3/3" : "RESULT=FAIL");
            string reportPath = Path.Combine(folder, "warm_hall_capture_" + stamp + ".txt");
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
            Debug.Log("[WarmHallCapture] " + report);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        [MenuItem("ShotField/Visual/Capture Mode Yield")]
        public static void CaptureModeYield()
        {
            EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            var director = UnityEngine.Object.FindAnyObjectByType<TabletopDirector>();
            var hall = GameObject.Find("[WarmHall]");
            var controller = hall != null ? hall.GetComponent<WarmHallVisualController>() : null;
            if (director == null || controller == null) throw new InvalidOperationException("hall or director missing");
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../05_验证记录/2026-11"));
            Directory.CreateDirectory(folder);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var holder = new GameObject("YieldCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = holder.AddComponent<Camera>();
            cam.enabled = false; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
            cam.fieldOfView = 56f; cam.aspect = 16f / 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.047f, 0.039f);
            cam.cullingMask = ~(1 << 5);
            var pos = new Vector3(0f, 0.90f, 0.00f);
            var tgt = new Vector3(0f, 0.30f, 1.20f);
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((tgt - pos).normalized, Vector3.up));
            bool miniBefore = director.currentMode == TabletopDirector.ScaleMode.TabletopMini;
            bool envBefore = controller.hallEnvironment.gameObject.activeSelf;
            bool roomBefore = director.sceneRoot1to1 != null && director.sceneRoot1to1.gameObject.activeSelf;
            string a = Path.Combine(folder, stamp + "_让位_沙盘态.png");
            File.WriteAllBytes(a, FrameExportUtility.RenderCameraToPng(cam, 1920, 1080));
            director.SwitchMode(TabletopDirector.ScaleMode.Immersive1to1);
            controller.hallEnvironment.gameObject.SetActive(false);
            if (controller.directorTable != null) controller.directorTable.gameObject.SetActive(false);
            if (director.sceneRoot1to1 != null) director.sceneRoot1to1.gameObject.SetActive(true);
            string b = Path.Combine(folder, stamp + "_让位_1to1态.png");
            File.WriteAllBytes(b, FrameExportUtility.RenderCameraToPng(cam, 1920, 1080));
            director.SwitchMode(miniBefore ? TabletopDirector.ScaleMode.TabletopMini : TabletopDirector.ScaleMode.Immersive1to1);
            controller.hallEnvironment.gameObject.SetActive(envBefore);
            if (controller.directorTable != null) controller.directorTable.gameObject.SetActive(true);
            if (director.sceneRoot1to1 != null) director.sceneRoot1to1.gameObject.SetActive(roomBefore);
            UnityEngine.Object.DestroyImmediate(holder);
            Debug.Log("[WarmHallCapture] MODE-YIELD a=" + a + " b=" + b);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
