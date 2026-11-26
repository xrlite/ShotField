using UnityEngine;
using UnityEngine.XR;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Editor 预览相机的常驻守卫：禁用贴地 XR 相机，并按沙盘/片场自动构图。
    /// 真机或 XR 设备已激活时不执行；autoFrame=false 供证据相机临时接管。
    /// </summary>
    public sealed class EditorCameraFallbackDriver : MonoBehaviour
    {
        public bool autoFrame = true;
        private Camera _camera;
        private TabletopDirector _tabletop;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _tabletop = FindAnyObjectByType<TabletopDirector>();
        }

        private void LateUpdate()
        {
            if (!Application.isEditor || XRSettings.isDeviceActive || _camera == null) return;
            var xr = Resources.FindObjectsOfTypeAll<Camera>();
            foreach (var camera in xr)
                if (camera != null && camera.name == "XR_Headset" && camera.gameObject.scene.IsValid())
                    camera.enabled = false;
            if (!autoFrame) return;
            if (_tabletop == null) _tabletop = FindAnyObjectByType<TabletopDirector>();

            bool mini = _tabletop != null &&
                        _tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini;
            _camera.transform.position = mini
                ? new Vector3(0.20f, 1.85f, -1.65f)
                : new Vector3(0.00f, 1.55f, 0.20f);
            _camera.transform.LookAt(mini
                ? new Vector3(0.00f, 1.00f, -0.10f)
                : new Vector3(-0.20f, 0.75f, 2.20f));
            _camera.fieldOfView = mini ? 66f : 62f;
        }
    }
}
