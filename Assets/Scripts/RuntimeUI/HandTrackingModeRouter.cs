using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Subsystems;
using UnityEngine.XR.Hands;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// OS 6 input router: when hands are tracked, the UI ray uses the Eye Gaze pose
    /// (the same target as the system gaze dot) and either-hand pinch confirms it.
    /// Falls back to the hand pointer ray only when eye gaze is unavailable.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class HandTrackingModeRouter : MonoBehaviour
    {
        private static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();
        private XRHandSubsystem _hands;
        private bool _leftHand;
        private bool _rightHand;
        private int _mode;

        public GameObject leftController;
        public GameObject rightController;
        public GameObject leftHand;
        public GameObject rightHand;
        public GameObject gazeRay;

        public bool LeftHandActive => _leftHand;
        public bool RightHandActive => _rightHand;

        private void Update()
        {
            if (_hands == null || !_hands.running)
            {
                SubsystemManager.GetSubsystems(Subsystems);
                _hands = Subsystems.Find(s => s != null && s.running);
            }

            bool left = _hands != null && _hands.leftHand.isTracked;
            bool right = _hands != null && _hands.rightHand.isTracked;
            _leftHand = left;
            _rightHand = right;
            bool gaze = GazeTracked();
            int next = left || right ? (gaze ? 2 : 1) : 0;
            if (next == _mode) return;
            _mode = next;
            SetActive(leftController, next == 0);
            SetActive(rightController, next == 0);
            SetActive(leftHand, next == 1 && left);
            SetActive(rightHand, next == 1 && right);
            SetActive(gazeRay, next == 2);
            Debug.Log($"[InputModeRouter] mode={(next == 2 ? "GazePinch" : next == 1 ? "HandPointer" : "Controller")} left={left} right={right} gaze={gaze}");
        }

        private bool GazeTracked()
        {
            if (gazeRay == null) return false;
            var pose = gazeRay.GetComponent<TrackedPoseDriver>();
            var action = pose != null ? pose.trackingStateInput.action : null;
            return action != null && action.enabled && action.ReadValue<float>() > 0.5f;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
