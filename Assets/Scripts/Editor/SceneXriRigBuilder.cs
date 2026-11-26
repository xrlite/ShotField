#if UNITY_EDITOR
using ShotField.RuntimeUI;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 把 XRI 骨架装进 SC01 场景：XROrigin（复用现有 XR_Headset 相机）、InteractionManager、
    /// 每手 Direct+Ray 两个 Interactor、HandTracking 锚点、走位 Provider，并给可摆道具挂抓取组件。
    /// </summary>
    internal static class SceneXriRigBuilder
    {
        internal static readonly string[] GrabbableProps = { "Envelope", "Tea", "Slipper" };

        internal static void BuildRig(GameObject root)
        {
            var manager = BuildManager(root.transform);
            var origin = BuildOrigin(root.transform);
            BuildLocomotion(origin);
            var cL = BuildHand(origin.transform, manager, "XRController_Left", new Vector3(-0.22f, 1.2f, 0.25f));
            var cR = BuildHand(origin.transform, manager, "XRController_Right", new Vector3(0.22f, 1.2f, 0.25f));
            var hL = XriRigPrefabBuilder.EnsureHandAnchor(origin.transform, manager, "XRHand_Left", true);
            var hR = XriRigPrefabBuilder.EnsureHandAnchor(origin.transform, manager, "XRHand_Right", false);
            XriRigPrefabBuilder.WireModality(origin, cL, cR, hL, hR);
            AttachGrabbables(root.transform);
            UpgradeWorkbenchCanvas();
            BuildPlacement(root.transform);
        }

        private static void BuildPlacement(Transform root)
        {
            var rayTf = root.Find("XR_Origin/XRController_Right/XRController_Right_Ray");
            if (rayTf == null) return;

            var go = new GameObject("PropPlacement");
            go.transform.SetParent(root, false);
            var placement = go.AddComponent<PropPlacementInteractor>();
            placement.rayInteractor = rayTf.GetComponent<XRRayInteractor>();
            placement.browser = Object.FindAnyObjectByType<PropBrowserUI>();
            placement.spawnRoot = root;
        }

        private static XRInteractionManager BuildManager(Transform parent)
        {
            var go = new GameObject("XR_InteractionManager");
            go.transform.SetParent(parent, false);
            return go.AddComponent<XRInteractionManager>();
        }

        private static GameObject BuildOrigin(Transform parent)
        {
            var originGo = new GameObject("XR_Origin");
            originGo.transform.SetParent(parent, false);

            var headset = parent.Find("XR_Headset");
            if (headset != null) headset.SetParent(originGo.transform, true);

            var origin = originGo.AddComponent<XROrigin>();
            if (headset != null)
            {
                var cam = headset.GetComponent<Camera>();
                if (cam != null) origin.Camera = cam;
                XriInputBindings.BindHeadset(headset.gameObject);
            }
            XriInputBindings.AttachInputActionManager(originGo);
            return originGo;
        }

        internal static void BuildLocomotion(GameObject originGo)
        {
            originGo.AddComponent<LocomotionMediator>();

            var snapGo = new GameObject("SnapTurn");
            snapGo.transform.SetParent(originGo.transform, false);
            var snap = snapGo.AddComponent<SnapTurnProvider>();
            snap.turnAmount = DirectorLocomotion.DefaultTurnAngle;

            var moveGo = new GameObject("ContinuousMove");
            moveGo.transform.SetParent(originGo.transform, false);
            var move = moveGo.AddComponent<ContinuousMoveProvider>();
            move.moveSpeed = DirectorLocomotion.DefaultMoveSpeed;
            XriInputBindings.BindLocomotion(snap, move);

            var dirLoco = originGo.AddComponent<DirectorLocomotion>();
            var comfort = originGo.AddComponent<LocomotionComfortController>();
            comfort.directorLocomotion = dirLoco;
            var headset = originGo.transform.Find("XR_Headset");
            if (headset != null) comfort.cameraRigOffsetNode = headset;
        }

        internal static void RemoveDirectDuplicates(Transform origin, Transform offset, params string[] names)
        {
            if (origin == offset) return;
            for (int i = origin.childCount - 1; i >= 0; i--)
            {
                var child = origin.GetChild(i);
                if (System.Array.IndexOf(names, child.name) >= 0) Object.DestroyImmediate(child.gameObject);
            }
        }

        internal static GameObject BuildHand(Transform origin, XRInteractionManager manager,
            string name, Vector3 localPosition)
        {
            var hand = new GameObject(name);
            hand.transform.SetParent(origin, false);
            hand.transform.localPosition = localPosition;

            var direct = hand.AddComponent<XRDirectInteractor>();
            direct.interactionManager = manager;
            var sphere = hand.AddComponent<SphereCollider>();
            sphere.radius = 0.12f;
            sphere.isTrigger = true;

            var rayGo = new GameObject(name + "_Ray");
            rayGo.transform.SetParent(hand.transform, false);
            var ray = rayGo.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            var line = rayGo.AddComponent<LineRenderer>();
            var vis = rayGo.AddComponent<XRInteractorLineVisual>();
            XriRigPrefabBuilder.ConfigureVisuals(line, vis);
            XriInputBindings.BindHand(hand, direct, ray, name.Contains("Left"));
            return hand;
        }

        private static void AttachGrabbables(Transform root)
        {
            for (int i = 0; i < GrabbableProps.Length; i++)
            {
                var child = root.Find(GrabbableProps[i]);
                if (child == null) continue;
                MakeGrabbable(child.gameObject);
            }
        }

        internal static void MakeGrabbable(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = false;

            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            if (go.GetComponent<XRGrabInteractable>() == null) go.AddComponent<XRGrabInteractable>();
            if (go.GetComponent<ScenePropGrab>() == null) go.AddComponent<ScenePropGrab>();
        }

        private static void UpgradeWorkbenchCanvas()
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
            if (canvas.renderMode != RenderMode.WorldSpace) return;
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null) return;
            canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }
    }
}
#endif
