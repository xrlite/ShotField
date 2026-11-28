using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 空间三入口欢迎浮层（黑金磨砂玻璃质感）：
    /// 启动时引导用户选择「我有一个创意」「从案例开始」或「继续我的项目」，
    /// 不破坏既有场景层级与射线配置，选定后平滑让位。
    /// </summary>
    public sealed partial class WelcomeHubOverlay : MonoBehaviour
    {
        private static readonly Color BgColor = new Color(0.08f, 0.09f, 0.12f, 0.94f);
        private static readonly Color GoldBg = new Color(0.79f, 0.66f, 0.38f, 0.95f);
        private static readonly Color GoldSoftBg = new Color(0.24f, 0.21f, 0.15f, 0.94f);
        private static readonly Color SlateBg = new Color(0.18f, 0.20f, 0.25f, 0.92f);
        private static readonly Color GoldText = new Color(0.89f, 0.75f, 0.44f, 1f);
        private static readonly Color DarkInk = new Color(0.08f, 0.09f, 0.12f, 1f);
        private static readonly Color SubInk = new Color(0.86f, 0.85f, 0.80f, 0.92f);

        public static bool IsOpen { get; private set; }
        private Transform _head;
        private GameObject _panel, _mainGroup, _casesGroup;
        private TMP_Text _statusLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EditorCameraFallback.Ensure();
            if (FindAnyObjectByType<WelcomeHubOverlay>(FindObjectsInactive.Include) != null || Camera.main == null) return;
            var go = new GameObject("WelcomeHubOverlay", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            go.AddComponent<WelcomeHubOverlay>().Build(Camera.main);
        }

        private void Build(Camera cam)
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 20;
            _head = cam.transform;

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(760f, 410f);
            rt.localScale = Vector3.one * 0.001f;

            _panel = RuntimeUiFactory.CreatePanel(rt, "WelcomePanel", BgColor, Vector2.zero, rt.sizeDelta);
            SpatialGlass.Apply(_panel.GetComponent<Image>(), BgColor, 28f);
            _panel.AddComponent<SpatialUiMotion>();

            var root = _panel.transform;
            RuntimeUiFactory.CreateTmpLabel(root, "Title", "欢迎来到镜场", 28, new Vector2(0f, 162f), new Vector2(680f, 40f), GoldText, FontStyle.Bold);
            RuntimeUiFactory.CreateTmpLabel(root, "Subtitle", "从一个想法开始，或者继续你已经开始的故事。", 16, new Vector2(0f, 126f), new Vector2(680f, 28f), SubInk);

            BuildMainEntries(root);
            BuildCasesView(root);
            _casesGroup.SetActive(false);

            _statusLabel = RuntimeUiFactory.CreateTmpLabel(root, "HubStatus", "请选择创作入口 支持手柄射线与鼠标点击", 14, new Vector2(-80f, -168f), new Vector2(520f, 30f), GoldText);
            CreateEntryButton(root, "SkipHubBtn", "暂跳过 看沙盘", SlateBg, SubInk, new Vector2(280f, -168f), new Vector2(148f, 34f), 13, CloseOverlay);
            // 重置召回（C-1）：hub 开着时胶囊栏被 SetSuppressed 收起，那个「重置面前」点不到，故在 hub 上补一个。
            CreateEntryButton(root, "ResetHubBtn", "重置 召回面板", SlateBg, SubInk, new Vector2(282f, -134f), new Vector2(144f, 30f), 13, RecallToFront);

            SpatialUiMotion.AttachButtonMotion(_panel);
            IsOpen = true;
            UpdatePose(true);
            PlayRevealThenLock();
            var introFade = FindAnyObjectByType<StudioFadeTransition>();
            if (introFade != null) { introFade.PlayIntro(IntroHoldSeconds); StudioIntroLights.Play(IntroHoldSeconds); }
            DiagBuild();
        }

        private void BuildMainEntries(Transform root)
        {
            _mainGroup = RuntimeUiFactory.CreatePanel(root, "MainGroup", Color.clear, new Vector2(0f, -14f), new Vector2(710f, 250f));
            _mainGroup.GetComponent<Image>().raycastTarget = false;
            bool hasSaved = WelcomeHubProjectState.HasSavedProject();
            CreateCard(_mainGroup.transform, "NewIdeaBtn", "我有一个创意", "直接输入你的故事剧本\n不加载默认案例 支持任意题材",
                !hasSaved ? GoldBg : SlateBg, !hasSaved ? DarkInk : Color.white, new Vector2(-228f, 4f), OnNewIdeaClicked);
            CreateCard(_mainGroup.transform, "CasesBtn", "从案例开始", "体验示范作品《桌上的信》\n及科幻 / 仙侠 / 城市题材示例",
                !hasSaved ? GoldSoftBg : SlateBg, !hasSaved ? GoldText : Color.white, new Vector2(0f, 4f), () => ShowCasesView(true));
            string contSub = hasSaved ? "恢复上次保存的项目进度\n与分镜护照状态" : "暂无历史项目\n点击可查看恢复说明";
            CreateCard(_mainGroup.transform, "ContinueBtn", "继续我的项目", contSub,
                hasSaved ? GoldBg : SlateBg, hasSaved ? DarkInk : SubInk, new Vector2(228f, 4f), OnContinueClicked);
        }

        private void BuildCasesView(Transform root)
        {
            _casesGroup = RuntimeUiFactory.CreatePanel(root, "CasesGroup", Color.clear, new Vector2(0f, -14f), new Vector2(710f, 250f));
            _casesGroup.GetComponent<Image>().raycastTarget = false;
            var t = _casesGroup.transform;
            CreateEntryButton(t, "BackBtn", "← 返回入口", SlateBg, SubInk, new Vector2(-276f, 98f), new Vector2(124f, 34f), 14, () => ShowCasesView(false));
            RuntimeUiFactory.CreateTmpLabel(t, "CasesHeader", "选择示范案例或题材占位（随时可切回自由创作）", 15, new Vector2(70f, 98f), new Vector2(520f, 32f), SubInk);
            CreateEntryButton(t, "CaseLetterBtn", "《桌上的信》 室内悬疑示范案例（含客厅微缩与双人对手戏）", GoldBg, DarkInk, new Vector2(0f, 36f), new Vector2(660f, 52f), 16, OnSelectLetterDemo);
            CreateEntryButton(t, "CaseSciFiBtn", "《星轨漂流》 科幻太空（示例占位 点击切换通用基座开拍）", GoldSoftBg, GoldText, new Vector2(0f, -26f), new Vector2(660f, 44f), 14, () => OnSelectGenreStarter("外景 科幻太空舱：宇航员在失重走廊交接能源核心。"));
            CreateEntryButton(t, "CaseXianxiaBtn", "《云巅问道》 仙侠古风（示例占位 点击切换通用基座开拍）", SlateBg, SubInk, new Vector2(-168f, -82f), new Vector2(322f, 42f), 13, () => OnSelectGenreStarter("外景 仙侠山巅：剑修在云海悬崖前拔剑对峙。"));
            CreateEntryButton(t, "CaseChaseBtn", "《霓虹极速》 城市追车（示例占位 点击切换通用基座开拍）", SlateBg, SubInk, new Vector2(168f, -82f), new Vector2(322f, 42f), 13, () => OnSelectGenreStarter("外景 城市夜街追车：两辆车在立交桥下高速交错。"));
        }

        private static void CreateCard(Transform parent, string name, string title, string sub,
            Color bg, Color fg, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = RuntimeUiFactory.CreatePanel(parent, name, bg, pos, new Vector2(212f, 206f));
            SpatialGlass.Apply(go.GetComponent<Image>(), bg, 22f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            RuntimeUiFactory.CreateTmpLabel(go.transform, "Title", title, 20,
                new Vector2(0f, 46f), new Vector2(190f, 36f), fg, FontStyle.Bold);
            RuntimeUiFactory.CreateTmpLabel(go.transform, "Sub", sub, 13,
                new Vector2(0f, -28f), new Vector2(188f, 88f), fg);
        }

        private static void CreateEntryButton(Transform parent, string name, string text, Color bg, Color fg,
            Vector2 pos, Vector2 size, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var go = RuntimeUiFactory.CreatePanel(parent, name, bg, pos, size);
            SpatialGlass.Apply(go.GetComponent<Image>(), bg, 18f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            RuntimeUiFactory.CreateTmpLabel(go.transform, "Label", text, fontSize, Vector2.zero,
                size - new Vector2(16f, 6f), fg, FontStyle.Bold);
        }

        private void ShowCasesView(bool show)
        {
            if (_mainGroup != null) _mainGroup.SetActive(!show);
            if (_casesGroup != null) _casesGroup.SetActive(show);
            if (_statusLabel != null)
                _statusLabel.text = show ? "选择《桌上的信》进入完整示范，或点示例题材以通用基座开拍" : "请选择创作入口 支持手柄射线与鼠标点击";
        }

        private void OnNewIdeaClicked()
        {
            WelcomeHubProjectState.BeginNewIdea();
            CloseOverlay();
            FindAnyObjectByType<DirectorDockPanel>()?.OpenScriptInput("", "请输入你的创意或剧本，点击「1. 拆解时间/地点/人物」");
        }

        private void OnSelectLetterDemo()
        {
            WelcomeHubProjectState.BeginLetterDemoCase();
            CloseOverlay();
            FindAnyObjectByType<DirectorDockPanel>()?.OpenScriptInput(
                WelcomeHubProjectState.LetterDemoScript, "已载入示范案例《桌上的信》，可直接拆解或修改");
        }

        private void OnSelectGenreStarter(string starterScript)
        {
            WelcomeHubProjectState.BeginNewIdea(starterScript, nonInterior: true);
            CloseOverlay();
            FindAnyObjectByType<DirectorDockPanel>()?.OpenScriptInput(
                starterScript, "已切换通用空间基座（已隐藏客厅家具），可直接拆解剧本");
        }

        private void OnContinueClicked()
        {
            if (WelcomeHubProjectState.TryRestoreSavedProject(out string status))
            {
                CloseOverlay();
                FindAnyObjectByType<DirectorDockPanel>()?.OpenScriptInput(WelcomeHubProjectState.PendingScriptText, status);
                return;
            }
            if (_statusLabel != null) _statusLabel.text = status;
        }

        private void CloseOverlay()
        {
            IsOpen = false;
            Destroy(gameObject);
        }

        private void OnDestroy() => IsOpen = false;

        private void Update()
        {
            if (XrAiConnectionView.IsAnyPanelOpen) { CloseOverlay(); return; }
            if ((_head == null || _head.position.y < 0.30f) && Camera.main != null) _head = Camera.main.transform;
            UpdatePose(false);
        }

        private void UpdatePose(bool snap)
        {
            if (_head == null) return;
            TickPanelPose(snap);   // 锁位/重贴全在 WelcomeHubOverlay.Anchor.cs
        }
    }
}
