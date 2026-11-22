namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Labs 分区与测试版标注的唯一口径：版本标签、主流程六步、实验功能开关（默认关）与限制说明。
    /// 主流程代码不得依赖 Labs 开关；开关只用于 UI 分区与在线/实验能力守卫，接口与代码全部保留不删。
    /// </summary>
    public static class LabFeatureFlags
    {
        /// <summary>对外版本号（同步 PlayerSettings.bundleVersion 与 APK 文件名）。</summary>
        public const string VersionLabel = "0.9.0-lab.20261122";

        /// <summary>渠道标注：首页/面板头显示「测试版本」。</summary>
        public const string ChannelLabel = "测试版本";

        /// <summary>主流程六步入口顺序。</summary>
        public static readonly string[] MainFlowSteps = { "剧本", "提案", "搭景", "机位", "时间轴", "导出" };

        /// <summary>MR 透视 / 平面识别 / 空间锚点沙盘：实验功能，默认关。</summary>
        public static bool MrLabEnabled { get; private set; }

        /// <summary>即梦 / Seedance 在线视频生成：实验功能，默认关。</summary>
        public static bool OnlineGenLabEnabled { get; private set; }

        /// <summary>离线演示：关闭 AI 在线解析，走本地结构化；默认关（配置接通时走在线）。</summary>
        public static bool OfflineDemo { get; private set; }

        public const string MrLabTitle = "MR 透视 / 平面识别 / 空间锚点沙盘";
        public const string MrLabLimitation =
            "实验功能：平面识别在 OpenXR 模拟器不支持（provider ERROR_VALIDATION_FAILURE，count=0）；" +
            "沙盘落于替身占位面，不代表真实桌面识别；真实透视观感、锚点跨重启回读 pending-真机。";

        public const string OnlineLabTitle = "在线视频生成（即梦 / Seedance）";
        public const string OnlineLabLimitation =
            "实验功能：未接 Key，默认离线 mock，不发任何在线请求；在线段仅接口占位，成片回放/对照只使用本地素材。";

        public static string MainFlowRail => string.Join(" → ", MainFlowSteps);

        public static void SetMrLabEnabled(bool enabled) => MrLabEnabled = enabled;
        public static void SetOnlineGenLabEnabled(bool enabled) => OnlineGenLabEnabled = enabled;
        public static void SetOfflineDemo(bool enabled) => OfflineDemo = enabled;
    }
}
