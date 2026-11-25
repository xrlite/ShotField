using System;
using System.Net;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>运行时唯一联机会话：桌面与 VR 共用，负责主线程 Tick、心跳、位姿广播与远端落地。</summary>
    public sealed partial class NetworkSessionController : MonoBehaviour
    {
        public NetworkSession Session { get; } = new NetworkSession();
        public string Address = "127.0.0.1";
        public int Port = 7777;
        public string Status { get; private set; } = "未连接";
        public event Action Changed;

        [Header("演员位姿同步")]
        [Tooltip("本机操控的演员；为空则不自动广播位姿。")]
        public ActorNavigator LocalActor;
        [Range(2f, 30f)] public float PoseSendHz = 12f;
        public int LastApplyCount { get; private set; }
        public string LastApplyReason { get; private set; } = "";

        private float _nextHeartbeat;
        private float _nextPose;
        private int _poseSequence;
        private string _lastState;

        private void Awake()
        {
            Application.runInBackground = true;
            Session.OnClientJoined += OnClientJoined;
            Session.OnCommentReceived += OnCommentReceived;
            Session.OnSceneEditReceived += OnSceneEditReceived;
            Session.OnActorPoseReceived += OnActorPoseReceived;
        }

        private void Update()
        {
            TickNow();
            TickLanDiscovery();
            if (!IsLive) return;

            if (Time.unscaledTime >= _nextHeartbeat)
            {
                _nextHeartbeat = Time.unscaledTime + 2f;
                try { Session.SendHeartbeat(); }
                catch (Exception ex) { SetStatus("心跳失败: " + ex.Message); }
            }

            if (LocalActor != null && PoseSendHz > 0f)
            {
                float interval = 1f / PoseSendHz;
                if (Time.unscaledTime >= _nextPose)
                {
                    _nextPose = Time.unscaledTime + interval;
                    BroadcastLocalActorNow();
                }
            }
        }

        private bool IsLive =>
            Session.State == NetworkSession.SessionState.Hosting ||
            Session.State == NetworkSession.SessionState.Connected;

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

        /// <summary>立即推一次本机演员位姿；摆位/走位确认后调用，不等下一个采样点。</summary>
        public bool BroadcastLocalActorNow(string actionTag = "")
        {
            if (!IsLive || LocalActor == null) return false;
            try
            {
                Session.BroadcastActorPose(RemoteActorSync.CapturePose(LocalActor, ++_poseSequence, actionTag));
                return true;
            }
            catch (Exception ex) { SetStatus("位姿广播失败: " + ex.Message); return false; }
        }

        /// <summary>提交一次走位编辑（带 revision 与回执），用于需要强一致的挪人操作。</summary>
        public bool SubmitLocalSceneEdit(int revision, string actionTag = "")
        {
            if (!IsLive || LocalActor == null) return false;
            try
            {
                Session.BroadcastSceneEdit(RemoteActorSync.CaptureSceneEdit(LocalActor, revision, actionTag));
                return true;
            }
            catch (Exception ex) { SetStatus("走位提交失败: " + ex.Message); return false; }
        }

        private void OnSceneEditReceived(SceneEditData data)
        {
            LastApplyCount = RemoteActorSync.ApplySceneEdit(data, out string reason) ? 1 : 0;
            LastApplyReason = data.actorId + ":" + reason;
            if (LastApplyCount == 0) SetStatus("远端走位未落地: " + LastApplyReason);
        }

        private void OnActorPoseReceived(ActorPoseData data)
        {
            LastApplyCount = RemoteActorSync.ApplyPose(data, out string reason) ? 1 : 0;
            LastApplyReason = data.actorId + ":" + reason;
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
            Session.OnSceneEditReceived -= OnSceneEditReceived;
            Session.OnActorPoseReceived -= OnActorPoseReceived;
            DisposeLanDiscovery();
            Session.Disconnect();
        }

        private void OnClientJoined(string id, string role) => SetStatus(role + " 已加入");
        private void OnCommentReceived(CommentData comment) => SetStatus(comment.sender + ": " + comment.text);
    }
}
