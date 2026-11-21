#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.SceneManagement;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static class Sc01NavMeshBuilder
    {
        public const string ScenePath = "Assets/Scenes/SC01_Final.unity";
        private const string NavDataPath = "Assets/Scenes/SC01_Walkable.asset";
        public const string ActorMotionPath = "Assets/Scenes/SC01_ActorMotion.controller";
        public const string RedMatPath = "Assets/Models/Characters/MAT_Actor_Red.mat";
        public const string BlueMatPath = "Assets/Models/Characters/MAT_Actor_Blue.mat";

        [MenuItem("ShotField/Navigation/Bake SC01 NavMesh & Setup Navigator")]
        public static void BakeAndSetup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) return;

            ColorUtility.TryParseHtmlString("#FF0000", out var red);
            ColorUtility.TryParseHtmlString("#0055FF", out var blue);
            var redMat = GetOrCreateMaterial(RedMatPath, red, "MAT_Actor_Red");
            var blueMat = GetOrCreateMaterial(BlueMatPath, blue, "MAT_Actor_Blue");
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ActorMotionPath);

            BakeFloorNavMesh();
            SetupPerformerNavigator("SC01_Performer_A", redMat, controller);
            SetupPerformerNavigator("SC01_Performer_B", blueMat, controller);
            var floor = FindDeep("Floor").gameObject;
            var navigator = FindDeep("SC01_Performer_A").GetComponent<ActorNavigator>();
            var walkTarget = floor.GetComponent<ActorWalkTarget>();
            if (!walkTarget) walkTarget = floor.AddComponent<ActorWalkTarget>();
            walkTarget.Bind(navigator);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[NavMeshBuilder] SC01 NavMesh baked, performers and materials wired successfully!");
        }

        public static void RunBatch()
        {
            try
            {
                BakeAndSetup();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[NavMeshBuilder] Batch run error: " + ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void BakeFloorNavMesh()
        {
            string[] obstacles = { "CoffeeTable", "Sofa", "Chair", "Plant", "HighCabinet", "ShoeCabinet", "Fridge" };
            foreach (var obs in obstacles)
            {
                var t = FindDeep(obs); if (t == null) continue;
                var mod = t.GetComponent<NavMeshModifier>(); if (!mod) mod = t.gameObject.AddComponent<NavMeshModifier>();
                mod.overrideArea = true; mod.area = 1; mod.applyToChildren = true;
                var o = t.GetComponent<NavMeshObstacle>(); if (!o) o = t.gameObject.AddComponent<NavMeshObstacle>();
                if (t.TryGetComponent<BoxCollider>(out var box)) { o.shape = NavMeshObstacleShape.Box; o.center = box.center; o.size = box.size; }
                o.carving = true;
            }
            foreach (var ign in new[] { "STUDIO_Boundary", "STUDIO_OpenEdge" })
            {
                var t = FindDeep(ign); if (t == null) continue;
                var mod = t.GetComponent<NavMeshModifier>(); if (!mod) mod = t.gameObject.AddComponent<NavMeshModifier>();
                mod.ignoreFromBuild = true; mod.applyToChildren = true;
            }
            var floor = (FindDeep("Floor") ?? FindDeep("Ground"))?.gameObject; if (floor == null) return;
            var surface = floor.GetComponent<NavMeshSurface>(); if (!surface) surface = floor.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.center = floor.transform.InverseTransformDirection(new Vector3(0f, 1.4f, 2f) - floor.transform.position); surface.size = new Vector3(6f, 2.8f, 4f);
            surface.BuildNavMesh();
            var baked = surface.navMeshData;
            if (!baked) throw new System.InvalidOperationException("SC01 NavMesh baking produced no data.");
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavDataPath);
            if (existing)
            {
                surface.RemoveData();
                EditorUtility.CopySerialized(baked, existing);
                surface.navMeshData = existing;
                surface.AddData();
                EditorUtility.SetDirty(existing);
            }
            else AssetDatabase.CreateAsset(baked, NavDataPath);
            Debug.Log("[NavMeshBuilder] NavMeshSurface successfully baked on " + floor.name);
        }

        private static Material GetOrCreateMaterial(string path, Color col, string name)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
                mat = new Material(s) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.name = name; mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetupPerformerNavigator(string rootName, Material mat, RuntimeAnimatorController controller)
        {
            var t = FindDeep(rootName); if (t == null) return;
            var root = t.gameObject;

            var agent = root.GetComponent<NavMeshAgent>(); if (!agent) agent = root.AddComponent<NavMeshAgent>();
            agent.height = 1.75f; agent.radius = 0.30f; agent.baseOffset = 0f; agent.speed = 1.35f; agent.updateRotation = false;

            var animator = root.GetComponentInChildren<Animator>();
            if (!animator) animator = root.GetComponent<Animator>();
            if (!animator) animator = root.AddComponent<Animator>();
            if (controller != null)
                animator.runtimeAnimatorController = controller;

            var nav = root.GetComponent<ActorNavigator>(); if (!nav) nav = root.AddComponent<ActorNavigator>();
            nav.agent = agent; nav.animator = animator;
            nav.walkSpeed = 1.35f; nav.runSpeed = 3.80f; nav.stoppingDistance = 0.15f; nav.turnSpeed = 10f;
            nav.ConfigureAgent();

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            Debug.Log($"[NavMeshBuilder] {rootName} successfully configured with Navigator and {mat.name}!");
        }

        internal static Transform FindDeep(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
                var hit = FindUnder(root.transform, name); if (hit != null) return hit;
            }
            return null;
        }

        internal static Transform FindUnder(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
                var hit = FindUnder(c, name); if (hit != null) return hit;
            }
            return null;
        }
    }
}
#endif
