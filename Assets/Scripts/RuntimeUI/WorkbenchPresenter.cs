using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.RuntimeUI
{
    public partial class SpatialWorkbench
    {
        public void RefreshUI()
        {
            if (library == null || library.Count == 0)
            {
                WorkbenchStatus.UpdatePill(visibilityPill, visibilityStatusText, null, "信封");
                WorkbenchStatus.UpdatePill(axisPill, axisStatusText, null, "轴线");
                if (shotTitleText != null) shotTitleText.text = "暂无镜头";
                if (shotInfoText != null) shotInfoText.text = "点击下方按钮以当前机位创建分镜";
                return;
            }

            currentShotIndex = Mathf.Clamp(currentShotIndex, 0, library.Count - 1);
            var shot = library.Shots[currentShotIndex];
            if (monitor != null)
            {
                bool portrait = shot.camera.aspect < 1f;
                if (monitor.IsPortrait != portrait) monitor.SetAspect(portrait);
                monitor.RequestRender();
            }

            if (shotTitleText != null)
                shotTitleText.text = $"分镜 [{shot.shotId}] (第 {currentShotIndex + 1}/{library.Count} 镜)";

            if (shotInfoText != null)
                shotInfoText.text = $"意图: {shot.intent}\n机位: {shot.camera.position}";

            EvaluateConstraints(shot);
        }

        private void EvaluateConstraints(ShotData shot)
        {
            shot.lastCheck.Clear();
            ConstraintResult vis = null, axis = null;
            var previous = currentShotIndex > 0 ? library.Shots[currentShotIndex - 1] : null;
            foreach (var rule in rules)
            {
                if (!IsApplicable(rule)) continue;
                var result = rule.Evaluate(shot, previous);
                shot.lastCheck.Add(result);
                if (rule is VisibilityConstraint && vis == null) vis = result;
                if (rule is AxisConstraint && axis == null) axis = result;
            }
            WorkbenchStatus.UpdatePill(visibilityPill, visibilityStatusText, vis?.status, "信封");
            WorkbenchStatus.UpdatePill(axisPill, axisStatusText, axis?.status, "轴线");
            shot.checkedAt = System.DateTimeOffset.UtcNow.ToString("O");
        }
    }
}
