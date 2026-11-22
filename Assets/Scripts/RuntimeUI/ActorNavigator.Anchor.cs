using System.Collections;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 脚底贴地校准：NavMeshAgent 根节点停在网格面 + baseOffset 高度，
    /// 按模型脚底相对根节点的局部偏移反推 baseOffset，避免视觉模型沉到网格面下（陷地）。
    /// 脚底是 SkinnedMeshRenderer，Awake/首帧蒙皮姿态未更新、bounds 偏旧，
    /// 因此先给初值，0.25s 后待 bounds 落定再校准一次，之后不再改动。
    /// </summary>
    public partial class ActorNavigator
    {
        private Coroutine _anchorRoutine;

        private void Start()
        {
            ApplyFootAnchor();
            _anchorRoutine = StartCoroutine(AnchorAfterSettle());
        }

        private IEnumerator AnchorAfterSettle()
        {
            yield return new WaitForSeconds(0.25f);
            ApplyFootAnchor();
            _anchorRoutine = null;
        }

        /// <summary>按各渲染体局部脚底最小值重算 baseOffset；bounds 未就绪返回 false。</summary>
        private bool ApplyFootAnchor()
        {
            if (agent == null) return false;
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            float minLocalY = float.MaxValue;
            bool valid = false;
            foreach (var r in renderers)
            {
                if (r.bounds.size.y < 0.1f) continue;
                valid = true;
                Vector3 foot = transform.InverseTransformPoint(r.bounds.min);
                if (foot.y < minLocalY) minLocalY = foot.y;
            }
            if (!valid || minLocalY >= 0f) return valid;
            agent.baseOffset = -minLocalY;
            return true;
        }
    }
}
