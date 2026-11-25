using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 全工程唯一的 Hover / Press 曲线数值来源。
    /// XRI 的 SpatialHoverEffect 与 uGUI 的 UguiHoverEffect 都只调用这里，
    /// 避免"统一反馈"最后变成两条各自演进的曲线。
    /// 数值来自 UI 规格 §4.1：hover 1.04/120ms、离开 90ms、按下 0.97/80ms、抬起 140ms。
    /// </summary>
    public static class HoverMotion
    {
        public const float HoverInDuration = 0.12f;
        public const float HoverOutDuration = 0.09f;
        public const float PressDuration = 0.08f;
        public const float ReleaseDuration = 0.14f;
        public const float HoverScale = 1.04f;
        public const float PressScale = 0.97f;

        /// <summary>推进权重；返回 -1(按下) ~ 0(常态) ~ 1(悬停)。</summary>
        public static float Step(float weight, bool pressed, bool hovering, float deltaTime)
        {
            float target = pressed ? -1f : (hovering ? 1f : 0f);
            float duration = DurationFor(target, weight);
            return Mathf.MoveTowards(weight, target, deltaTime / duration);
        }

        /// <summary>权重转 localScale 的乘数偏移（默认参数）。</summary>
        public static float Offset(float weight) => Offset(weight, HoverScale, PressScale);

        /// <summary>权重转偏移；保留实例级覆盖，让 Inspector 调过的数值仍生效。</summary>
        public static float Offset(float weight, float hoverScale, float pressScale) => weight >= 0f
            ? weight * (hoverScale - 1f)
            : weight * (1f - pressScale);

        private static float DurationFor(float target, float weight)
        {
            if (target < 0f) return PressDuration;
            if (Mathf.Abs(target) < 0.001f) return weight > 0f ? HoverOutDuration : ReleaseDuration;
            return HoverInDuration;
        }
    }
}
