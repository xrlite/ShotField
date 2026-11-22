using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 右侧编辑面板的屏幕空间布局：绝对 Rect 摆放，不靠流式 GUILayout 的高度分配，
    /// 保证任意 Game 视图高度下六个演员按钮、小地图、道具滚动区与保存栏都完整可见。
    /// </summary>
    public sealed partial class DesktopStudioEditView
    {
        private const float PanelW = 320f;
        private const float Margin = 8f;
        private const float TitleH = 22f;
        private const float HeaderH = 18f;
        private const float ActorBtnH = 24f;
        private const float SaveH = 30f;

        void OnGUI()
        {
            var panel = new Rect(Screen.width - PanelW - 12f, 12f, PanelW, Mathf.Min(Screen.height - 24f, 860f));
            GUI.Box(panel, "", GUI.skin.box);
            GUI.BeginGroup(panel);

            float x = Margin;
            float w = PanelW - Margin * 2;
            float y = 6f;
            GUI.Label(new Rect(x, y, w, TitleH), "场景编辑（PC）");
            y += TitleH + 4f;

            var actors = Actors();
            GUI.Label(new Rect(x, y, w, HeaderH), $"演员 {actors.Count} 位");
            y += HeaderH + 4f;
            y = DrawActorButtons(actors, y);
            y += 4f;

            float saveTop = panel.height - SaveH - Margin;
            float mapH = Mathf.Clamp(saveTop - y - 70f, 110f, 160f);
            var map = new Rect(x, y, w, mapH);
            DrawMap(map, actors);
            y += mapH + 4f;

            GUI.Label(new Rect(x, y, w, 16f), message);
            y += 18f;
            GUI.Label(new Rect(x, y, w, 16f), $"道具库 {PropLibrary.Count} 条");
            y += 18f;
            var propRect = new Rect(x, y, w, Mathf.Max(40f, saveTop - y - 6f));
            DrawPropList(propRect);
            DrawSaveBar(new Rect(x, saveTop, w, SaveH));

            GUI.EndGroup();
        }

        private float DrawActorButtons(List<ActorNavigator> actors, float y)
        {
            var current = ActorSelection.Current;
            foreach (var nav in actors)
            {
                var r = new Rect(Margin, y, PanelW - Margin * 2, ActorBtnH);
                bool isCurrent = ReferenceEquals(nav, current);
                bool hasAgent = nav.agent != null;
                var label = (isCurrent ? "● " : "○ ") + nav.name + (hasAgent ? "" : "（无 NavMeshAgent）");
                GUI.color = ChipColor(nav);
                if (GUI.Button(r, label)) ActorSelection.Select(nav);
                GUI.color = Color.white;
                y += ActorBtnH;
            }
            return y;
        }

        private void DrawMap(Rect map, List<ActorNavigator> actors)
        {
            GUI.Box(map, "1:20 俯视小地图 · 拖拽当前演员 / 点选待放道具");
            if (tabletop == null) { message = "未找到 TabletopDirector"; return; }
            foreach (var nav in actors) DrawMarker(map, nav);
            HandleMapInput(map);
            if (!dragging) return;
            GUI.color = dragValid ? Color.green : Color.red;
            var p = WorldToMap(dragWorld, map);
            GUI.Label(new Rect(p - Vector2.one * 8f, new Vector2(16f, 16f)), "◈");
            GUI.color = Color.white;
        }

        private void DrawMarker(Rect map, ActorNavigator nav)
        {
            GUI.color = ChipColor(nav);
            GUI.Label(new Rect(WorldToMap(nav.transform.position, map) - Vector2.one * 6f, new Vector2(12f, 12f)), "●");
            GUI.color = Color.white;
        }

        private void DrawPropList(Rect rect)
        {
            GUI.BeginGroup(rect);
            propScroll = GUILayout.BeginScrollView(propScroll, GUILayout.Width(rect.width - 16f), GUILayout.Height(rect.height));
            PropCategory? last = null;
            foreach (var entry in PropLibrary.All)
            {
                if (entry.category != last) { last = entry.category; GUILayout.Label(entry.category.ToString()); }
                var prefix = entry.id == pendingPropId ? "▸ " : "";
                if (GUILayout.Button(prefix + entry.displayName))
                {
                    pendingPropId = entry.id;
                    message = $"待放置: {entry.displayName}，点小地图落地";
                }
            }
            GUILayout.EndScrollView();
            GUI.EndGroup();
        }

        private void DrawSaveBar(Rect rect)
        {
            float half = (rect.width - Margin) / 2f;
            if (GUI.Button(new Rect(rect.x, rect.y, half, rect.height), "保存快照")) SaveSnapshot();
            if (GUI.Button(new Rect(rect.x + half + Margin, rect.y, half, rect.height), "重新加载存档")) RestoreFromDisk();
        }
    }
}
