using System;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public enum LocomotionMode { Teleport, Smooth, Fixed }
    public enum TurnMode { SnapTurn, SmoothTurn }
    public enum HeightMode { Auto, Sitting, Manual }
    public enum ProneToSicknessAnswer { Prone, NotProne, Uncertain }

    /// <summary>
    /// VR 舒适度与防晕中枢控制器（严格执行防晕人因工程）：
    /// 1. 默认瞬移；平滑移动必须强制绑定暗角（禁止裸跑平滑）。
    /// 2. 身高自适应（自动/坐姿1.2m/手动微调±30cm）。
    /// 3. 一键"我晕了"急救开关（瞬切瞬移+100%暗角+降速）。
    /// 4. 新手首问自动画像配置。
    /// </summary>
    public class LocomotionComfortController : MonoBehaviour
    {
        public const float DefaultSnapAngle = 45f;
        public const float DefaultVignette = 0.60f;
        public const float MinSmoothVignette = 0.30f;
        public const float SittingOffset = -0.45f;

        public LocomotionMode locomotionMode = LocomotionMode.Teleport;
        public TurnMode turnMode = TurnMode.SnapTurn;
        public HeightMode heightMode = HeightMode.Auto;
        public float snapTurnAngle = DefaultSnapAngle;
        public float vignetteIntensity = DefaultVignette;
        public float moveSpeed = 2.0f;
        public float manualHeightOffset = 0f;

        public DirectorLocomotion directorLocomotion;
        public Transform cameraRigOffsetNode;

        public event Action<LocomotionMode> OnLocomotionModeChanged;
        public event Action<float> OnVignetteChanged;
        public event Action OnPanicActivated;

        private void Awake()
        {
            if (directorLocomotion == null)
                directorLocomotion = GetComponentInChildren<DirectorLocomotion>(true);
            ApplySettings();
        }

        public void SetLocomotionMode(LocomotionMode mode)
        {
            locomotionMode = mode;
            if (locomotionMode == LocomotionMode.Smooth && vignetteIntensity < MinSmoothVignette)
            {
                vignetteIntensity = MinSmoothVignette;
            }
            ApplySettings();
            OnLocomotionModeChanged?.Invoke(locomotionMode);
        }

        public void SetVignetteIntensity(float intensity)
        {
            float target = Mathf.Clamp01(intensity);
            if (locomotionMode == LocomotionMode.Smooth && target < MinSmoothVignette)
            {
                target = MinSmoothVignette; // 强制约束：平滑移动必带暗角
            }
            vignetteIntensity = target;
            OnVignetteChanged?.Invoke(vignetteIntensity);
        }

        public void SetSnapTurnAngle(float angle)
        {
            snapTurnAngle = Mathf.Clamp(angle, 15f, 90f);
            ApplySettings();
        }

        public void SetHeightMode(HeightMode mode, float manualOffset = 0f)
        {
            heightMode = mode;
            manualHeightOffset = Mathf.Clamp(manualOffset, -0.30f, 0.30f);
            ApplyHeightOffset();
        }

        public void TriggerPanic()
        {
            locomotionMode = LocomotionMode.Teleport;
            turnMode = TurnMode.SnapTurn;
            snapTurnAngle = DefaultSnapAngle;
            vignetteIntensity = 1.0f;
            moveSpeed = 1.0f;
            ApplySettings();
            OnPanicActivated?.Invoke();
        }

        public void AnswerSicknessQuestion(ProneToSicknessAnswer answer)
        {
            switch (answer)
            {
                case ProneToSicknessAnswer.Prone:
                    locomotionMode = LocomotionMode.Teleport;
                    turnMode = TurnMode.SnapTurn;
                    snapTurnAngle = 45f;
                    vignetteIntensity = 0.80f;
                    moveSpeed = 1.0f;
                    break;
                case ProneToSicknessAnswer.NotProne:
                    locomotionMode = LocomotionMode.Smooth;
                    turnMode = TurnMode.SnapTurn;
                    snapTurnAngle = 45f;
                    vignetteIntensity = 0.40f;
                    moveSpeed = 2.0f;
                    break;
                case ProneToSicknessAnswer.Uncertain:
                default:
                    locomotionMode = LocomotionMode.Teleport;
                    turnMode = TurnMode.SnapTurn;
                    snapTurnAngle = 45f;
                    vignetteIntensity = 0.60f;
                    moveSpeed = 1.5f;
                    break;
            }
            ApplySettings();
        }

        public void ApplySettings()
        {
            if (directorLocomotion != null)
            {
                directorLocomotion.SetContinuousMove(locomotionMode == LocomotionMode.Smooth);
            }
            ApplyHeightOffset();
            OnVignetteChanged?.Invoke(vignetteIntensity);
        }

        private void ApplyHeightOffset()
        {
            if (cameraRigOffsetNode == null) return;
            float targetY = 0f;
            if (heightMode == HeightMode.Sitting) targetY = SittingOffset;
            else if (heightMode == HeightMode.Manual) targetY = manualHeightOffset;

            var p = cameraRigOffsetNode.localPosition;
            cameraRigOffsetNode.localPosition = new Vector3(p.x, targetY, p.z);
        }
    }
}
