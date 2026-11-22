using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 脚底贴地持续不变量：Start / 重新激活 / 落位沉降后，按渲染体脚底实测值把根节点抬回地面。
    /// 只校正 transform 落点，不改 agent.baseOffset（胶囊偏移由 prefab 授权值负责，避免双重补偿）。
    /// 脚底是 SkinnedMeshRenderer，Awake/首帧蒙皮姿态未更新、bounds 偏旧，故 0.25s 沉降后再校一次。
    /// </summary>
    public partial class ActorNavigator
    {
        private const float GroundTolerance = 0.02f;
        private Coroutine _anchorRoutine;
        private float _minLocalY = float.MaxValue;

        private void Start()
        {
            ScheduleGroundCheck();
        }

        private void ScheduleGroundCheck()
        {
            if (_anchorRoutine != null) StopCoroutine(_anchorRoutine);
            _anchorRoutine = StartCoroutine(AnchorAfterSettle());
        }

        private IEnumerator AnchorAfterSettle()
        {
            yield return new WaitForSeconds(0.25f);
            _anchorRoutine = null;
            RefreshFootLocalY();
            EnsureGroundContact();
        }

        /// <summary>实测各渲染体在根空间的脚底局部最小值。</summary>
        private void RefreshFootLocalY()
        {
            _minLocalY = float.MaxValue;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.bounds.size.y < 0.1f) continue;
                float y = transform.InverseTransformPoint(r.bounds.min).y;
                if (y < _minLocalY) _minLocalY = y;
            }
        }

        /// <summary>脚底-地面间隙超 ±0.02m 时把根节点抬到地面；不改 baseOffset。</summary>
        public void EnsureGroundContact()
        {
            if (_minLocalY == float.MaxValue) RefreshFootLocalY();
            if (_minLocalY == float.MaxValue) return;

            float groundY = SampleGroundY();
            if (float.IsNaN(groundY)) return;

            float footWorldY = transform.position.y + _minLocalY;
            if (Mathf.Abs(footWorldY - groundY) <= GroundTolerance) return;

            Vector3 p = transform.position;
            p.y = groundY - _minLocalY;
            transform.position = p;
            if (agent != null && agent.isOnNavMesh) agent.Warp(p);
        }

        private float SampleGroundY()
        {
            // 物理贴地以射线打到的地板顶面为准；NavMesh 烘焙面可能整体上浮，仅作兜底。
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down,
                    out RaycastHit ray, 2f, ~0, QueryTriggerInteraction.Ignore))
                return ray.point.y;

            if (agent != null && agent.isOnNavMesh &&
                NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, agent.areaMask))
                return hit.position.y;

            return float.NaN;
        }
    }
}
