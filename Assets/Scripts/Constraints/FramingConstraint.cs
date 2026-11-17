using UnityEngine;
using ShotField.Core;

namespace ShotField.Constraints
{
    /// <summary>
    /// 构图位置约束：指定的主体必须落在画面的某个水平区间内。
    /// 例：「男主位于画面右侧 1/3」，即 x 落在 [0.667, 1.0]。
    /// </summary>
    public class FramingConstraint : ShotConstraint
    {
        [Tooltip("主体的 markerId")]
        public string subjectId = "";

        [Range(0f, 1f)] public float minViewportX = 0f;
        [Range(0f, 1f)] public float maxViewportX = 1f;

        public override ConstraintSpec ToSpec()
        {
            var spec = base.ToSpec();
            spec.type = "framing";
            spec.targetId = subjectId;
            spec.minX = minViewportX;
            spec.maxX = maxViewportX;
            return spec;
        }

        public override ConstraintResult Evaluate(ShotData shot, ShotData previous)
        {
            if (shot == null) return ConstraintResult.Pending(ruleName, "无镜头数据", subjectId);
            if (string.IsNullOrEmpty(subjectId)) return ConstraintResult.Pending(ruleName, "未指定主体", "");

            Vector3 worldPos;
            var st = shot.snapshot != null ? shot.snapshot.Find(subjectId) : null;
            if (st != null)
            {
                worldPos = st.position;
            }
            else
            {
                var m = SemanticMarker.Find(subjectId);
                if (m == null) return ConstraintResult.Pending(ruleName, "找不到主体 " + subjectId, subjectId);
                worldPos = m.transform.position;
            }

            float vx;
            if (!ViewportX(shot.camera, worldPos, out vx))
            {
                return ConstraintResult.Violation(ruleName,
                    "主体不在画面内", subjectId);
            }

            if (vx < minViewportX || vx > maxViewportX)
            {
                return ConstraintResult.Violation(ruleName,
                    string.Format("主体水平位置 {0:F2}，要求在 [{1:F2}, {2:F2}] 内", vx, minViewportX, maxViewportX),
                    subjectId);
            }

            return ConstraintResult.Pass(ruleName,
                string.Format("主体水平位置 {0:F2}，符合要求", vx), subjectId);
        }

        private bool ViewportX(ShotCameraState cam, Vector3 worldPoint, out float vx)
        {
            vx = 0f;

            var local = cam.ViewMatrix.MultiplyPoint3x4(worldPoint);
            if (-local.z <= cam.nearClip) return false;

            var proj = cam.ProjectionMatrix();
            // z 用相机空间的 local.z（前方为负），与 VisibilityConstraint 保持一致
            var clip = proj * new Vector4(local.x, local.y, local.z, 1f);
            if (clip.w <= 0f) return false;

            float ndcX = clip.x / clip.w;
            if (ndcX < -1f || ndcX > 1f) return false;

            vx = (ndcX + 1f) * 0.5f;   // 转到 0..1
            return true;
        }
    }
}
