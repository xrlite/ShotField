using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 掌心菜单核心状态机（纯逻辑，不碰 Canvas / 手柄 / IMGUI）。
    /// VR 呈现与桌面回退共用同一份：呼出收起、分栏、条目构建、命令分发。
    /// 命令只走既有公开 API：<see cref="ActorSelection"/>、<see cref="PropBrowserUI"/>、
    /// <see cref="TabletopDirector"/>；快照存取委托 <see cref="PalmMenuSnapshotService"/>。
    /// </summary>
    public sealed class PalmMenuState
    {
        public PropBrowserUI Browser;
        public TabletopDirector Tabletop;

        private readonly List<KeyValuePair<string, GameObject>> _placedProps =
            new List<KeyValuePair<string, GameObject>>();
        private readonly PalmMenuSnapshotService _snapshot = new PalmMenuSnapshotService();

        private bool _isOpen;
        private PalmMenuTab _tab = PalmMenuTab.Actors;
        private string _status = "";

        public bool IsOpen => _isOpen;
        public PalmMenuTab Tab => _tab;
        public string Status => _status;

        /// <summary>开放状态 / 条目 / 状态文案变化时触发，呈现层据此刷新。</summary>
        public event Action Changed;

        public void Toggle() => SetOpen(!_isOpen);

        public void SetOpen(bool open)
        {
            if (_isOpen == open) return;
            _isOpen = open;
            Notify();
        }

        public void SelectTab(PalmMenuTab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            Notify();
        }

        /// <summary>按当前分栏构建条目；呈现层逐条生成按钮，不缓存本列表。</summary>
        public List<PalmMenuEntry> BuildEntries()
        {
            var entries = new List<PalmMenuEntry>();
            switch (_tab)
            {
                case PalmMenuTab.Actors: BuildActorEntries(entries); break;
                case PalmMenuTab.Props: BuildPropEntries(entries); break;
                case PalmMenuTab.Scene: BuildSceneEntries(entries); break;
            }
            return entries;
        }

        /// <summary>执行条目命令；返回执行后是否应自动收起菜单（选人/选道具后收起）。</summary>
        public bool Execute(PalmMenuEntry entry)
        {
            if (entry == null) return false;
            switch (entry.Kind)
            {
                case PalmMenuActionKind.SelectActor: return ExecuteSelectActor(entry);
                case PalmMenuActionKind.SpawnProp: return ExecuteSpawnProp(entry);
                case PalmMenuActionKind.SaveSnapshot: SaveSnapshot(); return false;
                case PalmMenuActionKind.ReloadSnapshot: ReloadSnapshot(); return false;
                case PalmMenuActionKind.ToggleMode: ToggleMode(); return false;
                default: return false;
            }
        }

        /// <summary>道具经浏览器生成后登记，快照重载时按此清单回收/重放。</summary>
        public void TrackSpawnedProp(string propId, GameObject instance)
        {
            if (instance == null) return;
            _placedProps.Add(new KeyValuePair<string, GameObject>(propId, instance));
        }

        private bool ExecuteSelectActor(PalmMenuEntry entry)
        {
            if (entry.Actor == null) { SetStatus("演员引用已失效"); return false; }
            bool changed = ActorSelection.Select(entry.Actor);
            SetStatus(changed ? $"已选中: {entry.Actor.name}" : $"已是当前演员: {entry.Actor.name}");
            return true;
        }

        private bool ExecuteSpawnProp(PalmMenuEntry entry)
        {
            if (Browser == null) { SetStatus("未绑定 PropBrowserUI"); return false; }
            var instance = Browser.SpawnProp(entry.PropId);
            if (instance == null) { SetStatus("道具生成失败: " + entry.Label); return false; }
            TrackSpawnedProp(entry.PropId, instance);
            SetStatus("已放置: " + entry.Label);
            return true;
        }

        private void BuildActorEntries(List<PalmMenuEntry> entries)
        {
            var current = ActorSelection.Current;
            foreach (var nav in ActorSelection.All)
            {
                if (nav == null || !nav.gameObject.activeInHierarchy) continue;
                entries.Add(new PalmMenuEntry
                {
                    Kind = PalmMenuActionKind.SelectActor,
                    Label = nav.name,
                    Actor = nav,
                    Active = ReferenceEquals(nav, current)
                });
            }
        }

        private void BuildPropEntries(List<PalmMenuEntry> entries)
        {
            foreach (var entry in PropLibrary.All)
            {
                entries.Add(new PalmMenuEntry
                {
                    Kind = PalmMenuActionKind.SpawnProp,
                    Label = entry.displayName,
                    PropId = entry.id
                });
            }
        }

        private void BuildSceneEntries(List<PalmMenuEntry> entries)
        {
            entries.Add(new PalmMenuEntry { Kind = PalmMenuActionKind.SaveSnapshot, Label = "保存快照" });
            entries.Add(new PalmMenuEntry { Kind = PalmMenuActionKind.ReloadSnapshot, Label = "重新加载存档" });
            bool tabletop = Tabletop != null &&
                Tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini;
            entries.Add(new PalmMenuEntry
            {
                Kind = PalmMenuActionKind.ToggleMode,
                Label = tabletop ? "回到 1:1 片场" : "进入 1:20 沙盘"
            });
        }

        private void ToggleMode()
        {
            if (Tabletop == null) { SetStatus("未绑定 TabletopDirector"); return; }
            var target = Tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini
                ? TabletopDirector.ScaleMode.Immersive1to1
                : TabletopDirector.ScaleMode.TabletopMini;
            Tabletop.SwitchMode(target);
            SetStatus(Tabletop.LastModeNote);
        }

        private void SaveSnapshot()
        {
            SetStatus(_snapshot.Save(Browser, _placedProps));
        }

        private void ReloadSnapshot()
        {
            SetStatus(_snapshot.Reload(Browser, _placedProps));
        }

        private void SetStatus(string message)
        {
            _status = message;
            Notify();
        }

        private void Notify() => Changed?.Invoke();
    }
}
