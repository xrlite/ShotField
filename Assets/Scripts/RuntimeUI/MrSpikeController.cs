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
    /// 状态字段驱动左上角 IMGUI 调试叠加层，让应用层自证"东西在哪、什么状态"。
    /// </summary>
    public sealed class MrSpikeController : MonoBehaviour
    {
        [Tooltip("锚点位姿上摆放的微缩沙盘代理")]
        public Transform tabletopProxy;

        // ---- 叠加层状态（只读给 OnGUI / 调试层） ----
        public string VstState { get; private set; } = "pending";
        public string AnchorProviderState { get; private set; } = "pending";
        public string AnchorCreateState { get; private set; } = "pending";
        public string AnchorPersistState { get; private set; } = "pending";
        public string LocateState { get; private set; } = "pending";
        public string AnchorPoseText { get; private set; } = "-";
        public string PlaneProviderState { get; private set; } = "pending";
        public int PlaneCount { get; private set; }

        private ulong _anchorHandle;
        private Guid _anchorUuid;

        private void Start()
        {
            Debug.Log("[MR-SPIKE] boot start");
            EnablePassthrough();
            _ = RunAnchorFlow();
            _ = ProbePlaneDetection();
        }

        private void OnEnable() => PXR_Manager.PlaneDetectionDataUpdated += OnPlanes;
        private void OnDisable() => PXR_Manager.PlaneDetectionDataUpdated -= OnPlanes;

        private void EnablePassthrough()
        {
            try
            {
                PXR_Manager.VstDisplayStatusChanged += OnVstStatus;
                PXR_Manager.EnableVideoSeeThrough = true;
                VstState = "requested";
                Debug.Log("[MR-SPIKE] VST enable requested (PXR_Manager route)");
            }
            catch (Exception ex)
            {
                VstState = "failed: " + ex.Message;
                Debug.LogWarning("[MR-SPIKE] PXR VST route failed: " + ex.Message);
            }
        }

        private static void OnVstStatus(PxrVstStatus status) =>
            Debug.Log("[MR-SPIKE] VST status=" + status);

        private async Task RunAnchorFlow()
        {
            var started = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.SpatialAnchor);
            AnchorProviderState = started.ToString();
            Debug.Log("[MR-SPIKE] anchor provider start=" + started);

            var cam = Camera.main != null ? Camera.main.transform : transform;
            var pos = cam.position + cam.forward * 1.5f;
            var rot = cam.rotation;
            var created = await PXR_MixedReality.CreateSpatialAnchorAsync(pos, rot);
            AnchorCreateState = created.result + " handle=" + created.anchorHandle;
            Debug.Log("[MR-SPIKE] anchor create=" + created.result + " handle=" + created.anchorHandle + " uuid=" + created.uuid);

            if (created.result != PxrResult.SUCCESS) return;
            _anchorHandle = created.anchorHandle;
            _anchorUuid = created.uuid;

            var persisted = await PXR_MixedReality.PersistSpatialAnchorAsync(_anchorHandle);
            AnchorPersistState = persisted.ToString();
            Debug.Log("[MR-SPIKE] anchor persist=" + persisted);

            if (tabletopProxy != null) tabletopProxy.SetPositionAndRotation(pos, rot);
            InvokeRepeating(nameof(LocateTick), 1f, 1f);
        }

        private void LocateTick()
        {
            if (PXR_MixedReality.LocateAnchor(_anchorHandle, out var p, out var q) == PxrResult.SUCCESS)
            {
                LocateState = "SUCCESS";
                AnchorPoseText = $"pos=({p.x:F2},{p.y:F2},{p.z:F2}) rot=({q.eulerAngles.x:F0},{q.eulerAngles.y:F0},{q.eulerAngles.z:F0})";
                Debug.Log($"[MR-SPIKE] anchor locate {AnchorPoseText}");
            }
            else
            {
                LocateState = "FAIL";
            }
        }

        private async Task ProbePlaneDetection()
        {
            var started = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.PlaneDetection);
            PXR_MixedReality.GetSenseDataProviderState(PxrSenseDataProviderType.PlaneDetection, out var state);
            PlaneProviderState = started + " / " + state;
            Debug.Log("[MR-SPIKE] plane provider start=" + started + " state=" + state);
            await Task.Delay(1500);
            Debug.Log("[MR-SPIKE] plane arrived=" + (PlaneCount > 0) + " count=" + PlaneCount);
        }

        private void OnPlanes(List<PxrPlaneData> planes)
        {
            if (planes != null) PlaneCount += planes.Count;
            Debug.Log("[MR-SPIKE] planes callback count=" + (planes != null ? planes.Count : 0));
        }

        private void OnGUI()
        {
            var rect = new Rect(8, 8, 320, 208);
            GUI.Box(rect, "");
            var lines = new[]
            {
                "MR spike 调试叠加层（应用层自证）",
                $"透视 VST: {VstState}",
                $"锚点 provider: {AnchorProviderState}",
                $"锚点 create: {AnchorCreateState}",
                $"锚点 persist: {AnchorPersistState}",
                $"定位: {LocateState}  {AnchorPoseText}",
                $"平面 provider: {PlaneProviderState}",
                $"平面 count: {PlaneCount}",
                "平面缺失：沙盘落于占位面（替身，非真实桌面）",
                "uuid: " + (_anchorUuid == Guid.Empty ? "-" : _anchorUuid.ToString("D"))
            };
            var y = rect.y + 6;
            GUI.color = Color.white;
            foreach (var line in lines)
            {
                GUI.Label(new Rect(rect.x + 8, y, rect.width - 16, 18), line);
                y += 19;
            }
        }
    }
}
