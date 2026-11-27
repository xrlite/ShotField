#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShotField.EditorTools
{
    internal static partial class WarmHallVisualBuilder
    {
        private static void BuildDome(Transform root, Material mat)
        {
            var go = Primitive(root, "GradientDome", PrimitiveType.Sphere,
                new Vector3(0f, 2f, 1.6f), new Vector3(36f, 36f, 36f), mat);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void BuildDais(Transform root, Material charcoal, Material ivory, Material gold)
        {
            Primitive(root, "Dais_Base", PrimitiveType.Cylinder,
                new Vector3(0f, 0.015f, 0f), CylinderScale(1.55f, 0.08f), charcoal);
            Primitive(root, "Dais_Top", PrimitiveType.Cylinder,
                new Vector3(0f, 0.065f, 0f), CylinderScale(1.48f, 0.025f), ivory);
            Ring(root, "Dais_GoldLine", 1.500f, 1.450f, 0.080f, gold);
        }

        [MenuItem("ShotField/Warm Hall/Refresh logo arches")]
        private static void RefreshLogoArches()
        {
            var hall = GameObject.Find(RootName);
            var root = hall != null ? hall.transform.Find("HallEnvironment") : null;
            if (root == null) return;
            var outer = root.Find("Arch_Outer");
            var middle = root.Find("Arch_Middle");
            var inner = root.Find("Arch_Inner");
            if (outer == null || middle == null || inner == null) return;
            BuildArches(root, outer.GetComponent<Renderer>().sharedMaterial,
                middle.GetComponent<Renderer>().sharedMaterial, inner.GetComponent<Renderer>().sharedMaterial);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        private static void BuildArches(Transform root, Material dark, Material gray, Material gold)
        {
            Arch(root, "Arch_Outer", 2.05f, 0.42f, 0.075f, 2.30f, dark);
            Arch(root, "Arch_Middle", 1.72f, 0.42f, 0.065f, 2.02f, gray);
            Arch(root, "Arch_Inner", 1.39f, 0.42f, 0.052f, 1.74f, gold);
        }

        private static void Arch(Transform root, string name, float radius, float spring,
            float tube, float z, Material mat)
        {
            var points = new List<Vector3>();
            const int arc = 56;
            for (int i = 8; i > 0; i--)
            {
                float t = i / 8f;
                points.Add(new Vector3(radius + tube * 2f * t * t, spring * (1f - t), 0f));
            }
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
            var go = MeshObject(root, name, ArchMesh(points, tube), mat, new Vector3(0f, 0.08f, z));
            go.transform.localScale = new Vector3(1f, 1f, 0.45f);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
        }

        private static Mesh ArchMesh(List<Vector3> path, float radius)
        {
            const int sides = 12;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var zAxis = Vector3.forward;
            for (int i = 0; i < path.Count; i++)
            {
                var prev = path[Mathf.Max(0, i - 1)];
                var next = path[Mathf.Min(path.Count - 1, i + 1)];
                var tangent = (next - prev).normalized;
                var normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                for (int j = 0; j < sides; j++)
                {
                    float a = Mathf.PI * 2f * j / sides;
                    vertices.Add(path[i] + (normal * Mathf.Cos(a) + zAxis * Mathf.Sin(a)) * radius);
                }
            }
            for (int i = 0; i < path.Count - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * sides + j, b = i * sides + (j + 1) % sides;
                int c = (i + 1) * sides + j, d = (i + 1) * sides + (j + 1) % sides;
                triangles.AddRange(new[] { a, b, c, b, d, c });
            }
            var mesh = new Mesh { name = "WarmHallArchTube" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void BuildTable(Transform root, Material charcoal, Material ivory, Material gold)
        {
            root.localScale = new Vector3(1.22f, 1f, 1f);
            Primitive(root, "Table_Foot", PrimitiveType.Cylinder,
                new Vector3(0f, 0.12f, 0f), CylinderScale(0.44f, 0.08f), charcoal);
            Primitive(root, "Table_Pedestal", PrimitiveType.Cylinder,
                new Vector3(0f, 0.45f, 0f), CylinderScale(0.34f, 0.62f), charcoal);
            Primitive(root, "Table_GoldCollar", PrimitiveType.Cylinder,
                new Vector3(0f, 0.77f, 0f), CylinderScale(0.37f, 0.018f), gold);
            Primitive(root, "Table_IvorySlab", PrimitiveType.Cylinder,
                new Vector3(0f, 0.855f, 0f), CylinderScale(0.64f, 0.070f), ivory);
            Primitive(root, "Table_Inset", PrimitiveType.Cylinder,
                new Vector3(0f, 0.895f, 0f), CylinderScale(0.53f, 0.010f), charcoal);
            Ring(root, "Table_GoldFrame", 0.575f, 0.545f, 0.902f, gold);
        }


        private static GameObject Primitive(Transform parent, string name, PrimitiveType type,
            Vector3 pos, Vector3 scale, Material mat)
        {
            var existing = parent.Find(name);
            var go = existing != null ? existing.gameObject : GameObject.CreatePrimitive(type);
            if (existing == null) go.transform.SetParent(parent, false);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject MeshObject(Transform parent, string name, Mesh mesh,
            Material mat, Vector3 pos)
        {
            var existing = parent.Find(name);
            var go = existing != null ? existing.gameObject : new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var filter = go.GetComponent<MeshFilter>();
            var renderer = go.GetComponent<MeshRenderer>();
            if (filter.sharedMesh != null && !EditorUtility.IsPersistent(filter.sharedMesh))
                Object.DestroyImmediate(filter.sharedMesh);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = mat;
            return go;
        }

        private static void Ring(Transform parent, string name, float outer, float inner,
            float y, Material mat)
        {
            const int segments = 96;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * outer, y, Mathf.Sin(a) * outer));
                vertices.Add(new Vector3(Mathf.Cos(a) * inner, y, Mathf.Sin(a) * inner));
                if (i == segments) continue;
                int n = i * 2;
                triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            MeshObject(parent, name, mesh, mat, Vector3.zero)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Vector3 CylinderScale(float diameter, float height) =>
            new Vector3(diameter, height * 0.5f, diameter);
    }
}
#endif
