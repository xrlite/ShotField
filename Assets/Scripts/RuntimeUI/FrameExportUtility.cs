using UnityEngine;
using UnityEngine.Rendering;

namespace ShotField.RuntimeUI
{
    public static class FrameExportUtility
    {
        public static byte[] ReadPng(RenderTexture source, bool requireVariation = true)
        {
            if (source == null) throw new System.InvalidOperationException("Render texture is unavailable.");
            var previous = RenderTexture.active;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = source;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                image.Apply(false);
                if (requireVariation)
                {
                    var pixels = image.GetRawTextureData<byte>();
                    bool varied = false;
                    for (int i = 3; i < pixels.Length && !varied; i++)
                        varied = pixels[i] != pixels[i % 3];
                    if (!varied)
                        throw new System.InvalidOperationException("Rendered a uniform frame.");
                }
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                if (Application.isPlaying) Object.Destroy(image); else Object.DestroyImmediate(image);
            }
        }

        public static byte[] ReadGrayscalePng(RenderTexture target, int minDelta = 3)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                var pixels = image.GetRawTextureData<byte>();
                int darkest = 255, brightest = 0;
                for (int i = 0; i < pixels.Length; i += 3)
                {
                    byte gray = (byte)((pixels[i] * 54 + pixels[i + 1] * 183 + pixels[i + 2] * 19 + 128) >> 8);
                    pixels[i] = gray;
                    pixels[i + 1] = gray;
                    pixels[i + 2] = gray;
                    darkest = Mathf.Min(darkest, gray);
                    brightest = Mathf.Max(brightest, gray);
                }
                if (brightest - darkest < minDelta)
                    throw new System.InvalidOperationException("Clay capture produced a flat frame.");
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                if (Application.isPlaying) Object.Destroy(image); else Object.DestroyImmediate(image);
            }
        }

        public static byte[] RenderCameraToPng(Camera camera, int width, int height, Color? bgColor = null)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = bgColor ?? new Color(0.18f, 0.18f, 0.18f);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new System.InvalidOperationException("当前渲染管线不支持静帧导出。");
                RenderPipeline.SubmitRenderRequest(camera, request);

                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply(false);
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (Application.isPlaying) Object.Destroy(image); else Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
