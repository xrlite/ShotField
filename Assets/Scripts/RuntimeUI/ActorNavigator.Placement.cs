using UnityEngine;
using UnityEngine.AI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 落点校验与显式摆位（M1 编辑链）。与既有 MoveTo 的 2m 兜底半径无关：
    /// 这里是面板拖拽/恢复时的显式导航区域判据，半径由调用方传入（≤0.6m）。
    /// </summary>
    public partial class ActorNavigator
    {
        /// <summary>只采样不改状态：落点能否站得住。snapped 为采样吸附点。</summary>
        public bool CanStandAt(Vector3 worldPos, float sampleRadius, out Vector3 snapped)
        {
            snapped = worldPos;
            if (agent == null) return false;
            var filter = BuildFilter();
            // 采样半径要覆盖演员枢轴（约 0.8m 高）到导航网格面的垂直落差；水平贴近度仍由网格面本身判定。
            float probeRadius = Mathf.Max(sampleRadius, 1.2f);
            if (!NavMesh.SamplePosition(worldPos, out var hit, probeRadius, filter)) return false;
            // 探针半径只该吃掉垂直落差：松开在水平方向的实际偏差必须仍受调用方半径约束，
            // 否则 1.2m 的探针会把 0.9m 外的网格点也判成"站得住"。
            var flat = new Vector2(hit.position.x - worldPos.x, hit.position.z - worldPos.z).magnitude;
            if (flat > Mathf.Max(0.01f, sampleRadius)) return false;
            if (OverlapsFurniture(hit.position, Mathf.Max(agent.radius, 0.3f) + 0.05f)) return false;
            snapped = hit.position;
            return true;
        }

        /// <summary>落点是否陷进带 NavMeshObstacle 的家具（跳过演员自身）。</summary>
        private static bool OverlapsFurniture(Vector3 pos, float radius)
        {
            var hits = Physics.OverlapSphere(pos, radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in hits)
            {
                if (col.GetComponentInParent<ActorNavigator>() != null) continue;
                if (col.GetComponentInParent<NavMeshObstacle>() != null) return true;
            }
            return false;
        }

        /// <summary>
        /// 校验通过后 Warp 到吸附点并 Stop()；采样失败或 Warp 失败返回 false 且不改状态。
        /// </summary>
        public bool TryPlace(Vector3 worldPos, float sampleRadius, out Vector3 snapped)
        {
            snapped = worldPos;
            if (agent == null || !isActiveAndEnabled) return false;
            if (!CanStandAt(worldPos, sampleRadius, out snapped)) return false;
            if (!agent.isOnNavMesh || !agent.Warp(snapped)) return false;
            Stop();
            return true;
        }

        private NavMeshQueryFilter BuildFilter()
        {
            return new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
        }
    }
}
