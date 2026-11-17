using UnityEngine;
using UnityEngine.UI;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 空间 3D 悬浮监视器（Spatial 3D Monitor）。
    /// 将 Director 相机捕获的画面实时投射到 3D 悬浮视口中，
    /// 支持 16:9 电影画幅与 9:16 短剧竖屏画幅一键切换。
    /// 遵循 PICO 空间多窗口交互规范。
    /// </summary>
    public class SpatialMonitor : MonoBehaviour
    {
        [Header("绑定的摄影机")]
        public Camera sourceCamera;

        [Header("视口渲染载体")]
        public RawImage monitorDisplay;
        public RectTransform viewportFrame;

        private RenderTexture renderTexture;
        private bool isPortrait = false;

        public bool IsPortrait => isPortrait;

        void Awake()
        {
            SetupRenderTexture(false);
        }

        void OnDestroy()
        {
            ReleaseRenderTexture();
        }

        public void ToggleAspect()
        {
            SetAspect(!isPortrait);
        }

        public void SetAspect(bool portrait)
        {
            isPortrait = portrait;
            SetupRenderTexture(isPortrait);
            if (sourceCamera != null)
            {
                sourceCamera.aspect = isPortrait ? ShotCameraState.PortraitAspect : ShotCameraState.DefaultAspect;
            }

            if (viewportFrame != null)
            {
                // 调整 3D 浮动监视器物理边框尺寸
                if (isPortrait)
                {
                    viewportFrame.sizeDelta = new Vector2(360f, 640f);
                }
                else
                {
                    viewportFrame.sizeDelta = new Vector2(640f, 360f);
                }
            }
        }

        private void SetupRenderTexture(bool portrait)
        {
            ReleaseRenderTexture();

            int w = portrait ? 720 : 1280;
            int h = portrait ? 1280 : 720;

            renderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "SpatialDirector_RT",
                antiAliasing = 2
            };
            renderTexture.Create();

            if (sourceCamera != null)
            {
                sourceCamera.targetTexture = renderTexture;
            }

            if (monitorDisplay != null)
            {
                monitorDisplay.texture = renderTexture;
            }
        }

        private void ReleaseRenderTexture()
        {
            if (sourceCamera != null && sourceCamera.targetTexture == renderTexture)
            {
                sourceCamera.targetTexture = null;
            }

            if (renderTexture != null)
            {
                if (RenderTexture.active == renderTexture)
                {
                    RenderTexture.active = null;
                }
                renderTexture.Release();
                DestroyImmediate(renderTexture);
                renderTexture = null;
            }
        }
    }
}
