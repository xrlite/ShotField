using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    public enum CheckStatus { Pass = 0, Violation = 1, Pending = 2 }
    public enum ShotSize { Auto = 0, Wide = 1, Full = 2, Medium = 3, CloseUp = 4, Detail = 5 }
    public enum ShotAngle { Auto = 0, EyeLevel = 1, Low = 2, High = 3, Dutch = 4 }
    public enum ApprovalStatus { Draft = 0, Reviewed = 1, Approved = 2, Locked = 3 }

    [Serializable]
    public class ActorState
    {
        public string actorId;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public bool active = true;
    }

    [Serializable]
    public class SceneSnapshot
    {
        public List<ActorState> actors = new List<ActorState>();

        public SceneSnapshot Clone()
        {
            var copy = new SceneSnapshot();
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                copy.actors.Add(new ActorState { actorId = a.actorId, position = a.position, rotation = a.rotation, scale = a.scale, active = a.active });
            }
            return copy;
        }

        public ActorState Find(string actorId)
        {
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].actorId == actorId) return actors[i];
            return null;
        }
    }

    [Serializable]
    public class ShotCameraState
    {
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public const float DefaultAspect = 16f / 9f;
        public const float PortraitAspect = 9f / 16f;

        public float fieldOfView = 45f;
        public float aspect = DefaultAspect;
        public bool usesUnityCameraRotation;

        public Quaternion CameraRotation => usesUnityCameraRotation
            ? rotation : rotation * Quaternion.Euler(0f, 180f, 0f);
        public float nearClip = 0.05f;
        public float farClip = 100f;
        public ShotSize shotSize = ShotSize.Auto;
        public ShotAngle angle = ShotAngle.Auto;
        public float heightAboveFloor;

        public static ShotCameraState FromCamera(Camera cam)
        {
            var s = new ShotCameraState();
            if (cam == null) return s;
            var t = cam.transform;
            s.position = t.position;
            s.rotation = t.rotation;
            s.usesUnityCameraRotation = true;
            s.fieldOfView = cam.fieldOfView;
            s.aspect = NormalizeAspect(cam.aspect);
            s.nearClip = cam.nearClipPlane;
            s.farClip = cam.farClipPlane;
            s.heightAboveFloor = t.position.y;
            return s;
        }

        public void ApplyTo(Camera cam)
        {
            if (cam == null) return;
            cam.transform.position = position;
            cam.transform.rotation = CameraRotation;
            cam.fieldOfView = fieldOfView;
            cam.aspect = NormalizeAspect(aspect);
            cam.nearClipPlane = nearClip;
            cam.farClipPlane = farClip;
        }

        public Matrix4x4 ViewMatrix => Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
            Matrix4x4.TRS(position, CameraRotation, Vector3.one).inverse;

        public Matrix4x4 ProjectionMatrix() =>
            Matrix4x4.Perspective(fieldOfView, NormalizeAspect(aspect), nearClip, farClip);

        public ShotCameraState Clone() => (ShotCameraState)MemberwiseClone();

        public bool TryProjectToViewport(Vector3 worldPoint, out Vector2 viewport)
        {
            viewport = default;
            var local = Quaternion.Inverse(CameraRotation) * (worldPoint - position);
            if (local.z <= nearClip || local.z > farClip) return false;
            float halfHeight = local.z * Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f);
            if (halfHeight <= 0f) return false;
            viewport = new Vector2(
                0.5f + local.x / (2f * halfHeight * NormalizeAspect(aspect)),
                0.5f + local.y / (2f * halfHeight));
            return true;
        }

        public static float NormalizeAspect(float value) =>
            value > 0f && !float.IsInfinity(value) ? value : DefaultAspect;
    }

    [Serializable]
    public class ConstraintResult
    {
        public string ruleName = "";
        public CheckStatus status = CheckStatus.Pending;
        public string detail = "";
        public string targetId = "";

        public static ConstraintResult Pass(string r, string d, string t) =>
            new ConstraintResult { ruleName = r, status = CheckStatus.Pass, detail = d, targetId = t };
        public static ConstraintResult Violation(string r, string d, string t) =>
            new ConstraintResult { ruleName = r, status = CheckStatus.Violation, detail = d, targetId = t };
        public static ConstraintResult Pending(string r, string d, string t) =>
            new ConstraintResult { ruleName = r, status = CheckStatus.Pending, detail = d, targetId = t };
    }

    [Serializable]
    public class ShotData
    {
        public string shotId = "";
        public int revision = 1;
        public ShotCameraState camera = new ShotCameraState();
        public SceneSnapshot snapshot = new SceneSnapshot();

        [TextArea(2, 5)]
        public string intent = "";
        public CameraTrajectory trajectory;
        public ActorTake take;
        public List<string> lockedRules = new List<string>();
        public ApprovalStatus approval = ApprovalStatus.Draft;
        public string updatedAt = "";
        public string updatedBy = "";

        public ConstraintSpec[] recordedConstraints;
        public ShotPassportDto.CheckSummary recordedCheck;
        [NonSerialized] public string checkedAt;
        [NonSerialized] public List<ConstraintResult> lastCheck = new List<ConstraintResult>();

        public ShotData Clone()
        {
            var c = (ShotData)MemberwiseClone();
            c.camera = camera.Clone();
            c.snapshot = snapshot.Clone();
            c.trajectory = trajectory?.Clone();
            c.take = take?.Clone();
            c.lockedRules = new List<string>(lockedRules);
            c.lastCheck = new List<ConstraintResult>();
            c.checkedAt = null;
            c.recordedConstraints = ShotPassportDto.CopySpecs(recordedConstraints);
            c.recordedCheck = recordedCheck?.Copy();
            return c;
        }
    }
}
