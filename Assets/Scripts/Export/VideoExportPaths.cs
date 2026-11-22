using System;
using System.IO;
using UnityEngine;

namespace ShotField.Export
{
    /// <summary>
    /// 导出文件命名与目录约定。Android 真实落盘目录由 Java 层
    /// getExternalFilesDir(DIRECTORY_MOVIES) 解析（应用专属外部目录，免权限）；
    /// 这里只给文件名规则与编辑器自测用的预览目录。
    /// </summary>
    public static class VideoExportPaths
    {
        public const string FilePrefix = "shotfield_";
        public const string FileExtension = ".mp4";

        public static string BuildFileName(DateTime when)
        {
            return FilePrefix + when.ToString("yyyyMMdd_HHmmss") + FileExtension;
        }

        /// <summary>编辑器内自测/预览落盘目录；Android 上不使用此路径。</summary>
        public static string EditorPreviewFolder()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Movies"));
        }
    }
}
