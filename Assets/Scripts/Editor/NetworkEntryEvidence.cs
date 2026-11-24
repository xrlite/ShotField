#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShotField.EditorTools
{
    /// <summary>用独立 UI 层相机渲染联机 modal；XR Game View 不作为 WorldSpace UI 取证层。</summary>
    public static class NetworkEntryEvidence
    {
        public static string OutputDirectory;

        [Serializable]
        internal sealed class Report
        {
            public string status, sessionState, controllerStatus, screenshot;
            public int port;
            public float worldScale;
            public bool hostButton, joinButton, disconnectButton, closeButton;
            public string playModeWiring = "controller/panel/PalmMenu VR/PalmMenu desktop = same instance";
        }

        [MenuItem("ShotField/Evidence/Capture VR Network Entry")]
        public static void Run()
        {
            string directory = OutputDirectory;
            if (string.IsNullOrEmpty(directory))
                directory = Environment.GetEnvironmentVariable("SHOTFIELD_NETWORK_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                string month = Path.Combine(B5EvidenceSupport.FindEvidenceRoot(), DateTime.Now.ToString("yyyy-MM"));
                Directory.CreateDirectory(month);
                directory = Path.Combine(month, "network_entry_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
            }
            Directory.CreateDirectory(directory);
            string screenshot = Path.Combine(directory, "vr_network_modal.png");
            string reportPath = Path.Combine(directory, "network_entry_evidence.json");
            if (File.Exists(screenshot) || File.Exists(reportPath))
                throw new InvalidOperationException("Network evidence already exists; no files were overwritten.");

            var host = new GameObject("[NetworkEntryEvidence]");
            NetworkSessionController controller = null;
            Camera camera = null;
            try
            {
                controller = host.AddComponent<NetworkSessionController>();
                controller.Port = 9198;
                var panel = host.AddComponent<VrNetworkPanel>();
                panel.Bind(controller);
                var head = new GameObject("Head").transform;
                head.SetParent(host.transform, false); head.position = new Vector3(0f, 1.6f, 0f);
                panel.headAnchor = head;
                panel.SetVisible(true);
                panel.PanelRoot.position = new Vector3(0f, 1.6f, 0.72f);
                panel.PanelRoot.rotation = Quaternion.identity;
                foreach (var child in panel.PanelRoot.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 5;
                panel.PanelRoot.Find("Host").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

                var cameraGo = new GameObject("NetworkEvidenceCamera");
                cameraGo.transform.SetParent(host.transform, false);
                cameraGo.transform.position = new Vector3(0f, 1.6f, 0f);
                cameraGo.transform.LookAt(panel.PanelRoot.position);
                camera = cameraGo.AddComponent<Camera>();
                camera.fieldOfView = 40f; camera.cullingMask = 1 << 5;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f, 1f);
                B5EvidenceRenderer.RenderPng(camera, screenshot, 1240, 840);
                if (new FileInfo(screenshot).Length == 0) throw new InvalidDataException("截图为空");

                var report = new Report
                {
                    status = "PASS", sessionState = controller.Session.State.ToString(),
                    controllerStatus = controller.Status, screenshot = screenshot, port = controller.Port,
                    worldScale = panel.PanelRoot.lossyScale.x,
                    hostButton = panel.PanelRoot.Find("Host") != null,
                    joinButton = panel.PanelRoot.Find("Join") != null,
                    disconnectButton = panel.PanelRoot.Find("Disconnect") != null,
                    closeButton = panel.PanelRoot.Find("Close") != null
                };
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log($"NETWORK-ENTRY-EVIDENCE=PASS state={report.sessionState} path={screenshot}");
            }
            finally
            {
                if (controller != null) controller.Disconnect();
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
