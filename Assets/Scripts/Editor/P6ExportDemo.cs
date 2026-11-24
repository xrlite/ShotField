#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Recorder;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>P6 编辑器出片驱动器：4 个硬切机位，每镜真实推位，15 秒 1080p30。</summary>
    public static class P6ExportDemo
    {
        private const string ScenePath = BuildAndroid.ScenePath;
        private const float ShotDuration = 3.75f;
        private static readonly CameraPresetId[] PresetIds =
        {
            CameraPresetId.Front, CameraPresetId.Side,
            CameraPresetId.Low, CameraPresetId.OverShoulder
        };
        private static readonly Vector3 StageAnchor = new Vector3(-0.80f, 1.00f, 1.80f);
        private static readonly Vector3[] Moves =
        {
            new Vector3(0.60f, 0.03f, 0.40f),
            new Vector3(-0.60f, 0.00f, 0.40f),
            new Vector3(0.50f, 0.03f, -0.40f),
            new Vector3(-0.50f, -0.05f, 0.40f)
        };
        private static readonly Vector3[] ActorTargets =
        {
            new Vector3(0.00f, 0f, 0.80f),
            new Vector3(-1.20f, 0f, 1.80f),
            new Vector3(0.80f, 0f, 2.20f),
            new Vector3(1.20f, 0f, 1.00f)
        };

        private static RecorderController _recorder;
        private static Camera _camera;
        private static ActorNavigator _actor;
        private static string _outputPath;
        private static float _startedAt;
        private static int _shot = -1;

        [MenuItem("ShotField/Demo/Run P6 Export")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[P6-DEMO] 已在 Play Mode，请先退出。");
                return;
            }

            _outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..",
                "05_验证记录", "2026-11", "20261124_P6预演_15s_v05.mp4"));
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            DevToolPanels.LegacyDesktopVisible = false;
            EditorApplication.isPlaying = true;
            EditorApplication.update += Tick;
            Debug.Log("[P6-DEMO] START：15 秒 / 4 机位 / 1080p30。");
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                Finish(false, "Play Mode 被提前停止");
                return;
            }

            if (_recorder == null)
            {
                Begin();
                return;
            }

            float elapsed = Time.time - _startedAt;
            int shot = Mathf.Min(3, Mathf.FloorToInt(elapsed / ShotDuration));
            if (shot != _shot)
            {
                _shot = shot;
                if (_actor != null) _actor.MoveTo(ActorTargets[_shot], true);
            }

            float local = Mathf.Clamp01((elapsed - _shot * ShotDuration) / ShotDuration);
            var preset = CameraPreset.Build(PresetIds[_shot], StageAnchor);
            _camera.transform.position = preset.position + Moves[_shot] * local;
            _camera.transform.LookAt(StageAnchor);
            _camera.fieldOfView = Mathf.Lerp(preset.fieldOfView, preset.fieldOfView - 15f, local);

            if (elapsed >= 15f)
            {
                bool fileOk = File.Exists(_outputPath) || File.Exists(_outputPath + ".mp4");
                Finish(fileOk && _shot == 3, $"shots=4 elapsed={elapsed:F2}s file={fileOk}");
            }
        }

        private static void Begin()
        {
            var guide = UnityEngine.Object.FindAnyObjectByType<WorkflowGuidePanel>(FindObjectsInactive.Include);
            if (guide != null) guide.enabled = false;
            var desktop = UnityEngine.Object.FindAnyObjectByType<DesktopStudioPanel>(FindObjectsInactive.Include);
            if (desktop != null) desktop.enabled = false;
            var edit = UnityEngine.Object.FindAnyObjectByType<DesktopStudioEditView>(FindObjectsInactive.Include);
            if (edit != null) edit.enabled = false;
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                cam.enabled = false;
            var go = new GameObject("P6FilmCamera");
            _camera = go.AddComponent<Camera>();
            _camera.tag = "MainCamera";
            _camera.fieldOfView = 50f;
            var lightGo = new GameObject("P6KeyLight");
            var keyLight = lightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.5f;
            keyLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            _actor = UnityEngine.Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Exclude)
                .FirstOrDefault(n => n != null && n.gameObject.activeInHierarchy);
            _recorder = ShotFieldRecorder.Start(_outputPath);
            _startedAt = Time.time;
            _shot = -1;
            Debug.Log("[P6-DEMO] RECORDER=" + _outputPath);
        }

        private static void Finish(bool pass, string detail)
        {
            EditorApplication.update -= Tick;
            ShotFieldRecorder.Stop(_recorder);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            Debug.Log($"[P6-DEMO] {(pass ? "PASS" : "FAIL")} {detail}");
            Debug.Log($"P6-EDITOR-RESULT={(pass ? "PASS" : "FAIL")}");
            Debug.Log("[P6-DEMO] MP4=" + _outputPath);
        }
    }
}
#endif
