using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// PC 场景编辑面板（M1 六步链）：选人 → 小地图拖拽吸附 → 加道具 → 保存 → 重载保持。
    /// 屏幕空间 IMGUI，挂在画布同一物体上，画在屏幕右侧，避开左侧 340px 既有面板。
    /// 绝对 Rect 布局绘制见 DesktopStudioEditView.Layout.cs；保存/恢复链路见 SceneEditRestore.cs。
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
        private Vector2 propScroll;

        void Awake()
        {
            tabletop = GetComponent<TabletopDirector>();
            browser = FindAnyObjectByType<PropBrowserUI>();
        }

        private List<ActorNavigator> Actors()
        {
            var result = new List<ActorNavigator>();
            var all = ActorSelection.All;
            if (all != null)
            {
                // 退役演员可能仍挂在场景里但不参与编辑，列表里只显示在场的
                foreach (var nav in all) if (nav != null && nav.gameObject.activeInHierarchy) result.Add(nav);
            }
            else
            {
                foreach (var nav in FindObjectsByType<ActorNavigator>(FindObjectsInactive.Exclude)) result.Add(nav);
            }
            return result;
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
    }
}
