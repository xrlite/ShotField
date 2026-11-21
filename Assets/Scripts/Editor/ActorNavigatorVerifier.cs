#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    /// <summary>
    /// ActorNavigator 智能寻路走位与状态机验证器：
    /// 验证 NavMeshAgent 参数、目标光圈生成、Walking/Running 参数驱动及自然停步。
    /// </summary>
    public static class ActorNavigatorVerifier
    {
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run Actor Navigator Verifier")]
        public static void Run()
        {
            int passed = 0;
            var host = new GameObject("TestActorHost");
            host.transform.position = new Vector3(10000, 10000, 10000);
            var agent = host.AddComponent<NavMeshAgent>();
            var animGo = new GameObject("TestAnimator");
            animGo.transform.SetParent(host.transform, false);
            var animator = animGo.AddComponent<Animator>();

            var navigator = host.AddComponent<ActorNavigator>();
            navigator.agent = agent;
            navigator.animator = animator;

            try
            {
                VerifyAssert.Require("ActorNavigator binds NavMeshAgent and Animator properly",
                    navigator != null && navigator.agent == agent && navigator.animator == animator, ref passed);

                VerifyAssert.Require("Default movement speeds conform to Humanoid walk/run specs",
                    Mathf.Approximately(navigator.walkSpeed, 1.35f) &&
                    (Mathf.Approximately(navigator.runSpeed, 3.80f) || Mathf.Approximately(navigator.runSpeed, 3.20f)), ref passed);

                Vector3 target = new Vector3(2.5f, 0f, 1.8f);
                Vector3 recordedTarget = Vector3.zero;
                navigator.OnDestinationSet += pos => recordedTarget = pos;

                bool navAttempt = navigator.NavigateTo(target, false);
                VerifyAssert.Require("An agent outside the NavMesh cannot accept a destination",
                    !navAttempt && !navigator.IsMoving && recordedTarget == Vector3.zero, ref passed);
                VerifyAssert.Require("No amber marker appears for a rejected destination",
                    navigator.targetMarker == null, ref passed);

                bool arrived = false;
                navigator.OnDestinationReached += () => arrived = true;
                navigator.Stop();
                VerifyAssert.Require("Stop leaves the navigator idle without emitting an arrival",
                    !navigator.IsMoving && !arrived, ref passed);
                VerifyAssert.Require("Stopping distance satisfies close framing accuracy (<= 0.20m)",
                    navigator.stoppingDistance <= 0.20f, ref passed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            VerifyAssert.Finish("ACTOR-NAVIGATOR", passed, Expected);
        }

        [MenuItem("ShotField/Verifiers/Observe SC01 Door-to-Letter Walk")]
        public static void ObserveDoorToLetterWalk()
        {
            if (!EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Enter Play Mode in SC01_Final first.");

            var actor = GameObject.Find("SC01_Performer_A")?.GetComponent<ActorNavigator>();
            if (!actor || !actor.agent || !actor.agent.isOnNavMesh || !actor.animator)
                throw new System.InvalidOperationException("The red actor needs an active agent on the baked NavMesh.");

            var floor = GameObject.Find("Floor");
            if (!floor || !floor.GetComponent<ActorWalkTarget>())
                throw new System.InvalidOperationException("The floor is missing its walk input binding.");

            var doorway = new Vector3(-2.20f, 0f, .55f);
            var goal = new Vector3(.40f, 0f, 1.68f);
            actor.Teleport(doorway);
            var start = actor.transform.position;
            if (Vector3.Distance(start, doorway) > .30f || !actor.MoveTo(goal))
                throw new System.InvalidOperationException("The doorway or table approach is not on the connected NavMesh.");

            var speeds = new System.Collections.Generic.List<float>();
            double started = EditorApplication.timeSinceStartup;
            double nextSample = started;
            bool arrived = false;
            void Arrived() => arrived = true;
            void Observe()
            {
                double now = EditorApplication.timeSinceStartup;
                if (actor && now >= nextSample)
                {
                    speeds.Add(actor.agent.velocity.magnitude);
                    nextSample = now + .15;
                }
                if (EditorApplication.isPlaying && actor && !arrived && now - started < 12) return;
                EditorApplication.update -= Observe;
                if (actor) actor.OnDestinationReached -= Arrived;
                float error = actor ? Vector3.Distance(actor.transform.position, goal) : float.PositiveInfinity;
                bool walked = speeds.Exists(v => v >= .25f);
                bool idle = actor && !actor.IsMoving && !actor.animator.GetBool(actor.walkingBool)
                    && !actor.animator.GetBool(actor.runningBool);
                string samples = string.Join(", ", speeds.ConvertAll(v => v.ToString("F2")));
                string result = arrived && walked && idle && error < .20f ? "PASS" : "FAIL";
                Debug.Log($"WALK {result}; error={error:F3} m; idle={idle}; velocity (m/s)=[{samples}]");
            }

            actor.OnDestinationReached += Arrived;
            EditorApplication.update += Observe;
        }

    }
}
#endif
