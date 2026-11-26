#if UNITY_EDITOR
using System.IO;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// Gate 3: renders the 45s promo keyframe sequence (hall -> orb map -> arch tunnel -> 1:1
    /// steadicam -> hall logo) to PNG frames for offline encoding. Editor-rendered, not device footage.
    /// </summary>
    internal static class Gate3FilmCapture
    {
        private const int Fps = 12;
        private const float Duration = 45f;

        [MenuItem("ShotField/Visual/Capture Gate3 Film Frames")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            var hall = GameObject.Find("[WarmHall]");
            var controller = hall != null ? hall.GetComponent<WarmHallVisualController>() : null;
            var director = Object.FindAnyObjectByType<TabletopDirector>();
            var orbDirector = Object.FindAnyObjectByType<SceneOrbDirector>();
            if (hall == null || controller == null || director == null)
                throw new System.InvalidOperationException("hall/director missing");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../05_验证记录/2026-11/gate3_frames"));
            Directory.CreateDirectory(folder);
            var holder = new GameObject("FilmCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = holder.AddComponent<Camera>();
            cam.enabled = false; cam.nearClipPlane = 0.05f; cam.farClipPlane = 80f;
            cam.fieldOfView = 60f; cam.aspect = 16f / 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.047f, 0.039f);
            cam.cullingMask = ~(1 << 5);
            int frames = Mathf.RoundToInt(Fps * Duration);
            bool mini = true;
            for (int i = 0; i < frames; i++)
            {
                float t = i / (float)Fps;
                ApplyState(t, controller, director, orbDirector, ref mini);
                ApplyCamera(t, cam);
                byte[] png = FrameExportUtility.RenderCameraToPng(cam, 1920, 1080);
                File.WriteAllBytes(Path.Combine(folder, $"f_{i:0000}.png"), png);
            }
            Object.DestroyImmediate(holder);
            Debug.Log("[Gate3Film] FRAMES=" + frames + " folder=" + folder);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void ApplyState(float t, WarmHallVisualController controller,
            TabletopDirector director, SceneOrbDirector orbDirector, ref bool mini)
        {
            bool wantMini = t < 16f || t >= 34f;
            if (wantMini != mini)
            {
                mini = wantMini;
                director.SwitchMode(mini ? TabletopDirector.ScaleMode.TabletopMini
                    : TabletopDirector.ScaleMode.Immersive1to1);
                controller.hallEnvironment.gameObject.SetActive(mini);
                if (controller.directorTable != null) controller.directorTable.gameObject.SetActive(mini);
                if (director.sceneRoot1to1 != null) director.sceneRoot1to1.gameObject.SetActive(!mini);
            }
            if (orbDirector != null && t >= 6f && t < 6.1f)
            {
                var orbs = Object.FindObjectsByType<SceneOrb>(FindObjectsInactive.Include);
                foreach (var o in orbs) if (o.orbId == "living") orbDirector.Activate(o);
            }
            if (director.tabletopProxyRoot != null && t >= 6f && t < 16f)
            {
                float k = Mathf.Lerp(1f, 1.5f, Mathf.Clamp01((t - 8f) / 4f));
                director.tabletopProxyRoot.localScale = Vector3.one * k;
            }
        }

        private static void ApplyCamera(float t, Camera cam)
        {
            Vector3 pos, tgt;
            if (t < 5f)
            {
                float k = t / 5f;
                pos = Vector3.Lerp(new Vector3(-0.5f, 0.95f, -0.1f), new Vector3(0.5f, 1.05f, 0.1f), k);
                tgt = new Vector3(0f, 0.6f, 1.2f);
            }
            else if (t < 14f)
            {
                float k = (t - 5f) / 9f;
                pos = Vector3.Lerp(new Vector3(0.5f, 1.05f, 0.1f), new Vector3(0f, 2.3f, 0.6f), k);
                tgt = Vector3.Lerp(new Vector3(0f, 0.6f, 1.2f), new Vector3(0f, 0.9f, 1.2f), k);
            }
            else if (t < 22f)
            {
                float k = (t - 14f) / 8f;
                pos = Vector3.Lerp(new Vector3(0f, 1.7f, 4.2f), new Vector3(0f, 1.6f, 0.4f), k);
                tgt = new Vector3(0f, 1.3f, 1.6f);
            }
            else if (t < 34f)
            {
                float a = (t - 22f) / 12f * Mathf.PI * 1.2f;
                pos = new Vector3(Mathf.Sin(a) * 1.7f, 1.6f, 1.2f + Mathf.Cos(a) * 1.7f);
                tgt = new Vector3(0f, 1.2f, 1.6f);
            }
            else
            {
                float k = (t - 34f) / 11f;
                pos = Vector3.Lerp(new Vector3(0f, 1.6f, 0.4f), new Vector3(0f, 0.95f, -0.1f), k);
                tgt = Vector3.Lerp(new Vector3(0f, 1.3f, 1.6f), new Vector3(0f, 0.6f, 1.2f), k);
            }
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((tgt - pos).normalized, Vector3.up));
        }
    }
}
#endif
