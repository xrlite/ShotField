#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ShotField.Core;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 语义视觉控制器（Semantic Vision Controller）。
    /// 实现 PRD 附录 F 规定的显示模式切换：
    /// 模式 1：标准白模（Clay）- 纯灰白中性色，供日常调度与导出基线；
    /// 模式 2：语义色彩（Semantic）- 角色/道具/环境分色，供导演人眼质检与评审展示。
    /// 核心铁律：模式切换仅改变显示渲染，不改变任何对象身份、坐标与场景护照数据。
    /// </summary>
    public static class SemanticVisionController
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [MenuItem("ShotField/ViewMode/Switch to Semantic Color")]
        [MenuItem("镜场 ShotField/显示模式/切换为语义色彩 (Semantic)")]
        public static void ApplySemanticMode()
        {
            ApplyMode(true);
            Debug.Log("[镜场 ShotField] 已切换为「语义色彩模式」：角色/道具/环境清晰分色。");
        }

        [MenuItem("ShotField/ViewMode/Switch to Standard Clay")]
        [MenuItem("镜场 ShotField/显示模式/切换为标准白模 (Clay)")]
        public static void ApplyClayMode()
        {
            ApplyMode(false);
            Debug.Log("[镜场 ShotField] 已切换为「标准白模模式」：纯灰白无贴图质感。");
        }

        public static void ApplyMode(bool isSemantic)
        {
            var markers = SemanticMarker.All(FindObjectsInactive.Include);
            var mpb = new MaterialPropertyBlock();

            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                var renderers = marker.GetComponentsInChildren<Renderer>(true);
                Color targetColor = isSemantic ? SemanticPalette.GetColor(marker.kind) : SemanticPalette.ClayColor;

                for (int r = 0; r < renderers.Length; r++)
                {
                    var rend = renderers[r];
                    rend.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, targetColor);
                    rend.SetPropertyBlock(mpb);
                }
            }

            SceneView.RepaintAll();
        }
    }
}
#endif
