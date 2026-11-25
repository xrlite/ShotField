using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 入站包路由。从 NetworkSession.cs 抽出以守住单文件行数预算；
    /// 新增 ActorPose：Host 只转发给其他 peer，Client 直接回调本地应用层。
    /// </summary>
    public partial class NetworkSession
    {
        private void Dispatch(SessionPeer peer, SetPacket packet, bool hosting)
        {
            if (hosting)
            {
                if (!peer.Role.HasValue && packet.type == nameof(PacketType.JoinRequest)) HandleJoin(peer, packet);
                else if (peer.Role.HasValue && packet.type == nameof(PacketType.Heartbeat))
                {
                    HandleHeartbeat();
                }
                else if (peer.Role.HasValue && packet.type == nameof(PacketType.Comment))
                {
                    var comment = packet.Unpack<CommentData>();
                    comment.sender = peer.Role.Value.ToString();
                    Broadcast(SetPacket.Create(PacketType.Comment, peer.Id, comment));
                    OnCommentReceived?.Invoke(comment);
                }
                else if (peer.Role.HasValue && packet.type == nameof(PacketType.SceneEdit)) DispatchHostSceneEdit(peer, packet);
                else if (peer.Role.HasValue && packet.type == nameof(PacketType.ActorPose)) DispatchActorPoseHost(peer, packet);
                return;
            }

            switch (packet.type)
            {
                case nameof(PacketType.JoinResponse):
                    var resp = packet.Unpack<JoinResponseData>();
                    LocalClientId = resp.clientId; InitialShotIndex = resp.currentShotIndex; State = SessionState.Connected;
                    break;
                case nameof(PacketType.Heartbeat): HandleHeartbeat(); break;
                case nameof(PacketType.CameraSync): OnCameraSyncReceived?.Invoke(packet.Unpack<CameraSyncData>()); break;
                case nameof(PacketType.PassportSync): OnPassportSyncReceived?.Invoke(packet.Unpack<PassportSyncData>()); break;
                case nameof(PacketType.Comment): OnCommentReceived?.Invoke(packet.Unpack<CommentData>()); break;
                case nameof(PacketType.SceneEdit): DispatchClientSceneEdit(packet); break;
                case nameof(PacketType.SceneEditResult): OnSceneEditResultReceived?.Invoke(packet.Unpack<SceneEditResult>()); break;
                case nameof(PacketType.ActorPose): DispatchActorPoseClient(packet); break;
            }
        }
    }
}
