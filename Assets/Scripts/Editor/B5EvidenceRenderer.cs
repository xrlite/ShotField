#if UNITY_EDITOR
using System;
using System.IO;
using ShotField.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
namespace ShotField.EditorTools
{
    /// <summary>B5 evidence-only camera readback with temporary, camera-space English labels.</summary>
    public static class B5EvidenceRenderer
    {
        public static void RenderJpg(Camera camera, string path, int width, int height)
        {
            var image = Capture(camera, width, height, null);
            try { File.WriteAllBytes(path, image.EncodeToJPG(90)); }
            finally { Object.DestroyImmediate(image); }
        }
        public static void RenderPng(Camera camera, string path, int width, int height, string label = null)
        {
            var image = Capture(camera, width, height, label);
            try { File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { Object.DestroyImmediate(image); }
        }
        public static void RenderSideBySide(Camera camera, TrajectoryPoint raw, TrajectoryPoint smoothed,
            string path, int width = 1920, int height = 540, string rawLabel = "RAW", string smoothedLabel = "SMOOTHED")
        {
            if (width % 2 != 0) throw new ArgumentException("Side-by-side width must be even.");
            int half = width / 2;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var fov = camera.fieldOfView;
            Texture2D left = null, right = null, combined = null;
            try
            {
                Apply(camera, raw); left = Capture(camera, half, height, rawLabel);
                Apply(camera, smoothed); right = Capture(camera, half, height, smoothedLabel);
                combined = new Texture2D(width, height, TextureFormat.RGB24, false);
                combined.SetPixels(0, 0, half, height, left.GetPixels(), 0);
                combined.SetPixels(half, 0, half, height, right.GetPixels(), 0);
                combined.Apply(false);
                File.WriteAllBytes(path, combined.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fov;
                Object.DestroyImmediate(left); Object.DestroyImmediate(right); Object.DestroyImmediate(combined);
            }
        }
        internal static void Apply(Camera camera, TrajectoryPoint sample)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            camera.orthographic = false;
            camera.transform.SetPositionAndRotation(sample.position, sample.rotation);
            camera.fieldOfView = sample.fieldOfView;
        }
        private static Texture2D Capture(Camera camera, int width, int height, string label)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (width <= 0 || height <= 0) throw new ArgumentException("Render dimensions must be positive.");
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var previousAspect = camera.aspect;
            var previousEnabled = camera.enabled;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default, 4);
            Texture2D image = null;
            GameObject labelObject = null;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.enabled = true;
                if (!string.IsNullOrEmpty(label)) labelObject = CreateLabel(camera, width, height, label);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("The active render pipeline does not support camera readback.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply(false);
                return image;
            }
            catch { Object.DestroyImmediate(image); throw; }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                camera.enabled = previousEnabled;
                Object.DestroyImmediate(labelObject);
                RenderTexture.ReleaseTemporary(target);
            }
        }
        private static GameObject CreateLabel(Camera camera, int width, int height, string value)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) throw new InvalidOperationException("LegacyRuntime.ttf is unavailable.");
            float visibleHeight = camera.orthographic ? camera.orthographicSize * 2f
                : 2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float visibleWidth = visibleHeight * width / height;
            float margin = visibleHeight * 0.035f;
            var go = new GameObject("B5EvidenceLabel") { hideFlags = HideFlags.HideAndDontSave };
            go.layer = 0;
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(-visibleWidth * 0.5f + margin, visibleHeight * 0.5f - margin, 1f);
            var text = go.AddComponent<TextMesh>();
            text.font = font; text.text = value; text.fontSize = 32;
            text.characterSize = visibleHeight * 0.05f; text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.UpperLeft; text.alignment = TextAlignment.Left; text.color = Color.white;
            return go;
        }
    }
}
#endif
