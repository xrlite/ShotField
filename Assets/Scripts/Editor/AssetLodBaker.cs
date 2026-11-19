#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 超标资产轻量化（S2）：多部件遍历 + 体素聚类 LOD 烘焙。
    /// 遍历 prefab 中所有 MeshFilter，逐部件复制相对变换并独立聚类。
    /// 任一部件三角数不合格即整级 FAIL，杜绝丢失部件或残缺网格。
    /// </summary>
    public static partial class AssetLodBaker
    {
        private static readonly string[] Targets =
        {
            "Assets/Models/Furniture/potted_plant_02/Imported/potted_plant_02.prefab",
            "Assets/Models/Furniture/modern_wooden_cabinet/Imported/modern_wooden_cabinet.prefab",
        };
        private const float Lod1Ratio = 0.34f;
        private const float Lod2Ratio = 0.12f;
        private const int MinStageTris = 120;
        private static int _stageTotal;
        private static int _stagePass;

        public static void Run()
        {
            int ok = 0;
            _stageTotal = _stagePass = 0;
            for (int i = 0; i < Targets.Length; i++)
                if (Bake(Targets[i])) ok++;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            bool pass = ok == Targets.Length && _stagePass == _stageTotal && _stageTotal > 0;
            Debug.Log("LOD-BAKE-RESULT=" + (pass ? "PASS" : "FAIL") + " (prefabs " + ok + "/"
                + Targets.Length + ", stages " + _stagePass + "/" + _stageTotal + ")");
            if (Application.isBatchMode) EditorApplication.Exit(pass ? 0 : 1);
        }

        private static bool Bake(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (root == null) { Debug.LogError("[LOD-BAKE-SKIP] prefab 不存在: " + prefabPath); return false; }
            TuneImporter(prefabPath);
            return BuildLodPrefab(root, prefabPath);
        }

        private static void TuneImporter(string prefabPath)
        {
            string folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                if (imp.optimizeMeshVertices && imp.optimizeMeshPolygons
                    && imp.meshCompression == ModelImporterMeshCompression.Medium) continue;
                imp.optimizeMeshVertices = true;
                imp.optimizeMeshPolygons = true;
                imp.meshCompression = ModelImporterMeshCompression.Medium;
                imp.SaveAndReimport();
                Debug.Log("[LOD-BAKE-IMPORT] 已优化导入设置: " + path);
            }
        }

        private static bool BuildLodPrefab(GameObject srcRoot, string prefabPath)
        {
            var srcFilters = srcRoot.GetComponentsInChildren<MeshFilter>(true);
            if (srcFilters.Length == 0) return false;
            string dir = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(prefabPath);
            string lodDir = dir + "/LOD";
            if (!AssetDatabase.IsValidFolder(lodDir)) AssetDatabase.CreateFolder(dir, "LOD");

            var root = new GameObject(name + "_LOD");
            var group = root.AddComponent<LODGroup>();
            var lods = new LOD[3];
            bool allOk = true;

            for (int i = 0; i < 3; i++)
            {
                var stage = UnityEngine.Object.Instantiate(srcRoot, root.transform);
                stage.name = "LOD" + i;
                stage.transform.localPosition = Vector3.zero;
                stage.transform.localRotation = Quaternion.identity;
                stage.transform.localScale = Vector3.one;

                var stageFilters = stage.GetComponentsInChildren<MeshFilter>(true);
                var stageRenderers = stage.GetComponentsInChildren<Renderer>(true);
                float ratio = i == 1 ? Lod1Ratio : Lod2Ratio;
                string assetPath = lodDir + "/" + name + "_LOD" + i + ".asset";

                if (i > 0)
                {
                    _stageTotal++;
                    bool stagePass = true;
                    var bakedMeshes = new List<Mesh>();

                    for (int p = 0; p < srcFilters.Length; p++)
                    {
                        var srcMesh = srcFilters[p].sharedMesh;
                        if (srcMesh == null) { stagePass = false; continue; }
                        int srcTris = CountTris(srcMesh);
                        int target = Mathf.Max(MinStageTris, Mathf.RoundToInt(srcTris * ratio));
                        var clustered = Cluster(srcMesh, ratio, target, srcTris);
                        int got = clustered == null ? 0 : CountTris(clustered);
                        bool ok = got >= target * 0.40f && got <= target * 2.5f && got < srcTris;
                        if (!ok) stagePass = false;
                        Debug.Log($"[LOD-BAKE-PART] {name} LOD{i} Part{p}({srcFilters[p].gameObject.name}): src={srcTris} target={target} got={got} -> {(ok ? "OK" : "FAIL")}");
                        var finalMesh = clustered != null ? clustered : srcMesh;
                        finalMesh.name = $"{srcMesh.name}_LOD{i}";
                        stageFilters[p].sharedMesh = finalMesh;
                        if (clustered != null) bakedMeshes.Add(clustered);
                    }

                    if (stagePass) _stagePass++; else allOk = false;
                    SaveLodAsset(assetPath, bakedMeshes);
                }

                lods[i] = new LOD(i == 0 ? 0.55f : (i == 1 ? 0.18f : 0.06f), stageRenderers);
            }

            group.SetLODs(lods);
            group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root, lodDir + "/" + name + "_LOD.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return allOk;
        }

        private static void SaveLodAsset(string assetPath, List<Mesh> meshes)
        {
            if (meshes == null || meshes.Count == 0) return;
            if (File.Exists(assetPath)) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(meshes[0], assetPath);
            for (int p = 1; p < meshes.Count; p++)
            {
                AssetDatabase.AddObjectToAsset(meshes[p], assetPath);
            }
        }
    }
}
#endif
