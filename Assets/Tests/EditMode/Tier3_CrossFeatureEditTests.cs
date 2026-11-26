using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShotField.Constraints;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.Tests.EditMode
{
    [TestFixture]
    public class Tier3_CrossFeatureEditTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown() => E2ETestHelpers.DestroyAllTracked(_created);

        [Test]
        public void Pair_F03AndF04_RayInteractorWithDualUiPressPairsWithWorldCanvasButton()
        {
            var mgrGo = new GameObject("Mgr");
            _created.Add(mgrGo);
            var mgr = mgrGo.AddComponent<XRInteractionManager>();

            var rayGo = new GameObject("HandRay");
            _created.Add(rayGo);
            var ray = rayGo.AddComponent<XRRayInteractor>();
            ray.interactionManager = mgr;
            ray.uiPressInput = new XRInputButtonReader("UI Press", inputSourceMode: XRInputButtonReader.InputSourceMode.InputAction);

            var canvasGo = new GameObject("WorldPanel", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            _created.Add(canvasGo);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var btn = RuntimeUiFactory.CreateTmpButton(canvasGo.transform, "Btn", "确认", Vector2.zero);

            Assert.IsNull(btn.GetComponent<Collider>());
            Assert.IsNull(btn.GetComponent<XRSimpleInteractable>());
            Assert.IsNotNull(canvasGo.GetComponent<TrackedDeviceGraphicRaycaster>());
            Assert.IsNull(canvasGo.GetComponent<GraphicRaycaster>());
        }

        [Test]
        public void Pair_F12AndF33_TripoEnvVarOverridesWithoutLeakingMaskOrResources()
        {
            const string EnvKey = "SHOTFIELD_TRIPO_KEY";
            string prev = Environment.GetEnvironmentVariable(EnvKey);
            try
            {
                string dummyKey = "tsk_" + "test_ephemeral_987654";
                Environment.SetEnvironmentVariable(EnvKey, dummyKey);
                var cfg = TripoConfig.Load();
                Assert.IsTrue(cfg.IsConfigured);
                Assert.AreEqual(dummyKey, cfg.ApiKey);
                StringAssert.DoesNotContain("ephemeral", LocalAiConfig.Mask(cfg.ApiKey));
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvKey, prev);
            }
        }

        [Test]
        public void Pair_F12AndF22_RuntimeGltfImporter_BoundsAndNavMeshObstacleCarving()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(cube);
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.localScale = new Vector3(1.2f, 0.8f, 0.6f);

            Bounds bounds = RuntimeGltfImporter.GetLocalBounds(cube);
            Assert.Greater(bounds.size.x, 0.9f);
            RuntimeGltfImporter.EnsurePhysicsAndObstacle(cube);

            var box = cube.GetComponent<BoxCollider>();
            var obstacle = cube.GetComponent<NavMeshObstacle>();
            Assert.IsNotNull(box);
            Assert.IsNotNull(obstacle);
            Assert.IsTrue(obstacle.carving);
        }

        [Test]
        public void Pair_F14AndF04_SpatialGlassWithTmpButtonAndWorldCanvas()
        {
            var canvasGo = new GameObject("GlassCanvas", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            _created.Add(canvasGo);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var btn = RuntimeUiFactory.CreateTmpButton(canvasGo.transform, "GlassBtn", "进入 1:1 片场", Vector2.zero, new Vector2(240f, 48f));
            var mat = SpatialGlass.Apply(btn.GetComponent<Image>(), new Color(0.12f, 0.13f, 0.14f, 0.94f), 20f);
            if (mat != null)
            {
                _created.Add(mat);
                Assert.AreEqual(20f, mat.GetFloat("_CornerRadius"), 0.01f);
                Assert.IsNotNull(btn.GetComponent<SpatialGlassBinder>());
            }
            Assert.IsNotNull(btn.GetComponentInChildren<TMPro.TMP_Text>());
        }

        [Test]
        public void Pair_F22AndF23_AxisConstraintAndProposalTimelineChainConsistency()
        {
            var proposal = new ChainProposalDto();
            proposal.items.Add(new ProposalItemDto
            {
                itemId = "SH_1", kind = ProposalItemKind.Shot, durationSeconds = 5f,
                position = Vector3Dto.FromUnityVector3(new Vector3(0, 1.5f, -3f)), decision = ProposalDecision.Accepted
            });
            proposal.items.Add(new ProposalItemDto
            {
                itemId = "ACT_1", kind = ProposalItemKind.Actor, actorId = "actorA",
                position = Vector3Dto.FromUnityVector3(Vector3.zero), decision = ProposalDecision.Accepted
            });
            proposal.items.Add(new ProposalItemDto
            {
                itemId = "SH_REJ", kind = ProposalItemKind.Shot, durationSeconds = 9f, decision = ProposalDecision.Rejected
            });

            var chain = TimelineChainModel.FromProposal(proposal);
            Assert.AreEqual(1, chain.Count);
            Assert.AreEqual(5f, chain.TotalDuration, 0.001f);
            Assert.IsTrue(chain.clips[0].TryGetProbe(TimelineTrackLayer.Actor, out var probe));
            Assert.AreEqual("actorA", probe.targetId);

            var host = new GameObject("AxisPairHost");
            _created.Add(host);
            var axis = host.AddComponent<AxisConstraint>();
            axis.actorAId = "A";
            axis.actorBId = "B";

            var s1 = E2ETestHelpers.CreateShot("S1", new Vector3(0, 1.5f, -3f), Vector3.zero, "A", new Vector3(-1, 0, 0));
            s1.snapshot.actors.Add(new ActorState { actorId = "B", position = new Vector3(1, 0, 0) });
            var s2 = E2ETestHelpers.CreateShot("S2", new Vector3(0, 1.5f, 3f), Vector3.zero, "A", new Vector3(-1, 0, 0));
            s2.snapshot.actors.Add(new ActorState { actorId = "B", position = new Vector3(1, 0, 0) });

            Assert.AreEqual(CheckStatus.Pass, axis.Evaluate(s1, null).status);
            Assert.AreEqual(CheckStatus.Violation, axis.Evaluate(s2, s1).status);
        }
    }
}