using System;
using UnityEngine;

namespace ShotField.Import
{
    /// <summary>
    /// UnitySendMessage 接收端：挂在名为 ShotFieldScriptImport 的 GameObject 上。
    /// Java 侧 ShotFieldScriptPicker 选中文件后回传 "uri|displayName"，
    /// 本组件把 URI 复制进沙盒并通过静态事件上交上游；上游确认前不调任何 AI。
    /// </summary>
    public sealed class ScriptImportReceiver : MonoBehaviour
    {
        /// <summary>URI 已复制进沙盒，参数为沙盒本地路径。</summary>
        public static event Action<string> UriResolved;

        /// <summary>选择失败或复制失败，参数为原因。</summary>
        public static event Action<string> PickFailed;

        public void OnScriptUriPicked(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                PickFailed?.Invoke("选择器回传空 payload。");
                return;
            }

            int sep = payload.IndexOf('|');
            string uri = sep >= 0 ? payload.Substring(0, sep) : payload;
            string displayName = sep >= 0 ? payload.Substring(sep + 1) : string.Empty;

            if (AndroidScriptFileBridge.TryCopyToSandbox(uri, out string sandboxPath, out string reason))
            {
                Debug.Log($"[ScriptImport] URI 已复制进沙盒：{displayName} -> {sandboxPath}");
                UriResolved?.Invoke(sandboxPath);
            }
            else
            {
                PickFailed?.Invoke(reason);
            }
        }

        public void OnScriptPickFailed(string reason)
        {
            PickFailed?.Invoke(string.IsNullOrWhiteSpace(reason) ? "选择器返回未知失败。" : reason);
        }
    }
}
