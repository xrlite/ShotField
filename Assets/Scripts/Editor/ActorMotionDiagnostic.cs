#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    /// <summary>隔离演员步态诊断：只留单演员、地面、A/B 标记和取证相机。</summary>
    public static partial class ActorMotionDiagnostic
    {
        private const string OutputDir = "F:/pico/05_验证记录/2026-11/actor_motion_20261125";
        private static Camera _camera;
        private static GameObject _actor;
        private static Animator _animator;
        private static NavMeshAgent _agent;
        private static ActorNavigator _navigator;
        private static readonly List<object> Samples = new List<object>();
        private static bool _done;
        private static int _exitCode;

        [MenuItem("ShotField/Diagnostics/Run Isolated Actor Motion")]
        public static void Run()
        {
            Directory.CreateDirectory(OutputDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLightingAndFloor(scene);
            _camera = CreateDiagnosticCamera();
            _actor = CreateActor();
            CreateMarker("A", new Vector3(-1f, 0.01f, 0f), new Color(0.2f, 0.8f, 0.3f));
            CreateMarker("B", new Vector3(1f, 0.01f, 0f), new Color(0.9f, 0.25f, 0.2f));
            _animator = _actor.GetComponentInChildren<Animator>();
            _agent = _actor.GetComponent<NavMeshAgent>();
            _navigator = _actor.GetComponent<ActorNavigator>();
            Samples.Clear();
            _done = false;
            _exitCode = 1;
            WriteClipInventory();
            EditorApplication.isPlaying = true;
            EditorApplication.update += WatchPlayMode;
        }

        internal static void CreateLightingAndFloor(UnityEngine.SceneManagement.Scene scene)
        {
            var lightGo = new GameObject("DiagnosticLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "DiagnosticFloor";
            floor.transform.position = Vector3.zero;
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { color = new Color(0.32f, 0.34f, 0.36f) };
            var surface = floor.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();
        }

        internal static Camera CreateDiagnosticCamera()
        {
            var go = new GameObject("DiagnosticCamera_Only");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 1.15f, -3.2f);
            cam.transform.LookAt(new Vector3(0f, 0.85f, 0f));
            cam.fieldOfView = 42f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
            return cam;
        }

        internal static GameObject CreateActor(string prefabPath = "Assets/Prefabs/SC01/ActorA_Red.prefab")
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing Assets/Prefabs/SC01/ActorA_Red.prefab");
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            actor.name = "DiagnosticActor";
            actor.transform.position = new Vector3(-1f, 0f, 0f);
            actor.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var agent = actor.GetComponent<NavMeshAgent>();
            if (agent == null) agent = actor.AddComponent<NavMeshAgent>();
            agent.height = 1.75f;
            agent.radius = 0.3f;
            agent.speed = 1.35f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.12f;
            agent.updateRotation = false;
            var nav = actor.GetComponent<ActorNavigator>();
            if (nav == null) nav = actor.AddComponent<ActorNavigator>();
            nav.agent = agent;
            nav.animator = actor.GetComponentInChildren<Animator>();
            nav.walkSpeed = 1.35f;
            nav.runSpeed = 3.8f;
            nav.turnSpeed = 10f;
            return actor;
        }

        private static void CreateMarker(string name, Vector3 position, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Marker_" + name;
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.22f, 0.01f, 0.22f);
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            go.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                { color = color };
        }

        private static void WatchPlayMode()
        {
            if (!EditorApplication.isPlaying || _done) return;
            new GameObject("ActorMotionRunner").AddComponent<ActorMotionRunner>();
            EditorApplication.update -= WatchPlayMode;
            EditorApplication.update += WatchDone;
        }

        private static void WatchDone()
        {
            if (!_done) return;
            EditorApplication.update -= WatchDone;
            EditorApplication.Exit(_exitCode);
        }

        private sealed class ActorMotionRunner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                Capture("01_A_起点");
                yield return null;
                bool started = _navigator.MoveTo(new Vector3(1f, 0f, 0f), false);
                float elapsed = 0f;
                bool walkSeen = false;
                bool midCaptured = false;
                int frame = 0;
                while (elapsed < 8f && _navigator.IsMoving)
                {
                    elapsed += Time.deltaTime;
                    var state = _animator.GetCurrentAnimatorStateInfo(0);
                    bool isWalk = state.shortNameHash == Animator.StringToHash("Walk");
                    walkSeen |= isWalk;
                    if (isWalk && !midCaptured && elapsed > 0.55f)
                    {
                        Capture("02_B_行走中");
                        midCaptured = true;
                    }
                    if (frame++ % 1 == 0)
                    {
                        Samples.Add(new
                        {
                            t = (float)Math.Round(elapsed, 3),
                            state = isWalk ? "Walk" : "NotWalk",
                            walking = _animator.GetFloat("Walking"),
                            running = _animator.GetFloat("Running"),
                            speedParam = _animator.GetFloat("Speed"),
                            speed = _agent.velocity.magnitude,
                            onNavMesh = _agent.isOnNavMesh,
                            pathPending = _agent.pathPending,
                            pathStatus = _agent.pathStatus.ToString(),
                            hasPath = _agent.hasPath,
                            corners = _agent.path.corners.Length,
                            destination = _agent.destination.ToString("F2"),
                            remaining = _agent.remainingDistance,
                            isMoving = _navigator.IsMoving,
                            stopReason = _navigator.LastStopReason,
                            x = _actor.transform.position.x,
                            leftFootY = _animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,
                            rightFootY = _animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y
                        });
                    }
                    yield return null;
                }
                Capture("03_B_终点");
                float distance = Vector3.Distance(_actor.transform.position, new Vector3(1f, 0f, 0f));
                WriteReport(started, walkSeen, distance, elapsed);
                _done = true;
                _exitCode = started && walkSeen && distance < 0.25f ? 0 : 1;
                EditorApplication.isPlaying = false;
            }
        }
    }
}
#endif
