using System;
using ShotField.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShotField.RuntimeUI
{
    public partial class SceneCommandPipeline
    {
        private void ExecuteCurrent()
        {
            if (_current == null) return;
            GameObject spawned = null;
            bool ok;
            string log;
            var type = (SceneCommandType)_current.command.commandType;
            try
            {
                switch (type)
                {
                    case SceneCommandType.Spawn:
                    {
                        var p = JsonUtility.FromJson<SpawnCommandPayload>(_current.command.payloadJson);
                        if (!browser.TrySpawnProp(p.propId, p.position, out spawned, out string callbackError))
                            throw new InvalidOperationException(callbackError);
                        LastExecutedObject = spawned;
                        spawned.transform.rotation = p.rotation;
                        spawned.transform.localScale = p.scale;
                        if (Vector3.Distance(spawned.transform.localScale, p.scale) > 0.001f)
                            throw new InvalidOperationException("Spawn 缩放回读失败");
                        var marker = spawned.GetComponent<SemanticMarker>();
                        _current.expectedObjectId = marker != null ? marker.markerId : spawned.name;
                        _current.expectedRotation = p.rotation;
                        _report.placedPropIds.Add(p.propId);
                        log = "Spawn " + p.propId;
                        if (callbackError != "") log += " | callback: " + callbackError;
                        ok = true;
                        break;
                    }
                    case SceneCommandType.Move:
                    {
                        var p = JsonUtility.FromJson<MoveCommandPayload>(_current.command.payloadJson);
                        if (!orchestrator.TryResolveActorStrict(p.actorId, out var actor) ||
                            !actor.CanStandAt(p.targetPosition, 0.6f, out var snapped))
                            throw new InvalidOperationException("演员落位失败");
                        if (actor.agent != null && actor.agent.isOnNavMesh && !actor.agent.Warp(snapped))
                            throw new InvalidOperationException("NavMesh Warp 失败");
                        actor.transform.position = snapped;
                        actor.Stop();
                        Physics.SyncTransforms();
                        _current.expectedPosition = snapped;
                        _current.expectedRotation = actor.transform.rotation;
                        LastExecutedObject = actor.gameObject;
                        _report.placedActorIds.Add(actor.name);
                        log = "Move " + actor.name;
                        ok = true;
                        break;
                    }
                    case SceneCommandType.ProposeShot:
                    {
                        var p = JsonUtility.FromJson<ProposeShotPayload>(_current.command.payloadJson);
                        filmCamera.transform.SetPositionAndRotation(p.cameraPosition, p.cameraRotation);
                        filmCamera.fieldOfView = p.fieldOfView;
                        filmCamera.aspect = ShotCameraState.DefaultAspect;
                        _current.expectedFieldOfView = p.fieldOfView;
                        LastExecutedObject = filmCamera.gameObject;
                        _report.cameraApplied = true;
                        log = "ProposeShot " + p.shotId;
                        ok = true;
                        break;
                    }
                    default:
                        throw new InvalidOperationException("未支持的执行类型");
                }
            }
            catch (Exception ex)
            {
                ok = false; log = ex.Message;
                Rollback(spawned);
                LastExecutedObject = null;
                _report.placementFailures.Add("执行失败: " + _current.command.commandId + " " + ex.Message);
            }

            if (ok)
            {
                Physics.SyncTransforms();
                var post = SceneContextCapture.Capture(filmCamera);
                var actual = SceneContextCapture.FindObject(post, _current.expectedObjectId);
                ok = actual != null && actual.isActive &&
                    Vector3.Distance(actual.position, _current.expectedPosition) <= 0.02f &&
                    Quaternion.Angle(actual.rotation, _current.expectedRotation) <= 1f &&
                    (type != SceneCommandType.ProposeShot ||
                        Mathf.Abs(filmCamera.fieldOfView - _current.expectedFieldOfView) <= 0.01f);
                log += ok ? " | readback=PASS" : " | readback=FAIL";
                if (!ok)
                {
                    Rollback(spawned);
                    _report.readbackFailedCount++;
                    _report.placementFailures.Add("Gate4 回读失败: " + _current.command.commandId);
                }
            }
            if (ok) _report.executedCount++;
            LastExecutionResult = new CommandExecutionResultDto(_current.command.commandId, ok, log);
            DestroyGhost(); _current = null;
            if (previewPanel != null) previewPanel.Hide();
            ShowNext();
        }

        private void Rollback(GameObject spawned)
        {
            var type = (SceneCommandType)_current.command.commandType;
            if (type == SceneCommandType.Spawn && spawned != null)
            {
                if (browser != null) browser.RemoveSpawnedProp(spawned);
                DisposeObject(spawned);
                return;
            }
            var before = SceneContextCapture.FindObject(_current.preContext, _current.expectedObjectId);
            if (before == null) return;
            if (type == SceneCommandType.Move && orchestrator.TryResolveActorStrict(_current.command.targetId, out var actor))
            {
                if (actor.agent != null && actor.agent.isOnNavMesh) actor.agent.Warp(before.position);
                actor.transform.position = before.position;
                actor.transform.rotation = before.rotation;
                actor.Stop();
            }
            else if (type == SceneCommandType.ProposeShot && filmCamera != null)
            {
                filmCamera.transform.SetPositionAndRotation(before.position, before.rotation);
                filmCamera.fieldOfView = _current.preFieldOfView;
                filmCamera.aspect = _current.preAspect;
            }
        }
    }
}
