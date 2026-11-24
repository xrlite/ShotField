using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public sealed partial class DesktopStudioPanel
    {
        private bool showLabs;
        private string _draftScript = "";

        void OnGUI()
        {
            if (!DevToolPanels.LegacyDesktopVisible || motion == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 340, Mathf.Min(Screen.height - 24f, 820f)), GUI.skin.box);

            GUILayout.Label($"镜场 ShotField · {LabFeatureFlags.ChannelLabel} {LabFeatureFlags.VersionLabel}");
            GUILayout.Label("四步走：① 导入剧本 → ② 确认镜头提案 → ③ 摆放演员与机位 → ④ 生成成片");

            DrawOnlineSection();
            DrawMainFlowActions();
            DrawLabsSection();

            showTabletop = GUILayout.Toggle(showTabletop, "1:20 tabletop walking marks");
            if (showTabletop && actor != null) DrawTabletop();
            GUILayout.Label(status);
            GUILayout.EndArea();

            var feed = DevToolPanels.MonitorFeedVisible ? workbench.monitor?.Feed : null;
            if (feed != null)
            {
                float feedRight = Screen.width - 336f;
                GUI.DrawTexture(new Rect(365, 12, Mathf.Max(1, feedRight - 365), Mathf.Max(1, Screen.height - 24)), feed, ScaleMode.ScaleToFit);
            }
        }

        // 联机会话：核心愿景，当前为离线演示；收进二级入口，首屏不直接暴露 IP 与联机按钮。
        private void DrawOnlineSection()
        {
            showAdvanced = GUILayout.Toggle(showAdvanced, "联机（可选）");
            if (!showAdvanced) return;

            GUILayout.Label(session.State.ToString());
            address = GUILayout.TextField(address);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Host")) RunSession(() =>
            {
                session.StartHost(Port);
                if (lastPassport != null) PublishPassport(lastPassport);
            });
            if (GUILayout.Button("Join")) RunSession(() => session.Connect(address, Port, ParticipantRole.Producer));
            if (GUILayout.Button("断开")) session.Disconnect();
            GUILayout.EndHorizontal();
        }

        // 主流程动作：撤销/重做、剧本导入、提案、连接设置、参考清单导出。
        private void DrawMainFlowActions()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("撤销")) workbench.UndoShot();
            if (GUILayout.Button("重做")) workbench.RedoShot();
            if (GUILayout.Button("连接设置")) ToggleConnection();
            GUILayout.EndHorizontal();

            GUILayout.Label("剧本：粘贴或输入剧情、台词");
            _draftScript = GUILayout.TextArea(_draftScript ?? "", GUILayout.Height(72f));
            if (GUILayout.Button("生成镜头提案")) ImportDraftScript();

            var selected = workbench.SelectedShot;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(motion.CurrentState == CameraMotionRecorder.State.Recording
                ? "停止走位预览" : "录制走位预览")) ToggleRecording();
            if (GUILayout.Button("保存场景")) workbench.SavePassport();
            GUILayout.EndHorizontal();

            if (_stageSwitcher != null && GUILayout.Button(_stageSwitcher.GrandStageActive
                ? "切回白模舞台" : "切换到天工舞台"))
            {
                _stageSwitcher.ToggleStage();
                status = _stageSwitcher.LastStageNote;
            }

            if (GUILayout.Button("回看所选镜头")) Play(selected, false);
            if (selected != null && GUILayout.Button("回看最近存档")) RunSession(() =>
            {
                var saved = ShotPassportStore.LoadLatest(ShotPassportMapper.QualifyShotId(selected.shotId, workbench.library.SceneId), ShotPassportStore.ProjectFolder);
                if (saved == null) status = "没有最近存档";
                else Play(saved.ToShot(), false);
            });
            if (receivedShot != null && GUILayout.Button("回看收到的镜头")) Play(receivedShot, false);
            if (selected != null && GUILayout.Button("机位预览")) Play(selected, true);

            if (GUILayout.Button("导出参考清单")) workbench.ExportReferencePackage();
            GUILayout.Label(workbench.AiState);
        }

        private void ToggleConnection()
        {
            if (_connection == null)
            {
                _connection = GetComponent<ConnectionCenterPanel>();
                if (_connection == null) _connection = gameObject.AddComponent<ConnectionCenterPanel>();
            }
            _connection.Visible = !_connection.Visible;
        }

        private void ImportDraftScript()
        {
            if (string.IsNullOrWhiteSpace(_draftScript))
            {
                status = "先粘贴或输入剧本";
                return;
            }
            var orchestrator = FindAnyObjectByType<ChainOrchestrator>();
            if (orchestrator == null) { status = "未找到 ChainOrchestrator"; return; }
            orchestrator.OnError += msg => status = msg;
            orchestrator.ImportScript(_draftScript, "DesktopPanel");
            if (string.IsNullOrEmpty(status) || !status.StartsWith("AI 未接通")) status = "已导入剧本，等待提案确认";
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
