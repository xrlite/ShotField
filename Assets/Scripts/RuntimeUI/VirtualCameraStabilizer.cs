using UnityEngine;

namespace ShotField.RuntimeUI
{
    public enum CameraStabilizerMode { Steadicam, TripodLocked, HandheldNatural }

    /// <summary>
    /// 虚拟摄影机防抖中介层：好莱坞级双阶平滑（One Euro 滤波 + 物理阻尼）与地平线校平。
    /// 消除手柄微颤与镜头歪斜，保障运镜沉稳顺滑。
    /// </summary>
    [ExecuteAlways]
    public class VirtualCameraStabilizer : MonoBehaviour
    {
        [Header("稳定模式")]
        public CameraStabilizerMode mode = CameraStabilizerMode.Steadicam;
        public Transform rawPoseSource;
        public bool lockHorizon = true;

        [Header("滤波参数")]
        [Range(0.01f, 0.5f)] public float smoothTime = 0.18f;
        [Range(0.1f, 5.0f)] public float minCutoff = 1.0f;
        [Range(0.001f, 1.0f)] public float beta = 0.05f;
        public float dCutoff = 1.0f;

        private Vector3 _currentVelocity;
        private Vector3 _filteredPos;
        private Vector3 _prevRawPos;
        private Vector3 _filteredDPos;
        private Quaternion _filteredRot = Quaternion.identity;
        private Quaternion _prevRawRot = Quaternion.identity;
        private float _filteredDRot;
        private bool _initialized;
        private Vector3 _tripodAnchorPos;
        private CameraStabilizerMode _prevMode = CameraStabilizerMode.Steadicam;

        public void ResetFilters(Vector3 pos, Quaternion rot)
        {
            _filteredPos = _prevRawPos = pos;
            _filteredDPos = Vector3.zero;
            _filteredRot = _prevRawRot = rot;
            _filteredDRot = 0f;
            _currentVelocity = Vector3.zero;
            _tripodAnchorPos = pos;
            _initialized = true;
            transform.SetPositionAndRotation(pos, rot);
        }

        public Vector3 FilterPosition(Vector3 raw, float dt)
        {
            if (!_initialized || dt <= 0.0001f) { ResetFilters(raw, _filteredRot); return raw; }
            Vector3 dPos = (raw - _prevRawPos) / dt;
            _prevRawPos = raw;
            float alphaD = CalculateAlpha(dt, dCutoff);
            _filteredDPos = Vector3.Lerp(_filteredDPos, dPos, alphaD);
            float speed = _filteredDPos.magnitude;
            float cutoff = minCutoff + beta * speed;
            float alpha = CalculateAlpha(dt, cutoff);
            _filteredPos = Vector3.Lerp(_filteredPos, raw, alpha);
            return _filteredPos;
        }

        public Quaternion FilterRotation(Quaternion raw, float dt)
        {
            if (!_initialized || dt <= 0.0001f) { ResetFilters(_filteredPos, raw); return raw; }
            float angleDelta = Quaternion.Angle(_prevRawRot, raw) / dt;
            _prevRawRot = raw;
            float alphaD = CalculateAlpha(dt, dCutoff);
            _filteredDRot = Mathf.Lerp(_filteredDRot, angleDelta, alphaD);
            float cutoff = minCutoff + beta * _filteredDRot;
            float alpha = CalculateAlpha(dt, cutoff);
            _filteredRot = Quaternion.Slerp(_filteredRot, raw, alpha);
            return _filteredRot;
        }

        private static float CalculateAlpha(float dt, float cutoff)
        {
            float tau = 1.0f / (2.0f * Mathf.PI * Mathf.Max(cutoff, 0.0001f));
            return 1.0f / (1.0f + tau / dt);
        }

        public Quaternion ApplyHorizonLock(Quaternion rot)
        {
            Vector3 forward = rot * Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f) return rot;
            Vector3 horiz = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (horiz.sqrMagnitude < 1e-4f)
            {
                Vector3 camUp = rot * Vector3.up;
                horiz = Vector3.ProjectOnPlane(camUp, Vector3.up);
                if (horiz.sqrMagnitude < 1e-4f) horiz = Vector3.forward;
            }
            float clampedY = Mathf.Clamp(forward.y, -0.999f, 0.999f);
            float horizLen = Mathf.Sqrt(Mathf.Max(0.0001f, 1f - clampedY * clampedY));
            Vector3 safeForward = (horiz.normalized * horizLen + Vector3.up * clampedY).normalized;
            return Quaternion.LookRotation(safeForward, Vector3.up);
        }

        void LateUpdate()
        {
            if (rawPoseSource == null) return;
            Vector3 rawPos = rawPoseSource.position;
            Quaternion rawRot = rawPoseSource.rotation;
            float dt = Mathf.Max(Time.deltaTime, 0.001f);

            if (!_initialized) ResetFilters(rawPos, rawRot);

            Vector3 fPos = FilterPosition(rawPos, dt);
            Quaternion fRot = FilterRotation(rawRot, dt);

            Vector3 finalPos;
            if (mode == CameraStabilizerMode.TripodLocked)
            {
                if (_prevMode != CameraStabilizerMode.TripodLocked) _tripodAnchorPos = transform.position;
                finalPos = _tripodAnchorPos;
                _currentVelocity = Vector3.zero;
            }
            else if (mode == CameraStabilizerMode.HandheldNatural)
            {
                finalPos = fPos;
                _currentVelocity = Vector3.zero;
            }
            else // Steadicam
            {
                finalPos = Vector3.SmoothDamp(transform.position, fPos, ref _currentVelocity, smoothTime, Mathf.Infinity, dt);
            }

            if (lockHorizon) fRot = ApplyHorizonLock(fRot);
            transform.SetPositionAndRotation(finalPos, fRot);
            _prevMode = mode;
        }
    }
}
