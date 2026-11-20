using System;
using UnityEngine;
#if !UNITY_EDITOR && (ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK)
using ByteDance.PICO.XR;
#endif

namespace ShotField.RuntimeUI
{
    public class SpatialTrackingCoordinator : MonoBehaviour
    {
        [Header("Tracking Extension Switches (Default Strictly False)")]
        [SerializeField] private bool _enableMotionTracker = false;
        [SerializeField] private bool _enableHandTracking = false;
        [SerializeField] private bool _enableEyeTracking = false;

        public bool EnableMotionTracker => _enableMotionTracker;
        public bool EnableHandTracking => _enableHandTracking;
        public bool EnableEyeTracking => _enableEyeTracking;

        public bool IsMotionTrackerActive { get; private set; }
        public bool IsHandTrackingActive { get; private set; }
        public bool IsEyeTrackingActive { get; private set; }

        private void Start()
        {
            InitializeTracking();
        }

        public void Configure(bool motionTracker, bool handTracking, bool eyeTracking)
        {
            _enableMotionTracker = motionTracker;
            _enableHandTracking = handTracking;
            _enableEyeTracking = eyeTracking;
        }

        public void InitializeTracking()
        {
            if (!_enableMotionTracker && !_enableHandTracking && !_enableEyeTracking)
            {
                Debug.Log("[SpatialTrackingCoordinator] All tracking extensions disabled (default safe state). Zero hardware calls.");
                return;
            }

#if !UNITY_EDITOR && (ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK)
            if (_enableMotionTracker) InitMotionTrackerHardware();
            if (_enableHandTracking) InitHandTrackingHardware();
            if (_enableEyeTracking) InitEyeTrackingHardware();
#else
            Debug.Log("[SpatialTrackingCoordinator] Running in Editor or Mock mode. Hardware tracking calls bypassed.");
#endif
        }

#if !UNITY_EDITOR && (ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK)
        private void InitMotionTrackerHardware()
        {
            try
            {
                bool supported = false;
                PXR_MotionTracking.GetBodyTrackingSupported(ref supported);
                if (supported)
                {
                    var bones = new BodyTrackingBoneLength();
                    int res = PXR_MotionTracking.StartBodyTracking(BodyJointSet.BODY_JOINT_SET_BODY_FULL_START, bones);
                    IsMotionTrackerActive = (res == 0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpatialTrackingCoordinator] Body tracking init failed: {ex.Message}");
            }
        }

#if ENABLE_PICO_XR_SDK
        private void InitHandTrackingHardware()
        {
            try
            {
                IsHandTrackingActive = (PXR_HandTracking.GetActiveInputDevice() == ActiveInputDevice.HandTrackingActive);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpatialTrackingCoordinator] Hand tracking init failed: {ex.Message}");
            }
        }
#else
        private void InitHandTrackingHardware()
        {
            Debug.Log("[SpatialTrackingCoordinator] Hand tracking requires ENABLE_PICO_XR_SDK.");
        }
#endif

        private void InitEyeTrackingHardware()
        {
            try
            {
                bool supported = false;
                int modesCount = 0;
                var modes = new EyeTrackingMode[4];
                int ret = PXR_MotionTracking.GetEyeTrackingSupported(ref supported, ref modesCount, modes);
                if (supported && ret == 0)
                {
                    var info = new EyeTrackingStartInfo { mode = EyeTrackingMode.PXR_ETM_BOTH };
                    IsEyeTrackingActive = (PXR_MotionTracking.StartEyeTracking(ref info) == 0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpatialTrackingCoordinator] Eye tracking init failed: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (IsMotionTrackerActive)
            {
                PXR_MotionTracking.StopBodyTracking();
                IsMotionTrackerActive = false;
            }
            if (IsEyeTrackingActive)
            {
                var stopInfo = new EyeTrackingStopInfo();
                PXR_MotionTracking.StopEyeTracking(ref stopInfo);
                IsEyeTrackingActive = false;
            }
        }
#endif
    }
}
