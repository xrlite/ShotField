using UnityEngine;
using UnityEngine.UI;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 空间 3D 悬浮监视器（Spatial 3D Monitor）。
    /// 将摄影机画面投射到 3D 悬浮视口中，支持 16:9 电影画幅与 9:16 短剧画幅切换。
    /// 遵循 PICO 空间多窗口交互规范，采用离线从属相机按需渲染，杜绝主视口黑屏与多活动相机冲突。
    /// </summary>
    public class SpatialMonitor : MonoBehaviour
    {
        [Header("绑定的摄影机")]
        public Camera sourceCamera;

        [Header("视口渲染载体")]
        public RawImage monitorDisplay;
        public RectTransform viewportFrame;

        private RenderTexture renderTexture;
        private Camera monitorCam;
        private bool isPortrait = false;

        public bool IsPortrait => isPortrait;

        void Awake()
        {
            EnsureMonitorCamera();
            SetupRenderTexture(false);
        }

        void LateUpdate()
        {
            RenderFeed();
        }

        void OnDestroy()
        {
            ReleaseRenderTexture();
            if (monitorCam != null)
            {
                DestroyImmediate(monitorCam.gameObject);
                monitorCam = null;
            }
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
                viewportFrame.sizeDelta = isPortrait ? new Vector2(360f, 640f) : new Vector2(640f, 360f);
            }
            RenderFeed();
        }

        private void EnsureMonitorCamera()
        {
            if (monitorCam != null) return;
            var camGo = new GameObject("Monitor_InternalCam");
            camGo.transform.SetParent(transform, false);
            camGo.hideFlags = HideFlags.DontSave;
            monitorCam = camGo.AddComponent<Camera>();
            monitorCam.enabled = false; // 严守 PICO 单活动相机红线，不进入主渲染循环
        }

        private void SetupRenderTexture(bool portrait)
        {
            ReleaseRenderTexture();
            EnsureMonitorCamera();

            int w = portrait ? 720 : 1280;
            int h = portrait ? 1280 : 720;

            renderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "SpatialDirector_RT",
                antiAliasing = 2
            };
            renderTexture.Create();

            if (monitorCam != null)
            {
                monitorCam.targetTexture = renderTexture;
            }

            if (monitorDisplay != null)
            {
                monitorDisplay.texture = renderTexture;
            }
        }

        public void RenderFeed()
        {
            if (monitorCam == null || sourceCamera == null || renderTexture == null) return;

            monitorCam.transform.position = sourceCamera.transform.position;
            monitorCam.transform.rotation = sourceCamera.transform.rotation;
            monitorCam.fieldOfView = sourceCamera.fieldOfView;
            monitorCam.nearClipPlane = sourceCamera.nearClipPlane;
            monitorCam.farClipPlane = sourceCamera.farClipPlane;
            monitorCam.clearFlags = sourceCamera.clearFlags;
            monitorCam.backgroundColor = sourceCamera.backgroundColor;
            monitorCam.cullingMask = sourceCamera.cullingMask;
            monitorCam.aspect = isPortrait ? ShotCameraState.PortraitAspect : ShotCameraState.DefaultAspect;
            monitorCam.Render();
        }

        private void ReleaseRenderTexture()
        {
            if (monitorCam != null && monitorCam.targetTexture == renderTexture)
            {
                monitorCam.targetTexture = null;
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
