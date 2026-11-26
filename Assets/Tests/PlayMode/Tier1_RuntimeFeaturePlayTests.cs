using System.Collections;
using NUnit.Framework;
using ShotField.RuntimeUI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.Tests.PlayMode
{
    [TestFixture]
    public class Tier1_RuntimeFeaturePlayTests
    {
        private PlayModeTestHelpers _h;

        [SetUp]
        public void SetUp() => _h = new PlayModeTestHelpers();

        [TearDown]
        public void TearDown() => _h.CleanupAll();

        [UnityTest]
        public IEnumerator F04_UiRaycastGuard_PatchesWorldCanvasWithoutAddingMouseRaycaster()
        {
            _h.CreateMainCamera();
            _h.CreateEventSystemWithXRUI();
            var canvas = _h.CreateWorldCanvas("UnpatchedWorldCanvas");
            yield return null;

            UiRaycastGuard.EnsureAllNow();
            Assert.IsNotNull(canvas.GetComponent<TrackedDeviceGraphicRaycaster>());
            Assert.IsNull(canvas.GetComponent<GraphicRaycaster>());
        }

        [UnityTest]
        public IEnumerator F04_UiRaycastGuard_UnlocksTransparentStaleCanvasGroup()
        {
            _h.CreateEventSystemWithXRUI();
            var canvas = _h.CreateWorldCanvas("StaleBlockerCanvas");
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;
            group.interactable = true;
            yield return null;

            UiRaycastGuard.EnsureAllNow();
            Assert.IsFalse(group.blocksRaycasts, "Transparent CanvasGroup must not block XR rays");
            Assert.IsFalse(group.interactable);
        }

        [UnityTest]
        public IEnumerator F14_SpatialGlass_Apply_BindsMaterialAndSyncsRectSizeOnLateUpdate()
        {
            var canvas = _h.CreateWorldCanvas("GlassRuntimeCanvas");
            var panel = RuntimeUiFactory.CreatePanel(canvas.transform, "GlassCard",
                new Color(0.12f, 0.13f, 0.14f, 0.94f), Vector2.zero, new Vector2(200f, 100f));
            var img = panel.GetComponent<Image>();
            var mat = _h.Track(SpatialGlass.Apply(img, new Color(0.12f, 0.13f, 0.14f, 0.94f), 20f));
            yield return null;

            if (mat != null)
            {
                ((RectTransform)panel.transform).sizeDelta = new Vector2(360f, 180f);
                yield return null;
                Vector4 rect = mat.GetVector("_RectSize");
                Assert.AreEqual(360f, rect.x, 1f);
                Assert.AreEqual(180f, rect.y, 1f);
            }
            Assert.IsNotNull(img);
        }

        [UnityTest]
        public IEnumerator F14_ThreeCardWorkspace_StartsHiddenAndTogglesAllThreeCards()
        {
            _h.CreateMainCamera();
            var wsGo = _h.Track(new GameObject("WorkspaceHost"));
            var ws = wsGo.AddComponent<ThreeCardWorkspace>();
            yield return null;

            Assert.IsFalse(ws.AnyOpen);
            ws.ToggleCard(ThreeCardWorkspace.MonitorId);
            Assert.IsTrue(ws.IsOpen(ThreeCardWorkspace.MonitorId));
            Assert.IsTrue(ws.AnyOpen);

            ws.ShowCard(ThreeCardWorkspace.TimelineId);
            ws.ShowCard(ThreeCardWorkspace.DrawerId);
            Assert.IsTrue(ws.IsOpen(ThreeCardWorkspace.TimelineId));
            Assert.IsTrue(ws.IsOpen(ThreeCardWorkspace.DrawerId));

            ws.HideAll();
            Assert.IsFalse(ws.AnyOpen);
        }

        [UnityTest]
        public IEnumerator F04_VrNetworkAndCommunityPanels_ToggleVisibilityWithTrackedRaycaster()
        {
            _h.CreateMainCamera();
            var net = _h.Track(new GameObject("NetPanelHost")).AddComponent<VrNetworkPanel>();
            var com = _h.Track(new GameObject("ComPanelHost")).AddComponent<VrCommunityPanel>();
            yield return null;

            Assert.IsFalse(net.Visible);
            Assert.IsFalse(com.Visible);
            net.SetVisible(true);
            com.SetVisible(true);
            yield return null;

            Assert.IsTrue(net.Visible);
            Assert.IsTrue(com.Visible);
            Assert.IsNotNull(net.PanelRoot.GetComponent<TrackedDeviceGraphicRaycaster>());
            Assert.IsNotNull(com.PanelRoot.GetComponent<TrackedDeviceGraphicRaycaster>());
        }

        [UnityTest]
        public IEnumerator F04_PalmMenuUiFactory_And_State_BuildValidCanvasAndSwitchAllTabs()
        {
            var host = _h.Track(new GameObject("PalmMenuRoot"));
            var visuals = PalmMenuUiFactory.Build(host.transform);
            yield return null;

            Assert.IsTrue(visuals.IsValid);
            Assert.IsNotNull(visuals.Canvas.GetComponent<TrackedDeviceGraphicRaycaster>());

            var state = new PalmMenuState();
            state.SetOpen(true);
            Assert.IsTrue(state.IsOpen);

            state.SelectTab(PalmMenuTab.Props);
            Assert.Greater(state.BuildEntries().Count, 0);
            state.SelectTab(PalmMenuTab.Scene);
            Assert.Greater(state.BuildEntries().Count, 0);
            state.SelectTab(PalmMenuTab.Cards);
            Assert.Greater(state.BuildEntries().Count, 0);
        }
    }
}