using System.Collections.Generic;
using NUnit.Framework;
using ShotField.Constraints;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEngine;

namespace ShotField.Tests.EditMode
{
    [TestFixture]
    public class Tier2_BoundaryContractTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown() => E2ETestHelpers.DestroyAllTracked(_created);

        [Test]
        public void LocalAiConfig_Mask_HandlesNullEmptyShortAndLongBoundaryInputs()
        {
            Assert.AreEqual("未配置", LocalAiConfig.Mask(null));
            Assert.AreEqual("未配置", LocalAiConfig.Mask(string.Empty));
            Assert.AreEqual("•", LocalAiConfig.Mask("a"));
            Assert.AreEqual("••••••", LocalAiConfig.Mask("123456"));
            Assert.AreEqual("123••••4567", LocalAiConfig.Mask("1234567"));

            Assert.IsFalse(LocalAiConfig.ShouldMigrateLegacy(false, false));
            Assert.IsFalse(LocalAiConfig.ShouldMigrateLegacy(false, true));
            Assert.IsTrue(LocalAiConfig.ShouldMigrateLegacy(true, false));
            Assert.IsFalse(LocalAiConfig.ShouldMigrateLegacy(true, true));
        }

        [Test]
        public void ShotCameraState_RejectsBehindCameraAndInvalidAspectRatios()
        {
            Assert.AreEqual(ShotCameraState.DefaultAspect, ShotCameraState.NormalizeAspect(0f), 1e-5f);
            Assert.AreEqual(ShotCameraState.DefaultAspect, ShotCameraState.NormalizeAspect(-2f), 1e-5f);
            Assert.AreEqual(ShotCameraState.DefaultAspect, ShotCameraState.NormalizeAspect(float.PositiveInfinity), 1e-5f);

            var cam = new ShotCameraState
            {
                position = Vector3.zero,
                rotation = Quaternion.identity,
                usesUnityCameraRotation = true,
                fieldOfView = 60f,
                nearClip = 0.1f,
                farClip = 20f
            };
            Assert.IsFalse(cam.TryProjectToViewport(new Vector3(0f, 0f, -1f), out _));
            Assert.IsFalse(cam.TryProjectToViewport(new Vector3(0f, 0f, 0.05f), out _));
            Assert.IsFalse(cam.TryProjectToViewport(new Vector3(0f, 0f, 25f), out _));
            Assert.IsTrue(cam.TryProjectToViewport(new Vector3(0f, 0f, 5f), out var vp));
            Assert.AreEqual(0.5f, vp.x, 1e-4f);
            Assert.AreEqual(0.5f, vp.y, 1e-4f);
        }

        [Test]
        public void TabletopDirector_ZeroMiniScale_FallsBackWithoutDivideByZero()
        {
            var go = new GameObject("ZeroScaleDirector");
            _created.Add(go);
            var director = go.AddComponent<TabletopDirector>();
            director.miniScale = 0f;
            Vector3 world = director.TabletopToWorld(director.TabletopAnchor.position + Vector3.right * 0.05f);
            Assert.IsFalse(float.IsNaN(world.x) || float.IsInfinity(world.x));
            Assert.AreEqual(1.0f, world.x, 0.001f);
        }

        [Test]
        public void TimelineChainModel_RejectsOutOfRangeIndicesAndClampsTime()
        {
            var chain = new TimelineChainModel();
            Assert.AreEqual(-1, chain.ClipAt(1.0f, out _));
            Assert.IsFalse(chain.RemoveAt(-1));
            Assert.IsFalse(chain.RemoveAt(0));

            chain.Add(new TimelineClipDto { clipId = "C1", durationSeconds = 0f });
            Assert.AreEqual(0.1f, chain.TotalDuration, 1e-4f);
            Assert.IsFalse(chain.Move(0, -1));
            Assert.IsFalse(chain.Move(0, 1));

            Assert.AreEqual(0, chain.ClipAt(-5f, out float localLow));
            Assert.AreEqual(0f, localLow, 1e-4f);
            Assert.AreEqual(0, chain.ClipAt(99f, out float localHigh));
            Assert.AreEqual(0.1f, localHigh, 1e-4f);
        }

        [Test]
        public void ActorPerformanceClip_HandlesEmptyJsonAndOutOfRangeEvaluation()
        {
            Assert.IsNull(ActorPerformanceClip.FromJson(null));
            Assert.IsNull(ActorPerformanceClip.FromJson(string.Empty));

            var empty = new ActorPerformanceClip();
            var f = empty.Evaluate(1.5f);
            Assert.AreEqual(Vector3.zero, f.rootPosition);

            empty.AddFrame(0.5f, Vector3.right, Quaternion.identity, Vector3.up, Quaternion.identity,
                Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(Vector3.right, empty.Evaluate(-1f).rootPosition);
            Assert.AreEqual(Vector3.right, empty.Evaluate(99f).rootPosition);
        }

        [Test]
        public void Constraints_ReturnPendingOnNullShotOrCoincidentActors_AndViolationOutOfFrame()
        {
            var go = new GameObject("Constraints_Boundary");
            _created.Add(go);
            var axis = go.AddComponent<AxisConstraint>();
            axis.actorAId = "A";
            axis.actorBId = "B";
            Assert.AreEqual(CheckStatus.Pending, axis.Evaluate(null, null).status);

            var coincident = E2ETestHelpers.CreateShot("S1", new Vector3(0, 1, -2), Vector3.zero, "A", Vector3.zero);
            coincident.snapshot.actors.Add(new ActorState { actorId = "B", position = Vector3.zero });
            Assert.AreEqual(CheckStatus.Pending, axis.Evaluate(coincident, null).status);

            var framing = go.AddComponent<FramingConstraint>();
            framing.subjectId = "A";
            framing.minViewportX = 0.6f;
            framing.maxViewportX = 0.9f;
            var centeredShot = E2ETestHelpers.CreateShot("S2", new Vector3(0, 1, -3), Vector3.zero, "A", Vector3.zero);
            Assert.AreEqual(CheckStatus.Violation, framing.Evaluate(centeredShot, null).status);
        }

        [Test]
        public void WorkflowCoordinator_ClampsAtExportAndResetsIdempotently()
        {
            var wf = new WorkflowCoordinator();
            int events = 0;
            wf.OnStageChanged += _ => events++;

            wf.Reset();
            Assert.AreEqual(0, events, "Resetting while already at Import must not emit duplicate event");

            for (int i = 0; i < 10; i++) wf.Advance();
            Assert.AreEqual(WorkflowStage.Export, wf.CurrentStage);
            Assert.AreEqual(WorkflowCoordinator.StageCount - 1, events);
        }
    }
}