#if UNITY_EDITOR
using System.IO;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>用真实 Tripo GLB 验证运行时导入、缩放与碰撞体。</summary>
    public static class TripoRuntimeImportVerifier
    {
        public const string Tag = "TRIPO-RUNTIME-IMPORT";

        [MenuItem("ShotField/Verifiers/Run Tripo Runtime Import Verifier")]
        public static void Run()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..",
                "06_素材", "Tripo生成", "door_v01.glb"));
            GameObject model = null;
            int passed = 0;
            try
            {
                model = RuntimeGltfImporter.Import(path, null, Vector3.zero, 1.5f, "door");
                Check("GLB 导入返回对象", model != null, ref passed);
                Check("MeshFilter 非空", model != null && model.GetComponentsInChildren<MeshFilter>().Length > 0, ref passed);
                Check("MeshRenderer 非空", model != null && model.GetComponentsInChildren<MeshRenderer>().Length > 0, ref passed);
                Check("Collider 已挂载", model != null && model.GetComponentInChildren<Collider>() != null, ref passed);
                var bounds = model != null ? model.GetComponentInChildren<Renderer>().bounds : new Bounds();
                Check("目标高度约 1.5m", model != null && Mathf.Abs(bounds.size.y - 1.5f) < 0.05f, ref passed);
            }
            catch (System.Exception ex) { Debug.LogError("[TRIPO-IMPORT] " + ex); }
            finally { if (model != null) Object.DestroyImmediate(model); }

            string result = passed == 5 ? "PASS" : "FAIL";
            Debug.Log($"{Tag}={result} ({passed}/5)");
            if (result == "FAIL" && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void Check(string label, bool ok, ref int passed)
        {
            if (ok) { passed++; Debug.Log($"[TRIPO-IMPORT] PASS: {label}"); }
            else Debug.LogError($"[TRIPO-IMPORT] FAIL: {label}");
        }
    }
}
#endif
