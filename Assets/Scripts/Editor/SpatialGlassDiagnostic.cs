#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    /// <summary>暖白 SpatialGlass 隔离截图与参数回读。</summary>
    public sealed class SpatialGlassDiagnostic
    {
        private const string OutputDir = "F:/pico/05_验证记录/2026-11/spatial_glass_20261125";
        private static Camera _camera;
        private static bool _mainScene;
        private static bool _done;
        private static int _exitCode;

        [MenuItem("ShotField/Diagnostics/Run Spatial Glass Visual")]
        public static void Run()
        {
            Directory.CreateDirectory(OutputDir);
            _mainScene = false;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateWarmScene();
            PlayerPrefs.SetInt("ShotField.FirstRunCardSeen.v1", 0);
            PlayerPrefs.Save();
            _done = false;
            _exitCode = 1;
            EditorApplication.isPlaying = true;
            EditorApplication.update += WatchPlayMode;
        }

        [MenuItem("ShotField/Diagnostics/Capture Spatial Glass In Main Scene")]
        public static void RunMainScene()
        {
            Directory.CreateDirectory(OutputDir);
            _mainScene = true;
            EditorSceneManager.OpenScene("Assets/Scenes/SC01_Main.unity", OpenSceneMode.Single);
            PlayerPrefs.SetInt("ShotField.FirstRunCardSeen.v1", 0);
            PlayerPrefs.Save();
            _done = false;
            _exitCode = 1;
            EditorApplication.isPlaying = true;
            EditorApplication.update += WatchPlayMode;
        }

        private static void CreateWarmScene()
        {
            var lightGo = new GameObject("WarmDiagnosticLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -25f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "WarmFloor";
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { color = new Color(0.78f, 0.75f, 0.69f) };
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WarmWall";
            wall.transform.position = new Vector3(0f, 1.5f, 3f);
            wall.transform.localScale = new Vector3(8f, 3f, 0.2f);
            wall.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { color = new Color(0.86f, 0.83f, 0.76f) };

            var camGo = new GameObject("GlassDiagnosticCamera");
            _camera = camGo.AddComponent<Camera>();
            _camera.tag = "MainCamera";
            _camera.transform.position = new Vector3(0f, 1.45f, -3.2f);
            _camera.transform.LookAt(new Vector3(0f, 1.05f, 0f));
            _camera.fieldOfView = 48f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.20f, 0.19f, 0.17f, 1f);
        }

        private static void WatchPlayMode()
        {
            if (!EditorApplication.isPlaying || _done) return;
            new GameObject("GlassDiagnosticRunner").AddComponent<GlassDiagnosticRunner>();
            EditorApplication.update -= WatchPlayMode;
            EditorApplication.update += WatchDone;
        }

        private static void WatchDone()
        {
            if (!_done) return;
            EditorApplication.update -= WatchDone;
            EditorApplication.Exit(_exitCode);
        }

        private sealed class GlassDiagnosticRunner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                yield return new WaitForSeconds(_mainScene ? 2.5f : 1.2f);
                if (_mainScene)
                {
                    var mainFirst = Object.FindAnyObjectByType<FirstRunOperationCard>();
                    var mainCapsule = Object.FindAnyObjectByType<ScaleToggleCapsule>();
                    var mainCam = Camera.main;
                    if (mainFirst == null || mainCapsule == null || mainCam == null)
                    {
                        File.WriteAllText(Path.Combine(OutputDir, "spatial_glass_main_report.txt"),
                            "RESULT=FAIL first=" + (mainFirst == null) + " capsule=" + (mainCapsule == null) + " camera=" + (mainCam == null), new UTF8Encoding(false));
                        _done = true; _exitCode = 1; EditorApplication.isPlaying = false; yield break;
                    }
                    ActorMotionDiagnostic.CaptureTo(mainCam, Path.Combine(OutputDir, "03_主场景暖白玻璃.png"));
                    var mainLines = new List<string> { "SPATIAL-GLASS-MAIN=PASS", "CAMERA=" + mainCam.name };
                    File.WriteAllLines(Path.Combine(OutputDir, "spatial_glass_main_report.txt"), mainLines, new UTF8Encoding(false));
                    _done = true; _exitCode = 0; EditorApplication.isPlaying = false; yield break;
                }
                var first = Object.FindAnyObjectByType<FirstRunOperationCard>();
                var capsule = Object.FindAnyObjectByType<ScaleToggleCapsule>();
                if (first == null || capsule == null)
                {
                    File.WriteAllText(Path.Combine(OutputDir, "spatial_glass_report.txt"),
                        "RESULT=FAIL missing first=" + (first == null) + " capsule=" + (capsule == null), new UTF8Encoding(false));
                    _done = true; _exitCode = 1; EditorApplication.isPlaying = false; yield break;
                }

                ActorMotionDiagnostic.CaptureTo(_camera, Path.Combine(OutputDir, "01_首屏暖白玻璃.png"));
                first.gameObject.SetActive(false);
                capsule.gameObject.SetActive(false);

                var communityGo = new GameObject("GlassCommunityHost");
                var community = communityGo.AddComponent<VrCommunityPanel>();
                community.headAnchor = _camera.transform;
                community.SetVisible(true);
                community.transform.position = new Vector3(0f, 1.15f, 0.4f);
                community.transform.rotation = Quaternion.LookRotation(community.transform.position - _camera.transform.position, Vector3.up);
                yield return new WaitForSeconds(0.3f);
                ActorMotionDiagnostic.CaptureTo(_camera, Path.Combine(OutputDir, "02_社区暖白玻璃.png"));

                var lines = new List<string> { "SPATIAL-GLASS-DIAGNOSTIC" };
                bool pass = true;
                pass &= Describe(lines, "FirstRun", first.transform.Find("Glass").GetComponent<Image>(), 0.68f);
                pass &= Describe(lines, "Capsule", capsule.transform.Find("CapsuleBg").GetComponent<Image>(), 0.68f);
                pass &= Describe(lines, "Community", community.PanelRoot.GetComponent<Image>(), 0.70f);
                lines.Add("RESULT=" + (pass ? "PASS" : "FAIL"));
                File.WriteAllLines(Path.Combine(OutputDir, "spatial_glass_report.txt"), lines, new UTF8Encoding(false));
                Debug.Log("SPATIAL-GLASS-RESULT=" + (pass ? "PASS" : "FAIL"));
                _done = true;
                _exitCode = pass ? 0 : 1;
                EditorApplication.isPlaying = false;
            }

            private static bool Describe(List<string> lines, string name, Image image, float expectedAlpha)
            {
                if (image == null || image.material == null) { lines.Add(name + "=MISSING"); return false; }
                var mat = image.material;
                float alpha = mat.GetColor("_BaseColor").a;
                float radius = mat.GetFloat("_CornerRadius");
                var size = mat.GetVector("_RectSize");
                bool ok = mat.shader.name == "ShotField/SpatialGlass" &&
                    Mathf.Abs(alpha - expectedAlpha) < 0.01f && Mathf.Abs(radius - 32f) < 0.1f;
                lines.Add(name + " shader=" + mat.shader.name + " alpha=" + alpha.ToString("F2") +
                    " radius=" + radius.ToString("F1") + " size=" + size.x.ToString("F0") + "x" + size.y.ToString("F0") +
                    " ok=" + ok);
                return ok;
            }
        }
    }
}
#endif
