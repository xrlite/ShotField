using UnityEngine;

namespace ShotField.Export
{
    /// <summary>
    /// 成片导出入口（J8）：挂在持久化 GameObject 上，按钮触发 <see cref="VideoRecorderController"/>，
    /// 回执取 <see cref="VideoExportResult.Summary"/>。首版无声轨；Android 编码路径仅打包后可验，
    /// 编辑器内只走参数校验与失败提示，不出片。
    /// </summary>
    public sealed class ExportRecorderPanel : MonoBehaviour
    {
        private VideoRecorderController _recorder;
        private string _status = "尚未导出成片";

        private void Awake()
        {
            _recorder = GetComponent<VideoRecorderController>();
            if (_recorder == null) _recorder = gameObject.AddComponent<VideoRecorderController>();
            _recorder.OnCompleted += HandleCompleted;
            _recorder.OnFailed += HandleFailed;
        }

        private void OnDestroy()
        {
            if (_recorder == null) return;
            _recorder.OnCompleted -= HandleCompleted;
            _recorder.OnFailed -= HandleFailed;
        }

        private void HandleCompleted(VideoExportResult result) => _status = result.Summary;

        private void HandleFailed(string message) => _status = "导出失败：" + message;

        void OnGUI()
        {
            var area = new Rect(Screen.width - 352f, Screen.height - 100f, 340f, 90f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("成片导出（本片无声轨）");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_recorder != null && _recorder.IsRecording ? "停止导出" : "导出成片"))
            {
                if (_recorder.IsRecording) _recorder.StopVideoRecording();
                else _recorder.StartVideoRecording();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(_status);
            GUILayout.EndArea();
        }
    }
}
