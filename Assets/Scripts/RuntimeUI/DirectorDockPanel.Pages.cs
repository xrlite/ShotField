using TMPro;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>五步导演工作台的分页构建与事件绑定（行数拆分分部）。</summary>
    public sealed partial class DirectorDockPanel
    {
        private AiSceneAssemblerBridge _assembler;
        private ActorNpcRoamController _roam;
        private ActorPerformancePanel _perf;
        private ExportAndSeedancePanel _export;

        private void EnsureControllers()
        {
            if (_assembler == null) _assembler = GetComponent<AiSceneAssemblerBridge>() ?? gameObject.AddComponent<AiSceneAssemblerBridge>();
            if (_roam == null) _roam = FindAnyObjectByType<ActorNpcRoamController>() ?? gameObject.AddComponent<ActorNpcRoamController>();
            if (_perf == null) _perf = GetComponent<ActorPerformancePanel>() ?? gameObject.AddComponent<ActorPerformancePanel>();
            if (_export == null) _export = GetComponent<ExportAndSeedancePanel>() ?? gameObject.AddComponent<ExportAndSeedancePanel>();
        }

        private void BuildPage1ScriptAndScene(Transform p)
        {
            EnsureControllers();
            var script = RuntimeUiFactory.CreateTmpInputField(p, "DockScript",
                "内景 客厅 日：主角将关键道具放在茶几上，另一人走近观察。", new Vector2(570f, 54f), 600);
            ((RectTransform)script.transform).anchoredPosition = new Vector2(0f, 66f);
            var info = RuntimeUiFactory.CreateTmpLabel(p, "BreakdownInfo",
                "时间/地点/人物拆解结果将显示在此", 14, new Vector2(0f, 4f), new Vector2(570f, 54f));
            RuntimeUiFactory.CreateTmpButton(p, "ParseScriptBtn", "1. 拆解时间/地点/人物", new Vector2(-180f, -66f), new Vector2(180f, 40f),
                onClick: () => { _assembler.AnalyzeScript(script.text); info.text = _assembler.LastStatus; SetStatus(_assembler.LastStatus); });
            RuntimeUiFactory.CreateTmpButton(p, "GenSceneBtn", "2. AI 生成/落场道具", new Vector2(10f, -66f), new Vector2(180f, 40f),
                onClick: async () => { SetStatus("正在调用 AI 建景…"); info.text = await _assembler.GenerateOrSpawnScenePropAsync(_assembler.ScenePrompt); SetStatus(info.text); });
            RuntimeUiFactory.CreateTmpButton(p, "GoStep2Btn", "下一步: 沙盘 →", new Vector2(200f, -66f), new Vector2(150f, 40f), TabActive, () => SelectTab(1));
        }

        private void BuildPage2TabletopRoam(Transform p)
        {
            EnsureControllers();
            var label = RuntimeUiFactory.CreateTmpLabel(p, "RoamInfo",
                "沙盘模式：点选演员指派走位，NPC 可开启自主漫步演戏", 15, new Vector2(0f, 54f), new Vector2(560f, 48f));
            RuntimeUiFactory.CreateTmpButton(p, "ToggleRoamBtn", "NPC 自由演: 开/关", new Vector2(-170f, -20f), new Vector2(180f, 44f),
                onClick: () => { bool on = _roam.ToggleFreeRoam(); label.text = $"NPC 自由漫步已{(on ? "开启" : "暂停")}"; SetStatus(label.text); });
            RuntimeUiFactory.CreateTmpButton(p, "MoveActorBtn", "指派 ActorA 走位", new Vector2(20f, -20f), new Vector2(170f, 44f),
                onClick: () => { SetStatus(_perf.TriggerActionKeyframe("walk")); });
            RuntimeUiFactory.CreateTmpButton(p, "Enter1to1Btn", "进入 1:1 拍摄 →", new Vector2(205f, -20f), new Vector2(160f, 44f), TabActive,
                () => { FindAnyObjectByType<TabletopDirector>()?.SwitchMode(TabletopDirector.ScaleMode.Immersive1to1); SelectTab(2); });
        }

        private void BuildPage3Performance(Transform p)
        {
            EnsureControllers();
            var info = RuntimeUiFactory.CreateTmpLabel(p, "PerfInfo",
                "1:1 片场：支持真人头手附身录制，或一键添加自然动作关键帧", 15, new Vector2(0f, 64f), new Vector2(560f, 40f));
            RuntimeUiFactory.CreateTmpButton(p, "PossessBtn", "附身录制 / 停止回放", new Vector2(-160f, 4f), new Vector2(210f, 42f), TabActive,
                () => { info.text = _perf.TogglePossessRecord(); SetStatus(info.text); });
            RuntimeUiFactory.CreateTmpButton(p, "CaptureTakeBtn", "开拍入轨 (+1镜)", new Vector2(130f, 4f), new Vector2(200f, 42f), TabActive,
                () => { FindAnyObjectByType<SpatialWorkbench>()?.CaptureCurrentViewToShot(); info.text = "已捕获当前机位并写入轨道"; SetStatus(info.text); });
            RuntimeUiFactory.CreateTmpButton(p, "KfWalkBtn", "关键帧:走近", new Vector2(-210f, -60f), new Vector2(126f, 38f), onClick: () => SetStatus(_perf.TriggerActionKeyframe("walk")));
            RuntimeUiFactory.CreateTmpButton(p, "KfTurnBtn", "关键帧:转身", new Vector2(-70f, -60f), new Vector2(126f, 38f), onClick: () => SetStatus(_perf.TriggerActionKeyframe("turn")));
            RuntimeUiFactory.CreateTmpButton(p, "KfGrabBtn", "关键帧:拿取", new Vector2(70f, -60f), new Vector2(126f, 38f), onClick: () => SetStatus(_perf.TriggerActionKeyframe("grab")));
            RuntimeUiFactory.CreateTmpButton(p, "KfTalkBtn", "关键帧:对话", new Vector2(210f, -60f), new Vector2(126f, 38f), onClick: () => SetStatus(_perf.TriggerActionKeyframe("talk")));
        }

        private void BuildPage4Timeline(Transform p)
        {
            RuntimeUiFactory.CreateTmpLabel(p, "TlInfo",
                "多轨剪辑导轨：支持拍一条进一轨、播放串联与空间时间轴大卡联动", 15, new Vector2(0f, 56f), new Vector2(560f, 44f));
            RuntimeUiFactory.CreateTmpButton(p, "OpenTlCardBtn", "呼出/收起 5 轨时间轴", new Vector2(-180f, -20f), new Vector2(190f, 44f), TabActive,
                () => { FindAnyObjectByType<ThreeCardWorkspace>()?.ToggleCard(ThreeCardWorkspace.TimelineId); SetStatus("已切换 5 轨时间轴大卡"); });
            RuntimeUiFactory.CreateTmpButton(p, "PlayTlBtn", "串联播放全部镜头", new Vector2(30f, -20f), new Vector2(180f, 44f),
                onClick: () => { FindAnyObjectByType<DirectorTimeline>()?.Play(); SetStatus("正在串联播放时间轴"); });
            RuntimeUiFactory.CreateTmpButton(p, "GoStep5Btn", "下一步: 导出 →", new Vector2(215f, -20f), new Vector2(140f, 44f), TabActive, () => SelectTab(4));
        }

        private void BuildPage5Export(Transform p)
        {
            EnsureControllers();
            var arkKey = RuntimeUiFactory.CreateTmpInputField(p, "ArkKeyInput", "", new Vector2(390f, 36f), 256);
            arkKey.contentType = TMP_InputField.ContentType.Password;
            ((RectTransform)arkKey.transform).anchoredPosition = new Vector2(-80f, 64f);
            RuntimeUiFactory.CreateTmpButton(p, "SaveArkBtn", "保存方舟 Key", new Vector2(195f, 64f), new Vector2(130f, 36f),
                onClick: () => { _export.SaveArkKey(arkKey.text); arkKey.text = ""; SetStatus(_export.LastStatus); });
            var info = RuntimeUiFactory.CreateTmpLabel(p, "ExportInfo",
                "本地导出 1080p 参考包 / 预留字节 Seedance 2.5 一键提交", 14, new Vector2(0f, 8f), new Vector2(560f, 40f));
            RuntimeUiFactory.CreateTmpButton(p, "ExportLocalBtn", "导出本地 1080p 包", new Vector2(-140f, -56f), new Vector2(210f, 44f), TabActive,
                () => { info.text = _export.ExportLocalReferencePackage(); SetStatus(info.text); });
            RuntimeUiFactory.CreateTmpButton(p, "SubmitSeedanceBtn", "提交字节 Seedance", new Vector2(140f, -56f), new Vector2(210f, 44f),
                onClick: async () => { SetStatus("正在连接字节火山方舟…"); info.text = await _export.SubmitOrPreviewSeedanceAsync(_assembler.ScenePrompt); SetStatus(info.text); });
        }
    }
}
