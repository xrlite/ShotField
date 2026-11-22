using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 全工程唯一的“当前选中演员”状态源。
    /// 只负责登记演员与发布选中变更，不直接绑定场景 UI；回放目标另由 ActorTakePlayer 持有，不在此合并。
    /// </summary>
    public static class ActorSelection
    {
        private static readonly List<ActorNavigator> Registered = new List<ActorNavigator>();
        private static ActorNavigator _current;

        /// <summary>当前选中的演员；无选中时为 null。All 为空时会懒加载补齐场景内演员。</summary>
        public static ActorNavigator Current
        {
            get
            {
                _ = All; // 触发懒加载，避免场景里漏登记
                return _current;
            }
        }

        /// <summary>已登记演员列表；首次访问时用场景现有演员懒加载补齐。</summary>
        public static IReadOnlyList<ActorNavigator> All
        {
            get
            {
                if (Registered.Count == 0)
                {
                    var found = UnityEngine.Object.FindObjectsByType<ActorNavigator>(FindObjectsSortMode.None);
                    Registered.AddRange(found);
                    if (_current == null && Registered.Count > 0) _current = Registered[0];
                }
                return Registered.AsReadOnly();
            }
        }

        /// <summary>选中演员发生变化时触发，参数为新的当前演员（清空时为 null）。</summary>
        public static event Action<ActorNavigator> Changed;

        /// <summary>登记一名演员；首个登记的演员自动成为当前选中。</summary>
        public static void Register(ActorNavigator navigator)
        {
            if (navigator == null || Registered.Contains(navigator)) return;
            Registered.Add(navigator);
            if (_current == null) Select(navigator);
        }

        /// <summary>取消登记；若移除的是当前选中，自动回退到剩余首位。</summary>
        public static void Unregister(ActorNavigator navigator)
        {
            if (navigator == null || !Registered.Remove(navigator)) return;
            if (!ReferenceEquals(_current, navigator)) return;
            _current = Registered.Count > 0 ? Registered[0] : null;
            Changed?.Invoke(_current);
        }

        /// <summary>选中一名已登记演员；未登记或为空时静默忽略，返回是否真的发生变化。</summary>
        public static bool Select(ActorNavigator navigator)
        {
            if (navigator == null || !Registered.Contains(navigator)) return false;
            if (ReferenceEquals(_current, navigator)) return false;
            _current = navigator;
            Changed?.Invoke(_current);
            return true;
        }

        /// <summary>清空登记与当前选中。</summary>
        public static void Clear()
        {
            Registered.Clear();
            if (_current == null) return;
            _current = null;
            Changed?.Invoke(null);
        }
    }
}
