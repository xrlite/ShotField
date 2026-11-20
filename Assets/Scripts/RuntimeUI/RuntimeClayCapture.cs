using System.Collections.Generic;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    internal static class RuntimeClayCapture
    {
        internal static byte[] Capture(SpatialMonitor monitor)
        {
            if (monitor == null || monitor.sourceCamera == null)
                throw new System.InvalidOperationException("Director monitor is unavailable.");
            if (monitor.Feed == null) monitor.SetAspect(monitor.IsPortrait);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("URP/Lit shader is unavailable.");
            var clayMaterial = new Material(shader)
            {
                name = "ShotField_RuntimeClay",
                color = new Color(0.75f, 0.75f, 0.75f, 1f)
            };
            clayMaterial.SetFloat("_Smoothness", 0f);
            var backups = new Dictionary<Renderer, Material[]>();
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            try
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer.gameObject.scene != monitor.sourceCamera.gameObject.scene) continue;
                    if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                    var original = renderer.sharedMaterials;
                    backups.Add(renderer, original);
                    var claySlots = new Material[original.Length];
                    for (int slot = 0; slot < claySlots.Length; slot++) claySlots[slot] = clayMaterial;
                    renderer.sharedMaterials = claySlots;
                }
                monitor.RenderFeed();
                return ReadPng(monitor.Feed as RenderTexture);
            }
            finally
            {
                foreach (var pair in backups)
                    if (pair.Key) pair.Key.sharedMaterials = pair.Value;
                if (Application.isPlaying) Object.Destroy(clayMaterial); else Object.DestroyImmediate(clayMaterial);
                monitor.RequestRender();
            }
        }

        private static byte[] ReadPng(RenderTexture source)
        {
            if (source == null) throw new System.InvalidOperationException("Monitor render texture is unavailable.");
            var previous = RenderTexture.active;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = source;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                image.Apply(false);
                var pixels = image.GetRawTextureData<byte>();
                bool varied = false;
                for (int i = 3; i < pixels.Length && !varied; i++)
                    varied = pixels[i] != pixels[i % 3];
                if (!varied)
                    throw new System.InvalidOperationException("Director rendered a uniform frame.");
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                if (Application.isPlaying) Object.Destroy(image); else Object.DestroyImmediate(image);
            }
        }
    }
}
