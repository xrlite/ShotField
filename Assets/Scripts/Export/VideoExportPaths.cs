using System;
using System.IO;
using UnityEngine;

namespace ShotField.Export
{
    /// <summary>
    /// 导出文件命名与目录约定。Android 落盘根目录由 Java 层
    /// getExternalFilesDir(DIRECTORY_MOVIES) 解析（应用专属外部目录，免权限）；
    /// 设备内相对子目录 Build/Video/，文件名 SC01_Demo_yyyyMMdd_HHmm.mp4。
    /// 禁用 final 等后缀；旧 SC01_Final.mp4 只作历史测试产物，不作验收。
    /// </summary>
    public static class VideoExportPaths
    {
        public const string FilePrefix = "SC01_Demo_";
        public const string FileExtension = ".mp4";
        public const string RelativeDirectory = "Build/Video";

        public static string BuildFileName(DateTime when)
        {
            return FilePrefix + when.ToString("yyyyMMdd_HHmm") + FileExtension;
        }

        /// <summary>设备内相对路径：Build/Video/SC01_Demo_....mp4。</summary>
        public static string BuildRelativePath(DateTime when)
        {
            return Path.Combine(RelativeDirectory, BuildFileName(when)).Replace('\\', '/');
        }

        /// <summary>编辑器内自测/预览落盘目录；Android 上不使用此路径。</summary>
        public static string EditorPreviewFolder()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Movies"));
        }
    }
}
