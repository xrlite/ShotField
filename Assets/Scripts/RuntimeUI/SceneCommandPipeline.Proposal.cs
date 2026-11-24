using System;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public partial class SceneCommandPipeline
    {
        private bool TryMapItem(ProposalItemDto item, out PendingSceneCommand pending, out string error)
        {
            pending = null; error = "";
            if (item == null) { error = "提案条目为空"; return false; }
            string id = "cmd_" + Guid.NewGuid().ToString("N");
            switch (item.kind)
            {
                case ProposalItemKind.Prop:
                {
                    var payload = new SpawnCommandPayload
                    {
                        propId = item.propId, position = item.position, rotation = Quaternion.identity
                    };
                    pending = new PendingSceneCommand
                    {
                        command = new SceneCommandEnvelopeDto(id, SceneCommandType.Spawn, "spawn:" + item.propId,
                            JsonUtility.ToJson(payload)),
                        proposalItem = item, expectedPosition = item.position.ToUnityVector3()
                    };
                    return true;
                }
                case ProposalItemKind.Actor:
                {
                    if (orchestrator == null || !orchestrator.TryResolveActorStrict(item.actorId, out var actor))
                    { error = "演员未精确匹配: " + item.actorId; return false; }
                    var marker = actor.GetComponent<SemanticMarker>();
                    string targetId = marker != null && !string.IsNullOrEmpty(marker.markerId) ? marker.markerId : actor.name;
                    var payload = new MoveCommandPayload
                    {
                        actorId = targetId, targetPosition = item.position, desiredSpeed = 0f, baseOffset = 0f
                    };
                    pending = new PendingSceneCommand
                    {
                        command = new SceneCommandEnvelopeDto(id, SceneCommandType.Move, targetId,
                            JsonUtility.ToJson(payload)),
                        proposalItem = item, expectedObjectId = targetId,
                        expectedPosition = item.position.ToUnityVector3()
                    };
                    return true;
                }
                case ProposalItemKind.Shot:
                case ProposalItemKind.Camera:
                {
                    if (filmCamera == null) { error = "导演相机未绑定"; return false; }
                    if (!Enum.IsDefined(typeof(CameraPresetId), item.cameraPresetId))
                    { error = "机位预设越界: " + item.cameraPresetId; return false; }
                    orchestrator.TryResolveActorStrict(item.actorId, out var actor);
                    if (!orchestrator.TryResolveAnchor(actor, out Vector3 anchor))
                    { error = "机位锚点缺失"; return false; }
                    var state = CameraPreset.Build((CameraPresetId)item.cameraPresetId, anchor);
                    var payload = new ProposeShotPayload
                    {
                        shotId = item.itemId, cameraPosition = state.position, cameraRotation = state.rotation,
                        fieldOfView = state.fieldOfView, shotSize = state.shotSize.ToString(),
                        intentDescription = item.intent
                    };
                    pending = new PendingSceneCommand
                    {
                        command = new SceneCommandEnvelopeDto(id, SceneCommandType.ProposeShot,
                            SceneContextCapture.DirectorCameraId, JsonUtility.ToJson(payload)),
                        proposalItem = item, expectedObjectId = SceneContextCapture.DirectorCameraId,
                        expectedPosition = state.position, expectedRotation = state.rotation,
                        expectedFieldOfView = state.fieldOfView
                    };
                    return true;
                }
                default:
                    error = "不支持的提案类型: " + item.kind;
                    return false;
            }
        }
    }
}
