using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ShotField.Core
{
    [Serializable]
    public class ConstraintSpec
    {
        public string type, ruleName, propId, face, actorA, actorB, targetId;
        public bool blocking, waivable;
        public float minX, maxX, epsilon;
        public int occluderMask;
        public ConstraintSpec Copy() => (ConstraintSpec)MemberwiseClone();
    }

    [Serializable]
    public class ShotPassportDto
    {
        [Serializable]
        public class CameraFields { public float[] pos, rot; public float fov, height; }

        [Serializable]
        public class CameraExtras
        {
            public float nearClip, farClip, aspect;
            public bool usesUnityCameraRotation;
            public ShotSize shotSize;
            public ShotAngle angle;
        }

        [Serializable]
        public class ActorFields
        {
            public string id, pose = "";
            public float[] pos;
            public float rot;
        }

        [Serializable]
        public class PropFields { public string id, state; public float[] pos; }

        [Serializable]
        public class CheckSummary
        {
            public int pass, violation, pending;
            public string checked_at = "";
            public CheckSummary Copy() => (CheckSummary)MemberwiseClone();
        }

        public string shot_id, approval_status, updated_at, updated_by;
        public int revision;
        public CameraFields camera;
        public CameraExtras cameraExtras;
        public List<ActorFields> actors = new List<ActorFields>();
        public List<PropFields> props = new List<PropFields>();
        public ConstraintSpec[] constraints;
        public CheckSummary check_summary;
        public string intent;
        public string[] lockedRules;
        public List<ActorState> snapshotExtras;
        public bool approvalLocked;
        public TrajectoryDto cameraTrajectory;
        public ActorTakeDto actorTake;

        public static string QualifyShotId(string shotId, string sceneId) =>
            shotId.StartsWith(sceneId + "-", StringComparison.Ordinal) ? shotId : sceneId + "-" + shotId;

        public static ShotPassportDto FromShot(ShotData shot, string sceneId, ConstraintSpec[] specs)
        {
            var state = shot.camera;
            var dto = new ShotPassportDto
            {
                shot_id = QualifyShotId(shot.shotId, sceneId),
                revision = shot.revision,
                camera = new CameraFields
                {
                    pos = Pack(state.position), rot = Pack(state.rotation.eulerAngles),
                    fov = state.fieldOfView, height = state.heightAboveFloor
                },
                cameraExtras = new CameraExtras
                {
                    nearClip = state.nearClip, farClip = state.farClip,
                    aspect = ShotCameraState.NormalizeAspect(state.aspect),
                    usesUnityCameraRotation = state.usesUnityCameraRotation,
                    shotSize = state.shotSize, angle = state.angle
                },
                constraints = CopySpecs(shot.recordedConstraints ?? specs),
                check_summary = Summarize(shot),
                approval_status = shot.approval == ApprovalStatus.Draft ? "draft"
                    : shot.approval == ApprovalStatus.Reviewed ? "pending" : "approved",
                approvalLocked = shot.approval == ApprovalStatus.Locked,
                updated_at = NormalizeTime(shot.updatedAt),
                updated_by = shot.updatedBy,
                intent = shot.intent,
                cameraTrajectory = shot.trajectory?.ToDto(),
                actorTake = shot.take?.ToDto(),
                lockedRules = shot.lockedRules.ToArray(),
                snapshotExtras = shot.snapshot.Clone().actors
            };
            dto.MapObjects();
            return dto;
        }

        private void MapObjects()
        {
            foreach (var item in snapshotExtras)
            {
                var marker = SemanticMarker.Find(item.actorId, FindObjectsInactive.Include);
                if (marker == null) throw new InvalidOperationException("找不到语义标记 " + item.actorId);
                switch (marker.kind)
                {
                    case SemanticMarker.Kind.ActorPrimary:
                    case SemanticMarker.Kind.ActorSecondary:
                        actors.Add(new ActorFields { id = item.actorId, pos = Pack(item.position), rot = item.rotation.eulerAngles.y });
                        break;
                    case SemanticMarker.Kind.KeyProp:
                    case SemanticMarker.Kind.Prop:
                        props.Add(new PropFields { id = item.actorId, pos = Pack(item.position), state = item.active ? "visible" : "hidden" });
                        break;
                }
            }
        }

        public ShotData ToShot()
        {
            if (camera == null || cameraExtras == null || snapshotExtras == null ||
                constraints == null || revision < 1)
                throw new FormatException("护照缺少必填字段。");
            return new ShotData
            {
                shotId = shot_id, revision = revision, intent = intent,
                updatedAt = updated_at, updatedBy = updated_by,
                approval = ReadApproval(),
                recordedConstraints = CopySpecs(constraints),
                recordedCheck = check_summary?.Copy(),
                lockedRules = new List<string>(lockedRules ?? Array.Empty<string>()),
                camera = new ShotCameraState
                {
                    position = Unpack(camera.pos), rotation = Quaternion.Euler(Unpack(camera.rot)),
                    fieldOfView = camera.fov, heightAboveFloor = camera.height,
                    nearClip = cameraExtras.nearClip, farClip = cameraExtras.farClip,
                    aspect = ShotCameraState.NormalizeAspect(cameraExtras.aspect),
                    usesUnityCameraRotation = cameraExtras.usesUnityCameraRotation,
                    shotSize = cameraExtras.shotSize, angle = cameraExtras.angle
                },
                snapshot = new SceneSnapshot { actors = snapshotExtras }.Clone(),
                trajectory = cameraTrajectory != null ? CameraTrajectory.FromDto(cameraTrajectory) : null,
                take = actorTake != null ? ActorTake.FromDto(actorTake) : null
            };
        }

        private ApprovalStatus ReadApproval() => approval_status switch
        {
            "draft" => ApprovalStatus.Draft,
            "pending" => ApprovalStatus.Reviewed,
            "approved" => approvalLocked ? ApprovalStatus.Locked : ApprovalStatus.Approved,
            _ => throw new FormatException("无法加载审批状态 " + approval_status)
        };

        public static ConstraintSpec[] CopySpecs(ConstraintSpec[] specs) =>
            specs == null ? null : Array.ConvertAll(specs, spec => spec.Copy());

        private static CheckSummary Summarize(ShotData shot)
        {
            if (string.IsNullOrEmpty(shot.checkedAt) && shot.recordedCheck != null)
                return shot.recordedCheck.Copy();
            var summary = new CheckSummary { checked_at = shot.checkedAt ?? "" };
            foreach (var result in shot.lastCheck)
            {
                if (result.status == CheckStatus.Pass) summary.pass++;
                else if (result.status == CheckStatus.Violation) summary.violation++;
                else if (result.status == CheckStatus.Pending) summary.pending++;
            }
            return summary;
        }

        private static string NormalizeTime(string value)
        {
            var offset = TimeSpan.FromHours(8);
            if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var legacy))
                return new DateTimeOffset(legacy, offset).ToString("O");
            if (DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed.ToOffset(offset).ToString("O");
            if (string.IsNullOrEmpty(value)) return DateTimeOffset.UtcNow.ToOffset(offset).ToString("O");
            throw new FormatException("无法识别镜头更新时间 " + value);
        }

        private static float[] Pack(Vector3 v) => new[] { v.x, v.y, v.z };

        private static Vector3 Unpack(float[] v) => (v == null || v.Length != 3)
            ? throw new FormatException("机位坐标须有三个分量。")
            : new Vector3(v[0], v[1], v[2]);
    }
}
