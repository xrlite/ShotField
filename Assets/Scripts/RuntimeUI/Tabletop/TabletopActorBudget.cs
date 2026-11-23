using System.Collections.Generic;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 沙盘 1:20 的按需演员预算。1:1 片场要六个人都在（走位、构图、轴线都要人），
    /// 但缩到 1:20 沙盘时六个微型人挤在桌面上既看不清也没必要 —— 只留当前选中的和
    /// 最少几个配角即可，信息密度反而更高。
    ///
    /// 可逆性约束（比功能本身重要）：
    /// 本类**只隐藏自己隐藏过的**演员，恢复时也只恢复这一批。
    /// 场景里本来就被别的逻辑关掉的演员不会被误开，
    /// 也不会因为反复切模式把人数越切越多。
    ///
    /// 已知副作用：被隐藏的演员 activeInHierarchy 为 false，
    /// <see cref="PalmMenuState.BuildActorEntries"/> 与 <see cref="ActorSelection.All"/>
    /// 都会过滤掉他们，所以沙盘模式下掌心菜单里看不到被预算掉的演员。这是预期行为。
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

        private readonly List<ActorNavigator> _hiddenByUs = new List<ActorNavigator>();
        private readonly List<ActorNavigator> _keepList = new List<ActorNavigator>();
        private TabletopDirector.ScaleMode _lastMode;
        private bool _hasApplied;

        private void Awake()
        {
            if (tabletop == null) tabletop = FindAnyObjectByType<TabletopDirector>();
            if (tabletop != null) _lastMode = tabletop.currentMode;
        }

        private void Update()
        {
            if (tabletop == null) return;
            if (_hasApplied && tabletop.currentMode == _lastMode) return;
            _lastMode = tabletop.currentMode;
            _hasApplied = true;
            Apply(_lastMode == TabletopDirector.ScaleMode.TabletopMini);
        }

        /// <summary>按目标模式调整演员显隐。可重复调用，结果一致。</summary>
        public void Apply(bool tabletopMode)
        {
            if (tabletopMode) ApplyBudget();
            else RestoreAll();
        }

        private void ApplyBudget()
        {
            BuildKeepList();
            var all = ActorSelection.All;

            for (int i = 0; i < all.Count; i++)
            {
                var nav = all[i];
                if (nav == null) continue;

                bool shouldShow = _keepList.Contains(nav);
                if (shouldShow) Show(nav);
                else Hide(nav);
            }
        }

        /// <summary>决定沙盘里留下谁：优先当前选中，不足预算再按名单顺序补。</summary>
        private void BuildKeepList()
        {
            _keepList.Clear();
            int keep = Mathf.Max(1, maxVisibleInTabletop);

            var selected = alwaysKeepSelected ? ActorSelection.Current : null;
            if (selected != null) _keepList.Add(selected);

            var all = ActorSelection.All;
            for (int i = 0; i < all.Count && _keepList.Count < keep; i++)
            {
                var nav = all[i];
                if (nav == null || _keepList.Contains(nav)) continue;
                _keepList.Add(nav);
            }
        }

        private void Show(ActorNavigator nav)
        {
            if (nav.gameObject.activeSelf) return;
            nav.gameObject.SetActive(true);
            _hiddenByUs.Remove(nav);
        }

        private void Hide(ActorNavigator nav)
        {
            if (!nav.gameObject.activeSelf) return;
            nav.gameObject.SetActive(false);
            if (!_hiddenByUs.Contains(nav)) _hiddenByUs.Add(nav);
        }

        private void RestoreAll()
        {
            for (int i = 0; i < _hiddenByUs.Count; i++)
            {
                var nav = _hiddenByUs[i];
                if (nav != null && !nav.gameObject.activeSelf)
                    nav.gameObject.SetActive(true);
            }
            _hiddenByUs.Clear();
        }

        private void OnDisable() => RestoreAll();
    }
}
