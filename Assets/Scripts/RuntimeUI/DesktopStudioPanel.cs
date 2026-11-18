using System;
using ShotField.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShotField.RuntimeUI
{
    [RequireComponent(typeof(SpatialWorkbench))]
    public sealed class DesktopStudioPanel : MonoBehaviour
    {
        private const int Port = 7777;
        private readonly NetworkSession session = new NetworkSession();
        private SpatialWorkbench workbench;
        private CameraMotionRecorder motion;
        private TabletopDirector tabletop;
        private ActorTakePlayer actor;
        private AudienceLab audience;
        private ShotData receivedShot, recordingShot;
        private ShotPassportDto lastPassport;
        private string address = "127.0.0.1";
        private string note = "";
        private string status = "";
        private bool showTabletop;
        private Transform recordingPose;

        void Start()
        {
            Application.runInBackground = true;
            workbench = GetComponent<SpatialWorkbench>();
            if (workbench.library == null || workbench.library.ShotCamera == null) return;
            var camera = workbench.library.ShotCamera;
            recordingPose = new GameObject("Desktop recording pose").transform;
            recordingPose.SetParent(transform);
            motion = gameObject.AddComponent<CameraMotionRecorder>();
            motion.useDeviceInput = false;
            motion.poseSource = recordingPose;
            motion.filmCamera = camera;
            motion.hmdObserver = Camera.main;
            tabletop = gameObject.AddComponent<TabletopDirector>();
            audience = gameObject.AddComponent<AudienceLab>();
            var marker = SemanticMarker.Find("actorA");
            if (marker != null)
            {
                actor = gameObject.AddComponent<ActorTakePlayer>();
                actor.actorTransform = marker.transform;
                actor.actorId = marker.markerId;
            }
            workbench.PassportSaved += PublishPassport;
            session.OnPassportSyncReceived += ReceivePassport;
            session.OnClientJoined += (_, role) => status = role + " joined";
            session.OnCommentReceived += comment => status = comment.sender + ": " + comment.text;
        }

        private void PublishPassport(ShotPassportDto passport)
        {
            lastPassport = passport;
            RunSession(() => session.BroadcastPassport(passport.shot_id, passport.revision, JsonUtility.ToJson(passport)));
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
            session.Tick();
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

        void OnDestroy()
        {
            if (workbench != null) workbench.PassportSaved -= PublishPassport;
            session.Disconnect();
        }

        void OnGUI()
        {
            if (motion == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 340, 590), GUI.skin.box);
            GUILayout.Label(session.State.ToString());
            address = GUILayout.TextField(address);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Host")) RunSession(() =>
            {
                session.StartHost(Port);
                if (lastPassport != null) PublishPassport(lastPassport);
            });
            if (GUILayout.Button("Join")) RunSession(() => session.Connect(address, Port, ParticipantRole.Producer));
            if (GUILayout.Button("Disconnect")) session.Disconnect();
            GUILayout.EndHorizontal();

            if (GUILayout.Button(motion.CurrentState == CameraMotionRecorder.State.Recording
                ? "Stop and attach to selected shot" : "Record (WASD/QE, arrow keys)")) ToggleRecording();
            var selected = workbench.SelectedShot;
            if (GUILayout.Button("Save selected passport")) workbench.SavePassport();
            if (GUILayout.Button("Replay selected shot")) Play(selected, false);
            if (selected != null && GUILayout.Button("Replay latest saved revision")) RunSession(() =>
            {
                var saved = ShotPassportStore.LoadLatest(ShotPassportDto.QualifyShotId(selected.shotId, workbench.library.SceneId), ShotPassportStore.ProjectFolder);
                if (saved == null) status = "No saved passport for " + selected.shotId;
                else Play(saved.ToShot(), false);
            });
            if (receivedShot != null && GUILayout.Button("Replay received shot")) Play(receivedShot, false);
            if (GUILayout.Button("Screen selected shot (PC camera-direction proxy)")) Play(selected, true);

            note = GUILayout.TextField(note);
            if (GUILayout.Button("Send review note") && !string.IsNullOrWhiteSpace(note))
                session.SendComment(note, (receivedShot ?? selected)?.shotId);

            showTabletop = GUILayout.Toggle(showTabletop, "1:20 tabletop walking marks");
            if (showTabletop && actor != null) DrawTabletop();
            GUILayout.Label(status);
            GUILayout.EndArea();
            var feed = workbench.monitor?.Feed;
            if (feed != null)
                GUI.DrawTexture(new Rect(365, 12, Mathf.Max(1, Screen.width - 377), Mathf.Max(1, Screen.height - 24)), feed, ScaleMode.ScaleToFit);
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
        private void DrawTabletop()
        {
            if (GUILayout.Button(actor.CurrentState == ActorTakePlayer.PlayState.Recording
                ? "Stop actor take" : "Record actor take"))
            {
                if (actor.CurrentState == ActorTakePlayer.PlayState.Recording) actor.StopRecordingTake();
                else actor.StartRecordingTake();
            }
            var center = tabletop.TabletopAnchor.position;
            var mini = tabletop.WorldToTabletop(actor.actorTransform.position) - center;
            const float pixelsPerMeter = 500f;
            var map = GUILayoutUtility.GetRect(240f, 120f);
            GUI.Box(map, "Click to move Actor A");
            var offset = new Vector2(mini.x, -mini.z) * pixelsPerMeter;
            GUI.Label(new Rect(map.center + offset - Vector2.one * 6f, new Vector2(20f, 20f)), "A");
            var input = Event.current;
            if (input.type != EventType.MouseDown || input.button != 0 || !map.Contains(input.mousePosition)
                || actor.CurrentState == ActorTakePlayer.PlayState.Playing) return;
            var mark = (input.mousePosition - map.center) / pixelsPerMeter;
            actor.actorTransform.position = tabletop.TabletopToWorld(center + new Vector3(mark.x, mini.y, -mark.y));
            input.Use();
            Physics.SyncTransforms();
            workbench.monitor?.RequestRender();
        }
    }
}