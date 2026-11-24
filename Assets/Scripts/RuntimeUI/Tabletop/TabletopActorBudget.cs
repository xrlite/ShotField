using System.Collections.Generic;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 沙盘演员预算：代理按演员匹配；仅恢复本类改动过的 actor/proxy activeSelf。
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class TabletopActorBudget : MonoBehaviour
    {
        [Header("绑定")]
        public TabletopDirector tabletop;

        [Header("预算")]
        [Tooltip("沙盘模式下最多同时显示的演员数，含当前选中演员。")]
        [Range(1, 8)] public int maxVisibleInTabletop = 2;

        [Tooltip("是否把当前选中演员无条件保留在沙盘里。")]
        public bool alwaysKeepSelected = true;

        private readonly List<ActorNavigator> _actors = new List<ActorNavigator>();
        private readonly List<TabletopProxyFollower> _proxies = new List<TabletopProxyFollower>();
        private readonly List<ActorNavigator> _keepList = new List<ActorNavigator>();
        private readonly Dictionary<GameObject, VisibilityState> _states = new Dictionary<GameObject, VisibilityState>();
        private ActorNavigator _selected;
        private TabletopDirector.ScaleMode _lastMode;
        private bool _hasApplied;
        private bool _applying;

        private sealed class VisibilityState
        {
            public bool OriginalActive;
            public bool ChangedByUs;
        }

        private void OnEnable()
        {
            if (tabletop == null) tabletop = FindAnyObjectByType<TabletopDirector>();
            ActorSelection.Changed += OnSelectionChanged;
            if (tabletop == null) return;
            _lastMode = tabletop.currentMode;
            _hasApplied = true;
            Apply(_lastMode == TabletopDirector.ScaleMode.TabletopMini);
        }

        private void OnDisable()
        {
            ActorSelection.Changed -= OnSelectionChanged;
            RestoreAll();
        }

        private void Update()
        {
            if (tabletop == null) return;
            if (_hasApplied && tabletop.currentMode == _lastMode) return;
            _lastMode = tabletop.currentMode;
            _hasApplied = true;
            Apply(_lastMode == TabletopDirector.ScaleMode.TabletopMini);
        }

        private void OnSelectionChanged(ActorNavigator _)
        {
            if (!_applying && isActiveAndEnabled && tabletop != null &&
                tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini)
                ApplyBudget();
        }

        /// <summary>按目标模式调整演员显隐。可重复调用，结果一致。</summary>
        public void Apply(bool tabletopMode)
        {
            if (tabletopMode) ApplyBudget();
            else RestoreAll();
        }

        private void ApplyBudget()
        {
            if (_applying) return;
            _applying = true;
            try
            {
                CollectScene();
                BuildKeepList();
                foreach (var nav in _actors) ApplyActor(nav);
                foreach (var follower in _proxies) ApplyProxy(follower);
            }
            finally
            {
                _applying = false;
            }
        }

        private void CollectScene()
        {
            _actors.Clear();
            _proxies.Clear();
            foreach (var nav in ActorSelection.All) AddActor(nav);
            var followers = FindObjectsByType<TabletopProxyFollower>(FindObjectsInactive.Include);
            foreach (var follower in followers)
            {
                if (follower == null) continue;
                _proxies.Add(follower);
                if (follower.actor != null) AddActor(follower.actor.GetComponent<ActorNavigator>());
            }
        }

        private void AddActor(ActorNavigator nav)
        {
            if (nav != null && !_actors.Contains(nav)) _actors.Add(nav);
        }

        private void BuildKeepList()
        {
            _keepList.Clear();
            int keep = Mathf.Max(1, maxVisibleInTabletop);
            _selected = alwaysKeepSelected ? ActorSelection.Current : null;
            if (_selected != null) _keepList.Add(_selected);

            foreach (var nav in _actors)
            {
                if (nav == null || _keepList.Count >= keep || _keepList.Contains(nav)) continue;
                if (IsEligible(nav.gameObject, GetState(nav.gameObject))) _keepList.Add(nav);
            }
        }

        private void ApplyActor(ActorNavigator nav)
        {
            if (nav == null) return;
            var state = GetState(nav.gameObject);
            bool keep = _keepList.Contains(nav);
            bool show = keep && (nav == _selected || IsEligible(nav.gameObject, state));
            SetActive(nav.gameObject, show, state);
        }

        private void ApplyProxy(TabletopProxyFollower follower)
        {
            if (follower == null) return;
            var state = GetState(follower.gameObject);
            bool keep = false;
            bool selected = false;
            foreach (var nav in _keepList)
            {
                if (nav == null || follower.actor != nav.transform) continue;
                keep = true;
                selected = nav == _selected;
                break;
            }
            bool show = keep && (selected || IsEligible(follower.gameObject, state));
            SetActive(follower.gameObject, show, state);
        }

        private static bool IsEligible(GameObject go, VisibilityState state)
        {
            if (!state.OriginalActive || (!state.ChangedByUs && !go.activeSelf)) return false;
            var parent = go.transform.parent;
            return parent == null || parent.gameObject.activeInHierarchy;
        }

        private static void SetActive(GameObject go, bool active, VisibilityState state)
        {
            if (go.activeSelf == active) return;
            if (!active && !state.OriginalActive && !state.ChangedByUs) return;
            go.SetActive(active);
            state.ChangedByUs = true;
        }

        private VisibilityState GetState(GameObject go)
        {
            if (_states.TryGetValue(go, out var state)) return state;
            state = new VisibilityState { OriginalActive = go.activeSelf };
            _states.Add(go, state);
            return state;
        }

        private void RestoreAll()
        {
            if (_applying) return;
            _applying = true;
            try
            {
                foreach (var pair in _states)
                {
                    if (pair.Key != null && pair.Value.ChangedByUs)
                        pair.Key.SetActive(pair.Value.OriginalActive);
                }
                _states.Clear();
                _selected = null;
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
