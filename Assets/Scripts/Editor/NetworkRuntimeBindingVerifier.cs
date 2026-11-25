#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 联机运行期接线门禁：验证 SceneEdit 与 ActorPose 两条通道能从会话层一路落到场景 Transform。
    /// 覆盖 id 规范化、Host→Client 落地、高频包、未知 id 拒绝、旧包兼容。
    /// 同进程回环；双机与真机仍 pending。
    /// </summary>
    public static class NetworkRuntimeBindingVerifier
    {
        public const string Tag = "NET-RUNTIME-BINDING-RESULT";
        private const int Total = 5;
        private const int Port = 9195;
        private const string ProbeId = "Actor_BindingProbe_01";

        [MenuItem("ShotField/Verifiers/Run Network Runtime Binding Verifier")]
        public static void Run()
        {
            int passed = 0;
            var host = new NetworkSession();
            var client = new NetworkSession();
            var edits = new List<SceneEditData>();
            var poses = new List<ActorPoseData>();
            var targetGo = new GameObject("[BindingTest_Actor_Red]");
            try
            {
                // 用独特探针 id，避免命中场景里真实演员而绕过注入解析器。
                string targetKey = RemoteActorSync.NormalizeId(ProbeId);
                RemoteActorSync.OverrideTransformResolver = key => key == targetKey ? targetGo.transform : null;

                host.StartHost(Port, ParticipantRole.Director);
                client.Connect("127.0.0.1", Port, ParticipantRole.Actor, "ActorPC");
                client.OnSceneEditReceived += edits.Add;
                client.OnActorPoseReceived += poses.Add;
                if (!WaitFor(host, client, () => client.State == NetworkSession.SessionState.Connected))
                    throw new InvalidOperationException("联机 peers 未成功建立");

                passed += Check("actorId 规范化匹配（大小写/下划线/actor 前缀不敏感）", VerifyNormalize());
                passed += Check("SceneEdit 落到场景 Transform（位移<1cm、角差<0.5°）",
                    VerifySceneEdit(host, client, edits, targetGo));
                passed += Check("ActorPose 高频包落地（20 帧末帧位移<1cm）",
                    VerifyPose(host, client, poses, targetGo));
                passed += Check("未知 actorId 显式拒绝且不改动任何演员", VerifyUnknown(targetGo));
                passed += Check("旧包仅含 ry 时退化为偏航旋转", VerifyLegacy());
            }
            catch (Exception ex) { Debug.LogError("[NET-RUNTIME-BINDING] 异常: " + ex); }
            finally
            {
                RemoteActorSync.OverrideTransformResolver = null;
                client.Disconnect();
                host.Disconnect();
                UnityEngine.Object.DestroyImmediate(targetGo);
            }

            string result = passed == Total ? "PASS" : "FAIL";
            if (passed == Total) Debug.Log($"{Tag}={result} ({passed}/{Total})");
            else Debug.LogError($"{Tag}={result} ({passed}/{Total})");

            // batchmode 下由本方法负责退出，否则进程会挂在编辑器循环里。
            if (Application.isBatchMode) EditorApplication.Exit(passed == Total ? 0 : 1);
        }

        private static bool VerifyNormalize()
        {
            string a = RemoteActorSync.NormalizeId("Actor_Red_Protagonist");
            string b = RemoteActorSync.NormalizeId("actor_red_protagonist");
            string c = RemoteActorSync.NormalizeId("actorRedProtagonist");
            return a == b && b == c && a == "redprotagonist";
        }

        private static bool VerifySceneEdit(NetworkSession host, NetworkSession client,
            List<SceneEditData> edits, GameObject target)
        {
            edits.Clear();
            var payload = new SceneEditData
            {
                actorId = ProbeId,
                px = 0.6f, py = 0f, pz = 1.2f,
                ry = 30f, fullRotation = false,
                revision = 1
            };
            host.BroadcastSceneEdit(payload);
            if (!WaitFor(host, client, () => edits.Count > 0)) return false;

            var received = edits[edits.Count - 1];
            if (received.actorId != payload.actorId || Mathf.Abs(received.px - 0.6f) > 0.0001f) return false;
            bool applied = RemoteActorSync.ApplySceneEdit(received, out string reason);
            if (!applied || reason != "applied") return false;

            float posErr = Vector3.Distance(target.transform.position, new Vector3(0.6f, 0f, 1.2f));
            float angleErr = Quaternion.Angle(target.transform.rotation, Quaternion.Euler(0f, 30f, 0f));
            return posErr < 0.01f && angleErr < 0.5f;
        }

        private static bool VerifyPose(NetworkSession host, NetworkSession client,
            List<ActorPoseData> poses, GameObject target)
        {
            poses.Clear();
            var last = new ActorPoseData();
            for (int i = 0; i < 20; i++)
            {
                last = new ActorPoseData
                {
                    actorId = ProbeId,
                    px = 0.02f * i, py = 0f, pz = -0.5f,
                    rx = 0f, ry = 0.5f, rz = 0f, rw = 0.8660254f,
                    actionTag = "Walk",
                    sequence = i
                };
                host.BroadcastActorPose(last);
                int expect = i + 1;
                if (!WaitFor(host, client, () => poses.Count >= expect, 800)) return false;
            }

            if (!RemoteActorSync.ApplyPose(last, out string reason) || reason != "applied") return false;
            float posErr = Vector3.Distance(target.transform.position, new Vector3(0.38f, 0f, -0.5f));
            float angleErr = Quaternion.Angle(target.transform.rotation, new Quaternion(0f, 0.5f, 0f, 0.8660254f));
            return poses.Count == 20 && posErr < 0.01f && angleErr < 0.5f;
        }

        private static bool VerifyUnknown(GameObject target)
        {
            Vector3 before = target.transform.position;
            bool ok = !RemoteActorSync.Apply("ghost_actor_999", new Vector3(9f, 9f, 9f), Quaternion.identity, out string reason);
            return ok && reason == "actor-not-found" && Vector3.Distance(target.transform.position, before) < 0.0001f;
        }

        private static bool VerifyLegacy()
        {
            var legacy = JsonUtility.FromJson<SceneEditData>(
                "{\"actorId\":\"legacy\",\"px\":1.5,\"py\":2.5,\"pz\":3.5,\"ry\":45}");
            if (legacy == null || legacy.fullRotation) return false;
            float angle = Quaternion.Angle(legacy.Rotation, Quaternion.Euler(0f, 45f, 0f));
            return angle < 0.01f && Mathf.Abs(legacy.px - 1.5f) < 0.0001f;
        }

        private static int Check(string label, bool condition)
        {
            if (condition) { Debug.Log("[PASS] " + label); return 1; }
            Debug.LogError("[FAIL] " + label);
            return 0;
        }

        private static bool WaitFor(NetworkSession host, NetworkSession client, Func<bool> condition, int timeoutMs = 3000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            {
                host.Tick();
                client.Tick();
                Thread.Sleep(5);
            }
            return condition();
        }
    }
}
#endif
