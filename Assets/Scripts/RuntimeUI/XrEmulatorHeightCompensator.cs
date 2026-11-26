using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// PICO Emulator reports controller/hand poses floor-absolute while the headset pose is
    /// offset-relative, so poses under XR_CameraOffset double-count CameraYOffset (hand at ~3.3m).
    /// On x86 emulator builds only, remove that offset after TrackedPoseDriver updates.
    /// Real devices are untouched.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class XrEmulatorHeightCompensator : MonoBehaviour
    {
        public float offset = 1.65f;
        private static bool? _isEmulator;

        private static bool IsEmulator
        {
            get
            {
                if (!_isEmulator.HasValue)
                    _isEmulator = SystemInfo.deviceModel.Contains("x86") || SystemInfo.processorType.Contains("x86");
                return _isEmulator.Value;
            }
        }

        private void LateUpdate()
        {
            if (Application.isEditor || !IsEmulator) return;
            var p = transform.localPosition;
            if (p.y > offset * 0.9f)
            {
                p.y -= offset;
                transform.localPosition = p;
            }
        }
    }
}
