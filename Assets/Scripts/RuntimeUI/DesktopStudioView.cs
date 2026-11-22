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
            GUILayout.BeginArea(new Rect(12, 12, 340, Mathf.Min(Screen.height - 24f, 820f)), GUI.skin.box);
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
                var saved = ShotPassportStore.LoadLatest(ShotPassportMapper.QualifyShotId(selected.shotId, workbench.library.SceneId), ShotPassportStore.ProjectFolder);
                if (saved == null) status = "No saved passport for " + selected.shotId;
                else Play(saved.ToShot(), false);
            });
            if (receivedShot != null && GUILayout.Button("Replay received shot")) Play(receivedShot, false);
            if (GUILayout.Button("Screen selected shot (PC camera-direction proxy)")) Play(selected, true);

            note = GUILayout.TextField(note);
            if (GUILayout.Button("Send review note") && !string.IsNullOrWhiteSpace(note))
                session.SendComment(note, (receivedShot ?? selected)?.shotId);

            DrawAdvancedControls();

            showTabletop = GUILayout.Toggle(showTabletop, "1:20 tabletop walking marks");
            if (showTabletop && actor != null) DrawTabletop();
            GUILayout.Label(status);
            GUILayout.EndArea();
            var feed = workbench.monitor?.Feed;
            if (feed != null)
            {
                // 中央预览只占左面板与右侧编辑面板之间的中栏，不再贴到右面板下沿，避免预览贴图盖住右侧按钮与小地图。
                float feedRight = Screen.width - 336f;
                GUI.DrawTexture(new Rect(365, 12, Mathf.Max(1, feedRight - 365), Mathf.Max(1, Screen.height - 24)), feed, ScaleMode.ScaleToFit);
            }
        }

        // 折叠区：不撑高既有布局，展开后才绘制新入口。
        private void DrawAdvancedControls()
        {
            showAdvanced = GUILayout.Toggle(showAdvanced, "高级 · 撤销 / AI / 成片对照");
            if (!showAdvanced) return;

            GUILayout.Label(workbench.HistoryState);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("撤销 Ctrl+Z")) workbench.UndoShot();
            if (GUILayout.Button("重做 Ctrl+Y")) workbench.RedoShot();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("解析当前剧本")) workbench.ParseStoryBible();
            if (GUILayout.Button("导出 Seedance 参考包")) workbench.ExportReferencePackage();
            GUILayout.EndHorizontal();
            GUILayout.Label(workbench.AiState);

            videoSource = GUILayout.TextField(videoSource);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("播放成片")) workbench.PlayReviewVideo(videoSource);
            if (GUILayout.Button("切换比对模式")) workbench.CycleCompareMode();
            GUILayout.EndHorizontal();
            GUILayout.Label("比对: " + workbench.CompareModeName + " / " + workbench.ReviewState);
            float progress = GUILayout.HorizontalSlider(workbench.ReviewProgress, 0f, 1f);
            if (Mathf.Abs(progress - workbench.ReviewProgress) > 0.01f) workbench.SeekReview(progress);
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
