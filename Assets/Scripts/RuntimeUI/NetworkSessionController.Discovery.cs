using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public sealed partial class NetworkSessionController
    {
        private LanRoomDiscovery _lan;
        private float _nextLanBroadcast;

        private void EnsureLan()
        {
            if (_lan != null) return;
            _lan = new LanRoomDiscovery();
            _lan.StartListening();
        }

        private void TickLanDiscovery()
        {
            if (Session.State != NetworkSession.SessionState.Hosting) return;
            if (Time.unscaledTime < _nextLanBroadcast) return;
            _nextLanBroadcast = Time.unscaledTime + 2f;
            EnsureLan();
            _lan.BroadcastRoom("1001", null, Port, "ShotFieldDirector");
        }

        /// <summary>扫描局域网活跃房间（UDP 7778），返回当前存活房间列表。</summary>
        public List<LanRoomAnnouncement> ScanLanRooms()
        {
            EnsureLan();
            var rooms = _lan.GetActiveRooms();
            SetStatus(rooms.Count > 0 ? $"发现 {rooms.Count} 个房间: {rooms[0].hostIp}" : "未发现局域网房间");
            return rooms;
        }

        private void DisposeLanDiscovery()
        {
            _lan?.Dispose();
            _lan = null;
        }
    }
}
