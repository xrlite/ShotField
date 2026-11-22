using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ByteDance.PICO.XR;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// MR spike 专用（独立场景 MR_Spike.unity，不被 SC01_Main 引用）。
    /// OpenXR 模式下验证：透视开关、空间锚点创建/持久化/周期定位；主动探测平面识别并如实记录。
    /// 平面检测在 OpenXR 模拟器不在支持清单（调研笔记 §5 矩阵），拿不到平面数据属预期阻塞，不伪造。
    /// </summary>
    public sealed class MrSpikeController : MonoBehaviour
    {
        [Tooltip("锚点位姿上摆放的微缩沙盘代理")]
        public Transform tabletopProxy;

        private ulong _anchorHandle;
        private Guid _anchorUuid;
        private int _planeCount;

        private void Start()
        {
            Debug.Log("[MR-SPIKE] boot start");
            EnablePassthrough();
            _ = RunAnchorFlow();
            _ = ProbePlaneDetection();
        }

        private void OnEnable() => PXR_Manager.PlaneDetectionDataUpdated += OnPlanes;
        private void OnDisable() => PXR_Manager.PlaneDetectionDataUpdated -= OnPlanes;

        private static void EnablePassthrough()
        {
            try
            {
                PXR_Manager.VstDisplayStatusChanged += OnVstStatus;
                PXR_Manager.EnableVideoSeeThrough = true;
                Debug.Log("[MR-SPIKE] VST enable requested (PXR_Manager route)");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MR-SPIKE] PXR VST route failed: " + ex.Message);
            }
        }

        private static void OnVstStatus(PxrVstStatus status) => Debug.Log("[MR-SPIKE] VST status=" + status);

        private async Task RunAnchorFlow()
        {
            var started = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.SpatialAnchor);
            Debug.Log("[MR-SPIKE] anchor provider start=" + started);

            var cam = Camera.main != null ? Camera.main.transform : transform;
            var pos = cam.position + cam.forward * 1.5f;
            var rot = cam.rotation;
            var created = await PXR_MixedReality.CreateSpatialAnchorAsync(pos, rot);
            Debug.Log("[MR-SPIKE] anchor create=" + created.result + " handle=" + created.anchorHandle + " uuid=" + created.uuid);

            if (created.result != PxrResult.SUCCESS) return;
            _anchorHandle = created.anchorHandle;
            _anchorUuid = created.uuid;

            var persisted = await PXR_MixedReality.PersistSpatialAnchorAsync(_anchorHandle);
            Debug.Log("[MR-SPIKE] anchor persist=" + persisted);

            if (tabletopProxy != null) tabletopProxy.SetPositionAndRotation(pos, rot);
            InvokeRepeating(nameof(LocateTick), 1f, 1f);
        }

        private void LocateTick()
        {
            if (PXR_MixedReality.LocateAnchor(_anchorHandle, out var p, out var q) == PxrResult.SUCCESS)
                Debug.Log($"[MR-SPIKE] anchor locate pos={p:F3} rot={q.eulerAngles:F1}");
        }

        private async Task ProbePlaneDetection()
        {
            var started = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.PlaneDetection);
            PXR_MixedReality.GetSenseDataProviderState(PxrSenseDataProviderType.PlaneDetection, out var state);
            Debug.Log("[MR-SPIKE] plane provider start=" + started + " state=" + state);
            await Task.Delay(1500);
            Debug.Log("[MR-SPIKE] plane arrived=" + (_planeCount > 0) + " count=" + _planeCount);
        }

        private void OnPlanes(List<PxrPlaneData> planes)
        {
            if (planes != null) _planeCount += planes.Count;
            Debug.Log("[MR-SPIKE] planes callback count=" + (planes != null ? planes.Count : 0));
        }
    }
}
