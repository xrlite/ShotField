using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// PC 场景编辑面板（M1 六步链）：选人 → 小地图拖拽吸附 → 加道具 → 保存 → 重载保持。
    /// 屏幕空间 IMGUI，挂在画布同一物体上，画在屏幕右侧，避开左侧 340px 既有面板。
    /// 注：DesktopStudioView.cs 为禁改文件且已声明 OnGUI，本视图作为同级驱动组件独立接收 OnGUI。
    /// </summary>
    public sealed partial class DesktopStudioEditView : MonoBehaviour
    {
        private const float SampleRadius = 0.6f;
        private const float PixelsPerMeter = 500f;
        private const float MarkerHitPx = 14f;

        private TabletopDirector tabletop;
        private PropBrowserUI browser;
        private readonly List<KeyValuePair<string, GameObject>> placedProps = new List<KeyValuePair<string, GameObject>>();
        private string pendingPropId;
        private string message = "";
        private string savePath = "";
        private string restoredUtc = "";
        private bool dragging;
        private Vector3 dragOrigin;
        private Vector3 dragWorld;
        private bool dragValid;

        void Awake()
        {
            tabletop = GetComponent<TabletopDirector>();
            browser = FindAnyObjectByType<PropBrowserUI>();
        }

        void OnGUI()
        {
            var area = new Rect(Screen.width - 332f, 12f, 320f, Mathf.Min(Screen.height - 24f, 860f));
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("场景编辑（PC）");
            DrawActorList();
            DrawMap();
            DrawPropList();
            DrawSaveBar();
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private List<ActorNavigator> Actors()
        {
            var result = new List<ActorNavigator>();
            var all = ActorSelection.All;
            if (all != null)
            {
                foreach (var nav in all) if (nav != null) result.Add(nav);
            }
            else
            {
                foreach (var nav in FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include)) result.Add(nav);
            }
            return result;
        }

        private void DrawActorList()
        {
            var actors = Actors();
            GUILayout.Label($"演员 {actors.Count} 位");
            var current = ActorSelection.Current;
            foreach (var nav in actors)
            {
                bool isCurrent = ReferenceEquals(nav, current);
                bool hasAgent = nav.agent != null;
                var label = (isCurrent ? "● " : "○ ") + nav.name + (hasAgent ? "" : "（无 NavMeshAgent）");
                GUI.color = ChipColor(nav);
                if (GUILayout.Button(label)) ActorSelection.Select(nav);
                GUI.color = Color.white;
            }
        }

        private void DrawMap()
        {
            var map = GUILayoutUtility.GetRect(290f, 170f);
            GUI.Box(map, "1:20 俯视小地图 · 拖拽当前演员 / 点选待放道具");
            if (tabletop == null) { message = "未找到 TabletopDirector"; return; }
            foreach (var nav in Actors()) DrawMarker(map, nav);
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

        private void HandleMapInput(Rect map)
        {
            var input = Event.current;
            if (input == null) return;
            var current = ActorSelection.Current;

            if (input.type == EventType.MouseDown && input.button == 0 && map.Contains(input.mousePosition))
            {
                if (current != null && current.agent != null && NearMarker(map, current, input.mousePosition))
                {
                    dragging = true;
                    dragOrigin = current.transform.position;
                    dragWorld = dragOrigin;
                    dragValid = true;
                    input.Use();
                }
                else if (!string.IsNullOrEmpty(pendingPropId))
                {
                    PlacePendingProp(MapToWorld(input.mousePosition, map));
                    input.Use();
                }
                return;
            }
            if (dragging && input.type == EventType.MouseDrag && input.button == 0)
            {
                dragWorld = MapToWorld(input.mousePosition, map);
                dragValid = current != null && current.CanStandAt(dragWorld, SampleRadius, out _);
                message = dragValid ? "落点可行走，松手落位" : "不可行走：松手回弹原位";
                input.Use();
                return;
            }
            if (dragging && input.type == EventType.MouseUp && input.button == 0)
            {
                dragging = false;
                FinishDrag(current);
                input.Use();
            }
        }

        private void FinishDrag(ActorNavigator current)
        {
            if (current == null) return;
            if (dragValid && current.TryPlace(dragWorld, SampleRadius, out var snapped))
                message = $"已落位: {snapped.x:F2}, {snapped.y:F2}, {snapped.z:F2}";
            else
            {
                if (!current.TryPlace(dragOrigin, SampleRadius, out _)) current.transform.position = dragOrigin;
                message = "落点不可行走，已回弹原位";
            }
        }

        private Vector2 WorldToMap(Vector3 world, Rect map)
        {
            var off = tabletop.WorldToTabletop(world) - tabletop.TabletopAnchor.position;
            return map.center + new Vector2(off.x, -off.z) * PixelsPerMeter;
        }

        private Vector3 MapToWorld(Vector2 mapPos, Rect map)
        {
            var rel = (mapPos - map.center) / PixelsPerMeter;
            var tt = tabletop.TabletopAnchor.position + new Vector3(rel.x, 0f, -rel.y);
            return tabletop.TabletopToWorld(tt);
        }

        private void DrawPropList()
        {
            GUILayout.Label($"道具库 {PropLibrary.Count} 条");
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
        }

        private void DrawSaveBar()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存快照")) SaveSnapshot();
            if (GUILayout.Button("重新加载存档")) RestoreFromDisk();
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(savePath)) GUILayout.Label("存档: " + savePath);
            if (!string.IsNullOrEmpty(restoredUtc)) GUILayout.Label("已恢复 " + restoredUtc);
        }
    }
}
