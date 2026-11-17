using UnityEngine;
using ShotField.Core;

namespace ShotField.Constraints
{
    /// <summary>
    /// 180 度轴线（越轴）检查。
    ///
    /// 做法：取两名角色的水平连线作为轴线，判断机位落在轴线的哪一侧。
    /// 与上一镜比较，如果换侧了就是越轴，弹风险提示。
    /// 注意：越轴不等于错误，是"可能让观众搞不清方位"的风险，所以默认可豁免。
    /// </summary>
    public class AxisConstraint : ShotConstraint
    {
        [Tooltip("角色 A 的 markerId")]
        public string actorAId = "";

        [Tooltip("角色 B 的 markerId")]
        public string actorBId = "";

        public override ConstraintSpec ToSpec()
        {
            var spec = base.ToSpec();
            spec.type = "axis";
            spec.actorA = actorAId;
            spec.actorB = actorBId;
            return spec;
        }

        public override ConstraintResult Evaluate(ShotData shot, ShotData previous)
        {
            if (shot == null) return ConstraintResult.Pending(ruleName, "无镜头数据", "");

            var a = LookupPosition(shot, actorAId);
            var b = LookupPosition(shot, actorBId);
            if (a == null || b == null)
            {
                return ConstraintResult.Pending(ruleName, "未指定两名角色的 markerId", "");
            }

            // 轴线在水平面上的方向
            var axis = b.Value - a.Value;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.0001f)
            {
                return ConstraintResult.Pending(ruleName, "两名角色几乎重合，轴线无法确定", "");
            }

            var camA = shot.camera.position;
            int side = SideOfLine(a.Value, axis, camA);

            if (previous == null)
            {
                return ConstraintResult.Pass(ruleName, "首镜，无越轴风险", "");
            }

            var prevA = LookupPosition(previous, actorAId);
            var prevB = LookupPosition(previous, actorBId);
            if (prevA == null || prevB == null)
            {
                return ConstraintResult.Pending(ruleName, "上一镜缺少角色定位，无法比对", "");
            }

            var prevAxis = prevB.Value - prevA.Value;
            prevAxis.y = 0f;
            int prevSide = SideOfLine(prevA.Value, prevAxis, previous.camera.position);

            if (prevSide == 0 || side == 0)
            {
                return ConstraintResult.Pending(ruleName, "机位几乎落在轴线上，侧别无法判定", "");
            }

            if (side != prevSide)
            {
                return ConstraintResult.Violation(ruleName,
                    "机位从轴线的{0}侧换到了{1}侧（相对上一镜）".Replace("{0}", SideName(prevSide)).Replace("{1}", SideName(side)),
                    actorAId + "/" + actorBId);
            }

            return ConstraintResult.Pass(ruleName, "与上一镜同侧，未越轴", actorAId + "/" + actorBId);
        }

        /// <summary>优先用快照里的位置；快照里没有则退化为场景里的实际对象。</summary>
        private Vector3? LookupPosition(ShotData shot, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (shot.snapshot != null)
            {
                var st = shot.snapshot.Find(id);
                if (st != null) return st.position;
            }

            var m = SemanticMarker.Find(id);
            return m != null ? (Vector3?)m.transform.position : null;
        }

        private int SideOfLine(Vector3 linePoint, Vector3 lineDir, Vector3 p)
        {
            // 用水平面叉积的 y 分量判断左右
            var d = p - linePoint;
            d.y = 0f;
            var cross = Vector3.Cross(lineDir.normalized, d.normalized);
            if (Mathf.Abs(cross.y) < 0.001f) return 0;
            return cross.y > 0f ? 1 : -1;
        }

        private string SideName(int side)
        {
            return side > 0 ? "左" : "右";
        }
    }
}
