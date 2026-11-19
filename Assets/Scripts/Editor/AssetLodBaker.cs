#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 超标资产轻量化（S2）。两条独立手段，都不改场景布局：
    /// 1) 导入器优化：optimizeMesh=true + MeshCompression.Medium，立即可生效且不影响外观；
    /// 2) LOD 网格烘焙：顶点聚类生成 LOD1/LOD2 网格资产与 LODGroup 变体 prefab，
    ///    是否切到 LOD 变体由调用方决定，本工具不自动替换 Sc01PrefabFactory 的引用。
    ///
    /// 只处理队长点名的两件：potted_plant_02(约 5.4 万面) 与 modern_wooden_cabinet(约 1.84 万面)。
    /// </summary>
    internal static class AssetLodBaker
    {
        private static readonly string[] Targets =
        {
            "Assets/Models/Furniture/potted_plant_02/Imported/potted_plant_02.prefab",
            "Assets/Models/Furniture/modern_wooden_cabinet/Imported/modern_wooden_cabinet.prefab",
        };

        private static readonly float[] Ratios = { 1f, 0.32f, 0.14f };

        public static void Run()
        {
            int ok = 0;
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Bake(Targets[i])) ok++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("LOD-BAKE-RESULT=Done (" + ok + "/" + Targets.Length + ")");
        }

        private static bool Bake(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (root == null)
            {
                Debug.LogWarning("[LOD-BAKE-SKIP] prefab 不存在: " + prefabPath);
                return false;
            }

            TuneImporter(prefabPath);
            return BuildLodPrefab(root, prefabPath);
        }

        /// <summary>把源模型导入设置改成"优化顶点 + 中等压缩"，不改面数但减内存与顶点重复。</summary>
        private static void TuneImporter(string prefabPath)
        {
            string folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            var guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                if (importer.optimizeMeshVertices && importer.optimizeMeshPolygons && importer.meshCompression == ModelImporterMeshCompression.Medium) continue;
                importer.optimizeMeshVertices = true;
                importer.optimizeMeshPolygons = true;
                importer.meshCompression = ModelImporterMeshCompression.Medium;
                importer.SaveAndReimport();
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

            var lods = new LOD[Ratios.Length];
            var root = new GameObject(name + "_LOD");
            var group = root.AddComponent<LODGroup>();

            for (int i = 0; i < Ratios.Length; i++)
            {
                var stage = new GameObject("LOD" + i);
                stage.transform.SetParent(root.transform, false);
                var filter = stage.AddComponent<MeshFilter>();
                var renderer = stage.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = srcRenderer.sharedMaterials;

                Mesh mesh = srcMesh;
                if (i > 0)
                {
                    mesh = Cluster(srcMesh, Ratios[i]);
                    if (mesh == null) mesh = srcMesh;
                    else AssetDatabase.CreateAsset(mesh, $"{lodDir}/{name}_LOD{i}.asset");
                }
                filter.sharedMesh = mesh;
                lods[i] = new LOD(i == 0 ? 0.55f : (i == 1 ? 0.18f : 0.06f), new[] { renderer });
            }

            group.SetLODs(lods);
            string outPath = $"{lodDir}/{name}_LOD.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, outPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[LOD-BAKE] {name}: {srcMesh.triangles.Length / 3} tris -> LOD 变体已生成 {outPath}");
            return true;
        }

        /// <summary>
        /// 顶点聚类简化：把包围盒按 cell 分格，每格取代表顶点，重建三角形并丢弃退化面。
        /// UV 按代表顶点复制（会轻微错位，属 LOD 远景可接受代价）。
        /// </summary>
        private static Mesh Cluster(Mesh src, float ratio)
        {
            var verts = src.vertices;
            var uvs = src.uv;
            var tris = src.triangles;
            if (verts.Length == 0 || tris.Length == 0) return null;

            var b = src.bounds;
            float cell = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) * ratio;
            if (cell <= 0.0001f) return null;

            var cellToNew = new Dictionary<long, int>(verts.Length);
            var newVerts = new List<Vector3>(verts.Length);
            var newUvs = new List<Vector2>(verts.Length);
            var map = new int[verts.Length];

            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                long key = Hash((int)(p.x / cell), (int)(p.y / cell), (int)(p.z / cell));
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

            var newTris = new List<int>(tris.Length);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = map[tris[i]], bb = map[tris[i + 1]], c = map[tris[i + 2]];
                if (a == bb || bb == c || a == c) continue;
                newTris.Add(a); newTris.Add(bb); newTris.Add(c);
            }
            if (newTris.Count == 0) return null;

            var mesh = new Mesh { name = src.name + "_LOD" };
            mesh.indexFormat = newVerts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(newVerts);
            mesh.SetUVs(0, newUvs);
            mesh.SetTriangles(newTris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static long Hash(int x, int y, int z)
        {
            return ((long)x * 73856093L) ^ ((long)y * 19349663L) ^ ((long)z * 83492791L);
        }
    }
}
#endif
