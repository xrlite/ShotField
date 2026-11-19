#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// PICO 移动 VR 资产规范化检查与修复工具：
    /// 1) 确保 *_nor_gl* 法线贴图设为 NormalMap；
    /// 2) 限制贴图 MaxSize <= 1024 控显存；
    /// 3) 扫描面数 > 15,000 的高面数道具并输出优化建议。
    /// </summary>
    public static class AssetMaterialNormalizer
    {
        private const int MaxPicoTextureSize = 1024;
        private const int RecommendedMaxTris = 15000;

        [MenuItem("ShotField/Art/Normalize Furniture Textures & Audit Polycount")]
        public static void Run()
        {
            int fixedTextures = 0;
            int highPolyCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Models/Furniture" });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (path.Contains("_nor_gl") && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
                if (importer.maxTextureSize > MaxPicoTextureSize)
                {
                    importer.maxTextureSize = MaxPicoTextureSize;
                    dirty = true;
                }
                if (dirty)
                {
                    importer.SaveAndReimport();
                    fixedTextures++;
                }
            }

            string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Models/Furniture" });
            for (int i = 0; i < meshGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(meshGuids[i]);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) continue;
                int tris = mesh.triangles.Length / 3;
                if (tris > RecommendedMaxTris)
                {
                    highPolyCount++;
                    Debug.LogWarning($"[ASSET-POLY-WARN] {path}: {tris} tris (> {RecommendedMaxTris}), 建议减面");
                }
            }

            Debug.Log($"[AssetMaterialNormalizer] 完成：修复贴图 {fixedTextures} 张，高面数警告 {highPolyCount} 项。");
        }
    }
}
#endif
