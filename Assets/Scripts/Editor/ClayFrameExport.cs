#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShotField.EditorTools
{
    internal static class ClayFrameExport
    {
        internal static void Save(Camera source)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../06_素材"));
            var path = EditorUtility.SaveFilePanel("导出当前机位的白模静帧", directory, "clay", "png");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                SaveToPath(source, path);
                Debug.Log("[ShotField] 白模静帧已保存：" + path);
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("导出失败", error.Message, "关闭");
            }
        }

        /// <summary>
        /// 无 GUI 版本：批处理模式（-executeMethod）只能走这条路径。
        /// 调用方负责给出完整文件路径与目录存在性。
        /// </summary>
        internal static string SaveToPath(Camera source, string path)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(path)) throw new System.ArgumentNullException(nameof(path));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var png = RenderClay(source);
            using var file = new FileStream(path, FileMode.Create);
            file.Write(png, 0, png.Length);
            return path;
        }

        /// <summary>返回导出尺寸，供批处理日志核对横竖画幅是否真的不同。</summary>
        internal static Vector2Int MeasureOutputSize(float aspect)
        {
            float normalized = ShotField.Core.ShotCameraState.NormalizeAspect(aspect);
            int width = Mathf.Clamp(Mathf.RoundToInt(1280f * Mathf.Min(1f, normalized)), 1, 1280);
            int height = Mathf.Clamp(Mathf.RoundToInt(1280f * Mathf.Min(1f, 1f / normalized)), 1, 1280);
            return new Vector2Int(width, height);
        }

        private static byte[] RenderClay(Camera source)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("找不到 URP Lit Shader。");

            float aspect = ShotField.Core.ShotCameraState.NormalizeAspect(source.aspect);
            int width = Mathf.Clamp(Mathf.RoundToInt(1280f * Mathf.Min(1f, aspect)), 1, 1280);
            int height = Mathf.Clamp(Mathf.RoundToInt(1280f * Mathf.Min(1f, 1f / aspect)), 1, 1280);
            var sceneObject = new GameObject("ShotField Clay Capture") { hideFlags = HideFlags.HideAndDontSave };
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var material = new Material(shader) { color = new Color(0.75f, 0.75f, 0.75f) };
            material.SetFloat("_Smoothness", 0f);
            var originals = new Dictionary<Renderer, Material[]>();
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sceneObject, source.gameObject.scene);
                ReplaceMaterials(source.gameObject.scene, material, originals);
                var capture = sceneObject.AddComponent<Camera>();
                capture.enabled = false;
                capture.CopyFrom(source);
                capture.enabled = false;
                capture.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                capture.targetTexture = target;
                capture.aspect = aspect;
                capture.stereoTargetEye = StereoTargetEyeMask.None;
                capture.clearFlags = CameraClearFlags.SolidColor;
                capture.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(capture, request))
                    throw new System.InvalidOperationException("当前渲染管线不支持静帧导出。");
                RenderPipeline.SubmitRenderRequest(capture, request);
                return FrameExportUtility.ReadGrayscalePng(target);
            }
            finally
            {
                foreach (var pair in originals) pair.Key.sharedMaterials = pair.Value;
                Object.DestroyImmediate(sceneObject);
                Object.DestroyImmediate(material);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void ReplaceMaterials(UnityEngine.SceneManagement.Scene scene, Material clay,
            Dictionary<Renderer, Material[]> originals)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) ||
                    renderer.gameObject.scene != scene) continue;
                var original = renderer.sharedMaterials;
                originals.Add(renderer, original);
                var replacement = new Material[original.Length];
                for (int i = 0; i < replacement.Length; i++) replacement[i] = clay;
                renderer.sharedMaterials = replacement;
            }
        }
    }
}
#endif
