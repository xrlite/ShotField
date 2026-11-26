#if UNITY_EDITOR
using System.Text;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>Gate 2 hard checks: orbs, proxy tri budget, dual-state yield, no realtime baking.</summary>
    internal static class Gate2Verifier
    {
        private static int _pass;
        private static int _total;
        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("ShotField/Verifiers/Gate2 Scene Orbs")]
        public static void Run()
        {
            _pass = 0; _total = 0; Report.Length = 0;
            EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            var director = Object.FindAnyObjectByType<TabletopDirector>();
            var orbDirector = Object.FindAnyObjectByType<SceneOrbDirector>();
            var orbs = Object.FindObjectsByType<SceneOrb>(FindObjectsInactive.Include);
            Check("two scene orbs", orbs.Length == 2);
            bool interactable = true;
            foreach (var o in orbs)
                if (o.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>() == null ||
                    o.GetComponent<Collider>() == null) interactable = false;
            Check("orbs selectable with collider", interactable);
            Check("orb director wired", orbDirector != null && orbDirector.livingPreset != null && orbDirector.streetPreset != null);
            Check("map manipulator present", orbDirector != null && orbDirector.manipulator != null);

            int tris = 0;
            if (director != null && director.tabletopProxyRoot != null)
                foreach (var r in director.tabletopProxyRoot.GetComponentsInChildren<Renderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                }
            Check("macro proxy tris <= 30000 (actual " + tris + ")", tris > 0 && tris <= 30000);

            bool realtime = false;
            foreach (var p in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include))
                if (p.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime) realtime = true;
            Check("no realtime reflection probes", !realtime);
            Check("no running lightmap bake", !Lightmapping.isRunning);

            if (director != null && director.sceneRoot1to1 != null)
            {
                bool miniBefore = director.currentMode == TabletopDirector.ScaleMode.TabletopMini;
                director.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
                Check("macro hides 1:1 root", !director.sceneRoot1to1.gameObject.activeSelf);
                director.SwitchMode(TabletopDirector.ScaleMode.Immersive1to1);
                Check("micro hides proxy", director.tabletopProxyRoot == null || !director.tabletopProxyRoot.gameObject.activeSelf);
                Check("micro shows 1:1 root", director.sceneRoot1to1.gameObject.activeSelf);
                int actors = 0;
                foreach (var a in Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Exclude)) actors++;
                Check("micro active actors <= 2 (actual " + actors + ")", actors <= 2);
                director.SwitchMode(miniBefore ? TabletopDirector.ScaleMode.TabletopMini : TabletopDirector.ScaleMode.Immersive1to1);
            }

            string verdict = _pass == _total ? "PASS" : "FAIL";
            Report.AppendLine($"GATE2-RESULT={verdict} ({_pass}/{_total})");
            Debug.Log("[Gate2Verifier] " + Report);
            if (Application.isBatchMode) EditorApplication.Exit(_pass == _total ? 0 : 1);
        }

        private static void Check(string label, bool ok)
        {
            _total++;
            if (ok) _pass++;
            Report.AppendLine((ok ? "PASS: " : "FAIL: ") + label);
        }
    }
}
#endif
