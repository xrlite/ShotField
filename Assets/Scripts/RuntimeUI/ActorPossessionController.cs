using System;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 1:1 演员附身录制控制器：接替场景角色、根据用户眼高标定身高比例、
    /// 采集 30Hz 头手位姿录制为 ActorPerformanceClip，并支持退出附身后即时还原复拍。
    /// </summary>
    public class ActorPossessionController : MonoBehaviour
    {
        public enum State { Idle, Rehearsing, Recording, Replaying }

        [Header("状态")]
        public State currentState = State.Idle;
        public GameObject possessedActor;
        public float scaleRatio = 1.0f;

        [Header("硬件/虚拟输入源")]
        public Transform headAnchor;
        public Transform leftHandAnchor;
        public Transform rightHandAnchor;

        [Header("当前片段")]
        public ActorPerformanceClip activeClip;

        private HumanoidThreePointIkDriver _ikDriver;
        private float _recordTimer = 0f;
        private float _playbackTimer = 0f;
        private const float SampleInterval = 1f / 30f;

        public bool IsRecording => currentState == State.Recording;
        public bool IsPossessing => possessedActor != null && currentState != State.Idle;

        public void Possess(GameObject actor, Transform head, Transform leftHand, Transform rightHand)
        {
            if (actor == null) return;
            possessedActor = actor;
            headAnchor = head;
            leftHandAnchor = leftHand;
            rightHandAnchor = rightHand;

            _ikDriver = actor.GetComponent<HumanoidThreePointIkDriver>();
            if (_ikDriver == null) _ikDriver = actor.AddComponent<HumanoidThreePointIkDriver>();

            float actorEyeH = 1.6f;
            var anim = actor.GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                var eyeBone = anim.GetBoneTransform(HumanBodyBones.Head);
                if (eyeBone != null) actorEyeH = eyeBone.position.y - actor.transform.position.y;
            }
            float userH = head != null ? head.position.y : 1.6f;
            scaleRatio = Mathf.Clamp(userH / Mathf.Max(0.5f, actorEyeH), 0.7f, 1.4f);

            currentState = State.Rehearsing;
            _ikDriver.ikActive = true;
        }

        public void StartRecording(string clipId = "Perf_01")
        {
            if (possessedActor == null) return;
            activeClip = new ActorPerformanceClip
            {
                clipId = clipId,
                actorId = possessedActor.name,
                scaleRatio = scaleRatio
            };
            _recordTimer = 0f;
            currentState = State.Recording;
        }

        public void StopRecording()
        {
            if (currentState == State.Recording) currentState = State.Idle;
        }

        public void StartReplay(ActorPerformanceClip clip)
        {
            if (possessedActor == null || clip == null || clip.Count == 0) return;
            activeClip = clip;
            _playbackTimer = 0f;
            currentState = State.Replaying;
        }

        public void Unpossess()
        {
            if (_ikDriver != null) _ikDriver.ikActive = false;
            possessedActor = null;
            currentState = State.Idle;
        }

        private void Update()
        {
            if (possessedActor == null || _ikDriver == null) return;

            if (currentState == State.Rehearsing || currentState == State.Recording)
            {
                if (headAnchor != null) _ikDriver.headPosition = headAnchor.position;
                if (leftHandAnchor != null)
                {
                    _ikDriver.leftHandPosition = leftHandAnchor.position;
                    _ikDriver.leftHandRotation = leftHandAnchor.rotation;
                }
                if (rightHandAnchor != null)
                {
                    _ikDriver.rightHandPosition = rightHandAnchor.position;
                    _ikDriver.rightHandRotation = rightHandAnchor.rotation;
                }

                if (currentState == State.Recording && activeClip != null)
                {
                    _recordTimer += Time.deltaTime;
                    if (activeClip.Count == 0 || _recordTimer - activeClip.duration >= SampleInterval)
                    {
                        var root = possessedActor.transform;
                        activeClip.AddFrame(_recordTimer, root.position, root.rotation,
                            _ikDriver.headPosition, Quaternion.identity,
                            _ikDriver.leftHandPosition, _ikDriver.leftHandRotation,
                            _ikDriver.rightHandPosition, _ikDriver.rightHandRotation);
                    }
                }
            }
            else if (currentState == State.Replaying && activeClip != null)
            {
                _playbackTimer += Time.deltaTime;
                if (_playbackTimer > activeClip.duration) _playbackTimer = activeClip.duration;
                var frame = activeClip.Evaluate(_playbackTimer);
                _ikDriver.ApplyFrame(frame);
                if (_playbackTimer >= activeClip.duration) currentState = State.Idle;
            }
        }
    }
}
