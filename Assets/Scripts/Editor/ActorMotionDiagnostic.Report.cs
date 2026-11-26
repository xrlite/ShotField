#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static partial class ActorMotionDiagnostic
    {
        private static void Capture(string name)
        {
            CaptureTo(_camera, Path.Combine(OutputDir, name + ".png"));
        }

        internal static void CaptureTo(Camera camera, string fullPath)
        {
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
        }

        private static void WriteClipInventory()
        {
            var lines = new List<string> { "ANIMATION-CLIP-INVENTORY" };
            var guids = AssetDatabase.FindAssets("t:AnimationClip");
            var clips = new List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.Contains("Starter Assets") && !path.Contains("Assets/Animations") &&
                    !path.Contains("Assets/Scenes")) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;
                clips.Add(clip.name + "\t" + path);
            }
            lines.AddRange(clips.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            string[] keywords = { "pickup", "reach", "crouch", "interact", "bend" };
            var matches = clips.Where(x => keywords.Any(k => x.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            lines.Add("PICKUP_LIKE_CLIPS=" + matches.Length);
            lines.AddRange(matches);
            File.WriteAllLines(Path.Combine(OutputDir, "animation_clip_inventory.txt"), lines, new UTF8Encoding(false));
        }

        private static void WriteReport(bool started, bool walkSeen, float distance, float elapsed)
        {
            var lines = new List<string>
            {
                "ACTOR-MOTION-DIAGNOSTIC",
                "SCENE=isolated",
                "PRODUCT_CAMERAS=0",
                "ACTOR=DiagnosticActor",
                "CONTROLLER=" + (_animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "missing"),
                "PARAMETERS=" + string.Join(",", _animator.parameters.Select(p => p.name + ":" + p.type).ToArray()),
                "DRIVER=" + (_actor.GetComponentInChildren<ActorLocomotionAnimator>()?.Describe() ?? "missing"),
                "STARTED=" + started,
                "WALK_STATE_SEEN=" + walkSeen,
                "DISTANCE_TO_B=" + distance.ToString("F4"),
                "ELAPSED=" + elapsed.ToString("F3"),
                "WALKING=" + _animator.GetFloat("Walking").ToString("F4"),
                "RUNNING=" + _animator.GetFloat("Running").ToString("F4"),
                "ROOT_MOTION=" + _animator.applyRootMotion,
                "STOP_REASON=" + _navigator.LastStopReason,
                "RESULT=" + (started && walkSeen && distance < 0.25f ? "PASS" : "FAIL"),
                "SAMPLES"
            };
            foreach (var sample in Samples) lines.Add(sample.ToString());
            File.WriteAllLines(Path.Combine(OutputDir, "actor_motion_report.txt"), lines, new UTF8Encoding(false));
            Debug.Log("ACTOR-MOTION-RESULT=" + (started && walkSeen && distance < 0.25f ? "PASS" : "FAIL") +
                " walkSeen=" + walkSeen + " distance=" + distance.ToString("F3"));
        }
    }
}
#endif
