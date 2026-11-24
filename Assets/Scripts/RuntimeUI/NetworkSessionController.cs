using System;
using System.Net;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>运行时唯一联机会话：桌面与 VR 共用，负责主线程 Tick、心跳与输入校验。</summary>
    public sealed class NetworkSessionController : MonoBehaviour
    {
        public NetworkSession Session { get; } = new NetworkSession();
        public string Address = "127.0.0.1";
        public int Port = 7777;
        public string Status { get; private set; } = "未连接";
        public event Action Changed;

        private float _nextHeartbeat;
        private string _lastState;

        private void Awake()
        {
            Application.runInBackground = true;
            Session.OnClientJoined += OnClientJoined;
            Session.OnCommentReceived += OnCommentReceived;
        }

        private void Update()
        {
            TickNow();
            if (Session.State != NetworkSession.SessionState.Hosting &&
                Session.State != NetworkSession.SessionState.Connected) return;
            if (Time.unscaledTime < _nextHeartbeat) return;
            _nextHeartbeat = Time.unscaledTime + 2f;
            try { Session.SendHeartbeat(); }
            catch (Exception ex) { SetStatus("心跳失败: " + ex.Message); }
        }

        public void TickNow()
        {
            Session.Tick();
            string state = Session.State.ToString();
            if (state == _lastState) return;
            _lastState = state;
            SetStatus(state == "Disconnected" ? "未连接" : state);
        }

        public bool StartHost()
        {
            if (!ValidatePort()) return false;
            try
            {
                Session.StartHost(Port, ParticipantRole.Director);
                SetStatus($"已开局 {Port}，等待加入");
                return true;
            }
            catch (Exception ex) { SetStatus("开局失败: " + ex.Message); return false; }
        }

        public bool Join(string address, ParticipantRole role = ParticipantRole.Producer)
        {
            if (!IPAddress.TryParse(address, out _))
            {
                SetStatus("IP 格式无效，仅接受 IPv4 地址");
                return false;
            }
            if (!ValidatePort()) return false;
            Address = address.Trim();
            try
            {
                Session.Connect(Address, Port, role, role.ToString());
                SetStatus($"正在连接 {Address}:{Port}");
                return true;
            }
            catch (Exception ex) { SetStatus("连接失败: " + ex.Message); return false; }
        }

        public void Disconnect()
        {
            Session.Disconnect();
            SetStatus("未连接");
        }

        private bool ValidatePort()
        {
            if (Port >= 1 && Port <= 65535) return true;
            SetStatus("端口必须在 1–65535");
            return false;
        }

        private void SetStatus(string message)
        {
            Status = message;
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            Session.OnClientJoined -= OnClientJoined;
            Session.OnCommentReceived -= OnCommentReceived;
            Session.Disconnect();
        }

        private void OnClientJoined(string id, string role) => SetStatus(role + " 已加入");
        private void OnCommentReceived(CommentData comment) => SetStatus(comment.sender + ": " + comment.text);
    }
}
