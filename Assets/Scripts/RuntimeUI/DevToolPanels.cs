namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 既有桌面 IMGUI 工具面板的显隐总闸：引导旅程默认收起，按需呼出。
    /// 这里只放显隐状态，不承载功能；产品正式界面走空间 Canvas，本批 IMGUI 属开发/桌面验证工具。
    /// </summary>
    public static class DevToolPanels
    {
        /// <summary>既有桌面工作室面板与右侧编辑面板默认隐藏，由引导面板的「桌面开发工具」呼出。</summary>
        public static bool LegacyDesktopVisible;

        /// <summary>右侧监看器回放画面按需呼出，不常驻占屏。</summary>
        public static bool MonitorFeedVisible;

        /// <summary>监看板缩放滑条按需呼出，不常驻顶栏。</summary>
        public static bool MonitorScalerVisible;
    }
}
