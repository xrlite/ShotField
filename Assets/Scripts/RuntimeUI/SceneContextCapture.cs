using System.Collections.Generic;
using ShotField.Constraints;
using ShotField.Core;
using UnityEngine;
using UnityEngine.AI;

namespace ShotField.RuntimeUI
{
    /// <summary>从真实场景捕获 AI 上行上下文与执行后回读上下文，不填估计值。</summary>
    public static class SceneContextCapture
    {
        public const string DirectorCameraId = "__director_camera__";

        public static SceneContextDto Capture(Camera directorCamera = null, string sceneId = "SC01")
        {
            var context = new SceneContextDto
            {
                sceneId = sceneId,
                timestampUtc = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                walkableBounds = CaptureWalkableBounds()
            };
            var markers = SemanticMarker.All(FindObjectsInactive.Include);
            foreach (var marker in markers)
            {
                if (marker == null || string.IsNullOrEmpty(marker.markerId)) continue;
                if (marker.GetComponentInParent<GhostPreviewMarker>() != null) continue;
                var entry = PropLibrary.Find(marker.markerId);
                context.objects.Add(new SceneObjectDto
                {
                    objectId = marker.markerId,
                    displayName = marker.displayName,
                    archetype = marker.kind.ToString(),
                    category = entry != null ? entry.category.ToString() : marker.kind.ToString(),
                    position = marker.transform.position,
                    rotation = marker.transform.rotation,
                    bounds = CaptureBounds(marker.gameObject),
                    isActive = marker.gameObject.activeInHierarchy
                });
            }
            if (directorCamera != null)
            {
                context.objects.Add(new SceneObjectDto
                {
                    objectId = DirectorCameraId, displayName = "Director Camera", archetype = "Camera",
                    category = "Camera", position = directorCamera.transform.position,
                    rotation = directorCamera.transform.rotation,
                    bounds = new BoundsDto(directorCamera.transform.position, new Vector3Dto(0.1f, 0.1f, 0.1f)),
                    isActive = directorCamera.gameObject.activeInHierarchy
                });
            }
            foreach (var rule in Object.FindObjectsByType<ShotConstraint>(FindObjectsInactive.Include))
            {
                if (rule == null) continue;
                context.activeConstraints.Add(new SceneConstraintSummaryDto
                {
                    constraintId = rule.ruleName, ruleType = rule.GetType().Name,
                    sourceId = rule is VisibilityConstraint visibility ? visibility.targetMarkerId : "",
                    targetId = "", isBlocking = rule.blocking, isWaivable = rule.waivable
                });
            }
            return context;
        }

        public static SceneObjectDto FindObject(SceneContextDto context, string objectId)
        {
            if (context == null || context.objects == null) return null;
            for (int i = 0; i < context.objects.Count; i++)
                if (context.objects[i] != null && context.objects[i].objectId == objectId) return context.objects[i];
            return null;
        }

        public static SceneConstraintContext BuildPromptContext(SceneContextDto context)
        {
            var prompt = new SceneConstraintContext
            {
                sceneTitle = context != null ? context.sceneId : "",
                availableActorSlots = new List<string>(), availableProps = new List<PropEntry>(),
                existingConstraints = new List<string>()
            };
            if (context != null && context.objects != null)
            {
                foreach (var obj in context.objects)
                {
                    if (obj == null || !obj.isActive) continue;
                    if (obj.archetype == nameof(SemanticMarker.Kind.ActorPrimary) ||
                        obj.archetype == nameof(SemanticMarker.Kind.ActorSecondary))
                        prompt.availableActorSlots.Add(obj.objectId);
                }
            }
            for (int i = 0; i < PropLibrary.Count; i++) prompt.availableProps.Add(PropLibrary.All[i]);
            if (context != null && context.activeConstraints != null)
                foreach (var rule in context.activeConstraints)
                    prompt.existingConstraints.Add(rule.ruleType + ":" + rule.constraintId);
            return prompt;
        }

        private static NavMeshBoundsDto CaptureWalkableBounds()
        {
            var result = new NavMeshBoundsDto { sampleTimestamp = System.DateTime.UtcNow.ToString("O") };
            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length == 0) return result;
            Vector3 min = triangulation.vertices[0], max = triangulation.vertices[0];
            foreach (var vertex in triangulation.vertices) { min = Vector3.Min(min, vertex); max = Vector3.Max(max, vertex); }
            result.isSampled = true;
            result.floorBounds = new BoundsDto((min + max) * 0.5f, max - min);
            result.walkableBounds = result.floorBounds;
            foreach (var obstacle in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include))
            {
                var collider = obstacle.GetComponent<Collider>();
                if (collider != null) result.obstacles.Add(CaptureBounds(obstacle.gameObject));
            }
            return result;
        }

        private static BoundsDto CaptureBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                return new BoundsDto(bounds.center, bounds.size);
            }
            var colliders = go.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                Bounds bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
                return new BoundsDto(bounds.center, bounds.size);
            }
            return new BoundsDto(go.transform.position, Vector3.zero);
        }
    }
}
