using UnityEngine;
using UnityEngine.AI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 角色移动动画驱动：让"走过去"这件事真的播走路动画，而不是整体平移。
    ///
    /// 背景（实测）：`SC01_ActorMotion.controller` 的参数名是 `Walking` / `Running`（Float），
    /// 但旧代码写的是 `SetFloat("Speed")` —— 参数名不存在，Unity 只报 warning 不报错，
    /// 于是 Animator 永远停在 Idle：人物滑着走、看着像木偶。
    ///
    /// 本组件按"实际存在的参数名"逐一套用（没有就跳过，不刷 warning），速度取自
    /// NavMeshAgent.velocity（有代理时）或位移差分（无代理时）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class ActorLocomotionAnimator : MonoBehaviour
    {
        [Header("速度换算")]
        [Tooltip("Walk 动画满速对应的移动速度（m/s）")]
        public float walkSpeed = 0.9f;
        [Tooltip("Run 动画满速对应的移动速度（m/s）")]
        public float runSpeed = 2.6f;
        [Tooltip("低于该速度视为静止（m/s）")]
        public float idleThreshold = 0.05f;
        [Tooltip("参数平滑时间，避免动画抖动")]
        public float damping = 0.12f;

        private Animator _animator;
        private NavMeshAgent _agent;
        private Vector3 _lastPos;

        private bool _hasWalking, _hasRunning, _hasSpeed, _hasIsWalking, _hasSpeed01;
        private bool _hasWalkingBool, _hasRunningBool;
        private float _walk01, _run01, _speed01;
        private float _walkVel, _runVel, _speedVel;
        private bool _wasMoving;

        public float CurrentSpeed { get; private set; }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _agent = GetComponent<NavMeshAgent>();
            _lastPos = transform.position;

            // 关闭根运动：位移由 NavMeshAgent / 调用方负责，开了会被动画再推一遍，造成漂移。
            if (_animator != null && _animator.applyRootMotion)
            {
                _animator.applyRootMotion = false;
                Debug.Log("[ActorLocomotionAnimator] 已关闭 applyRootMotion（位移交给 NavMeshAgent）。", this);
            }

            _hasWalking = HasParam("Walking", AnimatorControllerParameterType.Float);
            _hasRunning = HasParam("Running", AnimatorControllerParameterType.Float);
            _hasSpeed = HasParam("Speed", AnimatorControllerParameterType.Float);
            _hasSpeed01 = HasParam("Speed01", AnimatorControllerParameterType.Float);
            _hasIsWalking = HasParam("IsWalking", AnimatorControllerParameterType.Bool);
            _hasWalkingBool = HasParam("Walking", AnimatorControllerParameterType.Bool);
            _hasRunningBool = HasParam("Running", AnimatorControllerParameterType.Bool);

            if (!_hasWalking && !_hasRunning && !_hasSpeed && !_hasSpeed01 &&
                !_hasWalkingBool && !_hasRunningBool)
            {
                Debug.LogWarning("[ActorLocomotionAnimator] 控制器里找不到 Walking/Running/Speed 任一参数，" +
                                 "请检查 Animator Controller 的参数名。", this);
            }
            if (HasState("Idle")) _animator.Play("Idle", 0, Random.Range(0f, 1f));
        }

        private bool HasState(string stateName)
        {
            return _animator != null && _animator.HasState(0, Animator.StringToHash(stateName));
        }

        private bool HasParam(string name, AnimatorControllerParameterType type)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return false;
            var ps = _animator.parameters;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].type == type && ps[i].name == name) return true;
            return false;
        }

        private void Update()
        {
            if (_animator == null) return;

            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            float measured = dt > 0.0001f ? new Vector2(pos.x - _lastPos.x, pos.z - _lastPos.z).magnitude / dt : 0f;
            _lastPos = pos;

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                var v = _agent.velocity;
                measured = Mathf.Max(measured, new Vector2(v.x, v.z).magnitude);
            }

            CurrentSpeed = measured;
            bool moving = measured > idleThreshold;
            if (_wasMoving && !moving && HasState("Idle"))
                _animator.Play("Idle", 0, Random.Range(0f, 1f));
            _wasMoving = moving;

            float targetWalk = moving ? Mathf.Clamp01(measured / Mathf.Max(0.01f, walkSpeed)) : 0f;
            float targetRun = measured > walkSpeed + 0.05f ? Mathf.Clamp01((measured - walkSpeed) / Mathf.Max(0.01f, runSpeed - walkSpeed)) : 0f;
            float targetSpeed01 = moving ? Mathf.Clamp01(measured / Mathf.Max(0.01f, runSpeed)) : 0f;

            _walk01 = Mathf.SmoothDamp(_walk01, targetWalk, ref _walkVel, damping);
            _run01 = Mathf.SmoothDamp(_run01, targetRun, ref _runVel, damping);
            _speed01 = Mathf.SmoothDamp(_speed01, targetSpeed01, ref _speedVel, damping);

            if (_hasWalking) _animator.SetFloat("Walking", _walk01);
            if (_hasRunning) _animator.SetFloat("Running", _run01);
            if (_hasSpeed) _animator.SetFloat("Speed", _speed01);
            if (_hasSpeed01) _animator.SetFloat("Speed01", _speed01);
            if (_hasIsWalking) _animator.SetBool("IsWalking", moving);
        }

        /// <summary>供验证器/调试读出的诊断串。</summary>
        public string Describe() =>
            "speed=" + CurrentSpeed.ToString("F2") + "m/s walk01=" + _walk01.ToString("F2") +
            " run01=" + _run01.ToString("F2") +
            " params[Walking=" + _hasWalking + " Running=" + _hasRunning + " Speed=" + _hasSpeed +
            " WalkingBool=" + _hasWalkingBool + " RunningBool=" + _hasRunningBool + "]";
    }
}
