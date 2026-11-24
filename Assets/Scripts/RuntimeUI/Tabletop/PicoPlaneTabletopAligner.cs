using System;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 对接 PICO MR 空间水平面识别与沙盘桌面吸附。
    /// 识别到桌面时将 1:20 沙盘锚点吸附并平滑对齐到实体桌面；
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

        [Header("平滑吸附")]
        public bool smoothAlignment = true;
        [Range(1f, 20f)] public float alignSpeed = 8f;

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

        private void Start()
        {
            if (!_hasDetectedPlane)
            {
                ApplyFallback();
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

        /// <summary>实现 ITabletopSurfaceProvider 契约，向沙盘系统提供当前吸附面。</summary>
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

        /// <summary>将沙盘平滑吸附到外部传入或检测到的真实水平面。</summary>
        public void AlignToSurface(Vector3 center, Vector3 normal, Vector2 size)
        {
            var surface = new TabletopSurface(center, normal, size);
            if (!surface.FacesUp)
            {
                Debug.LogWarning("[PicoPlaneAligner] 传入表面法线非朝上，忽略吸附并保留回退");
                return;
            }

            _currentSurface = surface;
            _hasDetectedPlane = true;
            _targetPosition = center + normal * 0.01f;
            _targetRotation = Quaternion.FromToRotation(Vector3.up, normal);
            AlignmentMode = "PhysicalPlane";

            if (!smoothAlignment && tabletop != null && tabletop.TabletopAnchor != null)
            {
                tabletop.TabletopAnchor.SetPositionAndRotation(_targetPosition, _targetRotation);
            }
            Debug.Log($"[PicoPlaneAligner] 吸附到实体桌面: center={center}, size={size}");
        }

        /// <summary>执行虚拟创作台优雅回退（正前方 0.8m，高 0.75m）。</summary>
        public void ApplyFallback()
        {
            _currentSurface = CreateFallbackSurface();
            _hasDetectedPlane = false;
            _targetPosition = _currentSurface.Center;
            _targetRotation = Quaternion.identity;
            AlignmentMode = "VirtualFallback";

            if (tabletop != null && tabletop.TabletopAnchor != null)
            {
                if (smoothAlignment && Application.isPlaying)
                {
                    // Update will lerp to _targetPosition
                }
                else
                {
                    tabletop.TabletopAnchor.SetPositionAndRotation(_targetPosition, _targetRotation);
                }
            }
            Debug.Log($"[PicoPlaneAligner] 回退至虚拟创作台: pos={_targetPosition}");
        }

        /// <summary>构建标准 0.8m 距离、0.75m 高度的回退桌面面片。</summary>
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
