using System;
using UnityEngine;
using UnityEngine.AI;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Coordinates NavMeshAgent with Animator to achieve realistic actor locomotion.
    /// Scales animation playback using the actual agent velocity.
    /// Implements smooth turning via Slerp and empty animation event receivers.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public partial class ActorNavigator : MonoBehaviour
    {
        [Header("Core References")]
        public NavMeshAgent agent;
        public Animator animator;
        public Transform targetMarker;

        [Header("Stride Calibrated Speeds")]
        public float walkSpeed = 1.35f;
        public float runSpeed = 3.80f;
        public float stoppingDistance = 0.15f;
        public float turnSpeed = 10f;

        [Header("Animator Parameters")]
        public string walkingBool = "Walking";
        public string runningBool = "Running";

        public event Action<Vector3> OnDestinationSet;
        public event Action OnDestinationReached;

        private bool _isMoving;
        private bool _isRunning;
        private NavMeshPath _path;

        public bool IsMoving => _isMoving;
        public float RemainingDistance => (agent != null && agent.isOnNavMesh) ? agent.remainingDistance : 0f;

        private void Awake()
        {
            if (_path == null) _path = new NavMeshPath();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            ConfigureAgent();
        }

        public void ConfigureAgent()
        {
            if (agent == null) return;
            agent.speed = walkSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.acceleration = 8f;
            agent.autoBraking = true;
            agent.updateRotation = false;
        }

        private void Update()
        {
            if (agent == null || !agent.enabled) return;
            UpdateRotation();
            UpdateLocomotion();
        }

        public bool MoveTo(Vector3 destination, bool run = false)
        {
            if (!isActiveAndEnabled || agent == null || !agent.isOnNavMesh) return false;
            if (_path == null) _path = new NavMeshPath();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(destination, out var hit, 2f, filter) ||
                !agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete ||
                !agent.SetPath(_path))
                return false;

            _isRunning = run;
            _isMoving = true;
            agent.speed = run ? runSpeed : walkSpeed;
            SpawnOrUpdateMarker(hit.position);
            RefreshPathLine();
            OnDestinationSet?.Invoke(hit.position);
            return true;
        }

        public bool NavigateTo(Vector3 target, bool run = false) => MoveTo(target, run);

        public void Stop()
        {
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
            _isMoving = false;
            _isRunning = false;
            HideMarker();
            HidePathLine();
            SetAnimatorBooleans(false, false);
            if (animator != null) animator.speed = 1f;
        }

        public void Teleport(Vector3 worldPos)
        {
            if (agent == null || !agent.isOnNavMesh) return;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(worldPos, out var hit, 2f, filter) && agent.Warp(hit.position)) Stop();
        }

        private void UpdateRotation()
        {
            if (!_isMoving || agent == null) return;
            Vector3 desired = agent.desiredVelocity;
            desired.y = 0f;
            if (desired.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(desired.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
            }
        }

        private void UpdateLocomotion()
        {
            if (!_isMoving) return;
            if (!agent.isOnNavMesh || (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete))
            {
                Stop();
                return;
            }
            bool reached = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance &&
                agent.velocity.sqrMagnitude < 0.01f;
            if (reached)
            {
                Stop();
                OnDestinationReached?.Invoke();
                return;
            }

            Vector3 vel = agent.velocity;
            vel.y = 0f;
            float currentSpeed = vel.magnitude;
            bool isMoving = currentSpeed > 0.05f;
            bool isRunning = _isRunning && currentSpeed > (walkSpeed * 0.85f);

            SetAnimatorBooleans(isMoving && !isRunning, isMoving && isRunning);

            if (animator != null)
            {
                if (isMoving)
                {
                    float calibratedSpeed = isRunning ? runSpeed : walkSpeed;
                    animator.speed = Mathf.Clamp(currentSpeed / calibratedSpeed, 0.5f, 1.5f);
                }
                else
                {
                    animator.speed = 1f;
                }
            }
        }

        private void SetAnimatorBooleans(bool walk, bool run)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (!string.IsNullOrEmpty(walkingBool)) animator.SetBool(walkingBool, walk);
            if (!string.IsNullOrEmpty(runningBool)) animator.SetBool(runningBool, run);
        }

        private void SpawnOrUpdateMarker(Vector3 pos)
        {
            if (targetMarker == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "AmberTargetMarker";
                go.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                    var mat = new Material(shader) { color = PrevisPalette.AmberTrajectory };
                    rend.material = mat;
                }
                targetMarker = go.transform;
            }
            targetMarker.position = pos + Vector3.up * 0.01f;
            targetMarker.gameObject.SetActive(true);
        }

        private void HideMarker()
        {
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        }

        public void OnFootstep(AnimationEvent evt) { }
        public void OnLand(AnimationEvent evt) { }
    }
}
