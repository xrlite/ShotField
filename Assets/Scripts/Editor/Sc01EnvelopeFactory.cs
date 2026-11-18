#if UNITY_EDITOR
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static class Sc01EnvelopeFactory
    {
        private const string AssetRoot = "Assets/Models/Props/Generated";

        internal static void Build()
        {
            EnsureFolders();
            var paper = CreateMaterial("EnvelopePaper", new Color(0.94f, 0.91f, 0.84f), 0.75f);
            var red = CreateMaterial("SealRed", new Color(0.55f, 0.025f, 0.025f), 0.38f);
            var root = new GameObject("Envelope_Prop");
            var marker = root.AddComponent<SemanticMarker>();
            marker.markerId = "envelope";
            marker.displayName = "桌上的信";
            marker.kind = SemanticMarker.Kind.KeyProp;
            marker.criticalFaceMustStayHidden = true;
            marker.criticalFaceNormal = Vector3.forward;
            marker.criticalFaceSize = new Vector2(0.16f, 0.11f);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.16f, 0.11f, 0.006f);

            AddMesh(root.transform, "Envelope_Body", BoxMesh(0.16f, 0.11f, 0.004f), paper);
            AddMesh(root.transform, "Envelope_Flap", FlapMesh(), paper);
            AddMesh(root.transform, "Red_Wax_Seal", CylinderMesh(0.014f, 0.002f, 24), red);
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/SC01/Envelope_Prop.prefab");
            Object.DestroyImmediate(root);
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            string meshPath = $"{AssetRoot}/{name}.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (!saved)
            {
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, meshPath);
                saved = mesh;
            }
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = saved;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness)
        {
            string path = $"{AssetRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 1f - smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Mesh BoxMesh(float width, float height, float depth)
        {
            float x = width * 0.5f, y = height * 0.5f, z = depth * 0.5f;
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-x,-y,-z), new Vector3(x,-y,-z), new Vector3(x,y,-z), new Vector3(-x,y,-z),
                new Vector3(-x,-y,z), new Vector3(x,-y,z), new Vector3(x,y,z), new Vector3(-x,y,z)
            };
            mesh.triangles = new[]
            {
                0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4,
                3,7,6, 3,6,2, 0,4,7, 0,7,3, 1,2,6, 1,6,5
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh FlapMesh()
        {
            const float z = 0.003f;
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.078f, 0.053f, z), new Vector3(0.078f, 0.053f, z),
                new Vector3(0f, -0.012f, z), new Vector3(-0.078f, 0.053f, z + 0.001f),
                new Vector3(0.078f, 0.053f, z + 0.001f), new Vector3(0f, -0.012f, z + 0.001f)
            };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CylinderMesh(float radius, float depth, int segments)
        {
            var vertices = new Vector3[segments * 2 + 2];
            var triangles = new int[segments * 12];
            vertices[segments * 2] = new Vector3(0, 0, depth * 0.5f);
            vertices[segments * 2 + 1] = new Vector3(0, 0, -depth * 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                vertices[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, depth * 0.5f);
                vertices[i + segments] = new Vector3(vertices[i].x, vertices[i].y, -depth * 0.5f);
                int n = (i + 1) % segments;
                int t = i * 12;
                triangles[t] = i; triangles[t + 1] = n; triangles[t + 2] = segments * 2;
                triangles[t + 3] = i + segments; triangles[t + 4] = segments * 2 + 1; triangles[t + 5] = n + segments;
                triangles[t + 6] = i; triangles[t + 7] = i + segments; triangles[t + 8] = n;
                triangles[t + 9] = n; triangles[t + 10] = i + segments; triangles[t + 11] = n + segments;
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models/Props")) AssetDatabase.CreateFolder("Assets/Models", "Props");
            if (!AssetDatabase.IsValidFolder(AssetRoot)) AssetDatabase.CreateFolder("Assets/Models/Props", "Generated");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/SC01")) AssetDatabase.CreateFolder("Assets/Prefabs", "SC01");
        }
    }
}
#endif
