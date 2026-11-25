using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ShotField.Core
{
    [Serializable]
    public class LanRoomAnnouncement
    {
        public string roomCode;
        public string hostIp;
        public int port;
        public string hostName;
        public long timestamp;
    }

    /// <summary>
    /// 局域网 UDP 房间自动发现（默认 7778 端口）：免头显手敲 IP，支持房主广播与客端探测。
    /// </summary>
    public class LanRoomDiscovery : IDisposable
    {
        public const int DefaultDiscoveryPort = 7778;
        private readonly int _port;
        private UdpClient _udp;
        private Thread _recvThread;
        private volatile bool _running;
        private readonly object _lock = new object();
        private readonly Dictionary<string, LanRoomAnnouncement> _rooms = new Dictionary<string, LanRoomAnnouncement>();

        public LanRoomDiscovery(int port = DefaultDiscoveryPort)
        {
            _port = port;
        }

        public void StartListening()
        {
            if (_running) return;
            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            _udp.EnableBroadcast = true;
            _running = true;
            _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
            _recvThread.Start();
        }

        public void BroadcastRoom(string roomCode, string hostIp, int gamePort, string hostName)
        {
            if (string.IsNullOrEmpty(roomCode)) return;
            var msg = new LanRoomAnnouncement
            {
                roomCode = roomCode,
                hostIp = string.IsNullOrEmpty(hostIp) ? ResolveLocalIp() : hostIp,
                port = gamePort,
                hostName = string.IsNullOrEmpty(hostName) ? "ShotFieldHost" : hostName,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
            using (var sender = new UdpClient { EnableBroadcast = true })
            {
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, _port));
            }
        }

        public List<LanRoomAnnouncement> GetActiveRooms(long ttlSeconds = 5)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var active = new List<LanRoomAnnouncement>();
            lock (_lock)
            {
                var expired = new List<string>();
                foreach (var kv in _rooms)
                {
                    if (now - kv.Value.timestamp <= ttlSeconds) active.Add(kv.Value);
                    else expired.Add(kv.Key);
                }
                for (int i = 0; i < expired.Count; i++) _rooms.Remove(expired[i]);
            }
            return active;
        }

        private void ReceiveLoop()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] data = _udp.Receive(ref ep);
                    string json = Encoding.UTF8.GetString(data);
                    var msg = JsonUtility.FromJson<LanRoomAnnouncement>(json);
                    if (msg != null && !string.IsNullOrEmpty(msg.roomCode))
                    {
                        if (string.IsNullOrEmpty(msg.hostIp)) msg.hostIp = ep.Address.ToString();
                        msg.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        lock (_lock) { _rooms[msg.roomCode] = msg; }
                    }
                }
                catch { if (!_running) break; }
            }
        }

        public static string ResolveLocalIp()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
            }
            catch { /* 忽略网卡枚举异常 */ }
            return "127.0.0.1";
        }

        public void Dispose()
        {
            _running = false;
            try { _udp?.Close(); } catch { }
            _udp = null;
        }
    }
}
