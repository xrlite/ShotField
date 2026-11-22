using System;
using System.IO;
using UnityEngine;

namespace ShotField.Import
{
    /// <summary>
    /// Android SAF（Storage Access Framework）接线点。
    /// <para>Editor / Standalone：路径直接透传。</para>
    /// <para>Android：用户从 SAF 选择器拿到的是 <c>content://</c> URI，不能直接走 File.ReadAllText；
    /// 必须先通过 ContentResolver 打开流，复制进应用沙盒 <c>persistentDataPath/imports/</c>，再交给导入器解析。</para>
    /// <para>Java 侧（shotfield-script-picker.jar，ShotFieldScriptPicker）已实现：
    /// ① ACTION_OPEN_DOCUMENT 选择器 + MIME 过滤（txt/md/docx/pdf）；
    /// ② takePersistableUriPermission 持久化 URI 授权；
    /// ③ 选择结果经 UnityPlayer.UnitySendMessage 回传 Unity。</para>
    /// <para>本桥接负责「URI → 沙盒本地文件」；回传接收端见 <see cref="ScriptImportReceiver"/>。</para>
    /// <para>待 APK 接线（pending-APK，不在本层内造）：宿主 Activity 在 onActivityResult 中
    /// 转发到 ShotFieldScriptPicker.handleActivityResult；场景内建名为
    /// <see cref="PickerGameObject"/> 的 GameObject 并挂上 ScriptImportReceiver。</para>
    /// </summary>
    public static class AndroidScriptFileBridge
    {
        private const string ImportFolder = "imports";

        /// <summary>UnitySendMessage 目标 GameObject 名，与 Java 侧常量一致。</summary>
        public const string PickerGameObject = "ShotFieldScriptImport";

        private const string PickerJavaClass = "com.haoyunlai.shotfield.ShotFieldScriptPicker";

        /// <summary>
        /// 把选中的文件（Editor 下为本地路径，Android 下为 content:// URI）复制进沙盒，返回可被导入器读取的本地路径。
        /// </summary>
        public static bool TryCopyToSandbox(string uriOrPath, out string sandboxPath, out string reason)
        {
            sandboxPath = string.Empty;
            reason = string.Empty;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsContentUri(uriOrPath))
                return CopyContentUri(uriOrPath, out sandboxPath, out reason);

            sandboxPath = uriOrPath;
            return true;
#else
            sandboxPath = uriOrPath;
            return true;
#endif
        }

        /// <summary>
        /// 在 Android 上唤起 SAF 选择器；Editor / Standalone 下返回 false。
        /// 选中结果由 <see cref="ScriptImportReceiver"/> 经 UnitySendMessage 接收。
        /// </summary>
        public static bool OpenPicker()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null)
                {
                    Debug.LogWarning("[ScriptImport] currentActivity 为空，无法唤起选择器。");
                    return false;
                }

                using var picker = new AndroidJavaObject(PickerJavaClass, activity);
                return picker.Call<bool>("pickScriptFile");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScriptImport] 唤起 SAF 选择器失败：{ex.Message}");
                return false;
            }
#else
            Debug.LogWarning("[ScriptImport] SAF 选择器仅在 Android 真机可用。");
            return false;
#endif
        }

        private static bool IsContentUri(string uri)
        {
            return uri != null && uri.StartsWith("content://", StringComparison.OrdinalIgnoreCase);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool CopyContentUri(string uri, out string sandboxPath, out string reason)
        {
            sandboxPath = string.Empty;
            reason = string.Empty;
            AndroidJavaObject resolver = null;
            AndroidJavaObject inputStream = null;

            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null)
                {
                    reason = "Android 当前 Activity 为空，无法访问 ContentResolver。";
                    return false;
                }

                resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                if (resolver == null)
                {
                    reason = "ContentResolver 为空。";
                    return false;
                }

                // C# string 由 JNI 自动 marshal 为 jstring，无需手工包 Java 对象。
                inputStream = resolver.Call<AndroidJavaObject>("openInputStream", uri);
                if (inputStream == null)
                {
                    reason = "openInputStream 返回空，URI 不可读或未授权。";
                    return false;
                }

                string folder = Path.Combine(Application.persistentDataPath, ImportFolder);
                Directory.CreateDirectory(folder);
                string fileName = SanitizeFileName(uri);
                sandboxPath = Path.Combine(folder, fileName);

                CopyStreamToFile(inputStream, sandboxPath);
                return true;
            }
            catch (Exception ex)
            {
                reason = $"SAF 复制进沙盒失败：{ex.Message}";
                return false;
            }
            finally
            {
                inputStream?.Dispose();
                resolver?.Dispose();
            }
        }

        private static void CopyStreamToFile(AndroidJavaObject inputStream, string destination)
        {
            var buffer = new byte[8192];
            using var fs = new FileStream(destination, FileMode.Create, FileAccess.Write);
            while (true)
            {
                int read = inputStream.Call<int>("read", buffer);
                if (read <= 0) break;
                fs.Write(buffer, 0, read);
            }
        }

        private static string SanitizeFileName(string uri)
        {
            string segment = uri;
            int slash = uri.LastIndexOf('/');
            if (slash >= 0) segment = uri.Substring(slash + 1);
            if (string.IsNullOrWhiteSpace(segment)) segment = "imported_script";

            var chars = segment.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), chars[i]) >= 0) chars[i] = '_';
            }
            return new string(chars);
        }
#endif
    }
}
