#if UNITY_EDITOR
using System;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class ArchitectureRefactorVerifier
    {
        private const int Expected = 4;

        [MenuItem("ShotField/Verifiers/Run Architecture Refactor Verifier")]
        public static void Run()
        {
            int passed = 0;

            var traj = new CameraTrajectory();
            traj.AddPoint(0f, Vector3.zero, Quaternion.identity, 40f);
            traj.AddPoint(2f, new Vector3(2f, 0f, 0f), Quaternion.identity, 60f);
            var midPt = traj.Evaluate(1f);
            VerifyAssert.Require("KeyframeTrack drives CameraTrajectory midpoint accurately",
                Mathf.Abs(midPt.position.x - 1f) < 0.01f && Mathf.Abs(midPt.fieldOfView - 50f) < 0.01f, ref passed);

            var take = new ActorTake();
            take.AddKeyframe(0f, Vector3.zero, Quaternion.identity, "Idle");
            take.AddKeyframe(4f, new Vector3(0f, 0f, 4f), Quaternion.identity, "Reach");
            var midTake = take.Evaluate(3f);
            VerifyAssert.Require("KeyframeTrack drives ActorTake interpolation and actionTag",
                Mathf.Abs(midTake.position.z - 3f) < 0.01f && midTake.actionTag == "Reach", ref passed);

            var session = new NetworkSession();
            session.SendHeartbeat();
            VerifyAssert.Require("NetworkSession.SendHeartbeat safe when disconnected",
                session.LastHeartbeatUnixMs == 0 && session.CurrentShotIndex == 0, ref passed);

            VerifyAssert.Require("KeyframeTrack handles empty list safely",
                EvalEmpty(), ref passed);

            VerifyAssert.Finish("ARCH-REFACTOR-RESULT", passed, Expected);
        }

        private static bool EvalEmpty()
        {
            KeyframeTrack.FindSegment<int>(Array.Empty<int>(), x => x, 1f, out int a, out int b, out float alpha);
            return a == -1 && b == -1 && alpha == 0f;
        }
    }
}
#endif
