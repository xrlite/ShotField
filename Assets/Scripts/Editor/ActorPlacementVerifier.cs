#if UNITY_EDITOR
using System.Collections.Generic;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 场景实例级落位与绕行校验：
    /// 1) 茶几投影内落点必须被 CanStandAt 拒绝，空闲点必须通过；
    /// 2) 茶几挡在 A、B 中间时，静态 NavMesh 路径必须绕开障碍（拐点 ≥3、路径长 > 直线 1.15 倍）。
    /// 只读场景，不改资产。
    /// </summary>
    public static class ActorPlacementVerifier
    {
        private const int Expected = 5;
        private static readonly Vector3 DetourStart = new Vector3(0.8f, 0f, 0.6f);
        private static readonly Vector3 DetourGoal = new Vector3(0.8f, 0f, 3.2f);
        private static readonly Vector3 FreeSpot = new Vector3(-1.6f, 0f, 1.0f);

        [MenuItem("ShotField/Verifiers/Run Actor Placement Verifier")]
        public static void Run()
        {
            int passed = 0;
            var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            VerifyAssert.Require("main scene loaded cleanly", scene.IsValid(), ref passed);

            var actor = FindActiveNavigator();
            VerifyAssert.Require("scene contains an active actor", actor != null, ref passed);

            Vector3 tableCenter = FindTableCenter();
            bool tableBlocked = actor != null && actor.CanStandAt(tableCenter, 0.3f, out _);
            VerifyAssert.Require("point inside coffee table footprint is rejected",
                tableCenter != Vector3.zero && !tableBlocked, ref passed);

            bool freeAccepted = actor != null && actor.CanStandAt(FreeSpot, 0.3f, out _);
            VerifyAssert.Require("free standing spot is accepted", freeAccepted, ref passed);

            VerifyDetour(out bool detourOk, out string detail);
            VerifyAssert.Require($"detour path goes around furniture: {detail}", detourOk, ref passed);

            VerifyAssert.Finish("ACTOR-PLACEMENT", passed, Expected);
        }

        private static ActorNavigator FindActiveNavigator()
        {
            foreach (var n in Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include))
            {
                if (n.gameObject.activeInHierarchy) return n;
            }
            return null;
        }

        private static Vector3 FindTableCenter()
        {
            var obstacles = Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include);
            foreach (var ob in obstacles)
            {
                if (!ob.name.Contains("CoffeeTable")) continue;
                var col = ob.GetComponent<Collider>();
                return col != null ? col.bounds.center : ob.transform.position;
            }
            return Vector3.zero;
        }

        private static void VerifyDetour(out bool ok, out string detail)
        {
            var path = new NavMeshPath();
            bool computed = NavMesh.CalculatePath(DetourStart, DetourGoal, NavMesh.AllAreas, path);
            float straight = Vector3.Distance(DetourStart, DetourGoal);
            float length = PathLength(path);
            bool complete = computed && path.status == NavMeshPathStatus.PathComplete;
            bool corners = path.corners.Length >= 3;
            bool detours = straight > 0 && length / straight >= 1.15f;
            ok = complete && corners && detours;
            detail = $"corners={path.corners.Length} len={length:F2} straight={straight:F2} ratio={(straight > 0 ? length / straight : 0):F2}";
            Debug.Log($"[ActorPlacement] DETOUR {detail}");
        }

        private static float PathLength(NavMeshPath path)
        {
            float d = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                d += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return d;
        }
    }
}
#endif
