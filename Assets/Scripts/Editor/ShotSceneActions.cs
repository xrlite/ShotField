#if UNITY_EDITOR
using System.Collections.Generic;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static class ShotSceneActions
    {
        internal static void Apply(ShotLibrary library, ShotData shot)
        {
            var targets = new List<Object>();
            var camera = library.ShotCamera != null ? library.ShotCamera : Camera.main;
            if (camera != null)
            {
                targets.Add(camera);
                targets.Add(camera.transform);
            }

            foreach (var marker in SemanticMarker.All(FindObjectsInactive.Include))
            {
                if (marker.gameObject.scene != library.gameObject.scene ||
                    shot.snapshot == null || shot.snapshot.Find(marker.markerId) == null) continue;
                targets.Add(marker.gameObject);
                targets.Add(marker.transform);
            }

            if (targets.Count > 0)
                Undo.RegisterCompleteObjectUndo(targets.ToArray(), "应用镜头场景和机位");
            library.ApplyShot(shot);
            SceneView.RepaintAll();
        }
    }
}
#endif
