using System;

namespace ShotField.Core
{
    public partial class NetworkSession
    {
        public int CurrentShotIndex { get; private set; }
        public int InitialShotIndex { get; private set; }
        public long LastHeartbeatUnixMs { get; private set; }

        public void SendHeartbeat()
        {
            var pkt = SetPacket.Create(PacketType.Heartbeat, LocalClientId, new JoinRequestData
            {
                role = LocalRole.ToString(),
                displayName = LocalClientId
            });
            if (State == SessionState.Hosting) Broadcast(pkt);
            else if (State == SessionState.Connected && upstream != null) upstream.Send(pkt);
        }

        private void HandleHeartbeat()
        {
            LastHeartbeatUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void Disconnect()
        {
            generation++;
            listener?.Stop();
            listener = null;
            upstream?.Dispose();
            upstream = null;
            foreach (var peer in peers) peer.Dispose();
            peers.Clear();
            passports.Clear();
            latestSceneEdits.Clear();
            sceneEditRevisions.Clear();
            latestCamera = null;
            State = SessionState.Disconnected;
            LocalClientId = "local";
        }
    }
}
