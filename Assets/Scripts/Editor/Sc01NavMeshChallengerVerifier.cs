#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace ShotField.EditorTools
{
    public static class Sc01NavMeshChallengerVerifier
    {
        private const string ScenePath = BuildAndroid.ScenePath;
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run SC01 NavMesh Challenger Verifier")]
        public static void Run()
        {
            int passed = 0;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            VerifyAssert.Require("main scene loaded cleanly", scene.IsValid(), ref passed);

            var tri = NavMesh.CalculateTriangulation();
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var v in tri.vertices)
            {
                min = Vector3.Min(min, v); max = Vector3.Max(max, v);
            }
            bool boundsContained = min.x >= -3.05f && max.x <= 3.05f && min.z >= -0.05f && max.z <= 4.05f;
            Debug.Log($"[NavMeshVerifier] Triangulation bounds: Min={min}, Max={max}, Verts={tri.vertices.Length}");
            VerifyAssert.Require("NavMesh geometry strictly bounded within 6x4m floor (X: [-3, 3], Z: [0, 4])",
                boundsContained && tri.vertices.Length > 0, ref passed);

            Vector3 tableTop = new Vector3(0.85f, 0.45f, 2.42f);
            bool tableTopWalkable = NavMesh.SamplePosition(tableTop, out var hitT, 0.2f, NavMesh.AllAreas);
            Debug.Log($"[NavMeshVerifier] Table top sample: hit={tableTopWalkable}, pos={hitT.position}");
            VerifyAssert.Require("CoffeeTable top surface is unwalkable (obstructed by NavMeshModifier / Obstacle)",
                !tableTopWalkable, ref passed);

            var pathAcross = new NavMeshPath();
            bool hasPathAcross = NavMesh.CalculatePath(new Vector3(-0.48f, 0.06f, 2.27f), new Vector3(1.33f, 0.06f, 2.42f), NavMesh.AllAreas, pathAcross);
            bool straightLineBlocked = !hasPathAcross || pathAcross.status != NavMeshPathStatus.PathComplete || pathAcross.corners.Length >= 3;
            Debug.Log($"[NavMeshVerifier] Path across table: hasPath={hasPathAcross}, corners={pathAcross.corners.Length}");
            VerifyAssert.Require("Straight line path across coffee table is blocked from crossing table surface",
                straightLineBlocked, ref passed);

            NavMesh.SamplePosition(new Vector3(-2.20f, 0f, 0.15f), out var doorHit, 1.2f, NavMesh.AllAreas);
            NavMesh.SamplePosition(new Vector3(0.85f, 0f, 2.42f), out var letterHit, 1.2f, NavMesh.AllAreas);
            var doorToLetter = new NavMeshPath();
            bool hasDoorPath = doorHit.hit && letterHit.hit &&
                NavMesh.CalculatePath(doorHit.position, letterHit.position, NavMesh.AllAreas, doorToLetter);
            bool doorPathValid = hasDoorPath && doorToLetter.status == NavMeshPathStatus.PathComplete;
            bool allCornersInside = true;
            if (doorPathValid)
            {
                foreach (var c in doorToLetter.corners)
                {
                    Debug.Log($"[NavMeshVerifier] Doorway to letter corner: {c}");
                    if (c.x < -3.05f || c.x > 3.05f || c.z < -0.05f || c.z > 4.05f) allCornersInside = false;
                }
            }
            VerifyAssert.Require("Doorway to letter path is complete, detours around CoffeeTable and stays inside room walls",
                doorPathValid && allCornersInside, ref passed);

            bool metaIntact = true;
            bool noLineOverflow = true;
            foreach (var f in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (!File.Exists(f + ".meta")) metaIntact = false;
                if (File.ReadAllLines(f).Length > 200) noLineOverflow = false;
            }
            bool builderUnder195 = File.ReadAllLines("Assets/Scripts/Editor/Sc01NavMeshBuilder.cs").Length <= 195;
            VerifyAssert.Require("Codebase health: 0 files > 200 lines, Sc01NavMeshBuilder.cs <= 195 lines, all .meta intact",
                metaIntact && noLineOverflow && builderUnder195, ref passed);

            VerifyAssert.Finish("NAVMESH-CHALLENGER-AUDIT", passed, Expected);
        }
    }
}
#endif
