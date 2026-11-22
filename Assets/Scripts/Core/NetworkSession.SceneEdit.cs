using System;

namespace ShotField.Core
{
    /// <summary>
    /// 演员走位编辑同步：Host 转发给其他已入房间 peer，Client 上行到 Host；两端都在本地回调。
    /// 复用主文件的 Broadcast / peers / upstream，不另起一套 TCP 代码。
    /// </summary>
    public partial class NetworkSession
    {
        public event Action<SceneEditData> OnSceneEditReceived;

        /// <summary>广播一条演员走位编辑。Host：发全部已入房间 peer 并本地回调；Client：上行到 upstream。</summary>
        public void BroadcastSceneEdit(SceneEditData data)
        {
            var packet = SetPacket.Create(PacketType.SceneEdit, LocalClientId, data);
            if (State == SessionState.Hosting)
            {
                Broadcast(packet);
                OnSceneEditReceived?.Invoke(data);
            }
            else if (State == SessionState.Connected && upstream != null)
            {
                upstream.Send(packet);
            }
        }

        // Host 收到 peer 的走位编辑：转发给其他 peer（不回给发送者本人），再本地回调。
        private void DispatchHostSceneEdit(SessionPeer peer, SetPacket packet)
        {
            var data = packet.Unpack<SceneEditData>();
            foreach (var other in peers)
            {
                if (other != peer && other.Role.HasValue) other.Send(packet);
            }
            OnSceneEditReceived?.Invoke(data);
        }

        // Client 收到 Host 转发的走位编辑：触发本地回调。
        private void DispatchClientSceneEdit(SetPacket packet)
        {
            OnSceneEditReceived?.Invoke(packet.Unpack<SceneEditData>());
        }
    }
}
