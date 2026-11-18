using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    public class NetworkSession
    {
        public enum SessionState { Disconnected, Hosting, Connected }

        public SessionState State { get; private set; } = SessionState.Disconnected;
        public ParticipantRole LocalRole { get; private set; } = ParticipantRole.Director;
        public string LocalClientId { get; private set; } = "local";

        public event Action<string, string> OnClientJoined;
        public event Action<CameraSyncData> OnCameraSyncReceived;
        public event Action<PassportSyncData> OnPassportSyncReceived;
        public event Action<CommentData> OnCommentReceived;

        private TcpListener serverListener;
        private TcpClient clientSocket;
        private NetworkStream clientStream;
        private readonly List<TcpClient> connectedClients = new List<TcpClient>();
        private readonly Queue<SetPacket> incomingQueue = new Queue<SetPacket>();
        private readonly object queueLock = new object();
        private byte[] readBuffer = new byte[8192];
        private int clientCounter = 0;

        public void StartHost(int port = 7777, ParticipantRole role = ParticipantRole.Director)
        {
            Disconnect();
            LocalRole = role; LocalClientId = "host-0";
            serverListener = new TcpListener(IPAddress.Any, port);
            serverListener.Start();
            serverListener.BeginAcceptTcpClient(OnAcceptClient, null);
            State = SessionState.Hosting;
        }

        public void Connect(string ip, int port, ParticipantRole role, string displayName = "Client")
        {
            Disconnect();
            LocalRole = role;
            clientSocket = new TcpClient();
            clientSocket.Connect(ip, port);
            clientStream = clientSocket.GetStream();
            State = SessionState.Connected;
            clientStream.BeginRead(readBuffer, 0, readBuffer.Length, OnReadIncoming, clientStream);
            SendPacket(SetPacket.Create(PacketType.JoinRequest, "pending", new JoinRequestData { role = role.ToString(), displayName = displayName }));
        }

        public void BroadcastCamera(int shotIndex, Vector3 pos, Quaternion rot, float fov) =>
            Broadcast(SetPacket.Create(PacketType.CameraSync, LocalClientId, new CameraSyncData(shotIndex, pos, rot, fov)));

        public void BroadcastPassport(string shotId, int revision, string json) =>
            Broadcast(SetPacket.Create(PacketType.PassportSync, LocalClientId, new PassportSyncData { shotId = shotId, revision = revision, passportJson = json }));

        public void SendComment(string text, string shotId) =>
            Broadcast(SetPacket.Create(PacketType.Comment, LocalClientId, new CommentData { sender = LocalRole.ToString(), text = text, shotId = shotId, timestamp = DateTime.Now.ToString("HH:mm:ss") }));

        public void Broadcast(SetPacket packet)
        {
            if (State == SessionState.Hosting)
            {
                byte[] bytes = EncodePacket(packet);
                lock (connectedClients)
                {
                    for (int i = connectedClients.Count - 1; i >= 0; i--)
                    {
                        try { if (connectedClients[i].Connected) connectedClients[i].GetStream().Write(bytes, 0, bytes.Length); }
                        catch { connectedClients.RemoveAt(i); }
                    }
                }
            }
            else if (State == SessionState.Connected && clientStream != null)
            {
                byte[] bytes = EncodePacket(packet);
                clientStream.Write(bytes, 0, bytes.Length);
            }
        }

        private void OnAcceptClient(IAsyncResult ar)
        {
            if (serverListener == null) return;
            try
            {
                var client = serverListener.EndAcceptTcpClient(ar);
                lock (connectedClients) connectedClients.Add(client);
                var stream = client.GetStream();
                byte[] buf = new byte[8192];
                stream.BeginRead(buf, 0, buf.Length, OnReadFromClient, new KeyValuePair<TcpClient, byte[]>(client, buf));
                serverListener.BeginAcceptTcpClient(OnAcceptClient, null);
            }
            catch { }
        }

        private void OnReadFromClient(IAsyncResult ar)
        {
            var pair = (KeyValuePair<TcpClient, byte[]>)ar.AsyncState;
            var client = pair.Key;
            var buf = pair.Value;
            try
            {
                int bytesRead = client.GetStream().EndRead(ar);
                if (bytesRead <= 0) return;
                string msg = Encoding.UTF8.GetString(buf, 0, bytesRead);
                foreach (var line in msg.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var packet = JsonUtility.FromJson<SetPacket>(line.Trim());
                    if (packet != null) HandleServerIncoming(client, packet);
                }
                client.GetStream().BeginRead(buf, 0, buf.Length, OnReadFromClient, pair);
            }
            catch { }
        }

        private void HandleServerIncoming(TcpClient client, SetPacket packet)
        {
            if (packet.type == PacketType.JoinRequest.ToString())
            {
                var req = packet.Unpack<JoinRequestData>();
                string newId = "client-" + (++clientCounter);
                var resp = new JoinResponseData { clientId = newId, assignedRole = req.role, currentShotIndex = 0, sceneId = "SC01" };
                byte[] bytes = EncodePacket(SetPacket.Create(PacketType.JoinResponse, "host", resp));
                client.GetStream().Write(bytes, 0, bytes.Length);
                lock (queueLock) incomingQueue.Enqueue(SetPacket.Create(PacketType.JoinRequest, newId, req));
            }
            else
            {
                Broadcast(packet);
                lock (queueLock) incomingQueue.Enqueue(packet);
            }
        }

        private void OnReadIncoming(IAsyncResult ar)
        {
            var stream = (NetworkStream)ar.AsyncState;
            try
            {
                int bytesRead = stream.EndRead(ar);
                if (bytesRead <= 0) return;
                string msg = Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
                foreach (var line in msg.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var packet = JsonUtility.FromJson<SetPacket>(line.Trim());
                    if (packet != null)
                    {
                        if (packet.type == PacketType.JoinResponse.ToString())
                        {
                            var resp = packet.Unpack<JoinResponseData>();
                            LocalClientId = resp.clientId;
                        }
                        lock (queueLock) incomingQueue.Enqueue(packet);
                    }
                }
                stream.BeginRead(readBuffer, 0, readBuffer.Length, OnReadIncoming, stream);
            }
            catch { }
        }

        public void Tick()
        {
            while (true)
            {
                SetPacket packet = null;
                lock (queueLock)
                {
                    if (incomingQueue.Count > 0) packet = incomingQueue.Dequeue();
                }
                if (packet == null) break;
                DispatchPacket(packet);
            }
        }

        private void DispatchPacket(SetPacket packet)
        {
            if (packet.type == PacketType.JoinRequest.ToString())
            {
                var data = packet.Unpack<JoinRequestData>();
                OnClientJoined?.Invoke(packet.senderId, data.role);
            }
            else if (packet.type == PacketType.CameraSync.ToString())
                OnCameraSyncReceived?.Invoke(packet.Unpack<CameraSyncData>());
            else if (packet.type == PacketType.PassportSync.ToString())
                OnPassportSyncReceived?.Invoke(packet.Unpack<PassportSyncData>());
            else if (packet.type == PacketType.Comment.ToString())
                OnCommentReceived?.Invoke(packet.Unpack<CommentData>());
        }

        private byte[] EncodePacket(SetPacket p) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(p) + "\n");
        private void SendPacket(SetPacket p) { byte[] b = EncodePacket(p); clientStream?.Write(b, 0, b.Length); }

        public void Disconnect()
        {
            try { serverListener?.Stop(); } catch { }
            try { clientStream?.Close(); } catch { }
            try { clientSocket?.Close(); } catch { }
            lock (connectedClients) { foreach (var c in connectedClients) try { c.Close(); } catch { } connectedClients.Clear(); }
            serverListener = null; clientSocket = null; clientStream = null; State = SessionState.Disconnected;
        }
    }
}
