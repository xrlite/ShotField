#if UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ShotField.Core;
using ShotField.RuntimeUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.EditorTools
{
    /// <summary>验证 VR 联机入口确实操作同一 NetworkSession，而不是只有 Core 类存在。</summary>
    public static class NetworkEntryVerifier
    {
        private const int Expected = 9;

        [MenuItem("ShotField/Verifiers/Run Network Entry Verifier")]
        public static void Run()
        {
            int passed = 0;
            var hostGo = new GameObject("[NetworkEntryHost]");
            var clientGo = new GameObject("[NetworkEntryClient]");
            var host = hostGo.AddComponent<NetworkSessionController>();
            var client = clientGo.AddComponent<NetworkSessionController>();
            try
            {
                Check("无效 IP 被拒绝且不进入连接态", !client.Join("not-an-ip") &&
                    client.Session.State == NetworkSession.SessionState.Disconnected, ref passed);
                client.Port = 0;
                Check("非法端口被拒绝", !client.StartHost() && client.Status.Contains("端口"), ref passed);
                int port = FreePort();
                host.Port = port; client.Port = port; client.Address = "127.0.0.1";
                Check("VR 共用控制器可开局", host.StartHost() &&
                    host.Session.State == NetworkSession.SessionState.Hosting, ref passed);
                Check("VR 共用控制器可发起加入", client.Join("127.0.0.1"), ref passed);
                WaitFor(host, client, () => client.Session.State == NetworkSession.SessionState.Connected);
                Check("控制器 Tick 完成真实回环握手", client.Session.State == NetworkSession.SessionState.Connected &&
                    host.Session.State == NetworkSession.SessionState.Hosting, ref passed);

                var panel = hostGo.AddComponent<VrNetworkPanel>();
                panel.Bind(host);
                var head = new GameObject("Head").transform;
                head.SetParent(hostGo.transform, false); head.position = new Vector3(0f, 1.6f, 0f);
                panel.headAnchor = head;
                Check("联机 modal 默认隐藏", !panel.Visible, ref passed);
                panel.SetVisible(true);
                var canvas = panel.PanelRoot.GetComponentInChildren<Canvas>(true);
                bool buttons = panel.PanelRoot.Find("Host") != null &&
                    panel.PanelRoot.Find("Join") != null &&
                    panel.PanelRoot.Find("Disconnect") != null &&
                    panel.PanelRoot.Find("Close") != null;
                Check("WorldSpace modal 含 IP 输入与四个操作", canvas != null &&
                    canvas.renderMode == RenderMode.WorldSpace && buttons &&
                    Mathf.Abs(panel.PanelRoot.lossyScale.x - 0.001f) < 0.00001f &&
                    panel.PanelRoot.GetComponentInChildren<TMP_InputField>(true) != null, ref passed);

                var menu = new PalmMenuState { Network = panel };
                menu.SelectTab(PalmMenuTab.Scene);
                var entry = menu.BuildEntries().Find(e => e.Kind == PalmMenuActionKind.ToggleNetwork);
                Check("PalmMenu 场景栏可呼出/关闭联机", entry != null && entry.Active &&
                    !menu.Execute(entry) && !panel.Visible, ref passed);
                client.Disconnect(); host.Disconnect();
                Check("断开后双端回到未连接", client.Session.State == NetworkSession.SessionState.Disconnected &&
                    host.Session.State == NetworkSession.SessionState.Disconnected, ref passed);
            }
            catch (Exception ex) { Debug.LogError("[NETWORK-ENTRY] " + ex); }
            finally
            {
                client.Disconnect(); host.Disconnect();
                UnityEngine.Object.DestroyImmediate(clientGo); UnityEngine.Object.DestroyImmediate(hostGo);
            }
            VerifyAssert.Finish("NETWORK-ENTRY-RESULT", passed, Expected);
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static void WaitFor(NetworkSessionController host, NetworkSessionController client,
            Func<bool> condition)
        {
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && timeout.ElapsedMilliseconds < 3000)
            {
                host.TickNow(); client.TickNow();
                Thread.Sleep(5);
            }
        }

        private static void Check(string label, bool condition, ref int passed)
        {
            if (!condition) throw new InvalidOperationException(label);
            passed++; Debug.Log("[NETWORK-ENTRY] PASS: " + label);
        }
    }
}
#endif
