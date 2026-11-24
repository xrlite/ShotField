using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    /// <summary>
    /// 演员走位编辑同步：Client 上行后本地只回调一次；Host 按 actorId 校验 revision、显式回执并保存最新快照。
    /// </summary>
    public partial class NetworkSession
    {
        private const string SceneEditAcceptedReason = "accepted";
        private const string SceneEditStaleReason = "stale-revision";
        private const string SceneEditDuplicateReason = "duplicate-revision";
        private const string SceneEditInvalidReason = "invalid-scene-edit";

        private readonly Dictionary<string, SetPacket> latestSceneEdits = new Dictionary<string, SetPacket>();
        private readonly Dictionary<string, int> sceneEditRevisions = new Dictionary<string, int>();

        public event Action<SceneEditData> OnSceneEditReceived;
        public event Action<SceneEditResult> OnSceneEditResultReceived;

        /// <summary>Host 本地接受后回调；Client 先本地回调，再由 Host 回执确认或拒绝。</summary>
        public void BroadcastSceneEdit(SceneEditData data)
        {
            if (State != SessionState.Hosting && (State != SessionState.Connected || upstream == null)) return;
            if (data.clientTimestamp == 0) data.clientTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var packet = SetPacket.Create(PacketType.SceneEdit, LocalClientId, data);
            if (State == SessionState.Hosting)
            {
                DispatchHostSceneEdit(null, packet);
                return;
            }
            upstream.Send(packet);
            OnSceneEditReceived?.Invoke(data);
        }

        // Host 不回发 SceneEdit 给发送者，因此 Client 的本地回调不会与网络回调重复。
        private void DispatchHostSceneEdit(SessionPeer peer, SetPacket packet)
        {
            var data = packet.Unpack<SceneEditData>();
            if (!TryAcceptSceneEdit(data, out int currentRevision, out string reason))
            {
                SendSceneEditResult(peer, data, false, currentRevision, reason);
                return;
            }
            latestSceneEdits[data.actorId] = packet;
            foreach (var other in peers)
            {
                if (other != peer && other.Role.HasValue) other.Send(packet);
            }
            OnSceneEditReceived?.Invoke(data);
            SendSceneEditResult(peer, data, true, data.revision, SceneEditAcceptedReason);
        }

        private bool TryAcceptSceneEdit(SceneEditData data, out int currentRevision, out string reason)
        {
            currentRevision = 0;
            if (string.IsNullOrEmpty(data.actorId) || data.revision < 0)
            {
                reason = SceneEditInvalidReason;
                return false;
            }
            sceneEditRevisions.TryGetValue(data.actorId, out currentRevision);
            if (data.revision < currentRevision)
            {
                reason = SceneEditStaleReason;
                return false;
            }
            if (data.revision == currentRevision && latestSceneEdits.ContainsKey(data.actorId))
            {
                reason = SceneEditDuplicateReason;
                return false;
            }
            sceneEditRevisions[data.actorId] = data.revision;
            reason = SceneEditAcceptedReason;
            return true;
        }

        private void SendSceneEditResult(SessionPeer peer, SceneEditData edit, bool accepted,
            int currentRevision, string reason)
        {
            var result = new SceneEditResult
            {
                actorId = edit.actorId,
                submittedRevision = edit.revision,
                accepted = accepted,
                currentRevision = currentRevision,
                reason = reason
            };
            if (peer == null) OnSceneEditResultReceived?.Invoke(result);
            else peer.Send(SetPacket.Create(PacketType.SceneEditResult, LocalClientId, result));
        }

        private void SendSceneEditSnapshot(SessionPeer peer)
        {
            foreach (var packet in latestSceneEdits.Values) peer.Send(packet);
        }

        private void DispatchClientSceneEdit(SetPacket packet)
        {
            OnSceneEditReceived?.Invoke(packet.Unpack<SceneEditData>());
        }
    }
}
