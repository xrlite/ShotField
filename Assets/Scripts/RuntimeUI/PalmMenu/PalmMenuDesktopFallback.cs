using UnityEngine;
using UnityEngine.InputSystem;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 桌面回退呈现层：编辑器/无头显时按 Tab 在屏幕左上角弹出同一套 <see cref="PalmMenuState"/>。
    /// 与 VR presenter 共用状态机与公开 API，只是把射线+扳机换成鼠标点按钮；
    /// 输入走新输入系统 <see cref="Keyboard"/>，不碰旧遗留输入 API（已修事故）。
    /// 无业务差异：PC 上能完成选人/加道具/存快照/重载/切沙盘，供无头显阶段自测。
    /// </summary>
    public sealed class PalmMenuDesktopFallback : MonoBehaviour
    {
        private const float PanelW = 280f;
        private const float PanelH = 360f;
        private const float Margin = 8f;
        private const float TabH = 24f;
        private const float EntryH = 26f;

        public PropBrowserUI browser;
        public TabletopDirector tabletop;

        /// <summary>只读状态机访问器：自测/对齐 VR presenter 用，业务命令仍走 State 公开方法。</summary>
        public PalmMenuState State => _state;

        private PalmMenuState _state;
        private Vector2 _scroll;

        private void Awake()
        {
            _state = new PalmMenuState
            {
                Browser = browser != null ? browser : FindAnyObjectByType<PropBrowserUI>(),
                Tabletop = tabletop != null ? tabletop : FindAnyObjectByType<TabletopDirector>()
            };
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.tabKey.wasPressedThisFrame) _state.Toggle();
        }

        private void OnGUI()
        {
            if (!_state.IsOpen) return;

            var panel = new Rect(12f, 12f, PanelW, PanelH);
            GUI.Box(panel, "", GUI.skin.box);
            GUI.BeginGroup(panel);

            GUILayout.BeginArea(new Rect(Margin, Margin, PanelW - Margin * 2, PanelH - Margin * 2));
            GUILayout.Label("掌心菜单（PC 回退 · Tab 收起）");
            DrawTabRow();
            DrawEntries();
            GUILayout.Label(_state.Status);
            GUILayout.EndArea();

            GUI.EndGroup();
        }

        private void DrawTabRow()
        {
            GUILayout.BeginHorizontal();
            DrawTab("演员", PalmMenuTab.Actors);
            DrawTab("道具", PalmMenuTab.Props);
            DrawTab("场景", PalmMenuTab.Scene);
            GUILayout.EndHorizontal();
        }

        private void DrawTab(string label, PalmMenuTab tab)
        {
            bool active = _state.Tab == tab;
            var old = GUI.color;
            if (active) GUI.color = new Color(0.30f, 0.55f, 0.95f, 0.95f);
            if (GUILayout.Button(label, GUILayout.Height(TabH))) _state.SelectTab(tab);
            GUI.color = old;
        }

        private void DrawEntries()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var entry in _state.BuildEntries())
            {
                string label = (entry.Active ? "● " : "○ ") + entry.Label;
                if (GUILayout.Button(label, GUILayout.Height(EntryH))) OnEntryClicked(entry);
            }
            GUILayout.EndScrollView();
        }

        private void OnEntryClicked(PalmMenuEntry entry)
        {
            bool closesAfterExecute = _state.Execute(entry);
            if (closesAfterExecute) _state.SetOpen(false);
        }
    }
}
