#if UNITY_EDITOR
using System.Text;
using ShotField.Core;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>Checks that the saved emulator opening view shows the story subjects.</summary>
    internal static class Sc01VisualVerifier
    {
        private static int _pass;
        private static int _total;
        private static readonly StringBuilder Report = new StringBuilder();

        public static void Run()
        {
            _pass = 0;
            _total = 0;
            Report.Length = 0;
            EditorSceneManager.OpenScene(BuildAndroid.ScenePath);
            var camera = Camera.main;
            var actor = SemanticMarker.Find("actorA");
            var envelope = SemanticMarker.Find("envelope");
            Check("opening camera exists", camera != null);
            Check("actor marker exists", actor != null);
            Check("envelope marker exists", envelope != null);
            Check("actor is inside opening view", Visible(camera, actor));
            Check("envelope is inside opening view", Visible(camera, envelope));
            Check("actor is not hidden behind camera", InFront(camera, actor));

            string verdict = _pass == _total ? "PASS" : "FAIL";
            string result = $"SC01-VISUAL-RESULT={verdict} ({_pass}/{_total})";
            Report.AppendLine(result);
            Debug.Log("[Sc01VisualVerifier] " + result);
        }

        private static bool Visible(Camera camera, SemanticMarker marker)
        {
            if (!camera || !marker) return false;
            var bounds = ComputeObjectBounds(marker.gameObject);
            var point = camera.WorldToViewportPoint(bounds.center);
            return point.z > camera.nearClipPlane && point.x > 0.08f && point.x < 0.92f
                && point.y > 0.08f && point.y < 0.92f;
        }

        private static bool InFront(Camera camera, SemanticMarker marker)
        {
            if (!camera || !marker) return false;
            return Vector3.Dot(camera.transform.forward,
                ComputeObjectBounds(marker.gameObject).center - camera.transform.position) > 0f;
        }

        private static Bounds ComputeObjectBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void Check(string label, bool ok)
        {
            _total++;
            if (ok) _pass++;
            string line = (ok ? "PASS: " : "FAIL: ") + label;
            Report.AppendLine(line);
            Debug.Log("[Sc01VisualVerifier] " + line);
        }
    }
}
#endif
