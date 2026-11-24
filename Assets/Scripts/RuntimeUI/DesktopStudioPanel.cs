using System;
using ShotField.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShotField.RuntimeUI
{
    [RequireComponent(typeof(SpatialWorkbench))]
    public sealed partial class DesktopStudioPanel : MonoBehaviour
    {
        private NetworkSessionController _network;
        private SpatialWorkbench workbench;
        private CameraMotionRecorder motion;
        private TabletopDirector tabletop;
        private ActorTakePlayer actor;
        private AudienceLab audience;
        private ShotData receivedShot, recordingShot;
        private ShotPassportDto lastPassport;
        private string status = "";
        private string videoSource = "";
        private bool showAdvanced;
        private bool showTabletop;
        private Transform recordingPose;
        private ConnectionCenterPanel _connection;
        private StagePresetSwitcher _stageSwitcher;

        void Start()
        {
            Application.runInBackground = true;
            _network = FindAnyObjectByType<NetworkSessionController>();
            if (_network == null) _network = gameObject.AddComponent<NetworkSessionController>();
            workbench = GetComponent<SpatialWorkbench>();
            AttachEditViews();
            if (workbench.library == null || workbench.library.ShotCamera == null) return;
            var camera = workbench.library.ShotCamera;
            recordingPose = new GameObject("Desktop recording pose").transform;
            recordingPose.SetParent(transform);
            motion = GetComponent<CameraMotionRecorder>();
            if (!motion) { enabled = false; return; }
            motion.useDeviceInput = false;
            motion.poseSource = recordingPose;
            tabletop = GetComponent<TabletopDirector>();
            audience = GetComponent<AudienceLab>();
            actor = GetComponent<ActorTakePlayer>();
            _stageSwitcher = GetComponent<StagePresetSwitcher>();
            workbench.PassportSaved += PublishPassport;
            _network.Session.OnPassportSyncReceived += ReceivePassport;
            _network.Session.OnClientJoined += OnNetworkClientJoined;
            _network.Session.OnCommentReceived += OnNetworkComment;
        }

        private void PublishPassport(ShotPassportDto passport)
        {
            lastPassport = passport;
            RunSession(() => _network.Session.BroadcastPassport(passport.shot_id, passport.revision, JsonUtility.ToJson(passport)));
        }

        private void ReceivePassport(PassportSyncData data)
        {
            var dto = JsonUtility.FromJson<ShotPassportDto>(data.passportJson);
            if (dto == null || string.IsNullOrEmpty(dto.shot_id) || dto.shot_id != data.shotId || dto.revision != data.revision ||
                !dto.shot_id.StartsWith(workbench.library.SceneId + "-", StringComparison.Ordinal)) return;
            receivedShot = dto.ToShot();
            status = $"Received {data.shotId} rev{data.revision:D3}";
        }

        void Update()
        {
            HandleShortcuts();
            if (motion == null) return;
            if (motion.CurrentState == CameraMotionRecorder.State.Recording && Keyboard.current != null)
            {
                var keys = Keyboard.current;
                var move = new Vector3((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                    (keys.eKey.isPressed ? 1 : 0) - (keys.qKey.isPressed ? 1 : 0),
                    (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
                recordingPose.Translate(move * Time.deltaTime);
                var yaw = (keys.rightArrowKey.isPressed ? 1 : 0) - (keys.leftArrowKey.isPressed ? 1 : 0);
                recordingPose.Rotate(0, yaw * 60f * Time.deltaTime, 0, Space.World);
                motion.filmCamera.transform.SetPositionAndRotation(recordingPose.position, recordingPose.rotation);
            }
            if (actor != null && actor.CurrentState == ActorTakePlayer.PlayState.Playing)
                workbench.monitor?.RequestRender();
            if (audience.isScreeningActive && motion.CurrentState != CameraMotionRecorder.State.Playing)
            {
                audience.StopScreeningSession();
                status = $"Camera-direction proxy: {audience.GetFocusRatio():P0} target visibility by ray";
            }
        }

        // 快捷键归 UI 层：Core 程序集不引用 Unity.InputSystem，且工程为纯新输入系统。
        private void HandleShortcuts()
        {
            var kb = Keyboard.current;
            if (kb == null || workbench == null) return;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (!ctrl) return;
            if (kb.zKey.wasPressedThisFrame && !kb.leftShiftKey.isPressed) workbench.UndoShot();
            else if (kb.yKey.wasPressedThisFrame || (kb.zKey.wasPressedThisFrame && kb.leftShiftKey.isPressed)) workbench.RedoShot();
        }

        void OnDestroy()
        {
            if (workbench != null) workbench.PassportSaved -= PublishPassport;
            if (_network != null)
            {
                _network.Session.OnPassportSyncReceived -= ReceivePassport;
                _network.Session.OnClientJoined -= OnNetworkClientJoined;
                _network.Session.OnCommentReceived -= OnNetworkComment;
            }
        }

        private void OnNetworkClientJoined(string id, string role) => status = role + " joined";
        private void OnNetworkComment(CommentData comment) => status = comment.sender + ": " + comment.text;

        // M1 编辑链：右侧编辑面板 + 启动自动恢复组件，挂在同一画布物体上。
        private void AttachEditViews()
        {
            if (GetComponent<DesktopStudioEditView>() == null) gameObject.AddComponent<DesktopStudioEditView>();
            if (GetComponent<SceneEditRestore>() == null) gameObject.AddComponent<SceneEditRestore>();
        }

        private void RunSession(Action action)
        {
            try { action(); }
            catch (Exception ex) { status = ex.Message; }
        }

        private void ToggleRecording()
        {
            var selected = workbench.SelectedShot;
            if (motion.CurrentState == CameraMotionRecorder.State.Recording)
            {
                motion.StopRecording();
                motion.ApplyToShot(recordingShot);
                recordingShot = null;
                return;
            }
            if (motion.CurrentState != CameraMotionRecorder.State.Idle || selected == null) return;
            recordingShot = selected;
            var camera = motion.filmCamera.transform;
            recordingPose.SetPositionAndRotation(camera.position, camera.rotation);
            motion.StartRecording();
        }

        private void Play(ShotData shot, bool screening)
        {
            if (shot == null || motion.CurrentState == CameraMotionRecorder.State.Recording) return;
            workbench.library.ApplyShot(shot);
            workbench.monitor?.SetAspect(shot.camera.aspect < 1f);
            actor?.StartPlayback();
            if (shot.trajectory == null || shot.trajectory.Count == 0) return;
            motion.LoadAndPlay(shot.trajectory);
            if (screening) audience.StartScreeningSession(motion.filmCamera);
        }

    }
}
