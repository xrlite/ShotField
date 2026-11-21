using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 语义视觉色板（Semantic Palette）。
    /// 严格对照 PRD 附录 F4 与《语义视觉系统_SemanticVision.md》约定。
    /// 核心原则："颜色是线索，数据是基础"；仅用于工作台人眼质检与评审展示，喂模型的永远是 Clay 灰白。
    /// 桥接 PrevisPalette 唯一真相来源，彻底杜绝平行色板分化。
    /// </summary>
    public static class SemanticPalette
    {
        // ---------- 模式 1：标准白模（Clay）----------
        public static Color ClayColor => PrevisPalette.StageWallGray;

        // ---------- 模式 2：固定语义色板（桥接 PrevisPalette 唯一源头）----------
        // 主角 A（预演红 #FF0000）
        public static Color ActorPrimaryColor => PrevisPalette.ColorOf("Red");
        // 主角 B（预演蓝 #0055FF）
        public static Color ActorSecondaryColor => PrevisPalette.ColorOf("Blue");
        // 关键道具（预演信封 #F5F5F0）
        public static Color KeyPropColor => PrevisPalette.Envelope;
        // 普通道具（预演辅助紫 #8A3BE2）
        public static Color PropColor => PrevisPalette.ColorOf("Violet");
        // 环境结构（中性深灰/地灰 #9A9A9A）
        public static Color EnvironmentColor => PrevisPalette.StageFloorGray;

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
