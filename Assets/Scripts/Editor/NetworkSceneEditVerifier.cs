using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using ShotField.Core;

namespace ShotField.EditorTools
{
    public static class NetworkSceneEditVerifier
    {
        public const int TotalTests = 6;

        public static void Run(out int passed, out int total)
        {
            passed = 0;
            total = TotalTests;
            var host = new NetworkSession();
            var client = new NetworkSession();
            var observer = new NetworkSession();
            try
            {
                host.StartHost(9189, ParticipantRole.Director);
                client.Connect("127.0.0.1", 9189, ParticipantRole.Actor, "ActorPC");
                observer.Connect("127.0.0.1", 9189, ParticipantRole.Observer, "ObserverPC");
                if (!WaitFor(host, client, observer, () => client.State == NetworkSession.SessionState.Connected
                    && observer.State == NetworkSession.SessionState.Connected)) throw new InvalidOperationException("SceneEdit peers failed to join.");
                passed += VerifyHostToClient(host, client);
                passed += VerifyClientToHost(host, client);
                passed += VerifyRevisionRejection(host, client, observer);
                passed += VerifyReconnectSnapshot(host, client, observer);
                passed += VerifyActorRevisions(host, client, observer);
                passed += VerifyPayloadCompatibility();
            }
            catch (Exception ex) { Debug.LogError($"[EXCEPTION] SceneEdit 验证发生异常: {ex}"); }
            finally { client.Disconnect(); observer.Disconnect(); host.Disconnect(); }
            string result = passed == total ? "PASS" : "FAIL";
            if (passed == total) Debug.Log($"NET-SCENE-EDIT-RESULT={result} ({passed}/{total})");
            else
            {
                Debug.LogError($"NET-SCENE-EDIT-RESULT={result} ({passed}/{total})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static int VerifyHostToClient(NetworkSession host, NetworkSession client)
        {
            var received = new List<SceneEditData>();
            var results = new List<SceneEditResult>();
            client.OnSceneEditReceived += received.Add;
            host.OnSceneEditResultReceived += results.Add;
            host.BroadcastSceneEdit(CreateEdit("actorA", 3, 1f));
            WaitFor(host, client, null, () => received.Count > 0);
            return Check("Host 到 Client 的 SceneEdit 广播且 Client 回调一次",
                received.Count == 1 && Matches(received[0], "actorA", 3, 1f)
                && results.Count == 1 && results[0].accepted && results[0].currentRevision == 3
                && results[0].actorId == "actorA" && results[0].submittedRevision == 3);
        }

        private static int VerifyClientToHost(NetworkSession host, NetworkSession client)
        {
            var hostReceived = new List<SceneEditData>();
            var clientReceived = new List<SceneEditData>();
            var results = new List<SceneEditResult>();
            host.OnSceneEditReceived += hostReceived.Add;
            client.OnSceneEditReceived += clientReceived.Add;
            client.OnSceneEditResultReceived += results.Add;
            client.BroadcastSceneEdit(CreateEdit("actorB", 4, 2f));
            WaitFor(host, client, null, () => hostReceived.Count == 1 && results.Count == 1);
            return Check("Client 到 Host 的 SceneEdit、单次本地回调与 accepted 回执",
                hostReceived.Count == 1 && Matches(hostReceived[0], "actorB", 4, 2f)
                && clientReceived.Count == 1 && clientReceived[0].clientTimestamp > 0
                && results.Count == 1 && results[0].accepted && results[0].currentRevision == 4
                && results[0].actorId == "actorB" && results[0].submittedRevision == 4
                && results[0].reason == "accepted");
        }

        private static int VerifyRevisionRejection(NetworkSession host, NetworkSession client, NetworkSession observer)
        {
            var hostReceived = new List<SceneEditData>();
            var observerReceived = new List<SceneEditData>();
            var results = new List<SceneEditResult>();
            host.OnSceneEditReceived += hostReceived.Add;
            observer.OnSceneEditReceived += observerReceived.Add;
            client.OnSceneEditResultReceived += results.Add;
            host.BroadcastSceneEdit(CreateEdit("actorC", 5, 5f));
            WaitFor(host, client, observer, () => observerReceived.Count == 1);
            client.BroadcastSceneEdit(CreateEdit("actorC", 4, 99f));
            WaitFor(host, client, observer, () => results.Count == 1);
            WaitFor(host, client, observer, () => false, 100);
            int hostC = hostReceived.FindAll(data => data.actorId == "actorC").Count;
            int observerC = observerReceived.FindAll(data => data.actorId == "actorC").Count;
            return Check("旧 revision 显式拒绝且不覆盖 Host 或转发其他 peer",
                results.Count == 1 && !results[0].accepted && results[0].currentRevision == 5
                && results[0].reason == "stale-revision" && hostC == 1
                && results[0].actorId == "actorC" && results[0].submittedRevision == 4
                && observerC == 1 && Matches(hostReceived.Find(data => data.actorId == "actorC"), "actorC", 5, 5f));
        }

        private static int VerifyReconnectSnapshot(NetworkSession host, NetworkSession client, NetworkSession observer)
        {
            var snapshots = new List<SceneEditData>();
            client.OnSceneEditReceived += snapshots.Add;
            client.Disconnect();
            snapshots.Clear();
            client.Connect("127.0.0.1", 9189, ParticipantRole.Actor, "ActorPC");
            WaitFor(host, client, observer, () => client.State == NetworkSession.SessionState.Connected && snapshots.Count >= 3);
            SceneEditData latestC = snapshots.Find(data => data.actorId == "actorC");
            return Check("新 peer JoinResponse 后补发最新 SceneEdit 快照，重连可恢复",
                snapshots.Count == 3 && latestC != null && Matches(latestC, "actorC", 5, 5f));
        }

        private static int VerifyActorRevisions(NetworkSession host, NetworkSession client, NetworkSession observer)
        {
            var results = new List<SceneEditResult>();
            client.OnSceneEditResultReceived += results.Add;
            client.BroadcastSceneEdit(CreateEdit("actorD", 1, 6f));
            WaitFor(host, client, observer, () => results.Count == 1);
            client.BroadcastSceneEdit(CreateEdit("actorE", 1, 7f));
            WaitFor(host, client, observer, () => results.Count == 2);
            return Check("不同 actorId 独立维护 revision", results.Count == 2
                && results[0].accepted && results[0].currentRevision == 1
                && results[0].actorId == "actorD" && results[0].submittedRevision == 1
                && results[1].accepted && results[1].currentRevision == 1
                && results[1].actorId == "actorE" && results[1].submittedRevision == 1);
        }

        private static int VerifyPayloadCompatibility()
        {
            var source = CreateEdit("actorF", 8, 8f);
            source.clientTimestamp = 123456789;
            var copy = JsonUtility.FromJson<SceneEditData>(JsonUtility.ToJson(source));
            var legacy = JsonUtility.FromJson<SceneEditData>(
                "{\"actorId\":\"legacy\",\"px\":1.5,\"py\":2.5,\"pz\":3.5,\"ry\":4.5}");
            bool enumOk = Enum.TryParse(nameof(PacketType.SceneEditResult), out PacketType type) && Enum.IsDefined(typeof(PacketType), type);
            return Check("JsonUtility 元数据往返、旧包可选字段默认值与结果包枚举",
                Matches(copy, "actorF", 8, 8f) && copy.clientTimestamp == source.clientTimestamp && enumOk
                && legacy.actorId == "legacy" && legacy.revision == 0 && legacy.clientTimestamp == 0 && legacy.px == 1.5f);
        }

        private static SceneEditData CreateEdit(string actorId, int revision, float px)
        {
            return new SceneEditData { actorId = actorId, revision = revision, px = px, py = 0f, pz = 0f, ry = 0f };
        }

        private static bool Matches(SceneEditData data, string actorId, int revision, float px)
        {
            return data != null && data.actorId == actorId && data.revision == revision && data.px == px;
        }

        private static int Check(string label, bool condition)
        {
            if (condition) { Debug.Log($"[PASS] {label}"); return 1; }
            Debug.LogError($"[FAIL] {label}");
            return 0;
        }

        private static bool WaitFor(NetworkSession host, NetworkSession client, NetworkSession observer,
            Func<bool> condition, int timeoutMs = 3000)
        {
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && timeout.ElapsedMilliseconds < timeoutMs)
            {
                host.Tick();
                client.Tick();
                if (observer != null) observer.Tick();
                Thread.Sleep(5);
            }
            return condition();
        }
    }
}
