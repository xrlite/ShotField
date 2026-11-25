using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 演员位姿实时通道（高频，不走 revision 校验）：
    /// Host 收到后仅转发给其他 peer 并回调本地；Client 直接回调。
    /// 谁发谁不回调自己，避免本地对象被自己的回声覆盖。
    /// </summary>
    public partial class NetworkSession
    {
        public event System.Action<ActorPoseData> OnActorPoseReceived;

        public void BroadcastActorPose(ActorPoseData data)
        {
            if (data == null || string.IsNullOrEmpty(data.actorId)) return;
            if (State != SessionState.Hosting && (State != SessionState.Connected || upstream == null)) return;
            var packet = SetPacket.Create(PacketType.ActorPose, LocalClientId, data);
            if (State == SessionState.Hosting)
            {
                DispatchActorPoseHost(null, packet);
                return;
            }
            upstream.Send(packet);
        }

        private void DispatchActorPoseHost(SessionPeer peer, SetPacket packet)
        {
            foreach (var other in peers)
            {
                if (other != peer && other.Role.HasValue) other.Send(packet);
            }
            if (peer != null) OnActorPoseReceived?.Invoke(packet.Unpack<ActorPoseData>());
        }

        private void DispatchActorPoseClient(SetPacket packet)
        {
            OnActorPoseReceived?.Invoke(packet.Unpack<ActorPoseData>());
        }
    }
}
