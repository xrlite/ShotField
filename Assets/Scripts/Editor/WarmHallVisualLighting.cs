#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShotField.EditorTools
{
    internal static partial class WarmHallVisualBuilder
    {
        private static void BuildLight(GameObject root, Material fixture, Material beam)
        {
            var existing = root.transform.Find("KeyLight_Top");
            var lightGo = existing != null ? existing.gameObject : new GameObject("KeyLight_Top");
            if (existing == null) lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 4.20f, 0.15f);
            var light = lightGo.GetComponent<Light>() ?? lightGo.AddComponent<Light>();
            var fillTf = root.transform.Find("WarmFill_Arch");
            if (fillTf != null) Object.DestroyImmediate(fillTf.gameObject);
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.82f, 0.59f);
            light.intensity = 160f;
            light.range = 12f;
            light.spotAngle = 74f;
            light.innerSpotAngle = 46f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.82f;
            lightGo.transform.LookAt(new Vector3(0f, 0.75f, 0f));
            var fill = new GameObject("WarmFill_Arch");
            fill.transform.SetParent(root.transform, false);
            fill.transform.localPosition = new Vector3(0f, 2.2f, 2.6f);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.color = new Color(1f, 0.72f, 0.42f);
            fl.intensity = 26f;
            fl.range = 7f;
            fl.shadows = LightShadows.None;
            Primitive(root.transform, "CeilingFixture", PrimitiveType.Cylinder,
                new Vector3(0f, 4.18f, 0.15f), CylinderScale(0.16f, 0.025f), fixture);
            var shaft = MeshObject(root.transform, "LightShaft", ConeMesh(), beam,
                new Vector3(0f, 2.55f, 0.15f));
            shaft.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Mesh ConeMesh()
        {
            const int segments = 64;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                vertices.Add(new Vector3(x * 1.32f, -1.60f, z * 1.32f));
                vertices.Add(new Vector3(x * 0.07f, 1.60f, z * 0.07f));
                if (i == segments) continue;
                int n = i * 2;
                triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 });
            }
            var mesh = new Mesh { name = "WarmHallLightCone" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
#endif
