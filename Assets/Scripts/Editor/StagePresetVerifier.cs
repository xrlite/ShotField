#if UNITY_EDITOR
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>验证天工本地预设可加载、单网格灰模，并可在白模/天工之间往返切换。</summary>
    public static class StagePresetVerifier
    {
        public const string Tag = "STAGE-PRESET-RESULT";

        [MenuItem("ShotField/Verifiers/Run Stage Preset Verifier")]
        public static void Run()
        {
            int passed = 0;
            var prefab = Resources.Load<GameObject>("Stages/TianGong_Android");
            Check("Resources 天工预设存在", prefab != null, ref passed);

            var renderers = prefab != null ? prefab.GetComponentsInChildren<MeshRenderer>(true) : new MeshRenderer[0];
            Check("预设恰好 1 个 renderer", renderers.Length == 1, ref passed);

            var filters = prefab != null ? prefab.GetComponentsInChildren<MeshFilter>(true) : new MeshFilter[0];
            int triangles = 0;
            foreach (var filter in filters)
                if (filter.sharedMesh != null) triangles += filter.sharedMesh.triangles.Length / 3;
            Check("预设三角形数为 68569", triangles == 68569, ref passed);
            Check("预设使用 Previs 灰模材质",
                renderers.Length == 1 && renderers[0].sharedMaterial != null &&
                renderers[0].sharedMaterial.name == "MAT_Previs_Prop_Gray", ref passed);

            var canvas = GameObject.Find("Spatial_Workbench_Canvas");
            Check("主场景已挂舞台切换器", canvas != null && canvas.GetComponent<StagePresetSwitcher>() != null, ref passed);

            var host = new GameObject("StagePresetVerifierHost");
            var whitebox = new GameObject("WhiteboxTestRoot");
            var director = host.AddComponent<TabletopDirector>();
            director.sceneRoot1to1 = whitebox.transform;
            var switcher = host.AddComponent<StagePresetSwitcher>();
            switcher.tabletop = director;
            try
            {
                switcher.SetGrandStage(true);
                Check("切到天工后白模关闭", !whitebox.activeSelf && switcher.GrandStageActive, ref passed);
                switcher.SetGrandStage(false);
                Check("切回白模后天工关闭", whitebox.activeSelf && !switcher.GrandStageActive, ref passed);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(whitebox);
            }

            string result = passed == 7 ? "PASS" : "FAIL";
            Debug.Log($"{Tag}={result} ({passed}/7)");
            if (result == "FAIL" && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void Check(string label, bool ok, ref int passed)
        {
            if (ok) { passed++; Debug.Log($"[STAGE-PRESET] PASS: {label}"); }
            else Debug.LogError($"[STAGE-PRESET] FAIL: {label}");
        }
    }
}
#endif
