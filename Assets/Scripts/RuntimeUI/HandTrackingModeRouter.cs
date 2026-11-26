using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Subsystems;
using UnityEngine.XR.Hands;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// PICO OS 6 眼手模式兜底：XR Hands 报到手部已跟踪时，优先启用手部射线。
    /// XRInputModalityManager 在模拟器仍暴露控制器设备时可能一直选手柄，导致捏合命中不了应用 UI。
    /// 本组件只在手部确实跟踪时接管对应侧，不干预普通手柄模式。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HandTrackingModeRouter : MonoBehaviour
    {
        private static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();
        private XRHandSubsystem _hands;
        private bool _leftHand;
        private bool _rightHand;

        public GameObject leftController;
        public GameObject rightController;
        public GameObject leftHand;
        public GameObject rightHand;

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
            if (left != _leftHand)
            {
                _leftHand = left;
                SetActive(leftController, !left);
                SetActive(leftHand, left);
                Debug.Log($"[InputModeRouter] Left={(left ? "Hand" : "Controller")} tracked={left}");
            }
            if (right != _rightHand)
            {
                _rightHand = right;
                SetActive(rightController, !right);
                SetActive(rightHand, right);
                Debug.Log($"[InputModeRouter] Right={(right ? "Hand" : "Controller")} tracked={right}");
            }
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
