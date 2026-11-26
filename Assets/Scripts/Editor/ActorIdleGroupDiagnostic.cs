#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    /// <summary>六人 Idle 隔离诊断：确认非 T-pose、驱动齐全、Idle 相位错开。</summary>
    public sealed class ActorIdleGroupDiagnostic
    {
        private const string OutputDir = "F:/pico/05_验证记录/2026-11/actor_idle_group_20261125";
        private static Camera _camera;
        private static GameObject[] _actors;
        private static bool _done;
        private static int _exitCode;

        [MenuItem("ShotField/Diagnostics/Run Six Actor Idle Group")]
        public static void Run()
        {
            Directory.CreateDirectory(OutputDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ActorMotionDiagnostic.CreateLightingAndFloor(scene);
            _camera = ActorMotionDiagnostic.CreateDiagnosticCamera();
            _camera.transform.position = new Vector3(0f, 1.25f, -6.5f);
            _camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            _camera.fieldOfView = 45f;
            string[] prefabs =
            {
                "ActorA_Red", "ActorB_Blue", "ActorC_Yellow",
                "ActorD_Green", "ActorE_Violet", "ActorF_Magenta"
            };
            _actors = new GameObject[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                var actor = ActorMotionDiagnostic.CreateActor("Assets/Prefabs/SC01/" + prefabs[i] + ".prefab");
                actor.name = "IdleGroup_" + prefabs[i];
                actor.transform.position = new Vector3((i - 2.5f) * 0.95f, 0f, 0f);
                actor.transform.rotation = Quaternion.LookRotation(Vector3.back);
                _actors[i] = actor;
            }
            _done = false;
            _exitCode = 1;
            EditorApplication.isPlaying = true;
            EditorApplication.update += WatchPlayMode;
        }

        private static void WatchPlayMode()
        {
            if (!EditorApplication.isPlaying || _done) return;
            new GameObject("IdleGroupRunner").AddComponent<IdleGroupRunner>();
            EditorApplication.update -= WatchPlayMode;
            EditorApplication.update += WatchDone;
        }

        private static void WatchDone()
        {
            if (!_done) return;
            EditorApplication.update -= WatchDone;
            EditorApplication.Exit(_exitCode);
        }

        private sealed class IdleGroupRunner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                yield return new WaitForSeconds(1.5f);
                ActorMotionDiagnostic.CaptureTo(_camera, Path.Combine(OutputDir, "六人Idle_相位验证.png"));
                var lines = new List<string> { "ACTOR-IDLE-GROUP-DIAGNOSTIC", "PRODUCT_CAMERAS=0" };
                bool allIdle = true;
                bool allDrivers = true;
                var phases = new List<float>();
                foreach (var actor in _actors)
                {
                    var animator = actor.GetComponentInChildren<Animator>();
                    var driver = actor.GetComponentInChildren<ActorLocomotionAnimator>();
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    bool idle = state.shortNameHash == Animator.StringToHash("Idle");
                    allIdle &= idle;
                    allDrivers &= driver != null;
                    if (idle) phases.Add(state.normalizedTime);
                    lines.Add(actor.name + " state=" + (idle ? "Idle" : "Other") +
                        " phase=" + state.normalizedTime.ToString("F3") +
                        " driver=" + (driver != null) +
                        " controller=" + animator.runtimeAnimatorController.name);
                }
                int distinct = phases.Select(p => Mathf.RoundToInt(p * 100f)).Distinct().Count();
                bool pass = allIdle && allDrivers && distinct >= 4;
                lines.Add("ALL_IDLE=" + allIdle);
                lines.Add("ALL_DRIVERS=" + allDrivers);
                lines.Add("DISTINCT_PHASES=" + distinct);
                lines.Add("RESULT=" + (pass ? "PASS" : "FAIL"));
                File.WriteAllLines(Path.Combine(OutputDir, "actor_idle_group_report.txt"), lines, new UTF8Encoding(false));
                Debug.Log("ACTOR-IDLE-GROUP-RESULT=" + (pass ? "PASS" : "FAIL") +
                    " allIdle=" + allIdle + " allDrivers=" + allDrivers + " distinct=" + distinct);
                _done = true;
                _exitCode = pass ? 0 : 1;
                EditorApplication.isPlaying = false;
            }
        }
    }
}
#endif
