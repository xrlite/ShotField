using System;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Humanoid 3 点 VR IK 驱动器：通过 Unity 原生 OnAnimatorIK 驱动头部注视与双手 IK。
    /// 下肢由水平位移速度自动驱动 SC01_ActorMotion 的 Idle/Walk 融合，避免滑步。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HumanoidThreePointIkDriver : MonoBehaviour
    {
        [Header("IK 状态开关")]
        public bool ikActive = false;
        [Range(0f, 1f)] public float lookAtWeight = 1.0f;
        [Range(0f, 1f)] public float leftHandWeight = 1.0f;
        [Range(0f, 1f)] public float rightHandWeight = 1.0f;

        [Header("实时空间位姿（世界坐标）")]
        public Vector3 headPosition;
        public Vector3 leftHandPosition;
        public Quaternion leftHandRotation = Quaternion.identity;
        public Vector3 rightHandPosition;
        public Quaternion rightHandRotation = Quaternion.identity;

        private Animator _animator;
        private Vector3 _lastRootPos;

        public Animator Animator => _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _lastRootPos = transform.position;
        }

        private void Update()
        {
            if (_animator == null) return;
            float dt = Time.deltaTime;
            if (dt > 0.0001f)
            {
                var delta = transform.position - _lastRootPos;
                delta.y = 0f;
                float speed = delta.magnitude / dt;
                _animator.SetFloat("Speed", Mathf.Clamp01(speed / 2.0f));
            }
            _lastRootPos = transform.position;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || !ikActive) return;

            if (lookAtWeight > 0.001f)
            {
                _animator.SetLookAtWeight(lookAtWeight, 0.3f, 0.8f, 0.5f, 0.5f);
                _animator.SetLookAtPosition(headPosition);
            }

            if (leftHandWeight > 0.001f)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftHandWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandPosition);
                _animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandRotation);
            }

            if (rightHandWeight > 0.001f)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightHandWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandPosition);
                _animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandRotation);
            }
        }

        /// <summary>
        /// 从录制关键帧还原 1:1 表演。
        /// </summary>
        public void ApplyFrame(PerformancePoseFrame frame)
        {
            transform.position = frame.rootPosition;
            transform.rotation = frame.rootRotation;

            headPosition = frame.headPosition;
            leftHandPosition = frame.leftHandPosition;
            leftHandRotation = frame.leftHandRotation;
            rightHandPosition = frame.rightHandPosition;
            rightHandRotation = frame.rightHandRotation;

            ikActive = true;
        }

        /// <summary>
        /// 便捷配置：以当前 Transform 与相对偏移初始化默认测试机位。
        /// </summary>
        public void SetPoseDirect(Vector3 head, Vector3 lHand, Quaternion lRot, Vector3 rHand, Quaternion rRot)
        {
            headPosition = head;
            leftHandPosition = lHand;
            leftHandRotation = lRot;
            rightHandPosition = rHand;
            rightHandRotation = rRot;
            ikActive = true;
        }
    }
}
