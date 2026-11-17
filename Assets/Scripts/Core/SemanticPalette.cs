using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 语义视觉色板（Semantic Palette）。
    /// 严格对照 PRD 附录 F4 与《语义视觉系统_SemanticVision.md》约定。
    /// 核心原则："颜色是线索，数据是基础"；仅用于工作台人眼质检与评审展示，喂模型的永远是 Clay 灰白。
    /// </summary>
    public static class SemanticPalette
    {
        // ---------- 模式 1：标准白模（Clay）----------
        public static readonly Color ClayColor = new Color(0.85f, 0.85f, 0.85f, 1.0f);

        // ---------- 模式 2：固定语义色板（不随主题变）----------
        // 主角 A（青蓝）
        public static readonly Color ActorPrimaryColor = new Color(0.15f, 0.72f, 0.88f, 1.0f);
        // 主角 B（暖橙）
        public static readonly Color ActorSecondaryColor = new Color(1.00f, 0.55f, 0.15f, 1.0f);
        // 关键道具（警示黄 - 如信封）
        public static readonly Color KeyPropColor = new Color(1.00f, 0.88f, 0.20f, 1.0f);
        // 普通道具（辅助紫）
        public static readonly Color PropColor = new Color(0.67f, 0.35f, 0.75f, 1.0f);
        // 环境结构（中性中灰）
        public static readonly Color EnvironmentColor = new Color(0.65f, 0.65f, 0.67f, 1.0f);

        /// <summary>
        /// 根据标记类型获取对应的语义显示颜色。
        /// </summary>
        public static Color GetColor(SemanticMarker.Kind kind)
        {
            switch (kind)
            {
                case SemanticMarker.Kind.ActorPrimary:
                    return ActorPrimaryColor;
                case SemanticMarker.Kind.ActorSecondary:
                    return ActorSecondaryColor;
                case SemanticMarker.Kind.KeyProp:
                    return KeyPropColor;
                case SemanticMarker.Kind.Prop:
                    return PropColor;
                case SemanticMarker.Kind.Environment:
                default:
                    return EnvironmentColor;
            }
        }
    }
}
