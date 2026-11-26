using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShotField.Constraints;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.Tests.PlayMode
{
    [TestFixture]
    public class Tier4_DirectorScenarioPlayTests
    {
        private PlayModeTestHelpers _h;

        [SetUp]
        public void SetUp() => _h = new PlayModeTestHelpers();

        [TearDown]
        public void TearDown() => _h.CleanupAll();

        [UnityTest]
        public IEnumerator Scenario1_ColdBootUiStack_EnforcesSingleModuleAndPanelTrackedRaycasters()
        {
            _h.CreateMainCamera();
            var module = _h.CreateEventSystemWithXRUI();
            var ws = _h.Track(new GameObject("WS")).AddComponent<ThreeCardWorkspace>();
            var net = _h.Track(new GameObject("Net")).AddComponent<VrNetworkPanel>();
            var com = _h.Track(new GameObject("Com")).AddComponent<VrCommunityPanel>();
            var palm = PalmMenuUiFactory.Build(_h.Track(new GameObject("Palm")).transform);
            yield return null;

            UiRaycastGuard.EnsureAllNow();
            Assert.IsTrue(module.enableXRInput);
            Assert.IsTrue(palm.IsValid);
            Assert.IsNotNull(net.PanelRoot.GetComponent<TrackedDeviceGraphicRaycaster>());
            Assert.IsNotNull(com.PanelRoot.GetComponent<TrackedDeviceGraphicRaycaster>());
            Assert.IsFalse(ws.AnyOpen);

            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (c.renderMode != RenderMode.WorldSpace) continue;
                Assert.IsNotNull(c.GetComponent<TrackedDeviceGraphicRaycaster>(), c.name + " missing XR raycaster");
                Assert.IsNull(c.GetComponent<GraphicRaycaster>(), c.name + " has competing mouse GraphicRaycaster");
            }
        }

        [UnityTest]
        public IEnumerator Scenario2_DualScaleJourney_TabletopTo1to1AndBackPreservesHeadsetAndBudget()
        {
            var xrOrigin = _h.Track(new GameObject("XR_Origin"));
            var headset = new GameObject("XR_Headset");
            headset.transform.SetParent(xrOrigin.transform, false);
            headset.tag = "MainCamera";
            Vector3 headStart = new Vector3(0f, 1.65f, -0.6f);
            headset.transform.position = headStart;
            headset.AddComponent<Camera>().fieldOfView = 60f;

            var root1to1 = _h.Track(new GameObject("SceneRoot1to1"));
            var proxyRoot = _h.Track(new GameObject("TabletopProxyRoot"));
            new GameObject("MiniTable").transform.SetParent(proxyRoot.transform, false);

            var director = _h.Track(new GameObject("Director")).AddComponent<TabletopDirector>();
            director.sceneRoot1to1 = root1to1.transform;
            director.tabletopProxyRoot = proxyRoot.transform;
            director.observerCamera = headset.transform;

            var actors = new List<ActorNavigator>();
            for (int i = 0; i < 4; i++)
            {
                var nav = _h.Track(new GameObject("Actor_" + i)).AddComponent<ActorNavigator>();
                actors.Add(nav);
                ActorSelection.Register(nav);
            }
            var budget = _h.Track(new GameObject("Budget")).AddComponent<TabletopActorBudget>();
            budget.tabletop = director;
            budget.maxVisibleInTabletop = 2;

            var fade = _h.Track(new GameObject("Fade")).AddComponent<StudioFadeTransition>();
            fade.fadeDuration = 0.05f;
            fade.Bind(director, budget, headset.transform);
            yield return null;

            fade.TransitionTo(TabletopDirector.ScaleMode.TabletopMini);
            while (fade.IsRunning) yield return null;
            Assert.AreEqual(TabletopDirector.ScaleMode.TabletopMini, director.currentMode);
            Assert.IsTrue(proxyRoot.activeSelf);
            Assert.IsFalse(root1to1.activeSelf);

            fade.TransitionTo(TabletopDirector.ScaleMode.Immersive1to1);
            while (fade.IsRunning) yield return null;
            Assert.AreEqual(TabletopDirector.ScaleMode.Immersive1to1, director.currentMode);
            Assert.IsTrue(root1to1.activeSelf);
            Assert.Less(Vector3.Distance(headStart, headset.transform.position), 0.001f);
            foreach (var a in actors) Assert.IsTrue(a.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Scenario3_DirectorWorkflow_PrevisStabilizerIkConstraintsAndContracts()
        {
            var wf = new WorkflowCoordinator();
            while (wf.CanAdvance) wf.Advance();
            Assert.IsTrue(wf.IsFinished);

            Sc01PrevisShotData.EvaluateCamera(10f, out var camPos, out var camRot, out float fov);
            var stab = _h.Track(new GameObject("Stab")).AddComponent<VirtualCameraStabilizer>();
            stab.ResetFilters(camPos, camRot);
            Vector3 filtered = stab.FilterPosition(camPos + Vector3.right * 0.1f, 0.033f);
            Assert.Greater(filtered.x, camPos.x);
            Assert.Greater(fov, 20f);

            var ik = _h.Track(new GameObject("Ik", typeof(Animator), typeof(HumanoidThreePointIkDriver)))
                .GetComponent<HumanoidThreePointIkDriver>();
            ik.SetPoseDirect(Vector3.up * 1.6f, Vector3.left * 0.3f, Quaternion.identity,
                Vector3.right * 0.3f, Quaternion.identity);
            yield return null;
            Assert.IsTrue(ik.ikActive);

            var constraintGo = _h.Track(new GameObject("Constraints"));
            var framing = constraintGo.AddComponent<FramingConstraint>();
            framing.subjectId = "A";
            framing.minViewportX = 0.2f;
            framing.maxViewportX = 0.8f;

            var shot = new ShotData();
            shot.camera.position = new Vector3(0f, 1.5f, -3f);
            shot.camera.rotation = Quaternion.identity;
            shot.camera.usesUnityCameraRotation = true;
            shot.snapshot.actors.Add(new ActorState { actorId = "A", position = new Vector3(0f, 1.5f, 0f) });
            Assert.AreEqual(CheckStatus.Pass, framing.Evaluate(shot, null).status);

            Assert.IsNotNull(PlayModeTestHelpers.FindTypeByName("EmulatorRuntimeDetector"));
            Assert.IsNotNull(PlayModeTestHelpers.FindTypeByName("SpatialGlassStyle"));
            Assert.IsNotNull(PlayModeTestHelpers.FindTypeByName("SceneOrbController"));
        }
    }
}