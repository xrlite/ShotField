using System.Linq;
using UnityEngine;
using UnityEngine.XR;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Editor 无头显时的产品预览相机。仅在编辑器且 XR 设备未激活时接管，
    /// 不修改 XROrigin 或真实头显位姿；真机与已连接的 XR 设备完全跳过。
    /// </summary>
    public static class EditorCameraFallback
    {
        private const string CameraName = "PrevisSandboxCamera";

        public static Camera Ensure()
        {
            if (!Application.isEditor || XRSettings.isDeviceActive) return null;

            var existing = Camera.main;
            if (existing != null && existing.isActiveAndEnabled && existing.name != "XR_Headset")
                return existing;

            var transform = Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(t => t.name == CameraName && t.gameObject.scene.IsValid());
            if (transform == null) return null;

            var camera = transform.GetComponent<Camera>();
            if (camera == null) return null;

            transform.gameObject.SetActive(true);
            camera.enabled = true;
            camera.tag = "MainCamera";
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.depth = 10f;
            transform.position = new Vector3(0.20f, 1.65f, -1.20f);
            transform.LookAt(new Vector3(-0.20f, 0.75f, 2.20f));
            if (transform.GetComponent<EditorCameraFallbackDriver>() == null)
                transform.gameObject.AddComponent<EditorCameraFallbackDriver>();
            return camera;
        }
    }
}
