#if UNITY_EDITOR
using ShotField.RuntimeUI;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 把 XRI 骨架装进 SC01 场景：XROrigin（复用现有 XR_Headset 相机）、InteractionManager、
    /// 每手 Direct+Ray 两个 Interactor、走位 Provider，并给可摆道具挂抓取组件。
    /// 只装配、不新增第二个主相机。手柄位姿目前是相对原点占位，真机需补 TrackedPoseDriver 绑定。
    /// </summary>
    internal static class SceneXriRigBuilder
    {
        internal static readonly string[] GrabbableProps = { "Envelope", "Tea", "Slipper" };

        internal static void BuildRig(GameObject root)
        {
            var manager = BuildManager(root.transform);
            var origin = BuildOrigin(root.transform);
            BuildLocomotion(origin);
            BuildHand(origin.transform, manager, "XRController_Left", new Vector3(-0.22f, 1.2f, 0.25f));
            BuildHand(origin.transform, manager, "XRController_Right", new Vector3(0.22f, 1.2f, 0.25f));
            AttachGrabbables(root.transform);
            UpgradeWorkbenchCanvas();
            BuildPlacement(root.transform);
        }

        /// <summary>
        /// 装配道具放置交互器：复用右手射线做瞄准，复用 PropBrowserUI 做预览源与落地生成。
        /// 场景里没有 PropBrowserUI 时跳过（放置仍可用 Resources 兜底，但缺绑定列表更慢）。
        /// </summary>
        private static void BuildPlacement(Transform root)
        {
            var rayTf = root.Find("XR_Origin/XRController_Right/XRController_Right_Ray");
            if (rayTf == null) return;

            var go = new GameObject("PropPlacement");
            go.transform.SetParent(root, false);
            var placement = go.AddComponent<PropPlacementInteractor>();
            placement.rayInteractor = rayTf.GetComponent<XRRayInteractor>();
            placement.browser = UnityEngine.Object.FindAnyObjectByType<PropBrowserUI>();
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

            // 复用现有头显相机：移入 Origin 但保持世界坐标，不新增主相机
            var headset = parent.Find("XR_Headset");
            if (headset != null) headset.SetParent(originGo.transform, true);

            var origin = originGo.AddComponent<XROrigin>();
            if (headset != null)
            {
                var cam = headset.GetComponent<Camera>();
                if (cam != null) origin.Camera = cam;
            }
            return originGo;
        }

        private static void BuildLocomotion(GameObject originGo)
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

        private static void BuildHand(Transform origin, XRInteractionManager manager,
            string name, Vector3 localPosition)
        {
            var hand = new GameObject(name);
            hand.transform.SetParent(origin, false);
            hand.transform.localPosition = localPosition;

            // 近距抓：必须有 trigger SphereCollider
            var direct = hand.AddComponent<XRDirectInteractor>();
            direct.interactionManager = manager;
            var sphere = hand.AddComponent<SphereCollider>();
            sphere.radius = 0.12f;
            sphere.isTrigger = true;

            // 远距射线：不得带 collider（光线由 LineRenderer 表现）
            var rayGo = new GameObject(name + "_Ray");
            rayGo.transform.SetParent(hand.transform, false);
            var ray = rayGo.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            ray.maxRaycastDistance = 8f;
            var line = rayGo.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.004f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            rayGo.AddComponent<XRInteractorLineVisual>();
            XriInputBindings.BindHand(hand, direct, ray, name.Contains("Left"));
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
            // XRGrabInteractable 硬性要求：非 trigger Collider + Rigidbody
            var collider = go.GetComponent<Collider>();
            if (collider == null) collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = false;

            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;   // 不参与重力，避免道具自己滑走；抓取仍可用
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
