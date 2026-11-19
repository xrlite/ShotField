using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 道具放置 Ghost 半透明预览视觉辅助工具：
    /// 负责生成透明 Unlit 材质、变色以及剥离物理/抓取交互组件。
    /// </summary>
    public static class PropGhostVisualizer
    {
        public static Material BuildGhostMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = "GhostPlacement" };
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        public static Material BuildGhostMaterial(Material sourceMat, Color tint)
        {
            var mat = BuildGhostMaterial();
            SetTint(mat, tint);
            return mat;
        }

        public static void SetTint(Material ghostMaterial, Color color)
        {
            if (ghostMaterial == null) return;
            if (ghostMaterial.HasProperty("_BaseColor")) ghostMaterial.SetColor("_BaseColor", color);
            if (ghostMaterial.HasProperty("_Color")) ghostMaterial.SetColor("_Color", color);
        }

        public static void ApplyGhost(GameObject root, Color tint)
        {
            if (root == null) return;
            var mat = BuildGhostMaterial(null, tint);
            ApplyGhost(root, mat);
        }

        public static void ApplyGhost(GameObject root, Material ghostMaterial)
        {
            if (root == null || ghostMaterial == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            ApplyGhost(renderers, ghostMaterial);
        }

        public static void ApplyGhost(Renderer[] renderers, Material ghostMaterial)
        {
            if (renderers == null || ghostMaterial == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var slots = r.sharedMaterials;
                for (int s = 0; s < slots.Length; s++) slots[s] = ghostMaterial;
                r.sharedMaterials = slots;
            }
        }

        public static void StripGhost(GameObject go) => StripInteraction(go);

        public static void StripInteraction(GameObject go)
        {
            if (go == null) return;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var b in go.GetComponentsInChildren<Rigidbody>(true)) { b.isKinematic = true; b.detectCollisions = false; }
            foreach (var x in go.GetComponentsInChildren<XRGrabInteractable>(true)) x.enabled = false;
            foreach (var s in go.GetComponentsInChildren<ScenePropGrab>(true)) s.enabled = false;
        }
    }
}
