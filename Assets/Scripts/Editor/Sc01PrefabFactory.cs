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

        public static void Run()
        {
            BuildAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("SC01-PREFAB-RESULT=PASS");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        internal static void BuildAll()
        {
            EnsureFolders();
            BuildActor();
            BuildFurniture("Desk_Prop", "modern_coffee_table_01", "table", SemanticMarker.Kind.Prop,
                new Vector3(1.2f, 0.44f, 0.6f));
            BuildFurniture("Chair_Prop", "modern_arm_chair_01", "chair", SemanticMarker.Kind.Prop,
                new Vector3(0.8f, 0.9f, 0.8f));
            BuildFurniture("Sofa_Prop", "Sofa_01", "sofa", SemanticMarker.Kind.Prop,
                new Vector3(2f, 0.85f, 0.8f));
            BuildFurniture("Cabinet_Prop", "modern_wooden_cabinet", "cabinet", SemanticMarker.Kind.Prop,
                new Vector3(1f, 0.9f, 0.35f));
            BuildFurniture("Plant_Prop", "potted_plant_02", "plant", SemanticMarker.Kind.Prop,
                new Vector3(0.7f, 1.2f, 0.7f));
        }

        private static void BuildActor()
        {
            const string modelPath = "Assets/Models/Characters/Biped_Humanoid.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (!model || !avatar || !avatar.isHuman) throw new InvalidOperationException("Humanoid actor asset invalid.");
            var root = NewMarkedRoot("ActorA_Character", "actorA", SemanticMarker.Kind.ActorPrimary);
            // The idle clip animates Visual.localScale.y around one.
            // Keep model normalization separate from that animated transform.
            var normalization = new GameObject("Visual");
            normalization.transform.SetParent(root.transform, false);
            var visual = UnityEngine.Object.Instantiate(model, normalization.transform);
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || BoundsOf(renderers).size.y < 0.001f)
                throw new InvalidOperationException("Actor cannot be fitted: missing visual height.");
            visual.transform.localScale *= 1.72f / BoundsOf(renderers).size.y;
            visual.transform.position -= BoundsOf(renderers).center;
            ApplyActorClay(visual);
            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = BuildIdleController();
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
                var curve = new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(0.5f, 1.005f), new Keyframe(1f, 1f));
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("Visual", typeof(Transform), "m_LocalScale.y"), curve);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            return controller;
        }

        private static void BuildFurniture(string prefabName, string modelFolder, string markerId,
            SemanticMarker.Kind kind, Vector3 targetBounds)
        {
            string lodPath = $"Assets/Models/Furniture/{modelFolder}/Imported/LOD/{modelFolder}_LOD.prefab";
            string rawPath = $"Assets/Models/Furniture/{modelFolder}/Imported/{modelFolder}.prefab";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(lodPath) ??
                        AssetDatabase.LoadAssetAtPath<GameObject>(rawPath);
            if (!model) throw new InvalidOperationException("Furniture prefab missing: " + rawPath);
            var root = NewMarkedRoot(prefabName, markerId, kind);
            var visual = UnityEngine.Object.Instantiate(model, root.transform);
            visual.name = "Visual";
            FitExact(visual, targetBounds);
            AddBoxCollider(root, targetBounds);
            Save(root, prefabName);
        }

        private static GameObject NewMarkedRoot(string name, string id, SemanticMarker.Kind kind)
        {
            var root = new GameObject(name);
            var marker = root.AddComponent<SemanticMarker>();
            marker.markerId = id;
            marker.displayName = name;
            marker.kind = kind;
            return root;
        }

        private static void FitExact(GameObject visual, Vector3 target)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers: " + visual.name);
            var bounds = BoundsOf(renderers);
            var ratio = new Vector3(target.x / bounds.size.x, target.y / bounds.size.y, target.z / bounds.size.z);
            visual.transform.localScale = Vector3.Scale(visual.transform.localScale, ratio);
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
                material.SetColor("_BaseColor", new Color(0.72f, 0.70f, 0.66f, 1f));
                material.SetFloat("_Smoothness", 0.12f);
                AssetDatabase.CreateAsset(material, path);
            }
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
        }

        private static void AddBoxCollider(GameObject root, Vector3 size)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private static void Save(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "SC01");
            EnsureFolder("Assets", "Animations");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
