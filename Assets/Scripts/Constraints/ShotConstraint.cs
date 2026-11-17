using UnityEngine;
using ShotField.Core;

namespace ShotField.Constraints
{
    /// <summary>
    /// 约束基类。所有几何规则继承它。
    /// 约定：只读数，不改场景。
    /// </summary>
    public abstract class ShotConstraint : MonoBehaviour
    {
        [Tooltip("规则名，写入检查结果与护照")]
        public string ruleName = "";

        [Tooltip("勾选后，该规则判违规会阻断审批")]
        public bool blocking = true;

        [Tooltip("勾选后可被导演手动豁免（需记录理由）")]
        public bool waivable = false;

        /// <summary>对指定镜头求值。previous 可为 null（第一镜没有上一镜）。</summary>
        public abstract ConstraintResult Evaluate(ShotData shot, ShotData previous);

        public virtual ConstraintSpec ToSpec()
        {
            return new ConstraintSpec
            {
                type = GetType().Name, ruleName = ruleName,
                blocking = blocking, waivable = waivable
            };
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(ruleName))
            {
                ruleName = GetType().Name;
            }
        }
    }
}
