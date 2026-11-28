#if UNITY_EDITOR
// 目标位置：Assets/Scripts/Editor/WarmHallVisualArchTrim.cs
// 纪律：不修改 WarmHallVisualGeometry.cs；Arch_Inner 管身并入 Arch_Middle，Arch_Inner 只留 20mm 金带。
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static partial class WarmHallVisualBuilder
    {
        private const float TrimWidth = 0.020f;      // 20mm 金带
        private const float TrimChamfer = 0.001f;    // 两侧 1mm 微倒角（水平进给）
        private const float TrimProud = 0.0010f;     // 凸起 1mm，禁与灰管共面
        private const float TrimBuried = 0.0005f;    // 外缘埋进灰管 0.5mm
        private const float TrimTile = 0.5f;         // UV：0.5m 一个 tile
        private const float TrimCenterAngle = Mathf.PI; // a=π = extrados 脊线("上缘")；改 0f 即换 soffit

        /// <summary>与 Arch() 内联的路径公式逐字同步（不能改那 181 行文件）。73 点 / 72 段。</summary>
        private static List<Vector3> TrimPath(float radius, float spring, float tube)
        {
            var points = new List<Vector3>(73);
            for (int i = 8; i > 0; i--)
            {
                float t = i / 8f;
                points.Add(new Vector3(radius + tube * 2f * t * t, spring * (1f - t), 0f));
            }
            const int arc = 56;
            for (int i = 0; i <= arc; i++)
            {
                float a = Mathf.PI * i / arc;
                points.Add(new Vector3(Mathf.Cos(a) * radius, spring + Mathf.Sin(a) * radius, 0f));
            }
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                points.Add(new Vector3(-radius - tube * 2f * t * t, spring * (1f - t), 0f));
            }
            return points;
        }

        /// <summary>20mm 金带：4 圈剖面 × 73 点 = 292 verts / 432+4 tris，1 submesh。</summary>
        private static Mesh ArchTrimMesh(List<Vector3> path, float tube)
        {
            const int prof = 4;
            float halfA = TrimWidth * 0.5f / tube;
            float flatA = (TrimWidth * 0.5f - TrimChamfer) / tube;
            float[] ang = { TrimCenterAngle - halfA, TrimCenterAngle - flatA, TrimCenterAngle + flatA, TrimCenterAngle + halfA };
            float[] off = { -TrimBuried, TrimProud, TrimProud, -TrimBuried };

            var v = new List<Vector3>(path.Count * prof);
            var n = new List<Vector3>(path.Count * prof);
            var uv = new List<Vector2>(path.Count * prof);
            var t = new List<int>();
            float s = 0f;

            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) s += Vector3.Distance(path[i - 1], path[i]);
                Vector3 tangent = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                for (int k = 0; k < prof; k++)
                {
                    Vector3 dir = normal * Mathf.Cos(ang[k]) + Vector3.forward * Mathf.Sin(ang[k]);
                    v.Add(path[i] + dir * (tube + off[k]));
                    n.Add(dir);   // 顶点法线取截面径向 → 倒角读成柔边而非切面（1mm≈0.22px）
                    uv.Add(new Vector2(s / TrimTile, k / (float)(prof - 1)));
                }
            }

            for (int i = 0; i < path.Count - 1; i++)
            for (int k = 0; k < prof - 1; k++)
            {
                int a = i * prof + k, b = a + 1, c = a + prof, d = c + 1;
                Vector3 outward = (n[a] + n[b] + n[c] + n[d]) * 0.25f;
                if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), outward) >= 0f)
                    t.AddRange(new[] { a, b, c, b, d, c });
                else
                    t.AddRange(new[] { a, c, b, b, c, d });
            }

            int last = (path.Count - 1) * prof;   // 两端封口，各 2 tris
            t.AddRange(new[] { 0, 2, 1, 1, 2, 3 });
            t.AddRange(new[] { last, last + 1, last + 2, last + 1, last + 3, last + 2 });

            var mesh = new Mesh { name = "WarmHallArchTrim" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>管体 UV：u=沿路径弧长/tile，v=截面角/2π（接缝落在 soffit）。顶点顺序按环优先。</summary>
        private static List<Vector2> TubeUV(List<Vector3> rings, int sides, float tile)
        {
            var uv = new List<Vector2>(rings.Count * sides);
            float s = 0f;
            for (int i = 0; i < rings.Count; i++)
            {
                if (i > 0) s += Vector3.Distance(rings[i - 1], rings[i]);
                for (int j = 0; j < sides; j++) uv.Add(new Vector2(s / tile, j / (float)sides));
            }
            return uv;
        }

        /// <summary>合并两段管体：单 submesh，内管顶点先加 offsetB，索引加 baseIndex。</summary>
        private static Mesh MergeTubes(Mesh a, Mesh b, Vector3 offsetB, List<Vector3> rings, int sides, float tile)
        {
            var v = new List<Vector3>(); a.GetVertices(v);
            var n = new List<Vector3>(); a.GetNormals(n);
            var t = new List<int>(); a.GetTriangles(t, 0);
            var vb = new List<Vector3>(); b.GetVertices(vb);
            var nb = new List<Vector3>(); b.GetNormals(nb);
            var tb = new List<int>(); b.GetTriangles(tb, 0);

            int baseIndex = v.Count;
            for (int i = 0; i < vb.Count; i++) { v.Add(vb[i] + offsetB); n.Add(nb[i]); }
            for (int i = 0; i < tb.Count; i++) t.Add(tb[i] + baseIndex);   // 漏索引偏移 → 内拱贴到中拱上

            var mesh = new Mesh { name = "WarmHallArchTube_Merged" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, TubeUV(rings, sides, tile));
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        [MenuItem("ShotField/Warm Hall/Apply inner arch trim")]
        private static void ApplyInnerArchTrim()
        {
            var hall = GameObject.Find(RootName);
            var env = hall != null ? hall.transform.Find("HallEnvironment") : null;
            if (env == null) { Debug.LogWarning("[WarmHall] HallEnvironment not found."); return; }
            var midGo = env.Find("Arch_Middle");
            var inGo = env.Find("Arch_Inner");
            if (midGo == null || inGo == null) { Debug.LogWarning("[WarmHall] Arch_Middle/Arch_Inner missing."); return; }

            const float R_MID = 1.72f, R_IN = 1.39f, SPRING = 0.42f, T_MID = 0.065f, T_IN = 0.052f;
            const int sides = 12;

            var pMid = TrimPath(R_MID, SPRING, T_MID);
            var pIn = TrimPath(R_IN, SPRING, T_IN);

            // 位移不吃自身 scale：dz_mesh = (1.74-2.02)/0.45 = -0.622222
            float dz = (inGo.localPosition.z - midGo.localPosition.z) / midGo.localScale.z;

            var rings = new List<Vector3>(pMid);
            rings.AddRange(pIn);
            var merged = MergeTubes(ArchMesh(pMid, T_MID), ArchMesh(pIn, T_IN),
                new Vector3(0f, 0f, dz), rings, sides, TrimTile);
            AssignMesh(midGo.gameObject, merged);      // localScale/localPosition 不动
            var band = ArchTrimMesh(pIn, T_IN);
            AssignMesh(inGo.gameObject, band);
            // 1mm 金带自阴影必出 acne；内管阴影已由并入 Arch_Middle 的管体(On)保住
            inGo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
            EditorSceneManager.SaveScene(env.gameObject.scene);
            Debug.Log(string.Format(
                "[WarmHall] ARCH-TRIM=APPLIED mid tri={0} vtx={1} sub={2} | inner tri={3} vtx={4} sub={5} | dz={6:F6}",
                merged.triangles.Length / 3, merged.vertexCount, merged.subMeshCount,
                band.triangles.Length / 3, band.vertexCount, band.subMeshCount, dz));
        }
    }
}
#endif
