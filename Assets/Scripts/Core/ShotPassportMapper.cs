using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 镜头护照 DTO 与领域模型 ShotData / SceneSnapshot 之间的双向映射工具。
    /// </summary>
    public static class ShotPassportMapper
    {
        public static string QualifyShotId(string shotId, string sceneId) =>
            shotId.StartsWith(sceneId + "-", StringComparison.Ordinal) ? shotId : sceneId + "-" + shotId;

        public static ShotPassportDto FromShot(ShotData shot, string sceneId, ConstraintSpec[] specs)
        {
            var state = shot.camera;
            var dto = new ShotPassportDto
            {
                shot_id = QualifyShotId(shot.shotId, sceneId),
                revision = shot.revision,
                camera = new ShotPassportDto.CameraFields
                {
                    pos = Pack(state.position), rot = Pack(state.rotation.eulerAngles),
                    fov = state.fieldOfView, height = state.heightAboveFloor
                },
                cameraExtras = new ShotPassportDto.CameraExtras
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
            MapObjects(dto);
            return dto;
        }

        public static void MapObjects(ShotPassportDto dto)
        {
            if (dto.snapshotExtras == null) return;
            foreach (var item in dto.snapshotExtras)
            {
                var marker = SemanticMarker.Find(item.actorId, FindObjectsInactive.Include);
                if (marker == null) throw new InvalidOperationException("找不到语义标记 " + item.actorId);
                switch (marker.kind)
                {
                    case SemanticMarker.Kind.ActorPrimary:
                    case SemanticMarker.Kind.ActorSecondary:
                        dto.actors.Add(new ShotPassportDto.ActorFields { id = item.actorId, pos = Pack(item.position), rot = item.rotation.eulerAngles.y });
                        break;
                    case SemanticMarker.Kind.KeyProp:
                    case SemanticMarker.Kind.Prop:
                        dto.props.Add(new ShotPassportDto.PropFields { id = item.actorId, pos = Pack(item.position), state = item.active ? "visible" : "hidden" });
                        break;
                }
            }
        }

        public static ShotData ToShot(ShotPassportDto dto)
        {
            if (dto.camera == null || dto.cameraExtras == null || dto.snapshotExtras == null ||
                dto.constraints == null || dto.revision < 1)
                throw new FormatException("护照缺少必填字段。");
            return new ShotData
            {
                shotId = dto.shot_id, revision = dto.revision, intent = dto.intent,
                updatedAt = dto.updated_at, updatedBy = dto.updated_by,
                approval = ReadApproval(dto.approval_status, dto.approvalLocked),
                recordedConstraints = CopySpecs(dto.constraints),
                recordedCheck = dto.check_summary?.Copy(),
                lockedRules = new List<string>(dto.lockedRules ?? Array.Empty<string>()),
                camera = new ShotCameraState
                {
                    position = Unpack(dto.camera.pos), rotation = Quaternion.Euler(Unpack(dto.camera.rot)),
                    fieldOfView = dto.camera.fov, heightAboveFloor = dto.camera.height,
                    nearClip = dto.cameraExtras.nearClip, farClip = dto.cameraExtras.farClip,
                    aspect = ShotCameraState.NormalizeAspect(dto.cameraExtras.aspect),
                    usesUnityCameraRotation = dto.cameraExtras.usesUnityCameraRotation,
                    shotSize = dto.cameraExtras.shotSize, angle = dto.cameraExtras.angle
                },
                snapshot = new SceneSnapshot { actors = dto.snapshotExtras }.Clone(),
                trajectory = dto.cameraTrajectory != null ? CameraTrajectory.FromDto(dto.cameraTrajectory) : null,
                take = dto.actorTake != null ? ActorTake.FromDto(dto.actorTake) : null
            };
        }

        public static ApprovalStatus ReadApproval(string approval_status, bool approvalLocked) => approval_status switch
        {
            "draft" => ApprovalStatus.Draft,
            "pending" => ApprovalStatus.Reviewed,
            "approved" => approvalLocked ? ApprovalStatus.Locked : ApprovalStatus.Approved,
            _ => throw new FormatException("无法加载审批状态 " + approval_status)
        };

        public static ConstraintSpec[] CopySpecs(ConstraintSpec[] specs) =>
            specs == null ? null : Array.ConvertAll(specs, spec => spec.Copy());

        public static ShotPassportDto.CheckSummary Summarize(ShotData shot)
        {
            if (string.IsNullOrEmpty(shot.checkedAt) && shot.recordedCheck != null)
                return shot.recordedCheck.Copy();
            var summary = new ShotPassportDto.CheckSummary { checked_at = shot.checkedAt ?? "" };
            foreach (var result in shot.lastCheck)
            {
                if (result.status == CheckStatus.Pass) summary.pass++;
                else if (result.status == CheckStatus.Violation) summary.violation++;
                else if (result.status == CheckStatus.Pending) summary.pending++;
            }
            return summary;
        }

        public static string NormalizeTime(string value)
        {
            var offset = TimeSpan.FromHours(8);
            if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var legacy))
                return new DateTimeOffset(legacy, offset).ToString("O");
            if (DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed.ToOffset(offset).ToString("O");
            if (string.IsNullOrEmpty(value)) return DateTimeOffset.UtcNow.ToOffset(offset).ToString("O");
            throw new FormatException("无法识别镜头更新时间 " + value);
        }

        public static float[] Pack(Vector3 v) => new[] { v.x, v.y, v.z };

        public static Vector3 Unpack(float[] v) => (v == null || v.Length != 3)
            ? throw new FormatException("机位坐标须有三个分量。")
            : new Vector3(v[0], v[1], v[2]);
    }
}
