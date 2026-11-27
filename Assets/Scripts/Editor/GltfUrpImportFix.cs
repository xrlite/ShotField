#if UNITY_EDITOR
using Siccity.GLTFUtility;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// GLTFUtility 的默认 URP 着色器已被裁剪；导入时显式绑定工程现有 URP/Lit，
    /// 避免家具 glTF 因 Shader.Find 返回 null 而整包导入失败。
    /// </summary>
    internal sealed class GltfUrpImportFix : AssetPostprocessor
    {
        private const string OpaqueShader = "Universal Render Pipeline/Lit";
        private const string TransparentShader = "Universal Render Pipeline/Lit";

        private void OnPreprocessAsset()
        {
            if (!assetImporter.assetPath.EndsWith(".gltf", System.StringComparison.OrdinalIgnoreCase)) return;
            if (!(assetImporter is GLTFImporter importer)) return;

            var settings = importer.importSettings ?? new ImportSettings();
            SetShader(settings.shaderOverrides, "metallic", OpaqueShader);
            SetShader(settings.shaderOverrides, "metallicBlend", TransparentShader);
            SetShader(settings.shaderOverrides, "specular", OpaqueShader);
            SetShader(settings.shaderOverrides, "specularBlend", TransparentShader);
            settings.shaderOverrides.CacheDefaultShaders();
            importer.importSettings = settings;
        }

        private static void SetShader(ShaderSettings settings, string field, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[GltfUrpImportFix] Missing shader: {shaderName}");
                return;
            }
            var info = typeof(ShaderSettings).GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (info == null)
            {
                Debug.LogError($"[GltfUrpImportFix] Missing shader field: {field}");
                return;
            }
            info.SetValue(settings, shader);
        }
    }
}
#endif
