using System.Collections.Generic;
using NUnit.Framework;
using ShotField.Core;
using ShotField.RuntimeUI;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.Tests.E2E
{
    [TestFixture]
    public class XrUiRigContractTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }
            _created.Clear();
        }

        private GameObject Track(GameObject go)
        {
            _created.Add(go);
            return go;
        }

        [Test]
        public void WorldSpaceCanvas_WithTrackedDeviceGraphicRaycaster_TriggersButtonOnClick()
        {
            var esGo = Track(new GameObject("TestEventSystem"));
            esGo.AddComponent<EventSystem>();
            var uiModule = esGo.AddComponent<XRUIInputModule>();

            var canvasGo = Track(new GameObject("TestWorldCanvas"));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var raycaster = canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var btnGo = new GameObject("TestActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(canvasGo.transform, false);
            var btn = btnGo.GetComponent<Button>();

            int clickCount = 0;
            btn.onClick.AddListener(() => clickCount++);
            btn.onClick.Invoke();

            Assert.IsNotNull(uiModule);
            Assert.IsNotNull(raycaster);
            Assert.IsNull(btnGo.GetComponent<XRSimpleInteractable>(), "uGUI Button must not share GO with XRSimpleInteractable");
            Assert.IsNull(btnGo.GetComponent<BoxCollider>(), "uGUI Button must not have BoxCollider intercepting UI ray");
            Assert.AreEqual(1, clickCount);
        }

        [Test]
        public void XrOriginAndRayInteractor_RequireValidHandednessAndUiPressBinding()
        {
            var mgrGo = Track(new GameObject("XR_InteractionManager"));
            var mgr = mgrGo.AddComponent<XRInteractionManager>();

            var originGo = Track(new GameObject("XR_Origin"));
            var origin = originGo.AddComponent<XROrigin>();
            var camGo = new GameObject("XR_Headset");
            camGo.transform.SetParent(originGo.transform, false);
            origin.Camera = camGo.AddComponent<Camera>();

            var rayGo = new GameObject("XRController_Right_Ray");
            rayGo.transform.SetParent(originGo.transform, false);
            var ray = rayGo.AddComponent<XRRayInteractor>();
            ray.interactionManager = mgr;
            ray.handedness = InteractorHandedness.Right;
            var uiPressReader = new XRInputButtonReader(
                "UI Press",
                inputSourceMode: XRInputButtonReader.InputSourceMode.InputAction);
            uiPressReader.inputActionPerformed = new InputAction(
                "UI Press",
                InputActionType.Button,
                "<XRController>{RightHand}/triggerPressed");
            ray.uiPressInput = uiPressReader;

            Assert.AreEqual(InteractorHandedness.Right, ray.handedness);
            Assert.AreNotEqual(InteractorHandedness.None, ray.handedness);
            Assert.IsNotNull(ray.uiPressInput);
            Assert.IsNull(rayGo.GetComponent<Collider>(), "Ray interactor GO should not carry a blocking Collider");
        }

        [Test]
        public void ArchTunnelOverlay_DisablesCanvasAtZeroAlphaAndEnablesDuringTransition()
        {
            var parentGo = Track(new GameObject("HeadAnchor"));
            var overlayGo = new GameObject("ArchTunnelOverlay");
            var overlay = overlayGo.AddComponent<ArchTunnelOverlay>();
            overlay.Initialize(parentGo.transform, 0.25f);

            var canvas = overlayGo.GetComponent<Canvas>();
            var group = overlayGo.GetComponent<CanvasGroup>();
            Assert.IsNotNull(canvas);
            Assert.IsNotNull(group);
            Assert.IsFalse(canvas.enabled, "Idle overlay canvas must stay disabled at alpha=0");

            overlay.SetProgress(0.5f, true);
            Assert.IsTrue(canvas.enabled, "Active transition canvas must enable at mid-progress");
            Assert.Greater(group.alpha, 0.5f);

            overlay.SetProgress(0f, false);
            Assert.IsFalse(canvas.enabled, "Completed overlay canvas must disable at alpha=0");
        }

        [Test]
        public void LocalAiConfig_MasksSecretsAndControlsLegacyMigration()
        {
            Assert.AreEqual("未配置", LocalAiConfig.Mask(""));
            Assert.AreEqual("••••", LocalAiConfig.Mask("1234"));
            StringAssert.StartsWith("sk-", LocalAiConfig.Mask("sk-1234567890abcdef"));
            Assert.IsFalse(LocalAiConfig.Mask("sk-1234567890abcdef").Contains("1234567890"));

            Assert.IsTrue(LocalAiConfig.ShouldMigrateLegacy(true, false));
            Assert.IsFalse(LocalAiConfig.ShouldMigrateLegacy(true, true));
            Assert.IsFalse(LocalAiConfig.ShouldMigrateLegacy(false, false));
        }
    }
}
