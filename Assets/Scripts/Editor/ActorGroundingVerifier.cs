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
    /// 场景实例贴地校验：遍历当前场景每个激活 ActorNavigator（旧名新名一视同仁），
    /// 实测渲染体脚底与地面（NavMesh 采样 / 地板碰撞体）间隙，阈值 ±0.02m。
    /// 只读场景，不改资产。旧 actor_navigator 校验器仍负责 agent 参数与状态机单测。
    /// </summary>
    public static class ActorGroundingVerifier
    {
        private const float Tolerance = 0.02f;
        private const int Expected = 3;

        [MenuItem("ShotField/Verifiers/Run Actor Grounding Verifier")]
        public static void Run()
        {
            int passed = 0;
            var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            VerifyAssert.Require("main scene loaded cleanly", scene.IsValid(), ref passed);

            var navs = Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include);
            var floor = GameObject.Find("Floor");
            var floorCol = floor != null ? floor.GetComponent<Collider>() : null;
            float floorTop = floorCol != null ? floorCol.bounds.max.y : 0f;

            int active = 0;
            bool allGrounded = true;
            var rows = new List<string>();
            foreach (var n in navs)
            {
                var t = n.transform;
                float footMin = float.MaxValue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.bounds.size.y > 0.1f && r.bounds.min.y < footMin) footMin = r.bounds.min.y;
                }
                float groundY = SampleGroundY(t.position, floorTop);
                float gap = footMin - groundY;
                rows.Add($"{t.name} active={t.gameObject.activeInHierarchy} foot={footMin:F3} ground={groundY:F3} gap={gap:F3}");
                if (!t.gameObject.activeInHierarchy) continue;
                active++;
                if (Mathf.Abs(gap) > Tolerance) allGrounded = false;
            }
            foreach (var row in rows) Debug.Log($"[ActorGrounding] DATA {row}");

            VerifyAssert.Require("scene contains at least one active actor", active > 0, ref passed);
            VerifyAssert.Require($"every active actor foot gap within ±{Tolerance:F2}m", allGrounded, ref passed);
            VerifyAssert.Finish("ACTOR-GROUNDING", passed, Expected);
        }

        private static float SampleGroundY(Vector3 pos, float floorTop)
        {
            // 物理贴地平面以地板碰撞体顶面为准；NavMesh 烘焙面可能整体上浮（本场景 0.05m），不作视觉贴地参考。
            if (!float.IsNaN(floorTop)) return floorTop;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 1f, NavMesh.AllAreas)) return hit.position.y;
            return 0f;
        }
    }
}
#endif
