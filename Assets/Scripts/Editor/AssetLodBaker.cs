#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// 超标资产轻量化（S2）：导入器优化 + LOD 烘焙（按目标面数反推体素边长聚类，逐 submesh 保留三角形）。
    /// 门禁：每级三角数须落在目标区间且严格少于上一级，否则 FAIL，绝不把空壳网格报成成功。
    internal static class AssetLodBaker
    {
        // 队长点名的两件：potted_plant_02(约 5.4 万面) 与 modern_wooden_cabinet(约 1.84 万面)
        private static readonly string[] Targets =
        {
            "Assets/Models/Furniture/potted_plant_02/Imported/potted_plant_02.prefab",
            "Assets/Models/Furniture/modern_wooden_cabinet/Imported/modern_wooden_cabinet.prefab",
        };
        private const float Lod1Ratio = 0.34f;
        private const float Lod2Ratio = 0.12f;
        private const int MinStageTris = 400;
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
            bool pass = ok == Targets.Length && _stagePass == _stageTotal;
            Debug.Log("LOD-BAKE-RESULT=" + (pass ? "PASS" : "FAIL") + " (prefabs " + ok + "/"
                + Targets.Length + ", stages " + _stagePass + "/" + _stageTotal + ")");
        }

        private static bool Bake(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (root == null) { Debug.LogError("[LOD-BAKE-SKIP] prefab 不存在: " + prefabPath); return false; }
            TuneImporter(prefabPath);
            return BuildLodPrefab(root, prefabPath);
        }

        /// 导入设置改成"优化顶点/多边形 + 中等压缩"，不改面数，只减重复顶点与内存。
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
            var filters = srcRoot.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) return false;
            var srcMesh = filters[0].sharedMesh;
            var srcRenderer = filters[0].GetComponent<Renderer>();
            if (srcMesh == null || srcRenderer == null) return false;
            string dir = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(prefabPath);
            string lodDir = dir + "/LOD";
            if (!AssetDatabase.IsValidFolder(lodDir)) AssetDatabase.CreateFolder(dir, "LOD");

            int srcTris = CountTris(srcMesh);
            var root = new GameObject(name + "_LOD");
            var group = root.AddComponent<LODGroup>();
            var lods = new LOD[3];
            bool allOk = true;
            for (int i = 0; i < 3; i++)
            {
                var stage = new GameObject("LOD" + i);
                stage.transform.SetParent(root.transform, false);
                var filter = stage.AddComponent<MeshFilter>();
                var renderer = stage.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = srcRenderer.sharedMaterials;
                Mesh mesh = srcMesh;
                if (i > 0)
                {
                    float ratio = i == 1 ? Lod1Ratio : Lod2Ratio;
                    int target = Mathf.Max(MinStageTris, Mathf.RoundToInt(srcTris * ratio));
                    mesh = Cluster(srcMesh, ratio, target, srcTris);
                    _stageTotal++;
                    int got = mesh == null ? 0 : CountTris(mesh);
                    bool okStage = got >= target * 0.5f && got <= target * 2f && got < srcTris * (i == 1 ? 0.72f : 0.42f);
                    if (okStage) _stagePass++; else allOk = false;
                    Debug.Log("[LOD-BAKE-STAGE] " + name + " LOD" + i + " target=" + target + " got=" + got
                        + " -> " + (okStage ? "OK" : "FAIL"));
                    if (mesh == null) mesh = srcMesh;
                    else AssetDatabase.CreateAsset(mesh, lodDir + "/" + name + "_LOD" + i + ".asset");
                }
                filter.sharedMesh = mesh;
                lods[i] = new LOD(i == 0 ? 0.55f : (i == 1 ? 0.18f : 0.06f), new[] { renderer });
            }
            group.SetLODs(lods);
            PrefabUtility.SaveAsPrefabAsset(root, lodDir + "/" + name + "_LOD.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[LOD-BAKE] " + name + ": " + srcTris + " tris -> 变体 " + lodDir + "/" + name + "_LOD.prefab");
            return allOk;
        }

        /// 按目标面数反推体素边长（表面网格面数约与边长平方成反比），最多自适应 3 轮。
        private static Mesh Cluster(Mesh src, float ratio, int target, int srcTris)
        {
            var verts = src.vertices;
            var uvs = src.uv;
            if (verts.Length == 0) return null;
            float diag = src.bounds.size.magnitude;
            if (diag <= 0.0001f) return null;
            float cell = diag * Mathf.Sqrt(6f / (Mathf.Max(1, srcTris) * ratio));
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var mesh = BuildClustered(src, verts, uvs, cell);
                if (mesh == null) return null;
                int got = CountTris(mesh);
                if (got >= target * 0.6f && got <= target * 1.7f) return mesh;
                Object.DestroyImmediate(mesh);
                cell *= Mathf.Sqrt(Mathf.Max(got, 1) / (float)target);
                if (cell <= 0.0001f) return null;
            }
            Debug.LogWarning("[LOD-BAKE-CLUSTER] 三轮未收敛，target=" + target);
            return null;
        }

        private static Mesh BuildClustered(Mesh src, Vector3[] verts, Vector2[] uvs, float cell)
        {
            var cellToNew = new Dictionary<long, int>(verts.Length);
            var newVerts = new List<Vector3>(verts.Length);
            var newUvs = new List<Vector2>(verts.Length);
            var map = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                long key = Hash(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
                int idx;
                if (!cellToNew.TryGetValue(key, out idx))
                {
                    idx = newVerts.Count;
                    cellToNew[key] = idx;
                    newVerts.Add(p);
                    newUvs.Add(uvs.Length > i ? uvs[i] : Vector2.zero);
                }
                map[i] = idx;
            }
            var mesh = new Mesh { name = src.name + "_LOD" };
            mesh.indexFormat = newVerts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(newVerts);
            mesh.SetUVs(0, newUvs);
            mesh.subMeshCount = src.subMeshCount;
            int kept = 0;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tris = src.GetTriangles(s);
                var outTris = new List<int>(tris.Length);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int a = map[tris[i]], b = map[tris[i + 1]], c = map[tris[i + 2]];
                    if (a == b || b == c || a == c) continue;
                    outTris.Add(a); outTris.Add(b); outTris.Add(c);
                }
                mesh.SetTriangles(outTris, s);
                kept += outTris.Count;
            }
            if (kept == 0) { Object.DestroyImmediate(mesh); return null; }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int CountTris(Mesh mesh)
        {
            int n = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) n += mesh.GetTriangles(s).Length;
            return n / 3;
        }

        private static long Hash(int x, int y, int z)
        {
            return ((long)x * 73856093L) ^ ((long)y * 19349663L) ^ ((long)z * 83492791L);
        }
    }
}
#endif
