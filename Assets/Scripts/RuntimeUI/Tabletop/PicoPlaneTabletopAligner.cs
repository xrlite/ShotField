using System;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 对接 PICO MR 空间水平面识别与沙盘桌面吸附。
    /// 支持动态择优最大物理桌面，避免误吸附小杂物；提供手动锁定/重扫机制；
    /// 未检测到或处于非真机环境时，优雅回退至导演正前方 0.8m、高 0.75m 的虚拟创作台。
    /// </summary>
    [DisallowMultipleComponent]
    public class PicoPlaneTabletopAligner : MonoBehaviour, ITabletopSurfaceProvider
    {
        [Header("绑定引用")]
        public TabletopDirector tabletop;
        public Transform headAnchor;

        [Header("虚拟创作台回退")]
        [Range(0.4f, 2.0f)] public float fallbackDistance = 0.8f;
        [Range(0.4f, 1.5f)] public float fallbackHeight = 0.75f;
        public Vector2 defaultTableSize = new Vector2(1.2f, 0.8f);

        [Header("平滑吸附与多平面治理")]
        public bool smoothAlignment = true;
        [Range(1f, 20f)] public float alignSpeed = 8f;
        public bool isLocked = false;

        private TabletopSurface _currentSurface;
        private bool _hasDetectedPlane;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation = Quaternion.identity;

        public bool HasDetectedPlane => _hasDetectedPlane;
        public TabletopSurface CurrentSurface => _currentSurface;
        public string AlignmentMode { get; private set; } = "Uninitialized";

        private void Awake()
        {
            if (tabletop == null) tabletop = FindAnyObjectByType<TabletopDirector>();
            if (headAnchor == null)
            {
                var cam = Camera.main;
                headAnchor = cam != null ? cam.transform : transform;
            }
        }

        private void OnEnable()
        {
            try { PXR_Manager.PlaneDetectionDataUpdated += OnPlanesUpdated; }
            catch (Exception ex) { Debug.LogWarning("[PicoPlaneAligner] PICO Plane 注册跳过: " + ex.Message); }
        }

        private void OnDisable()
        {
            try { PXR_Manager.PlaneDetectionDataUpdated -= OnPlanesUpdated; }
            catch (Exception) { /* 安全解绑 */ }
        }

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (Application.isPlaying)
                    _ = PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.PlaneDetection);
            }
            catch (Exception ex) { Debug.Log($"[PicoPlaneAligner] SenseProvider 启动回退: {ex.Message}"); }
#endif
            if (!_hasDetectedPlane) ApplyFallback();
        }

        private void OnPlanesUpdated(List<PxrPlaneData> planes)
        {
            if (planes == null || planes.Count == 0 || isLocked) return;
            int bestIdx = -1; float maxArea = _hasDetectedPlane ? _currentSurface.Size.x * _currentSurface.Size.y : 0.08f;
            for (int i = 0; i < planes.Count; i++)
            {
                var p = planes[i];
                var normal = p.rotation * Vector3.up;
                float w = p.box2D.extent.width; float h = p.box2D.extent.height;
                if (Vector3.Dot(normal, Vector3.up) >= 0.7f && w >= 0.3f && h >= 0.3f)
                {
                    float area = w * h;
                    if (area > maxArea * 1.15f || !_hasDetectedPlane) { maxArea = area; bestIdx = i; }
                }
            }
            if (bestIdx >= 0)
            {
                var best = planes[bestIdx];
                AlignToSurface(best.position, best.rotation * Vector3.up,
                    new Vector2(best.box2D.extent.width, best.box2D.extent.height));
            }
        }

        private void Update()
        {
            if (tabletop == null || tabletop.TabletopAnchor == null) return;
            if (smoothAlignment)
            {
                var anchor = tabletop.TabletopAnchor;
                anchor.position = Vector3.Lerp(anchor.position, _targetPosition, Time.deltaTime * alignSpeed);
                anchor.rotation = Quaternion.Slerp(anchor.rotation, _targetRotation, Time.deltaTime * alignSpeed);
            }
        }

        public bool TryGetSurface(out TabletopSurface surface)
        {
            if (_hasDetectedPlane && _currentSurface.FacesUp && _currentSurface.HasExtent)
            {
                surface = _currentSurface;
                return true;
            }
            surface = CreateFallbackSurface();
            return false;
        }

        public void LockCurrentSurface(bool locked = true) => isLocked = locked;

        public void Rescan()
        {
            isLocked = false;
            _hasDetectedPlane = false;
            ApplyFallback();
        }

        public void AlignToSurface(Vector3 center, Vector3 normal, Vector2 size)
        {
            var surface = new TabletopSurface(center, normal, size);
            if (!surface.FacesUp)
            {
                Debug.LogWarning("[PicoPlaneAligner] 法线非朝上，忽略吸附");
                return;
            }

            _currentSurface = surface;
            _hasDetectedPlane = true;
            _targetPosition = center + normal * 0.01f;
            _targetRotation = Quaternion.FromToRotation(Vector3.up, normal);
            AlignmentMode = "PhysicalPlane";

            if ((!smoothAlignment || !Application.isPlaying) && tabletop != null && tabletop.TabletopAnchor != null)
                tabletop.TabletopAnchor.SetPositionAndRotation(_targetPosition, _targetRotation);
            Debug.Log($"[PicoPlaneAligner] 吸附到实体桌面: center={center}, size={size}");
        }

        public void ApplyFallback()
        {
            _currentSurface = CreateFallbackSurface();
            _hasDetectedPlane = false;
            _targetPosition = _currentSurface.Center;
            _targetRotation = Quaternion.identity;
            AlignmentMode = "VirtualFallback";

            if (tabletop != null && tabletop.TabletopAnchor != null)
            {
                if (smoothAlignment && Application.isPlaying) { }
                else tabletop.TabletopAnchor.SetPositionAndRotation(_targetPosition, _targetRotation);
            }
            Debug.Log($"[PicoPlaneAligner] 回退至虚拟创作台: pos={_targetPosition}");
        }

        public TabletopSurface CreateFallbackSurface()
        {
            var forward = headAnchor != null ? headAnchor.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            var headPos = headAnchor != null ? headAnchor.position : Vector3.zero;
            var center = headPos + forward * fallbackDistance;
            center.y = fallbackHeight;
            return new TabletopSurface(center, Vector3.up, defaultTableSize);
        }
    }
}
