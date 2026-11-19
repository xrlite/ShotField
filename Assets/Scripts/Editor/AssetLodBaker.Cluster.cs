#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static partial class AssetLodBaker
    {
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
                if (got >= target * 0.40f && got <= target * 2.5f) return mesh;
                Object.DestroyImmediate(mesh);
                cell *= Mathf.Sqrt(Mathf.Max(got, 1) / (float)target);
                if (cell <= 0.0001f) return null;
            }
            Debug.LogWarning("[LOD-BAKE-CLUSTER] 三轮未收敛: " + src.name + " target=" + target);
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
            if (mesh == null) return 0;
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
