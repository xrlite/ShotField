using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace ShotField.RuntimeUI
{
    /// <summary>三块空间积木的唯一容器：默认隐藏，PalmMenu 呼出，旧面板让位。</summary>
    public sealed class ThreeCardWorkspace : MonoBehaviour
    {
        public const string MonitorId = "monitor";
        public const string TimelineId = "timeline";
        public const string DrawerId = "drawer";
        private readonly Dictionary<string, SpatialBlockCard> _cards = new Dictionary<string, SpatialBlockCard>();
        private GameObject _root;
        private GameObject _controllerRoot;
        public bool useSceneDependencies = true;
        public SpatialMonitor monitor;
        public DirectorTimeline timeline;
        public PropBrowserUI browser;
        public ScriptSelectionPanel scriptPanel;
        public ChainTimelineController timelineController;
        private BlockPanelRegistry _registry;
        private Transform _head;
        private SpatialMonitor _monitor;
        private DirectorTimeline _timeline;
        private PropBrowserUI _browser;
        private ScriptSelectionPanel _scriptPanel;
        public bool AnyOpen { get; private set; }
        public string LastNote { get; private set; } = "三卡默认隐藏";
        public event Action Changed;
        private void Awake() => BuildCards();
        private void Start()
        {
            BindPalmMenus();
            ApplyProductGates();
        }
        public void BuildCards()
        {
            if (_root != null) return;
            _registry = GetComponent<BlockPanelRegistry>() ?? FindAnyObjectByType<BlockPanelRegistry>();
            _head = _registry != null ? _registry.headAnchor : null;
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_registry != null && _registry.headAnchor == null) _registry.headAnchor = _head;
            _monitor = monitor; _timeline = timeline; _browser = browser; _scriptPanel = scriptPanel;
            if (useSceneDependencies)
            {
                if (_monitor == null) _monitor = FindAnyObjectByType<SpatialMonitor>();
                if (_timeline == null) _timeline = FindAnyObjectByType<DirectorTimeline>();
                if (_browser == null) _browser = FindAnyObjectByType<PropBrowserUI>();
                if (_scriptPanel == null) _scriptPanel = FindAnyObjectByType<ScriptSelectionPanel>();
            }
            _root = new GameObject("[ThreeCardWorkspace_Root]");
            if (_head != null && _head.position.y < 0.30f)
            {
                var proxy = new GameObject("CardHeadAnchor_EditorFallback");
                proxy.transform.SetParent(_root.transform, false);
                proxy.transform.position = new Vector3(_head.position.x, 1.60f, _head.position.z);
                proxy.transform.rotation = _head.rotation;
                _head = proxy.transform;
                if (_registry != null) _registry.headAnchor = _head;
            }
            var monitorCard = SpatialBlockCard.Create(_root.transform, MonitorId, "监看板", new Vector2(620f, 420f), 0, _registry, _head);
            monitorCard.gameObject.AddComponent<MonitorCardView>().Bind(monitorCard, _monitor);
            _cards[MonitorId] = monitorCard;
            var timelineCard = SpatialBlockCard.Create(_root.transform, TimelineId, "多轨时间轴", new Vector2(620f, 420f), 1, _registry, _head);
            var controller = timelineController;
            if (controller == null && useSceneDependencies) controller = FindAnyObjectByType<ChainTimelineController>();
            if (controller == null)
            {
                var go = new GameObject("[ThreeCardTimelineController]");
                _controllerRoot = go;
                controller = go.AddComponent<ChainTimelineController>(); controller.timeline = _timeline;
            }
            timelineCard.gameObject.AddComponent<TimelineCardView>().Bind(timelineCard, _timeline, controller);
            _cards[TimelineId] = timelineCard;
            var drawer = SpatialBlockCard.Create(_root.transform, DrawerId, "剧本 / 资产", new Vector2(620f, 420f), 2, _registry, _head);
            drawer.gameObject.AddComponent<AssetDrawerCardView>().Bind(drawer, _browser, _scriptPanel);
            _cards[DrawerId] = drawer;
        }
        public SpatialBlockCard GetCard(string id) => _cards.TryGetValue(id, out var card) ? card : null;
        public bool IsOpen(string id) => GetCard(id) != null && GetCard(id).IsOpen;
        public void ToggleCard(string id)
        {
            var card = GetCard(id);
            if (card == null) { LastNote = "未知卡片: " + id; Changed?.Invoke(); return; }
            card.SetOpen(!card.IsOpen);
            if (card.IsOpen) _registry?.Layout();
            AnyOpen = false;
            foreach (var c in _cards.Values) if (c != null && c.IsOpen) { AnyOpen = true; break; }
            LastNote = card.Title + (card.IsOpen ? " 已呼出" : " 已收纳");
            Changed?.Invoke();
        }
        public void ShowCard(string id)
        {
            var card = GetCard(id);
            if (card == null || card.IsOpen) return;
            card.SetOpen(true); _registry?.Layout(); AnyOpen = true;
            LastNote = card.Title + " 已呼出"; Changed?.Invoke();
        }
        public void ShowAll()
        {
            foreach (var card in _cards.Values) if (card != null) card.SetOpen(true);
            _registry?.Layout(); AnyOpen = true; LastNote = "三卡已呼出"; Changed?.Invoke();
        }
        public void HideAll()
        {
            foreach (var card in _cards.Values) if (card != null) card.SetOpen(false);
            AnyOpen = false; LastNote = "三卡已收纳"; Changed?.Invoke();
        }
        public void Layout() => _registry?.Layout();
        private void BindPalmMenus()
        {
            var vr = FindAnyObjectByType<PalmMenuController>();
            if (vr != null && vr.State != null) vr.State.Cards = this;
            var desktop = FindAnyObjectByType<PalmMenuDesktopFallback>();
            if (desktop != null && desktop.State != null) desktop.State.Cards = this;
        }
        private void ApplyProductGates()
        {
            DevToolPanels.LegacyDesktopVisible = false; DevToolPanels.MonitorFeedVisible = false;
            DevToolPanels.MonitorScalerVisible = false; DevToolPanels.LightingConsoleVisible = false;
            var guide = FindAnyObjectByType<WorkflowGuidePanel>(); if (guide != null) guide.enabled = false;
            string[] legacy = { "Spatial_Workbench_Canvas", "WindowA_Monitor", "WindowB_Controls", "WindowC_HUD", "WindowD_PropBrowser", "WindowE_Inspector", "WindowF_Timeline", "WindowD_Lighting" };
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (Array.IndexOf(legacy, t.name) < 0) continue;
                var canvas = t.GetComponent<Canvas>();
                if (canvas != null) canvas.enabled = false;
                foreach (var graphic in t.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
            }
            if (_monitor != null) _monitor.RequestRender();
        }
        public void DestroyCards()
        {
            if (_root != null)
            {
                if (Application.isPlaying) Destroy(_root); else DestroyImmediate(_root);
            }
            if (_controllerRoot != null)
            {
                if (Application.isPlaying) Destroy(_controllerRoot); else DestroyImmediate(_controllerRoot);
            }
            _controllerRoot = null; _root = null; _cards.Clear(); AnyOpen = false;
        }
        private void OnDestroy() => DestroyCards();
    }
}
