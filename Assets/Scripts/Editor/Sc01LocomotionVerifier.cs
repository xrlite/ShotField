#if UNITY_EDITOR
using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static class Sc01LocomotionVerifier
    {
        private const string ScenePath = BuildAndroid.ScenePath;
        private const int Expected = 10;

        [MenuItem("ShotField/Verifiers/Run SC01 Locomotion Verifier")]
        public static void Run()
        {
            int passed = 0;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            VerifyAssert.Require("main scene loaded cleanly", scene.IsValid(), ref passed);

            var floor = GameObject.Find("Floor") ?? GameObject.Find("Ground");
            var surface = floor != null ? floor.GetComponent<NavMeshSurface>() : null;
            VerifyAssert.Require("NavMeshSurface is baked with valid NavMeshData",
                surface != null && surface.navMeshData != null, ref passed);

            bool sampled = NavMesh.SamplePosition(new Vector3(0f, 0f, 2f), out var hit, 0.5f, NavMesh.AllAreas);
            var path = new NavMeshPath();
            bool hasPath = NavMesh.CalculatePath(new Vector3(-0.60f, 0f, 2.35f), new Vector3(0.10f, 0f, 1.85f), NavMesh.AllAreas, path);
            VerifyAssert.Require("NavMesh walkability and complete path calculation confirmed",
                sampled && hasPath && path.status == NavMeshPathStatus.PathComplete, ref passed);

            var perfA = (Sc01NavMeshBuilder.FindDeep("SC01_Performer_A") ?? Sc01NavMeshBuilder.FindDeep("ActorA"))?.gameObject;
            var navA = perfA != null ? perfA.GetComponent<ActorNavigator>() : null;
            VerifyAssert.Require("Performer A configured with NavMeshAgent, Animator, and ActorNavigator",
                perfA != null && navA != null && navA.agent != null && navA.animator != null, ref passed);

            var perfB = Sc01NavMeshBuilder.FindDeep("SC01_Performer_B")?.gameObject;
            var navB = perfB != null ? perfB.GetComponent<ActorNavigator>() : null;
            VerifyAssert.Require("Performer B configured with NavMeshAgent, Animator, and ActorNavigator",
                perfB != null && navB != null && navB.agent != null && navB.animator != null, ref passed);

            navA?.ConfigureAgent();
            VerifyAssert.Require("Stride calibration: Walk ~1.35 m/s, Run ~3.80 m/s with decoupled smooth turning",
                navA != null && Mathf.Approximately(navA.walkSpeed, 1.35f) &&
                Mathf.Approximately(navA.runSpeed, 3.80f) && !navA.agent.updateRotation, ref passed);

            navA.OnFootstep(null);
            navA.OnLand(null);
            VerifyAssert.Require("ActorNavigator animation event receivers and public API verified",
                !navA.IsMoving && navA.RemainingDistance >= 0f, ref passed);

            var matA = perfA.GetComponentInChildren<Renderer>()?.sharedMaterial;
            var matB = perfB.GetComponentInChildren<Renderer>()?.sharedMaterial;
            Color colA = matA != null && matA.HasProperty("_BaseColor") ? matA.GetColor("_BaseColor") : Color.black;
            Color colB = matB != null && matB.HasProperty("_BaseColor") ? matB.GetColor("_BaseColor") : Color.black;
            VerifyAssert.Require("Previs color materials wired: Protagonist Red (#FF0000), Antagonist Blue (#0055FF)",
                colA.r > 0.8f && colA.b < 0.2f && colB.b > 0.8f && colB.r < 0.2f, ref passed);

            var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Studio/Materials/MAT_STU_Wall_v05.mat");
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Studio/Materials/MAT_STU_Floor_v05.mat");
            Color wallCol = wallMat != null ? wallMat.GetColor("_BaseColor") : Color.black;
            Color floorCol = floorMat != null ? floorMat.GetColor("_BaseColor") : Color.black;
            VerifyAssert.Require("Stage two-tier gray wired: Wall #D8D8D8 and Floor #9A9A9A",
                Mathf.Abs(wallCol.r - 0.847f) < 0.05f && Mathf.Abs(floorCol.r - 0.604f) < 0.05f, ref passed);

            bool metaIntact = true;
            bool noLineOverflow = true;
            foreach (var f in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (!File.Exists(f + ".meta")) metaIntact = false;
                if (File.ReadAllLines(f).Length > 200) noLineOverflow = false;
            }
            bool workerFilesWithin195 =
                File.ReadAllLines("Assets/Scripts/RuntimeUI/ActorNavigator.cs").Length <= 195 &&
                File.ReadAllLines("Assets/Scripts/Editor/Sc01NavMeshBuilder.cs").Length <= 195 &&
                File.ReadAllLines("Assets/Scripts/Editor/Sc01LocomotionVerifier.cs").Length <= 195;

            VerifyAssert.Require("Codebase health: all .meta intact, 0 files > 200 lines, worker files <= 195 lines",
                metaIntact && noLineOverflow && workerFilesWithin195, ref passed);

            VerifyAssert.Finish("LOCOMOTION", passed, Expected);
        }
    }
}
#endif
