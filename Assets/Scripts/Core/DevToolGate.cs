namespace ShotField.Core
{
    /// <summary>
    /// 跨程序集的开发工具显隐闸位。RuntimeUI 与 Export 都只依赖 Core，
    /// 共享状态放这里，避免 Export 反向引用 RuntimeUI 程序集。
    /// </summary>
    public static class DevToolGate
    {
        /// <summary>右下成片视频导出小面板属开发工具，默认收起；引导面板的镜头包 JSON 导出入口不在此闸内。</summary>
        public static bool ExportRecorderVisible;
    }
}
