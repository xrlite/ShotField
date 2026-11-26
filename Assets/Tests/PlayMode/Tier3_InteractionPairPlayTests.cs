using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ShotField.Tests.PlayMode
{
    [TestFixture]
    public class Tier3_InteractionPairPlayTests
    {
        private PlayModeTestHelpers _h;

        [SetUp]
        public void SetUp() => _h = new PlayModeTestHelpers();

        [TearDown]
        public void TearDown() => _h.CleanupAll();

        [UnityTest]
        public IEnumerator Pair_F05AndF22_WarmHallVisualController_HidesLegacyAndTracksAnchor()
        {
            var director = _h.Track(new GameObject("PairDirector")).AddComponent<TabletopDirector>();
            director.TabletopAnchor.position = new Vector3(1.5f, 0.8f, 2.2f);
            var legacy = _h.Track(new GameObject("LegacyStage"));
            var hallEnv = _h.Track(new GameObject("HallEnv"));

            var ctrl = _h.Track(new GameObject("WarmHallCtrl")).AddComponent<WarmHallVisualController>();
            ctrl.tabletop = director;
            ctrl.hallEnvironment = hallEnv.transform;
            ctrl.legacyStageRoots = new[] { legacy };
            ctrl.followTabletopAnchor = true;

            yield return null;
            yield return null;

            Assert.IsFalse(legacy.activeSelf, "Legacy stage root must stay hidden");
            Assert.IsTrue(hallEnv.activeSelf, "Editor/emulator dark-hall stage must remain visible");
            Assert.AreEqual(1.5f, ctrl.transform.position.x, 0.01f);
            Assert.AreEqual(2.2f, ctrl.transform.position.z, 0.01f);
        }

        [UnityTest]
        public IEnumerator Pair_F04AndF23_PalmMenuStateAndTabletopDirector_SwitchModesCleanly()
        {
            var director = _h.Track(new GameObject("MenuDirector")).AddComponent<TabletopDirector>();
            var proxyRoot = _h.Track(new GameObject("MenuProxyRoot"));
            new GameObject("Child").transform.SetParent(proxyRoot.transform, false);
            director.tabletopProxyRoot = proxyRoot.transform;

            var state = new PalmMenuState { Tabletop = director };
            state.SelectTab(PalmMenuTab.Scene);
            var toggleEntry = state.BuildEntries().Find(e => e.Kind == PalmMenuActionKind.ToggleMode);
            Assert.IsNotNull(toggleEntry);

            state.Execute(toggleEntry);
            yield return null;
            Assert.AreEqual(TabletopDirector.ScaleMode.TabletopMini, director.currentMode);

            state.SelectTab(PalmMenuTab.Scene);
            toggleEntry = state.BuildEntries().Find(e => e.Kind == PalmMenuActionKind.ToggleMode);
            state.Execute(toggleEntry);
            yield return null;
            Assert.AreEqual(TabletopDirector.ScaleMode.Immersive1to1, director.currentMode);
        }

        [UnityTest]
        public IEnumerator Pair_F23_HumanoidThreePointIkDriver_And_PerformanceClip_ComputePlanarSpeed()
        {
            var actorGo = _h.Track(new GameObject("IkActor", typeof(Animator), typeof(HumanoidThreePointIkDriver)));
            var ik = actorGo.GetComponent<HumanoidThreePointIkDriver>();
            yield return null;

            var clip = new ActorPerformanceClip();
            clip.AddFrame(0f, Vector3.zero, Quaternion.identity, Vector3.up * 1.6f, Quaternion.identity,
                Vector3.left * 0.3f, Quaternion.identity, Vector3.right * 0.3f, Quaternion.identity);
            clip.AddFrame(1f, Vector3.forward * 2f, Quaternion.identity, Vector3.up * 1.6f + Vector3.forward * 2f,
                Quaternion.identity, Vector3.left * 0.3f + Vector3.forward * 2f, Quaternion.identity,
                Vector3.right * 0.3f + Vector3.forward * 2f, Quaternion.identity);

            ik.ApplyFrame(clip.Evaluate(0.5f));
            yield return null;
            Assert.IsTrue(ik.ikActive);
            Assert.AreEqual(1.0f, ik.transform.position.z, 0.01f);

            ik.ApplyFrame(clip.Evaluate(1.0f));
            yield return null;
            Assert.Greater(ik.CurrentPlanarSpeed, 0.1f);
        }

        [UnityTest]
        public IEnumerator Pair_F12AndF22_RuntimeGltfImporter_HandlesMissingFileAndAddsNavObstacle()
        {
            string missing = Path.Combine(Application.temporaryCachePath, "missing_tripo_prop.glb");
            Assert.Throws<FileNotFoundException>(() => RuntimeGltfImporter.Import(missing, null, Vector3.zero));

            Exception asyncErr = null;
            RuntimeGltfImporter.ImportAsync(missing, null, Vector3.zero, _ => { }, ex => asyncErr = ex);
            yield return null;
            Assert.IsInstanceOf<FileNotFoundException>(asyncErr);

            var prop = _h.Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            RuntimeGltfImporter.EnsurePhysicsAndObstacle(prop);
            var obs = prop.GetComponent<NavMeshObstacle>();
            Assert.IsNotNull(obs);
            Assert.IsTrue(obs.carving);
        }

        [UnityTest]
        public IEnumerator Pair_F01_HandTrackingModeRouter_And_SpatialTrackingCoordinator_KeepControllersSafe()
        {
            var leftCtrl = _h.Track(new GameObject("LeftController"));
            var rightCtrl = _h.Track(new GameObject("RightController"));
            var leftHand = _h.Track(new GameObject("LeftHand"));
            var rightHand = _h.Track(new GameObject("RightHand"));

            var router = _h.Track(new GameObject("Router")).AddComponent<HandTrackingModeRouter>();
            router.leftController = leftCtrl;
            router.rightController = rightCtrl;
            router.leftHand = leftHand;
            router.rightHand = rightHand;

            var coord = _h.Track(new GameObject("Coord")).AddComponent<SpatialTrackingCoordinator>();
            yield return null;
            yield return null;

            Assert.IsTrue(leftCtrl.activeSelf);
            Assert.IsTrue(rightCtrl.activeSelf);
            Assert.IsFalse(coord.EnableEyeTracking);
            Assert.IsFalse(coord.EnableMotionTracker);
        }
    }
}