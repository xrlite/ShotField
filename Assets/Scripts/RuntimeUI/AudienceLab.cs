using System.Collections.Generic;
using UnityEngine;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    public class AudienceLab : MonoBehaviour
    {
        [System.Serializable]
        public struct GazeSample
        {
            public float time;
            public Vector3 origin;
            public Vector3 forward;
            public string hitTargetId;
        }

        [Header("观众视点")]
        public Transform audienceHead;
        public Camera screeningCamera;

        [Header("试看状态")]
        public bool isScreeningActive = false;
        public float screeningDuration = 0f;
        public List<GazeSample> gazeSamples = new List<GazeSample>();

        [Header("注视统计")]
        public string focusTargetId = "envelope";
        public float focusHitDuration = 0f;

        private float screeningStartTime;
        private float nextSampleTime;

        void Update()
        {
            if (!isScreeningActive) return;
            screeningDuration = Time.time - screeningStartTime;
            if (Time.time >= nextSampleTime)
            {
                RecordGazeSample();
                nextSampleTime = Time.time + 0.1f; // 10Hz 采样率
            }
        }

        public void StartScreeningSession(Camera shotCamera, string keyPropId = "envelope")
        {
            isScreeningActive = true;
            screeningCamera = shotCamera;
            focusTargetId = keyPropId;
            screeningStartTime = Time.time;
            nextSampleTime = screeningStartTime;
            screeningDuration = 0f;
            focusHitDuration = 0f;
            gazeSamples.Clear();
        }

        public void StopScreeningSession()
        {
            isScreeningActive = false;
            ComputeSummary();
        }

        private void RecordGazeSample()
        {
            var head = audienceHead != null ? audienceHead : (screeningCamera != null ? screeningCamera.transform : transform);
            Ray ray = new Ray(head.position, head.forward);
            string hitId = "";

            if (Physics.Raycast(ray, out RaycastHit hit, 50f))
            {
                var marker = hit.collider.GetComponentInParent<SemanticMarker>();
                if (marker != null) hitId = marker.markerId;
            }

            if (!string.IsNullOrEmpty(focusTargetId) && hitId == focusTargetId)
            {
                focusHitDuration += 0.1f;
            }

            gazeSamples.Add(new GazeSample
            {
                time = screeningDuration,
                origin = ray.origin,
                forward = ray.direction,
                hitTargetId = hitId
            });
        }

        public float GetFocusRatio() =>
            screeningDuration > 0.001f ? Mathf.Clamp01(focusHitDuration / screeningDuration) : 0f;

        private void ComputeSummary()
        {
            Debug.Log($"[观众实验室] 试看结束: 总时长 {screeningDuration:F1}s | 焦点道具 [{focusTargetId}] 注视时长 {focusHitDuration:F1}s (占比 {GetFocusRatio() * 100f:F1}%)");
        }
    }
}
