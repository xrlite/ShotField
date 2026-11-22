using System;
using ShotField.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShotField.RuntimeUI
{
    public sealed partial class DesktopStudioPanel
    {
        private bool showLabs;

        void OnGUI()
        {
            if (motion == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 340, Mathf.Min(Screen.height - 24f, 820f)), GUI.skin.box);

            GUILayout.Label($"镜场 ShotField · {LabFeatureFlags.ChannelLabel} {LabFeatureFlags.VersionLabel}");
            GUILayout.Label("主流程：" + LabFeatureFlags.MainFlowRail);

            // 联机会话：核心愿景，当前为离线演示，不并入 Labs 实验分区。
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

            var selected = workbench.SelectedShot;
            if (GUILayout.Button(motion.CurrentState == CameraMotionRecorder.State.Recording
                ? "Stop and attach to selected shot" : "Record (WASD/QE, arrow keys)")) ToggleRecording();
            if (GUILayout.Button("Save selected passport")) workbench.SavePassport();
            if (GUILayout.Button("Replay selected shot")) Play(selected, false);
            if (selected != null && GUILayout.Button("Replay latest saved revision")) RunSession(() =>
            {
                var saved = ShotPassportStore.LoadLatest(ShotPassportMapper.QualifyShotId(selected.shotId, workbench.library.SceneId), ShotPassportStore.ProjectFolder);
                if (saved == null) status = "No saved passport for " + selected.shotId;
                else Play(saved.ToShot(), false);
            });
            if (receivedShot != null && GUILayout.Button("Replay received shot")) Play(receivedShot, false);
            if (selected != null && GUILayout.Button("Screen selected shot (PC camera-direction proxy)")) Play(selected, true);

            note = GUILayout.TextField(note);
            if (GUILayout.Button("Send review note") && !string.IsNullOrWhiteSpace(note))
                session.SendComment(note, (receivedShot ?? selected)?.shotId);

            DrawMainFlowActions();
            DrawLabsSection();

            showTabletop = GUILayout.Toggle(showTabletop, "1:20 tabletop walking marks");
            if (showTabletop && actor != null) DrawTabletop();
            GUILayout.Label(status);
            GUILayout.EndArea();

            var feed = workbench.monitor?.Feed;
            if (feed != null)
            {
                float feedRight = Screen.width - 336f;
                GUI.DrawTexture(new Rect(365, 12, Mathf.Max(1, feedRight - 365), Mathf.Max(1, Screen.height - 24)), feed, ScaleMode.ScaleToFit);
            }
        }

        // 主流程动作：撤销/重做、剧本解析、提案导入、参考包导出。
        private void DrawMainFlowActions()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("撤销 Ctrl+Z")) workbench.UndoShot();
            if (GUILayout.Button("重做 Ctrl+Y")) workbench.RedoShot();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("解析当前剧本")) workbench.ParseStoryBible();
            if (GUILayout.Button("导入剧本 → 提案")) ImportChainProposal();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("导出参考包")) workbench.ExportReferencePackage();
            GUILayout.Label(workbench.AiState);
        }

        // Labs 实验分区：默认折叠、开关默认关；接口与代码保留，主流程不依赖。
        private void DrawLabsSection()
        {
            showLabs = GUILayout.Toggle(showLabs, "实验室功能（Labs）· 实验性，默认关");
            if (!showLabs) return;

            LabFeatureFlags.SetOfflineDemo(GUILayout.Toggle(LabFeatureFlags.OfflineDemo, "离线演示：关闭 AI 在线解析，走本地结构化"));
            GUILayout.Label("· " + LabFeatureFlags.MrLabTitle);
            LabFeatureFlags.SetMrLabEnabled(GUILayout.Toggle(LabFeatureFlags.MrLabEnabled, "启用 MR 透视 / 平面 / 锚点"));
            GUILayout.Label(LabFeatureFlags.MrLabLimitation);

            GUILayout.Label("· " + LabFeatureFlags.OnlineLabTitle);
            LabFeatureFlags.SetOnlineGenLabEnabled(GUILayout.Toggle(LabFeatureFlags.OnlineGenLabEnabled, "启用在线视频生成"));
            GUILayout.Label(LabFeatureFlags.OnlineLabLimitation);

            videoSource = GUILayout.TextField(videoSource);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("播放成片（本地素材）")) workbench.PlayReviewVideo(videoSource);
            if (GUILayout.Button("切换比对模式")) workbench.CycleCompareMode();
            GUILayout.EndHorizontal();
            GUILayout.Label("比对: " + workbench.CompareModeName + " / " + workbench.ReviewState);
            float progress = GUILayout.HorizontalSlider(workbench.ReviewProgress, 0f, 1f);
            if (Mathf.Abs(progress - workbench.ReviewProgress) > 0.01f) workbench.SeekReview(progress);
        }

        private void ImportChainProposal()
        {
            var orchestrator = FindAnyObjectByType<ChainOrchestrator>();
            if (orchestrator == null) { status = "未找到 ChainOrchestrator"; return; }
            orchestrator.OnError += msg => status = msg;
            const string sample = "内景 客厅 日。A_Red 坐在沙发上看信，B_Blue 从门口走进来拿起茶几上的信封；机位先俯拍客厅全景，再推近信封特写。";
            orchestrator.ImportScript(sample, "DesktopPanel");
            if (string.IsNullOrEmpty(status) || !status.StartsWith("AI 未接通")) status = "已导入剧本，等待提案确认";
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
