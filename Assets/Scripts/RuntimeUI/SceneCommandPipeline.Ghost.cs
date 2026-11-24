using ShotField.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShotField.RuntimeUI
{
    public partial class SceneCommandPipeline
    {
        private void CreateGhost(PendingSceneCommand pending)
        {
            DestroyGhost();
            var type = (SceneCommandType)pending.command.commandType;
            if (type == SceneCommandType.Spawn)
            {
                var payload = JsonUtility.FromJson<SpawnCommandPayload>(pending.command.payloadJson);
                var prefab = browser.FindPrefab(payload.propId);
                if (prefab == null) return;
                _ghostRoot = new GameObject("AI_Ghost_Spawn_Root");
                _ghostRoot.SetActive(false);
                _ghost = Object.Instantiate(prefab, payload.position, payload.rotation, _ghostRoot.transform);
                _ghost.name = "AI_Ghost_Spawn";
                PropGhostVisualizer.StripGhost(_ghost);
                _ghostRoot.SetActive(true);
            }
            else
            {
                _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _ghostRoot = _ghost;
                _ghost.name = type == SceneCommandType.Move ? "AI_Ghost_Move" : "AI_Ghost_Camera";
                var collider = _ghost.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
                }
                if (type == SceneCommandType.Move)
                {
                    orchestrator.TryResolveActorStrict(pending.command.targetId, out var actor);
                    Vector3 size = new Vector3(0.4f, 1.6f, 0.4f);
                    if (actor != null)
                    {
                        var renderers = actor.GetComponentsInChildren<Renderer>();
                        if (renderers.Length > 0)
                        {
                            Bounds bounds = renderers[0].bounds;
                            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                            size = bounds.size;
                        }
                    }
                    _ghost.transform.position = pending.expectedPosition + Vector3.up * size.y * 0.5f;
                    _ghost.transform.localScale = size;
                }
                else
                {
                    _ghost.transform.position = pending.expectedPosition;
                    _ghost.transform.rotation = pending.expectedRotation;
                    _ghost.transform.localScale = new Vector3(0.14f, 0.10f, 0.20f);
                }
            }
            PropGhostVisualizer.StripGhost(_ghost);
            _ghostMaterial = PropGhostVisualizer.BuildGhostMaterial();
            PropGhostVisualizer.SetTint(_ghostMaterial, new Color(0.25f, 0.85f, 1f, 0.35f));
            PropGhostVisualizer.ApplyGhost(_ghost, _ghostMaterial);
        }

        private void DestroyGhost()
        {
            DisposeObject(_ghostRoot != null ? _ghostRoot : _ghost);
            if (_ghostMaterial != null)
            {
                if (Application.isPlaying) Object.Destroy(_ghostMaterial); else Object.DestroyImmediate(_ghostMaterial);
            }
            _ghost = null; _ghostRoot = null; _ghostMaterial = null;
        }

        private static void DisposeObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }
    }
}
