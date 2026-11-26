using System;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// ActorNavigator 的 Animator 兼容层。Walking/Running 在本工程是 Float，
    /// 由 ActorLocomotionAnimator 写入；这里只处理真正声明为 Bool 的旧控制器。
    /// </summary>
    public partial class ActorNavigator
    {
        [Header("Legacy Animator Parameters")]
        public string walkingBool = "Walking";
        public string runningBool = "Running";
        public string LastStopReason { get; private set; } = "";

        private void SetAnimatorBooleans(bool walk, bool run)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            SetBoolIfPresent(walkingBool, walk);
            SetBoolIfPresent(runningBool, run);
        }

        private void SetBoolIfPresent(string parameterName, bool value)
        {
            if (string.IsNullOrEmpty(parameterName)) return;
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != parameterName ||
                    parameters[i].type != AnimatorControllerParameterType.Bool) continue;
                animator.SetBool(parameterName, value);
                return;
            }
        }

        public void OnFootstep(AnimationEvent evt) { }
        public void OnLand(AnimationEvent evt) { }
    }
}
