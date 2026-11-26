using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShotField.RuntimeUI;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShotField.Tests.PlayMode
{
    [TestFixture]
    public class Tier2_BoundaryPlayTests
    {
        private PlayModeTestHelpers _h;

        [SetUp]
        public void SetUp() => _h = new PlayModeTestHelpers();

        [TearDown]
        public void TearDown() => _h.CleanupAll();

        [UnityTest]
        public IEnumerator F23_StudioFadeTransition_ReleasesOverlayAndRestoresCameraFov()
        {
            var cam = _h.CreateMainCamera();
            var director = _h.Track(new GameObject("DirectorHost")).AddComponent<TabletopDirector>();
            var proxyRoot = _h.Track(new GameObject("ProxyRoot"));
            var proxyChild = new GameObject("ProxyChild");
            proxyChild.transform.SetParent(proxyRoot.transform, false);
            director.tabletopProxyRoot = proxyRoot.transform;

            var fade = _h.Track(new GameObject("FadeHost")).AddComponent<StudioFadeTransition>();
            fade.fadeDuration = 0.05f;
            fade.Bind(director, null, cam.transform);

            fade.TransitionTo(TabletopDirector.ScaleMode.TabletopMini);
            float guard = 0f;
            while (fade.IsRunning && guard < 3f) { guard += Time.unscaledDeltaTime; yield return null; }

            Assert.IsFalse(fade.IsRunning);
            Assert.AreEqual(TabletopDirector.ScaleMode.TabletopMini, director.currentMode);
            Assert.AreEqual(60f, cam.fieldOfView, 0.1f);
            foreach (var cg in Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include))
            {
                if (cg.alpha <= 0.01f) Assert.IsFalse(cg.blocksRaycasts, "Completed fade must release raycast lock");
            }
        }

        [UnityTest]
        public IEnumerator F24_TabletopActorBudget_CapsToMaxTwoAndKeepsSelectedThenRestores()
        {
            var director = _h.Track(new GameObject("BudgetDirector")).AddComponent<TabletopDirector>();
            director.currentMode = TabletopDirector.ScaleMode.Immersive1to1;
            var actors = new List<ActorNavigator>();
            for (int i = 0; i < 4; i++)
            {
                var go = _h.Track(new GameObject("Actor_" + i));
                var nav = go.AddComponent<ActorNavigator>();
                actors.Add(nav);
                ActorSelection.Register(nav);
            }
            ActorSelection.Select(actors[3]);
            yield return null;

            var budget = _h.Track(new GameObject("BudgetHost")).AddComponent<TabletopActorBudget>();
            budget.tabletop = director;
            budget.maxVisibleInTabletop = 2;
            budget.alwaysKeepSelected = true;

            budget.Apply(true);
            int visible = 0;
            foreach (var a in actors) if (a.gameObject.activeSelf) visible++;
            Assert.LessOrEqual(visible, 2, "Tabletop mode must cap active actors to <= 2");
            Assert.IsTrue(actors[3].gameObject.activeSelf, "Selected actor must stay visible");

            budget.Apply(false);
            foreach (var a in actors) Assert.IsTrue(a.gameObject.activeSelf, "1:1 mode must restore all actors");
        }

        [UnityTest]
        public IEnumerator F23_ArchTunnelOverlay_DisablesAtBoundsAndEnablesMidTransition()
        {
            var head = _h.Track(new GameObject("HeadNode"));
            var overlayGo = _h.Track(new GameObject("ArchOverlay"));
            var overlay = overlayGo.AddComponent<ArchTunnelOverlay>();
            overlay.Initialize(head.transform, 0.25f);
            yield return null;

            var canvas = overlayGo.GetComponent<Canvas>();
            Assert.IsFalse(canvas.enabled);

            overlay.SetProgress(0.5f, true);
            Assert.IsTrue(canvas.enabled);
            overlay.SetProgress(1.0f, true);
            Assert.IsFalse(canvas.enabled);
        }

        [UnityTest]
        public IEnumerator F23_VirtualCameraStabilizer_ResetsOnZeroDtLocksHorizonAndHoldsTripod()
        {
            var rawSource = _h.Track(new GameObject("RawPose"));
            rawSource.transform.SetPositionAndRotation(new Vector3(1f, 1.5f, 2f), Quaternion.Euler(89.5f, 30f, 45f));

            var stab = _h.Track(new GameObject("Stabilizer")).AddComponent<VirtualCameraStabilizer>();
            stab.rawPoseSource = rawSource.transform;
            stab.mode = CameraStabilizerMode.TripodLocked;
            stab.lockHorizon = true;
            stab.ResetFilters(rawSource.transform.position, Quaternion.identity);

            Vector3 zeroDt = stab.FilterPosition(new Vector3(5f, 2f, 3f), 0f);
            Assert.AreEqual(new Vector3(5f, 2f, 3f), zeroDt);

            Quaternion horizon = stab.ApplyHorizonLock(rawSource.transform.rotation);
            Assert.Less(Mathf.Abs((horizon * Vector3.right).y), 0.01f, "Horizon lock must eliminate camera roll");

            stab.ResetFilters(new Vector3(1f, 1.5f, 2f), Quaternion.identity);
            rawSource.transform.position = new Vector3(10f, 1.5f, 2f);
            yield return null;
            Assert.Less(Vector3.Distance(new Vector3(1f, 1.5f, 2f), stab.transform.position), 0.01f);
        }

        [UnityTest]
        public IEnumerator F22_PicoPlaneTabletopAligner_IgnoresVerticalWallAndUsesVirtualFallback()
        {
            _h.CreateMainCamera();
            var director = _h.Track(new GameObject("AlignDirector")).AddComponent<TabletopDirector>();
            var aligner = _h.Track(new GameObject("PlaneAligner")).AddComponent<PicoPlaneTabletopAligner>();
            aligner.tabletop = director;
            yield return null;

            aligner.AlignToSurface(Vector3.zero, Vector3.forward, Vector2.one);
            Assert.IsFalse(aligner.HasDetectedPlane, "Vertical wall normal must be rejected");

            aligner.ApplyFallback();
            Assert.AreEqual("VirtualFallback", aligner.AlignmentMode);
            Assert.IsFalse(aligner.TryGetSurface(out var fallback));
            Assert.IsTrue(fallback.FacesUp && fallback.HasExtent);
        }
    }
}