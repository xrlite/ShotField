using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    public partial class NetworkSession
    {
        public enum SessionState { Disconnected, Hosting, Connected, Connecting }

        public SessionState State { get; private set; }
        public ParticipantRole LocalRole { get; private set; }
        public string LocalClientId { get; private set; } = "local";

        public event Action<string, string> OnClientJoined;
        public event Action<CameraSyncData> OnCameraSyncReceived;
        public event Action<PassportSyncData> OnPassportSyncReceived;
        public event Action<CommentData> OnCommentReceived;

        private TcpListener listener;
        private SessionPeer upstream;
        private readonly List<SessionPeer> peers = new List<SessionPeer>();
        private readonly ConcurrentQueue<Action> incoming = new ConcurrentQueue<Action>();
        private readonly Dictionary<string, SetPacket> passports = new Dictionary<string, SetPacket>();
        private SetPacket latestCamera;
        private int generation;

        public void StartHost(int port = 7777, ParticipantRole role = ParticipantRole.Director)
        {
            Disconnect();
            var server = new TcpListener(IPAddress.Any, port);
            server.Start();
            listener = server;
            LocalRole = role;
            LocalClientId = "host";
            State = SessionState.Hosting;
            _ = Accept(server, generation);
        }

        public async void Connect(string ip, int port, ParticipantRole role, string displayName = "Client")
        {
            Disconnect();
            int epoch = generation;
            LocalRole = role;
            var socket = new TcpClient();
            var peer = new SessionPeer(socket);
            upstream = peer;
            State = SessionState.Connecting;
            try
            {
                await socket.ConnectAsync(ip, port).ConfigureAwait(false);
                if (!peer.Send(SetPacket.Create(PacketType.JoinRequest, "", new JoinRequestData
                    { role = role.ToString(), displayName = displayName }))) return;
                await Receive(peer, epoch, false).ConfigureAwait(false);
            }
            catch (Exception ex) { Post(epoch, () => Debug.LogWarning(ex.Message)); }
            finally
            {
                peer.Dispose();
                Post(epoch, Disconnect);
            }
        }

        public void BroadcastCamera(int index, Vector3 position, Quaternion rotation, float fov)
        {
            if (State != SessionState.Hosting) return;
            CurrentShotIndex = index;
            latestCamera = SetPacket.Create(PacketType.CameraSync, LocalClientId,
                new CameraSyncData(index, position, rotation, fov));
            Broadcast(latestCamera);
        }

        public void BroadcastPassport(string shotId, int revision, string json)
        {
            if (State != SessionState.Hosting) return;
            var packet = SetPacket.Create(PacketType.PassportSync, LocalClientId, new PassportSyncData
                { shotId = shotId, revision = revision, passportJson = json });
            passports[shotId] = packet;
            Broadcast(packet);
        }

        public void SendComment(string text, string shotId)
        {
            var packet = SetPacket.Create(PacketType.Comment, LocalClientId, new CommentData
                { sender = LocalRole.ToString(), text = text, shotId = shotId, timestamp = DateTimeOffset.UtcNow.ToString("O") });
            if (State == SessionState.Hosting)
            {
                Broadcast(packet);
                OnCommentReceived?.Invoke(packet.Unpack<CommentData>());
            }
            else if (State == SessionState.Connected) upstream.Send(packet);
        }

        private void Broadcast(SetPacket packet) => peers.ForEach(peer => { if (peer.Role.HasValue) peer.Send(packet); });

        private async Task Accept(TcpListener server, int epoch)
        {
            try
            {
                while (true)
                {
                    var socket = await server.AcceptTcpClientAsync().ConfigureAwait(false);
                    var peer = new SessionPeer(socket);
                    incoming.Enqueue(() =>
                    {
                        if (epoch != generation) { peer.Dispose(); return; }
                        peers.Add(peer);
                        _ = Receive(peer, epoch, true);
                    });
                }
            }
            catch (Exception ex)
            {
                Post(epoch, () => { Debug.LogWarning(ex.Message); Disconnect(); });
            }
        }

        private async Task Receive(SessionPeer peer, int epoch, bool hosting)
        {
            try
            {
                await peer.Read(packet => Post(epoch, () => Dispatch(peer, packet, hosting))).ConfigureAwait(false);
            }
            catch (Exception ex) { Post(epoch, () => Debug.LogWarning(ex.Message)); }
            finally
            {
                peer.Dispose();
                if (hosting) Post(epoch, () => peers.Remove(peer));
            }
        }

        private void Dispatch(SessionPeer peer, SetPacket packet, bool hosting)
        {
            if (hosting)
            {
                if (!peer.Role.HasValue && packet.type == nameof(PacketType.JoinRequest))
                {
                    var request = packet.Unpack<JoinRequestData>();
                    if (!Enum.TryParse(request.role, out ParticipantRole role) || !Enum.IsDefined(typeof(ParticipantRole), role)
                        || role == ParticipantRole.Director) { peer.Dispose(); return; }
                    peer.Role = role;
                    peer.Send(SetPacket.Create(PacketType.JoinResponse, LocalClientId,
                        new JoinResponseData
                        {
                            clientId = peer.Id, assignedRole = role.ToString(),
                            currentShotIndex = CurrentShotIndex, sceneId = "SC01"
                        }));
                    if (latestCamera != null) peer.Send(latestCamera);
                    foreach (var passport in passports.Values) peer.Send(passport);
                    OnClientJoined?.Invoke(peer.Id, role.ToString());
                }
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
            }
        }

        private void Post(int epoch, Action action) => incoming.Enqueue(() => { if (epoch == generation) action(); });

        public void Tick()
        {
            for (int i = 0; i < 64 && incoming.TryDequeue(out var action); i++)
            {
                try { action(); } catch (Exception ex) { Debug.LogWarning(ex.Message); }
            }
        }
    }
}
