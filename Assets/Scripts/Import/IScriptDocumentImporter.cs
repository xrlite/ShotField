namespace ShotField.Import
{
    /// <summary>
    /// 单格式剧本导入器。实现只做「读文件 → 提取纯文本 → 结构化统计」，
    /// 不发网络请求、不调 AI；返回状态为 <see cref="ScriptImportStatus.PendingConfirmation"/>
    /// 时，表示产物待用户预览确认，上游不得直接送 DeepSeek。
    /// </summary>
    public interface IScriptDocumentImporter
    {
        /// <summary>是否接受该路径（按扩展名与文件头判断）。</summary>
        bool CanImport(string path);

        /// <summary>执行导入；任何失败都走结构化 <see cref="ScriptImportResult"/>，不抛裸异常给界面层。</summary>
        ScriptImportResult Import(string path);
    }
}
