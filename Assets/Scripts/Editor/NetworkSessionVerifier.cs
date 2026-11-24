using System;
using System.Threading;
using UnityEngine;
using UnityEditor;
using ShotField.Core;

namespace ShotField.EditorTools
{
    public static class NetworkSessionVerifier
    {
        [MenuItem("ShotField/Verifiers/Run Network Session Verifier")]
        public static void Run()
        {
            int passCount = RunBaseTests();
            NetworkSceneEditVerifier.Run(out int scenePassed, out int sceneTotal);
            passCount += scenePassed;
            int totalTests = 7 + sceneTotal;
            if (passCount == totalTests) Debug.Log($"NET-VERIFY-RESULT=PASS ({passCount}/{totalTests})");
            else
            {
                Debug.LogError($"NET-VERIFY-RESULT=FAIL ({passCount}/{totalTests})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static int RunBaseTests()
        {
            int passCount = 0;
            var host = new NetworkSession();
            var client = new NetworkSession();
            try
            {
                host.StartHost(9188, ParticipantRole.Director);
                passCount += Check("Host 成功监听本地端口，角色绑定为 Director",
                    host.State == NetworkSession.SessionState.Hosting && host.LocalRole == ParticipantRole.Director);
                string joinedClientId = null;
                string joinedRole = null;
                host.OnClientJoined += (id, role) => { joinedClientId = id; joinedRole = role; };
                client.Connect("127.0.0.1", 9188, ParticipantRole.Producer, "ProducerPC");
                WaitFor(host, client, () => joinedClientId != null && client.State == NetworkSession.SessionState.Connected);
                passCount += Check("双端握手成功，识别角色为 Producer",
                    !string.IsNullOrEmpty(joinedClientId) && joinedRole == "Producer" && client.State == NetworkSession.SessionState.Connected);
                passCount += VerifyCamera(host, client);
                string longPassport = new string('镜', 12000);
                passCount += VerifyPassport(host, client, longPassport);
                passCount += VerifyComment(host, client);
                passCount += VerifyPassportRejoin(host, client, longPassport);
                client.Disconnect();
                host.Disconnect();
                passCount += Check("双端连接优雅关闭",
                    client.State == NetworkSession.SessionState.Disconnected && host.State == NetworkSession.SessionState.Disconnected);
            }
            catch (Exception ex) { Debug.LogError($"[EXCEPTION] 联机基座测试发生异常: {ex}"); }
            finally { client.Disconnect(); host.Disconnect(); }
            return passCount;
        }

        private static int VerifyCamera(NetworkSession host, NetworkSession client)
        {
            CameraSyncData receivedCam = null;
            client.OnCameraSyncReceived += data => receivedCam = data;
            var testPos = new Vector3(2.5f, 1.2f, -3.0f);
            var testRot = Quaternion.Euler(15f, 45f, 0f);
            host.BroadcastCamera(2, testPos, testRot, 40f);
            WaitFor(host, client, () => receivedCam != null);
            return Check("实时机位流 (pos, rot, fov, shotIndex) 广播并精准同步",
                receivedCam != null && receivedCam.shotIndex == 2 && receivedCam.Position == testPos && receivedCam.Rotation == testRot);
        }

        private static int VerifyPassport(NetworkSession host, NetworkSession client, string longPassport)
        {
            PassportSyncData receivedPassport = null;
            client.OnPassportSyncReceived += data => receivedPassport = data;
            host.BroadcastPassport("SC01-S02", 2, longPassport);
            WaitFor(host, client, () => receivedPassport != null);
            return Check("镜头护照更新事件可靠广播接收",
                receivedPassport != null && receivedPassport.shotId == "SC01-S02" && receivedPassport.revision == 2
                && receivedPassport.passportJson == longPassport);
        }

        private static int VerifyComment(NetworkSession host, NetworkSession client)
        {
            CommentData receivedComment = null;
            host.OnCommentReceived += data => receivedComment = data;
            client.SendComment("机位偏高，信封被桌角挡住，请微调", "SC01-S02");
            WaitFor(host, client, () => receivedComment != null);
            return Check("制片人端向导演端发送审片批注评论",
                receivedComment != null && receivedComment.sender == "Producer" && receivedComment.text.Contains("信封被桌角挡住"));
        }

        private static int VerifyPassportRejoin(NetworkSession host, NetworkSession client, string longPassport)
        {
            PassportSyncData receivedPassport = null;
            client.OnPassportSyncReceived += data => receivedPassport = data;
            client.Disconnect();
            client.Connect("127.0.0.1", 9188, ParticipantRole.Producer, "ProducerPC");
            WaitFor(host, client, () => receivedPassport != null);
            return Check("Rejoin restores the latest complete passport", receivedPassport?.passportJson == longPassport);
        }

        private static int Check(string label, bool condition)
        {
            if (condition) { Debug.Log($"[PASS] {label}"); return 1; }
            Debug.LogError($"[FAIL] {label}");
            return 0;
        }

        private static void WaitFor(NetworkSession host, NetworkSession client, Func<bool> condition)
        {
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && timeout.ElapsedMilliseconds < 3000)
            {
                host.Tick();
                client.Tick();
                Thread.Sleep(5);
            }
        }
    }
}
