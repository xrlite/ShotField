using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    internal sealed class SessionPeer : IDisposable
    {
        private const int MaxFrameSize = 2 * 1024 * 1024;
        private const int MaxPendingWrites = 64;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly TcpClient socket;
        private readonly System.Collections.Generic.Queue<byte[]> writes = new System.Collections.Generic.Queue<byte[]>();
        private bool writing;
        private int closed;

        public string Id { get; } = Guid.NewGuid().ToString("N");
        public ParticipantRole? Role { get; set; }

        public SessionPeer(TcpClient socket) => this.socket = socket;

        public bool Send(SetPacket packet)
        {
            if (Volatile.Read(ref closed) != 0) return false;
            var bytes = Utf8.GetBytes(JsonUtility.ToJson(packet) + "\n");
            if (bytes.Length > MaxFrameSize) throw new InvalidDataException("Session packet exceeds the size limit.");
            lock (writes)
            {
                if (writes.Count >= MaxPendingWrites)
                {
                    Dispose();
                    return false;
                }
                writes.Enqueue(bytes);
                if (!writing)
                {
                    writing = true;
                    _ = WritePending();
                }
            }
            return true;
        }

        private async Task WritePending()
        {
            try
            {
                var stream = socket.GetStream();
                while (true)
                {
                    byte[] bytes;
                    lock (writes)
                    {
                        if (writes.Count == 0) { writing = false; return; }
                        bytes = writes.Dequeue();
                    }
                    await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                if (Volatile.Read(ref closed) == 0) Debug.LogWarning(ex.Message);
                Dispose();
            }
        }

        public async Task Read(Action<SetPacket> onPacket)
        {
            var stream = socket.GetStream();
            var buffer = new byte[8192];
            using var frame = new MemoryStream();
            int count;
            while ((count = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
            {
                int start = 0;
                while (start < count)
                {
                    int lineEnd = Array.IndexOf(buffer, (byte)10, start, count - start);
                    int length = (lineEnd < 0 ? count : lineEnd) - start;
                    if (frame.Length + length >= MaxFrameSize) throw new InvalidDataException("Session packet exceeds the size limit.");
                    frame.Write(buffer, start, length);
                    if (lineEnd < 0) break;
                    var packet = JsonUtility.FromJson<SetPacket>(Utf8.GetString(frame.GetBuffer(), 0, (int)frame.Length));
                    frame.SetLength(0);
                    if (packet != null) onPacket(packet);
                    start = lineEnd + 1;
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) == 0) socket.Dispose();
        }
    }
}
