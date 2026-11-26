#if UNITY_EDITOR
using System.IO;
using ShotField.RuntimeUI;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.EditorTools
{
    /// <summary>Builds Hand Tracking anchors, ShotField_XRRig.prefab, and upgrades SC01_Main.</summary>
    internal static partial class XriRigPrefabBuilder
    {
        internal const string PrefabPath = "Assets/Prefabs/XR/ShotField_XRRig.prefab";

        internal static void EnsureEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                es = go.GetComponent<EventSystem>();
            }
            var ui = es.GetComponent<XRUIInputModule>() ?? es.gameObject.AddComponent<XRUIInputModule>();
            ui.activeInputMode = XRUIInputModule.ActiveInputMode.InputSystemActions;
            ui.enableXRInput = true;
            ui.enableMouseInput = true;
            ui.enableBuiltinActionsAsFallback = true;
        }

        [MenuItem("ShotField/XR/Build XRRig Prefab & Upgrade SC01_Main")]
        public static void BuildPrefabAndUpgradeMainScene()
        {
            var prevScene = EditorSceneManager.GetActiveScene().path;
            BuildAndSavePrefab();
            UpgradeMainScene();
            if (!string.IsNullOrEmpty(prevScene) && File.Exists(prevScene) && prevScene != BuildAndroid.ScenePath)
                EditorSceneManager.OpenScene(prevScene, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
        }

        internal static void BuildAndSavePrefab()
        {
            var dir = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var originGo = new GameObject("XR_Origin");
            try
            {
                var mgrGo = new GameObject("XR_InteractionManager");
                mgrGo.transform.SetParent(originGo.transform, false);
                var mgr = mgrGo.AddComponent<XRInteractionManager>();
                var offsetGo = new GameObject("XR_CameraOffset");
                offsetGo.transform.SetParent(originGo.transform, false);
                var hmd = new GameObject("XR_Headset", typeof(Camera), typeof(AudioListener));
                hmd.tag = "MainCamera";
                hmd.transform.SetParent(offsetGo.transform, false);
                var origin = originGo.AddComponent<XROrigin>();
                origin.CameraFloorOffsetObject = offsetGo;
                origin.CameraYOffset = 1.65f;
                origin.Camera = hmd.GetComponent<Camera>();
                XriInputBindings.BindHeadset(hmd);
                XriInputBindings.AttachInputActionManager(originGo);
                SceneXriRigBuilder.BuildLocomotion(originGo);
                var cL = SceneXriRigBuilder.BuildHand(offsetGo.transform, mgr, "XRController_Left", new Vector3(-0.22f, 1.2f, 0.25f));
                var cR = SceneXriRigBuilder.BuildHand(offsetGo.transform, mgr, "XRController_Right", new Vector3(0.22f, 1.2f, 0.25f));
                var hL = EnsureHandAnchor(offsetGo.transform, mgr, "XRHand_Left", true);
                var hR = EnsureHandAnchor(offsetGo.transform, mgr, "XRHand_Right", false);
                AddCompensator(cL); AddCompensator(cR); AddCompensator(hL); AddCompensator(hR);
                WireModality(originGo, cL, cR, hL, hR);
                PrefabUtility.SaveAsPrefabAsset(originGo, PrefabPath);
                Debug.Log("[XriRigPrefabBuilder] Saved prefab: " + PrefabPath);
            }
            finally { Object.DestroyImmediate(originGo); }
        }

        internal static void UpgradeMainScene()
        {
            var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            var origin = Object.FindAnyObjectByType<XROrigin>();
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            if (origin == null || manager == null)
            {
                Debug.LogError("[XriRigPrefabBuilder] Missing XR_Origin or XRInteractionManager in SC01_Main.");
                return;
            }
            var originGo = origin.gameObject;
            var offsetTf = origin.CameraFloorOffsetObject != null
                ? origin.CameraFloorOffsetObject.transform
                : (originGo.transform.Find("XR_CameraOffset") ?? originGo.transform);
            XriInputBindings.AttachInputActionManager(originGo);
            XriInputBindings.BindHeadset(offsetTf.Find("XR_Headset")?.gameObject ?? GameObject.Find("XR_Headset"));
            XriInputBindings.BindLocomotion(Object.FindAnyObjectByType<SnapTurnProvider>(), Object.FindAnyObjectByType<ContinuousMoveProvider>());
            SceneXriRigBuilder.RemoveDirectDuplicates(originGo.transform, offsetTf, "XRController_Left", "XRController_Right", "XRHand_Left", "XRHand_Right");
            var ctrlL = UpgradeController(offsetTf, manager, "XRController_Left", true);
            var ctrlR = UpgradeController(offsetTf, manager, "XRController_Right", false);
            var handL = EnsureHandAnchor(offsetTf, manager, "XRHand_Left", true);
            var handR = EnsureHandAnchor(offsetTf, manager, "XRHand_Right", false);
            AddCompensator(ctrlL); AddCompensator(ctrlR); AddCompensator(handL); AddCompensator(handR);
            foreach (Transform child in originGo.transform)
            {
                var n = child.name;
                if (n != "XRController_Left" && n != "XRController_Right" && n != "XRHand_Left" && n != "XRHand_Right") continue;
                if (child.gameObject == ctrlL || child.gameObject == ctrlR || child.gameObject == handL || child.gameObject == handR) continue;
                Object.DestroyImmediate(child.gameObject);
            }
            WireModality(originGo, ctrlL, ctrlR, handL, handR);
            var placement = Object.FindAnyObjectByType<PropPlacementInteractor>();
            var rightRay = ctrlR != null ? ctrlR.transform.Find("XRController_Right_Ray")?.GetComponent<XRRayInteractor>() : null;
            if (placement != null && rightRay != null) placement.rayInteractor = rightRay;
            EnsureEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[XriRigPrefabBuilder] Upgraded SC01_Main.unity successfully.");
        }

        private static GameObject UpgradeController(Transform origin, XRInteractionManager manager, string name, bool left)
        {
            var tf = origin.Find(name);
            if (tf == null) return SceneXriRigBuilder.BuildHand(origin, manager, name, left ? new Vector3(-0.22f, 1.2f, 0.25f) : new Vector3(0.22f, 1.2f, 0.25f));
            var go = tf.gameObject;
            var direct = go.GetComponent<XRDirectInteractor>();
            var rayTf = tf.Find(name + "_Ray");
            var ray = rayTf != null ? rayTf.GetComponent<XRRayInteractor>() : null;
            if (rayTf != null)
                ConfigureVisuals(rayTf.GetComponent<LineRenderer>(), rayTf.GetComponent<XRInteractorLineVisual>());
            XriInputBindings.BindHand(go, direct, ray, left);
            return go;
        }
    }
}
#endif
