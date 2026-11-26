using System.Collections.Generic;
using NUnit.Framework;
using ShotField.Core;
using ShotField.Core.Community;
using ShotField.RuntimeUI;
using UnityEngine;

namespace ShotField.Tests.E2E
{
    [TestFixture]
    public class DualScaleStoryContractTests
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
        public void TabletopDirector_SwitchesModesIdempotentlyAndTogglesProxyVisibility()
        {
            var host = Track(new GameObject("TabletopDirectorHost"));
            var root1to1 = Track(new GameObject("SceneRoot1to1"));
            var proxyRoot = Track(new GameObject("TabletopProxyRoot"));
            var proxyChild = new GameObject("ProxyMesh");
            proxyChild.transform.SetParent(proxyRoot.transform, false);

            var observer = Track(new GameObject("ObserverCam"));
            observer.transform.SetPositionAndRotation(new Vector3(1f, 2f, -3f), Quaternion.Euler(10f, 20f, 0f));

            var director = host.AddComponent<TabletopDirector>();
            director.sceneRoot1to1 = root1to1.transform;
            director.tabletopProxyRoot = proxyRoot.transform;
            director.observerCamera = observer.transform;
            director.miniScale = 0.05f;

            director.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
            Assert.AreEqual(TabletopDirector.ScaleMode.TabletopMini, director.currentMode);
            Assert.IsTrue(proxyRoot.activeSelf);
            Assert.IsFalse(root1to1.activeSelf);

            Vector3 miniCamPos = observer.transform.position;
            director.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
            Assert.AreEqual(miniCamPos, observer.transform.position, "Repeat switch to same mode must be a no-op");

            director.SwitchMode(TabletopDirector.ScaleMode.Immersive1to1);
            Assert.AreEqual(TabletopDirector.ScaleMode.Immersive1to1, director.currentMode);
            Assert.IsFalse(proxyRoot.activeSelf);
            Assert.IsTrue(root1to1.activeSelf);
            Assert.Less(Vector3.Distance(new Vector3(1f, 2f, -3f), observer.transform.position), 0.001f);
        }

        [Test]
        public void TabletopDirector_NeverMovesXrHeadsetCamera()
        {
            var xrOrigin = Track(new GameObject("XR_Origin"));
            var headset = new GameObject("XR_Headset");
            headset.transform.SetParent(xrOrigin.transform, false);
            headset.AddComponent<Camera>();
            Vector3 initialPos = new Vector3(0.25f, 1.65f, -0.5f);
            Quaternion initialRot = Quaternion.Euler(5f, 45f, 0f);
            headset.transform.SetPositionAndRotation(initialPos, initialRot);

            var host = Track(new GameObject("TabletopHost"));
            var director = host.AddComponent<TabletopDirector>();
            director.observerCamera = headset.transform;

            director.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
            Assert.Less(Vector3.Distance(initialPos, headset.transform.position), 1e-5f);
            Assert.Less(Quaternion.Angle(initialRot, headset.transform.rotation), 1e-3f);

            director.SwitchMode(TabletopDirector.ScaleMode.Immersive1to1);
            Assert.Less(Vector3.Distance(initialPos, headset.transform.position), 1e-5f);
        }

        [Test]
        public void TabletopDirector_WorldAndTabletopCoordinates_RoundtripAt1To20Scale()
        {
            var host = Track(new GameObject("ScaleMathHost"));
            var director = host.AddComponent<TabletopDirector>();
            director.miniScale = 0.05f;
            director.TabletopAnchor.position = new Vector3(0f, 0.8f, 1.2f);
            director.TabletopAnchor.rotation = Quaternion.Euler(0f, 30f, 0f);

            var worldSample = new Vector3(2.4f, 1.0f, -1.6f);
            var tabletopPos = director.WorldToTabletop(worldSample);
            var roundtrip = director.TabletopToWorld(tabletopPos);

            Assert.Less(Vector3.Distance(worldSample, roundtrip), 0.001f);
        }

        [Test]
        public void WorkflowCoordinator_EnforcesSequentialOrderAndClampsAtExport()
        {
            var coordinator = new WorkflowCoordinator();
            var observed = new List<WorkflowStage>();
            coordinator.OnStageChanged += s => observed.Add(s);

            Assert.AreEqual(WorkflowStage.Import, coordinator.CurrentStage);
            Assert.IsTrue(coordinator.CanAdvance);
            Assert.IsFalse(coordinator.IsFinished);

            for (int i = 0; i < WorkflowCoordinator.StageCount - 1; i++)
            {
                coordinator.Advance();
            }

            Assert.AreEqual(WorkflowStage.Export, coordinator.CurrentStage);
            Assert.IsTrue(coordinator.IsFinished);
            Assert.IsFalse(coordinator.CanAdvance);
            Assert.AreEqual(5, observed.Count);

            var clamped = coordinator.Advance();
            Assert.AreEqual(WorkflowStage.Export, clamped);
            Assert.AreEqual(5, observed.Count);

            coordinator.Reset();
            Assert.AreEqual(WorkflowStage.Import, coordinator.CurrentStage);
        }

        [Test]
        public void PropLibrary_And_LocalCommunityRepository_ExposeValidAssets()
        {
            Assert.Greater(PropLibrary.Count, 0);
            Assert.GreaterOrEqual(PropLibrary.NonEmptyCategoryCount(), 3);
            Assert.Greater(PropLibrary.CountWithPrefab(), 0);
            Assert.IsNotNull(PropLibrary.Find("envelope"));

            var repo = new LocalCommunityAssetRepository();
            var listTask = repo.ListAsync(null);
            listTask.Wait();
            Assert.IsNotNull(listTask.Result);
            Assert.GreaterOrEqual(listTask.Result.Count, 1);
        }
    }
}
