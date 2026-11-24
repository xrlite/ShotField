using System;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public partial class SceneCommandPipeline
    {
        public static CommandValidationResultDto ValidateSchema(SceneCommandEnvelopeDto command)
        {
            if (command == null) return Invalid("", 1, "NULL_COMMAND", "命令为空");
            string id = command.commandId ?? "";
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(command.targetId))
                return Invalid(id, 1, "SCHEMA", "commandId/targetId 为空");
            if (!Enum.IsDefined(typeof(SceneCommandType), command.commandType))
                return Invalid(id, 1, "UNKNOWN_TYPE", "命令类型不在白名单");
            var type = (SceneCommandType)command.commandType;
            if (type != SceneCommandType.Spawn && type != SceneCommandType.Move && type != SceneCommandType.ProposeShot)
                return Invalid(id, 1, "UNSUPPORTED_TYPE", "V1 执行器不支持该白名单类型");
            try
            {
                switch (type)
                {
                    case SceneCommandType.Spawn:
                    {
                        var p = JsonUtility.FromJson<SpawnCommandPayload>(command.payloadJson);
                        if (p == null || string.IsNullOrEmpty(p.propId) || !IsFinite(p.position) ||
                            !IsFinite(p.rotation) || !IsFinite(p.scale) || p.scale.x <= 0 || p.scale.y <= 0 || p.scale.z <= 0)
                            return Invalid(id, 1, "SPAWN_SCHEMA", "Spawn 参数缺失或非有限值");
                        break;
                    }
                    case SceneCommandType.Move:
                    {
                        var p = JsonUtility.FromJson<MoveCommandPayload>(command.payloadJson);
                        if (p == null || string.IsNullOrEmpty(p.actorId) || p.actorId != command.targetId ||
                            !IsFinite(p.targetPosition) || Mathf.Abs(p.baseOffset) > 0.0001f || Mathf.Abs(p.desiredSpeed) > 0.0001f)
                            return Invalid(id, 1, "MOVE_SCHEMA", "Move 目标不匹配、baseOffset 非零或速度非即时");
                        break;
                    }
                    case SceneCommandType.ProposeShot:
                    {
                        var p = JsonUtility.FromJson<ProposeShotPayload>(command.payloadJson);
                        if (p == null || string.IsNullOrEmpty(p.shotId) || !IsFinite(p.cameraPosition) ||
                            !IsFinite(p.cameraRotation) || p.fieldOfView <= 10f || p.fieldOfView >= 120f)
                            return Invalid(id, 1, "SHOT_SCHEMA", "ProposeShot 参数缺失或视场角越界");
                        break;
                    }
                }
            }
            catch (Exception ex) { return Invalid(id, 1, "JSON", ex.Message); }
            return CommandValidationResultDto.CreateValid(id);
        }

        public CommandValidationResultDto ValidateBoundary(SceneCommandEnvelopeDto command, SceneContextDto context)
        {
            string id = command != null ? command.commandId : "";
            if (context == null || string.IsNullOrEmpty(context.sceneId))
                return Invalid(id, 2, "NO_CONTEXT", "缺少执行前场景上下文");
            var type = (SceneCommandType)command.commandType;
            if (type == SceneCommandType.Spawn)
            {
                var p = JsonUtility.FromJson<SpawnCommandPayload>(command.payloadJson);
                var entry = PropLibrary.Find(p.propId);
                if (entry == null || entry.isProcedural || browser == null || browser.FindPrefab(p.propId) == null)
                    return Invalid(id, 2, "ASSET_NOT_BOUND", "道具不在真实资产库或未绑定 prefab");
                if (!IsInsideRoom(p.position.ToUnityVector3(), context))
                    return Invalid(id, 2, "OUT_OF_BOUNDS", "道具落点越出真实场景边界");
            }
            else if (type == SceneCommandType.Move)
            {
                var p = JsonUtility.FromJson<MoveCommandPayload>(command.payloadJson);
                var obj = SceneContextCapture.FindObject(context, p.actorId);
                if (obj == null || !obj.isActive || orchestrator == null ||
                    !orchestrator.TryResolveActorStrict(p.actorId, out var actor))
                    return Invalid(id, 2, "ACTOR_MISSING", "目标演员不存在或未精确匹配");
                if (!IsInsideRoom(p.targetPosition.ToUnityVector3(), context) ||
                    !actor.CanStandAt(p.targetPosition.ToUnityVector3(), 0.6f, out _))
                    return Invalid(id, 2, "UNWALKABLE", "目标点不可行走或越界");
            }
            else if (type == SceneCommandType.ProposeShot)
            {
                var p = JsonUtility.FromJson<ProposeShotPayload>(command.payloadJson);
                if (filmCamera == null || SceneContextCapture.FindObject(context, SceneContextCapture.DirectorCameraId) == null)
                    return Invalid(id, 2, "CAMERA_MISSING", "导演相机未进入上下文");
                var state = new ShotCameraState
                {
                    position = p.cameraPosition, rotation = p.cameraRotation,
                    fieldOfView = p.fieldOfView, usesUnityCameraRotation = true
                };
                if (!CameraPreset.IsPlausible(state) || !IsInsideRoom(p.cameraPosition.ToUnityVector3(), context))
                    return Invalid(id, 2, "CAMERA_BOUNDS", "机位越界或高度不合理");
            }
            return CommandValidationResultDto.CreateValid(id);
        }

        private static bool IsInsideRoom(Vector3 position, SceneContextDto context)
        {
            if (context.walkableBounds == null || !context.walkableBounds.isSampled ||
                context.walkableBounds.walkableBounds == null) return false;
            var bounds = context.walkableBounds.walkableBounds.ToUnityBounds();
            bounds.Expand(0.05f);
            return position.x >= bounds.min.x && position.x <= bounds.max.x &&
                position.z >= bounds.min.z && position.z <= bounds.max.z &&
                position.y >= -0.05f && position.y <= 3f;
        }

        private static bool IsFinite(Vector3Dto value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(QuaternionDto value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w) &&
            Mathf.Abs(value.x) + Mathf.Abs(value.y) + Mathf.Abs(value.z) + Mathf.Abs(value.w) > 0.1f;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static CommandValidationResultDto Invalid(string id, int gate, string code, string reason) =>
            CommandValidationResultDto.CreateInvalid(id, gate, code, reason);
    }
}
