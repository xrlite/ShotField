#if UNITY_EDITOR
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>Gate 2: two glass scene orbs + offline low-poly map presets + map manipulator.</summary>
    internal static class SceneOrbBuilder
    {
        [MenuItem("ShotField/Visual/Build Scene Orbs")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            var hall = GameObject.Find("[WarmHall]");
            var table = hall != null ? hall.transform.Find("DirectorTable") : null;
            var director = Object.FindAnyObjectByType<TabletopDirector>();
            if (table == null || director == null || director.tabletopProxyRoot == null)
                throw new System.InvalidOperationException("hall table or proxy root missing");

            var orbDirector = hall.GetComponent<SceneOrbDirector>() ?? hall.AddComponent<SceneOrbDirector>();
            orbDirector.tabletop = director;
            orbDirector.transition = Object.FindAnyObjectByType<StudioFadeTransition>();
            var proxy = director.tabletopProxyRoot;
            var manip = proxy.GetComponent<TabletopMapManipulator>() ?? proxy.gameObject.AddComponent<TabletopMapManipulator>();
            orbDirector.manipulator = manip;

            var living = EnsureGroup(proxy, "MiniSet_Living");
            var street = EnsureGroup(proxy, "MiniSet_Street");
            BuildLiving(living);
            BuildStreet(street);
            living.gameObject.SetActive(true);
            street.gameObject.SetActive(false);
            orbDirector.livingPreset = living;
            orbDirector.streetPreset = street;

            var glass = GlassMaterial();
            var orbA = EnsureOrb(table, "SceneOrb_Living", new Vector3(-0.80f, 1.12f, 0f), glass);
            var orbB = EnsureOrb(table, "SceneOrb_Street", new Vector3(0.80f, 1.12f, 0f), glass);
            orbA.orbId = "living"; orbA.label = "桌上的信";
            orbB.orbId = "street"; orbB.label = "微缩街角";
            orbDirector.Register(orbA);
            orbDirector.Register(orbB);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneOrbBuilder] GATE2-BUILD=PASS orbs=2 presets=2 manipulator=1");
        }

        private static Transform EnsureGroup(Transform parent, string name)
        {
            var tf = parent.Find(name);
            if (tf != null) return tf;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static SceneOrb EnsureOrb(Transform parent, string name, Vector3 pos, Material glass)
        {
            var tf = parent.Find(name);
            GameObject go;
            if (tf != null) go = tf.gameObject;
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localScale = Vector3.one * 0.16f;
                var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
                core.name = name + "_Core";
                core.transform.SetParent(go.transform, false);
                core.transform.localScale = Vector3.one * 0.42f;
                Object.DestroyImmediate(core.GetComponent<Collider>());
                core.GetComponent<Renderer>().sharedMaterial = glass;
            }
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = glass;
            var col = go.GetComponent<SphereCollider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            if (go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>() == null)
                go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var orb = go.GetComponent<SceneOrb>() ?? go.AddComponent<SceneOrb>();
            return orb;
        }

        private static Material GlassMaterial()
        {
            const string path = "Assets/Materials/WarmHall/MAT_WarmHall_OrbGlass.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetColor("_BaseColor", new Color(0.95f, 0.85f, 0.62f, 0.30f));
            mat.SetFloat("_Smoothness", 0.85f);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildLiving(Transform root)
        {
            if (root.childCount > 0) return;
            Box(root, "MiniSet_Floor", new Vector3(0f, -0.02f, 0f), new Vector3(2.4f, 0.04f, 2.4f), new Color(0.35f, 0.32f, 0.28f));
            Box(root, "MiniSet_BackWall", new Vector3(0f, 0.35f, 1.2f), new Vector3(2.4f, 0.7f, 0.05f), new Color(0.55f, 0.50f, 0.44f));
            Box(root, "MiniSet_LeftWall", new Vector3(-1.2f, 0.35f, 0f), new Vector3(0.05f, 0.7f, 2.4f), new Color(0.55f, 0.50f, 0.44f));
            Box(root, "MiniSet_Sofa", new Vector3(0f, 0.12f, 0.85f), new Vector3(0.9f, 0.24f, 0.35f), new Color(0.72f, 0.66f, 0.58f));
            Box(root, "MiniSet_CoffeeTable", new Vector3(0f, 0.08f, 0.1f), new Vector3(0.5f, 0.10f, 0.3f), new Color(0.45f, 0.34f, 0.24f));
            Box(root, "MiniSet_Cabinet", new Vector3(-0.95f, 0.2f, 0.6f), new Vector3(0.3f, 0.4f, 0.2f), new Color(0.45f, 0.34f, 0.24f));
        }

        private static void BuildStreet(Transform root)
        {
            if (root.childCount > 0) return;
            Box(root, "Street_Road", new Vector3(0f, -0.02f, 0f), new Vector3(2.4f, 0.04f, 2.4f), new Color(0.22f, 0.21f, 0.20f));
            Box(root, "Street_BlockA", new Vector3(-0.7f, 0.25f, -0.6f), new Vector3(0.6f, 0.5f, 0.6f), new Color(0.62f, 0.55f, 0.46f));
            Box(root, "Street_BlockB", new Vector3(0.6f, 0.35f, -0.7f), new Vector3(0.7f, 0.7f, 0.5f), new Color(0.58f, 0.52f, 0.45f));
            Box(root, "Street_BlockC", new Vector3(-0.6f, 0.18f, 0.7f), new Vector3(0.5f, 0.36f, 0.5f), new Color(0.66f, 0.60f, 0.52f));
            Box(root, "Street_BlockD", new Vector3(0.7f, 0.22f, 0.6f), new Vector3(0.6f, 0.44f, 0.6f), new Color(0.60f, 0.54f, 0.47f));
            Box(root, "Street_Lamp", new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.6f, 0.05f), new Color(0.79f, 0.66f, 0.38f));
        }

        private static void Box(Transform root, string name, Vector3 pos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
#endif
