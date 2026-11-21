#if UNITY_EDITOR
using System;
using System.Linq;
using ShotField.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class Sc01PrefabFactory
    {
        private const string Root = "Assets/Prefabs/SC01";

        [MenuItem("镜场 ShotField/更新 SC01 家具预制体")]
        public static void Run()
        {
            BuildAll(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("SC01-PREFAB-RESULT=PASS");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        internal static void BuildAll()
        {
            EnsureFolders();
            // 尺寸口径与 SC01_Blender场景_20261120.blend 一致 (Unity X/Y/Z)
            BuildFurniture("Desk_Prop", "sc01_coffee_table", new Vector3(1.2f, 0.45f, 0.6f));
            BuildFurniture("Chair_Prop", "sc01_chair", new Vector3(0.7f, 0.86f, 0.64f));
            BuildFurniture("Sofa_Prop", "sc01_sofa", new Vector3(2f, 0.8f, 0.88f));
            BuildFurniture("Cabinet_Prop", "sc01_shoe_cabinet", new Vector3(0.35f, 0.9f, 1f));
            BuildFurniture("Plant_Prop", "sc01_plant", new Vector3(0.7f, 1.2f, 0.7f));
        }

        private static void BuildActor()
        {
            const string modelPath = "Assets/Models/Characters/Biped_Humanoid.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (!model || !avatar || !avatar.isHuman) throw new InvalidOperationException("Humanoid actor asset invalid.");
            var root = NewMarkedRoot("ActorA_Character", "actorA", SemanticMarker.Kind.ActorPrimary);
            var normalization = new GameObject("Visual");
            normalization.transform.SetParent(root.transform, false);
            var visual = UnityEngine.Object.Instantiate(model, normalization.transform);
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || BoundsOf(renderers).size.y < 0.001f)
                throw new InvalidOperationException("Actor cannot be fitted: missing visual height.");
            visual.transform.localScale *= 1.72f / BoundsOf(renderers).size.y; visual.transform.position -= BoundsOf(renderers).center;
            ApplyActorClay(visual);
            var animator = root.AddComponent<Animator>(); animator.avatar = avatar; animator.runtimeAnimatorController = BuildIdleController();
            AddBoxCollider(root, new Vector3(0.52f, 1.72f, 0.42f));
            Save(root, "ActorA_Character");
        }

        private static RuntimeAnimatorController BuildIdleController()
        {
            const string clipPath = "Assets/Animations/SC01/ActorA_Idle.anim";
            const string controllerPath = "Assets/Animations/SC01/ActorA_Idle.controller";
            EnsureFolder("Assets/Animations", "SC01");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (!clip)
            {
                clip = new AnimationClip { name = "ActorA_Idle", frameRate = 30f };
                var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 1.005f), new Keyframe(1f, 1f));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Visual", typeof(Transform), "m_LocalScale.y"), curve);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            return controller;
        }

        private static void BuildFurniture(string prefabName, string modelFolder, Vector3 targetBounds)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/SC01_Export/{modelFolder}/{modelFolder}.fbx");
            if (!model) throw new InvalidOperationException("Missing furniture FBX: " + modelFolder);
            string path = $"{Root}/{prefabName}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var oldVisual = root.transform.Find("Visual");
                if (!oldVisual) throw new InvalidOperationException("Missing Visual: " + path);
                foreach (Transform child in oldVisual.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                oldVisual.localPosition = Vector3.zero; oldVisual.localRotation = Quaternion.identity; oldVisual.localScale = Vector3.one;
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, oldVisual);
                FitExact(oldVisual.gameObject, targetBounds);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(FurnitureMaterial).ToArray();
                if (!PrefabUtility.SaveAsPrefabAsset(root, path)) throw new InvalidOperationException("Could not save " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Material FurnitureMaterial(Material source)
        {
            var (color, smoothness) = source ? source.name switch
            {
                "SC01_Fabric_Grey" => (new Color(.42f,.44f,.48f), .15f),
                "SC01_Wood" => (new Color(.20f,.135f,.085f), .25f),
                "SC01_Stone" => (new Color(.34f,.33f,.32f), .35f),
                "SC01_Paint_White" => (new Color(.70f,.70f,.69f), .30f),
                "SC01_Leaf" => (new Color(.13f,.26f,.14f), .10f),
                "SC01_Pot" => (new Color(.30f,.26f,.23f), .20f),
                _ => throw new InvalidOperationException("Unexpected furniture material: " + source.name)
            } : throw new InvalidOperationException("FBX material slot is missing.");
            string path = $"Assets/Models/SC01_Export/{source.name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject NewMarkedRoot(string name, string id, SemanticMarker.Kind kind)
        {
            var root = new GameObject(name);
            var marker = root.AddComponent<SemanticMarker>();
            marker.markerId = id; marker.displayName = name; marker.kind = kind;
            return root;
        }

        private static void FitExact(GameObject visual, Vector3 target)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers: " + visual.name);
            var bounds = BoundsOf(renderers);
            // 等比缩放：高度对齐为主，横向允许最多超标 15% 避免瘦高件形变
            float k = Mathf.Min(target.y / bounds.size.y, Mathf.Min(target.x / bounds.size.x, target.z / bounds.size.z) * 1.15f);
            visual.transform.localScale = Vector3.Scale(visual.transform.localScale, new Vector3(k, k, k));
            bounds = BoundsOf(renderers);
            visual.transform.position -= bounds.center;
        }

        private static Bounds BoundsOf(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void ApplyActorClay(GameObject visual)
        {
            const string path = "Assets/Models/Characters/ActorA_Clay.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(0.72f, 0.70f, 0.66f, 1f)); material.SetFloat("_Smoothness", 0.12f);
                AssetDatabase.CreateAsset(material, path);
            }
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
        }

        private static void AddBoxCollider(GameObject root, Vector3 size) => root.AddComponent<BoxCollider>().size = size;

        private static void Save(GameObject root, string name) { PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/{name}.prefab"); UnityEngine.Object.DestroyImmediate(root); }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "SC01");
            EnsureFolder("Assets", "Animations");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
