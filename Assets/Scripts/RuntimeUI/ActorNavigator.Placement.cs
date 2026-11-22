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
            if (!NavMesh.SamplePosition(worldPos, out var hit, Mathf.Max(0.01f, sampleRadius), filter)) return false;
            snapped = hit.position;
            return true;
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
