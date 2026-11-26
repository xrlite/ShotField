#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>生成 UGUI 空间玻璃共用材质；不手改 .mat，统一由 Unity 序列化。</summary>
    public static class SpatialGlassMaterialBuilder
    {
        private const string MaterialPath = "Assets/Resources/UI/SpatialGlass.mat";

        [MenuItem("ShotField/UI/Create SpatialGlass Material")]
        public static void Create()
        {
            AssetDatabase.ImportAsset("Assets/Shaders/SpatialGlass.shader", ImportAssetOptions.ForceUpdate);
            var shader = Shader.Find("ShotField/SpatialGlass");
            if (shader == null)
            {
                Debug.LogError("SPATIAL-GLASS-MATERIAL=FAIL shader missing");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else material.shader = shader;

            material.SetColor("_BaseColor", new Color(0.96f, 0.93f, 0.86f, 0.68f));
            material.SetFloat("_CornerRadius", 32f);
            material.SetFloat("_BorderWidth", 1f);
            material.SetColor("_BorderColor", new Color(1f, 0.98f, 0.92f, 0.55f));
            material.SetColor("_HighlightColor", new Color(1f, 0.99f, 0.96f, 0.65f));
            material.SetFloat("_HighlightHeight", 2f);
            material.SetFloat("_InnerGlow", 0.35f);
            material.SetVector("_RectSize", new Vector4(560f, 220f, 0f, 0f));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("SPATIAL-GLASS-MATERIAL=PASS path=" + MaterialPath);
        }
    }
}
#endif
