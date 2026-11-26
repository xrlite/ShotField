#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>Beveled solid meshes for the art hall: sweep, arch, disc, ring, cone. No external assets.</summary>
    internal static class HallMeshKit
    {
        public static Vector2[] Octagon(float w, float h, float b)
        {
            float x = w * 0.5f, y = h * 0.5f;
            return new[]
            {
                new Vector2(x - b, y), new Vector2(x, y - b), new Vector2(x, -y + b), new Vector2(x - b, -y),
                new Vector2(-x + b, -y), new Vector2(-x, -y + b), new Vector2(-x, y - b), new Vector2(-x + b, y)
            };
        }

        static void Quad(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nr)
        {
            bool flip = Vector3.Dot(Vector3.Cross(b - a, d - a), nr) < 0f;
            int i0 = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(nr);
            if (flip) { t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2); t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1); }
            else { t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2); t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3); }
        }

        static void Fan(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 center, Vector3[] ring, Vector3 nr)
        {
            for (int i = 0; i < ring.Length; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % ring.Length];
                bool flip = Vector3.Dot(Vector3.Cross(a - center, b - center), nr) < 0f;
                int i0 = v.Count;
                v.Add(center); v.Add(a); v.Add(b);
                n.Add(nr); n.Add(nr); n.Add(nr);
                if (flip) { t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1); } else { t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2); }
            }
        }

        /// <summary>Sweep a convex 2D section along a 3D path; binormal is the section's V axis.</summary>
        public static Mesh Sweep(Vector3[] path, Vector3 binormal, Vector2[] sec, bool closed, bool caps)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            int count = path.Length, m = sec.Length;
            var tan = new Vector3[count]; var nor = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 prev = path[closed ? (i - 1 + count) % count : Mathf.Max(i - 1, 0)];
                Vector3 next = path[closed ? (i + 1) % count : Mathf.Min(i + 1, count - 1)];
                tan[i] = (next - prev).normalized;
                nor[i] = Vector3.Cross(binormal, tan[i]).normalized;
            }
            var rings = new Vector3[count][]; var nrms = new Vector3[count][];
            for (int i = 0; i < count; i++)
            {
                rings[i] = new Vector3[m]; nrms[i] = new Vector3[m];
                for (int j = 0; j < m; j++)
                {
                    rings[i][j] = path[i] + nor[i] * sec[j].x + binormal * sec[j].y;
                    nrms[i][j] = (nor[i] * sec[j].x + binormal * sec[j].y).normalized;
                }
            }
            int spans = closed ? count : count - 1;
            for (int i = 0; i < spans; i++)
            {
                int i2 = (i + 1) % count;
                for (int j = 0; j < m; j++)
                {
                    int j2 = (j + 1) % m;
                    Vector3 nr = (nrms[i][j] + nrms[i2][j] + nrms[i2][j2] + nrms[i][j2]) * 0.25f;
                    Quad(v, n, t, rings[i][j], rings[i2][j], rings[i2][j2], rings[i][j2], nr.normalized);
                }
            }
            if (caps && !closed)
            {
                Fan(v, n, t, path[0], rings[0], -tan[0]);
                Fan(v, n, t, path[count - 1], rings[count - 1], tan[count - 1]);
            }
            return Finish(v, n, t);
        }

        public static Mesh Arch(float halfWidth, float height, float ringWidth, float depth, float bevel, int arcSeg)
        {
            var path = new List<Vector3> { new Vector3(-halfWidth, 0f, 0f), new Vector3(-halfWidth, height - halfWidth, 0f) };
            for (int i = 1; i < arcSeg; i++)
            {
                float a = Mathf.PI * (1f - (float)i / arcSeg);
                path.Add(new Vector3(Mathf.Cos(a) * halfWidth, height - halfWidth + Mathf.Sin(a) * halfWidth, 0f));
            }
            path.Add(new Vector3(halfWidth, height - halfWidth, 0f));
            path.Add(new Vector3(halfWidth, 0f, 0f));
            return Sweep(path.ToArray(), Vector3.forward, Octagon(ringWidth, depth, bevel), false, true);
        }

        public static Mesh BevelBox(float w, float h, float d, float b)
        {
            float hx = w * 0.5f - b, hz = d * 0.5f - b;
            var path = new[]
            {
                new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz),
                new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz)
            };
            var mesh = Sweep(path, Vector3.up, Octagon(b * 2f, h, b * 0.9f), true, false);
            var v = new List<Vector3>(mesh.vertices); var n = new List<Vector3>(mesh.normals); var t = new List<int>(mesh.triangles);
            float y = h * 0.5f, cap = b * 0.1f;
            Quad(v, n, t, new Vector3(-hx + cap, y, -hz + cap), new Vector3(hx - cap, y, -hz + cap), new Vector3(hx - cap, y, hz - cap), new Vector3(-hx + cap, y, hz - cap), Vector3.up);
            Quad(v, n, t, new Vector3(-hx + cap, -y, -hz + cap), new Vector3(hx - cap, -y, -hz + cap), new Vector3(hx - cap, -y, hz - cap), new Vector3(-hx + cap, -y, hz - cap), Vector3.down);
            return Finish(v, n, t);
        }

        public static Mesh Disc(float radius, float thick, float bevel, int seg)
        {
            var path = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                path[i] = new Vector3(Mathf.Cos(a) * (radius - bevel), 0f, Mathf.Sin(a) * (radius - bevel));
            }
            var sec = new[]
            {
                new Vector2(0f, thick), new Vector2(bevel, thick - bevel),
                new Vector2(bevel, 0f), new Vector2(0f, 0f)
            };
            var mesh = Sweep(path, Vector3.up, sec, true, false);
            var v = new List<Vector3>(mesh.vertices); var n = new List<Vector3>(mesh.normals); var t = new List<int>(mesh.triangles);
            var ringTop = new Vector3[seg]; var ringBot = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                ringTop[i] = new Vector3(Mathf.Cos(a) * (radius - bevel), thick, Mathf.Sin(a) * (radius - bevel));
                ringBot[i] = new Vector3(Mathf.Cos(a) * (radius - bevel), 0f, Mathf.Sin(a) * (radius - bevel));
            }
            Fan(v, n, t, new Vector3(0f, thick, 0f), ringTop, Vector3.up);
            Fan(v, n, t, Vector3.zero, ringBot, Vector3.down);
            return Finish(v, n, t);
        }

        public static Mesh Ring(float radius, float tube, float y, int seg)
        {
            var path = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                path[i] = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
            }
            return Sweep(path, Vector3.up, Octagon(tube, tube, tube * 0.35f), true, false);
        }

        public static Mesh Cone(float r0, float r1, float y0, float y1, int seg)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a0 = 2f * Mathf.PI * i / seg, a1 = 2f * Mathf.PI * ((i + 1) % seg) / seg;
                Vector3 nr = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0.25f, Mathf.Sin((a0 + a1) * 0.5f)).normalized;
                Quad(v, n, t,
                    new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0),
                    new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0),
                    new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1),
                    new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1), nr);
            }
            return Finish(v, n, t);
        }

        static Mesh Finish(List<Vector3> v, List<Vector3> n, List<int> t)
        {
            var mesh = new Mesh { name = "HallMesh" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
