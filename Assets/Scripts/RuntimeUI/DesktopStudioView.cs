using System;
using ShotField.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShotField.RuntimeUI
{
    public sealed partial class DesktopStudioPanel
    {
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
