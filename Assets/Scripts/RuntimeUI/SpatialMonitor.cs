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
        private Vector3 lastPos;
        private Quaternion lastRot;
        private float lastFov;
        private bool isDirty = true;
        private float lastRenderTime;

        public Texture Feed => renderTexture;
        public bool IsPortrait => isPortrait;
        public void RequestRender() => isDirty = true;

        void Awake()
        {
            EnsureMonitorCamera();
            SetupRenderTexture(false);
        }

        // 头显每帧都有亚毫米级抖动，用 != 判脏会变成「每帧重绘一次整个场景」——
        // 真机上实测每帧都在 RenderFeed，白烧一份全场景渲染。改成带阈值的判脏。
        const float PosEpsilon = 0.01f;      // 1 cm
        const float RotEpsilonDeg = 0.5f;    // 0.5 度
        const float FovEpsilon = 0.1f;
        const float TimeEpsilon = 2f;        // 即便不动，2 秒也补一帧

        void LateUpdate()
        {
            if (sourceCamera != null)
            {
                var tr = sourceCamera.transform;
                if ((tr.position - lastPos).sqrMagnitude > PosEpsilon * PosEpsilon
                    || Quaternion.Angle(tr.rotation, lastRot) > RotEpsilonDeg
                    || Mathf.Abs(sourceCamera.fieldOfView - lastFov) > FovEpsilon
                    || Time.unscaledTime - lastRenderTime > TimeEpsilon)
                {
                    isDirty = true;
                    lastPos = tr.position;
                    lastRot = tr.rotation;
                    lastFov = sourceCamera.fieldOfView;
                }
            }
            if (isDirty)
            {
                RenderFeed();
                lastRenderTime = Time.unscaledTime;
                isDirty = false;
            }
        }

        void OnEnable()
        {
            if (renderTexture == null)
            {
                SetupRenderTexture(isPortrait);
                isDirty = true;
            }
        }

        void OnDisable()
        {
            ReleaseRenderTexture();
        }

        void OnDestroy()
        {
            ReleaseRenderTexture();
            if (monitorCam != null)
            {
                ReleaseObject(monitorCam.gameObject);
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
            isDirty = true;

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

            int w = portrait ? 360 : 640;
            int h = portrait ? 640 : 360;

            renderTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32)
            {
                name = "SpatialDirector_RT",
                antiAliasing = 1
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

            var prevActive = RenderTexture.active;
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
            if (RenderTexture.active != prevActive) RenderTexture.active = prevActive;
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
                if (monitorDisplay != null && monitorDisplay.texture == renderTexture)
                    monitorDisplay.texture = null;
                ReleaseObject(renderTexture);
                renderTexture = null;
            }
        }

        private static void ReleaseObject(Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
