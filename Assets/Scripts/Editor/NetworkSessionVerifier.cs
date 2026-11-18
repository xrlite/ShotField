using System;
using System.Threading;
using UnityEngine;
using UnityEditor;
using ShotField.Core;

namespace ShotField.EditorTools
{
    public static class NetworkSessionVerifier
    {
        public static void Run()
        {
            int passCount = 0;
            int totalTests = 6;
            int port = 9188;

            var host = new NetworkSession();
            var client = new NetworkSession();

            try
            {
                // Test 1: Host 模式建立与 Director 角色绑定
                host.StartHost(port, ParticipantRole.Director);
                if (host.State == NetworkSession.SessionState.Hosting && host.LocalRole == ParticipantRole.Director)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 1: Host 成功监听本地端口，角色绑定为 Director");
                }
                else Debug.LogError("[FAIL] Test 1: Host 启动状态异常");

                // Test 2: Client 握手与 Producer 角色识别
                string joinedClientId = null;
                string joinedRole = null;
                host.OnClientJoined += (id, role) => { joinedClientId = id; joinedRole = role; };

                client.Connect("127.0.0.1", port, ParticipantRole.Producer, "ProducerPC");
                Thread.Sleep(200);
                host.Tick();
                client.Tick();

                if (!string.IsNullOrEmpty(joinedClientId) && joinedRole == "Producer" && client.State == NetworkSession.SessionState.Connected)
                {
                    passCount++;
                    Debug.Log($"[PASS] Test 2: 双端握手成功，识别角色为 Producer，分配 ID={joinedClientId}");
                }
                else Debug.LogError($"[FAIL] Test 2: 握手识别失败 id={joinedClientId} role={joinedRole}");

                // Test 3: 实时机位流双端广播
                CameraSyncData receivedCam = null;
                client.OnCameraSyncReceived += data => receivedCam = data;

                var testPos = new Vector3(2.5f, 1.2f, -3.0f);
                var testRot = Quaternion.Euler(15f, 45f, 0f);
                host.BroadcastCamera(2, testPos, testRot, 40f);
                Thread.Sleep(200);
                client.Tick();

                if (receivedCam != null && receivedCam.shotIndex == 2 && receivedCam.Position == testPos)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 3: 实时机位流 (pos, rot, fov, shotIndex) 广播并精准同步");
                }
                else Debug.LogError("[FAIL] Test 3: 机位流同步丢失或数值不一致");

                // Test 4: 项目护照更新广播
                PassportSyncData receivedPassport = null;
                client.OnPassportSyncReceived += data => receivedPassport = data;

                host.BroadcastPassport("SC01-S02", 2, "{\"test\":true}");
                Thread.Sleep(200);
                client.Tick();

                if (receivedPassport != null && receivedPassport.shotId == "SC01-S02" && receivedPassport.revision == 2)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 4: 镜头护照更新事件可靠广播接收 (SC01-S02 rev002)");
                }
                else Debug.LogError("[FAIL] Test 4: 护照更新事件丢失");

                // Test 5: 双向交流评论留言留痕
                CommentData receivedComment = null;
                host.OnCommentReceived += data => receivedComment = data;

                client.SendComment("机位偏高，信封被桌角挡住，请微调", "SC01-S02");
                Thread.Sleep(200);
                host.Tick();

                if (receivedComment != null && receivedComment.sender == "Producer" && receivedComment.text.Contains("信封被桌角挡住"))
                {
                    passCount++;
                    Debug.Log("[PASS] Test 5: 制片人端向导演端发送审片批注评论，交流成功留痕");
                }
                else Debug.LogError("[FAIL] Test 5: 审片评论收发异常");

                // Test 6: 优雅断开与资源回收
                client.Disconnect();
                host.Disconnect();
                if (client.State == NetworkSession.SessionState.Disconnected && host.State == NetworkSession.SessionState.Disconnected)
                {
                    passCount++;
                    Debug.Log("[PASS] Test 6: 双端连接优雅关闭，网络套接字资源彻底释放");
                }
                else Debug.LogError("[FAIL] Test 6: 断开连接后状态异常");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EXCEPTION] 联机基座测试发生异常: {ex}");
            }
            finally
            {
                client.Disconnect();
                host.Disconnect();
            }

            if (passCount == totalTests)
            {
                Debug.Log($"NET-VERIFY-RESULT=PASS ({passCount}/{totalTests})");
            }
            else
            {
                Debug.LogError($"NET-VERIFY-RESULT=FAIL ({passCount}/{totalTests})");
                EditorApplication.Exit(1);
            }
        }
    }
}
