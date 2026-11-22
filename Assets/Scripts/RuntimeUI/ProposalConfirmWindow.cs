using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// P1 提案确认窗口（桌面 IMGUI，独立浮层，不占右侧既有 336px 面板区）。
    /// 逐条展示镜头 / 演员 / 道具提案，支持接受 / 拒绝 / 改参数（时长、备注）；
    /// 纯 UI 状态回写 <see cref="ProposalItemDto"/>，场景应用由 ChainOrchestrator 执行。
    /// </summary>
    public class ProposalConfirmWindow : MonoBehaviour
    {
        public ChainProposalDto Proposal { get; set; }
        public bool Visible { get; set; } = true;

        private Vector2 _scroll;
        private const float PanelW = 360f;
        private const float Margin = 8f;

        /// <summary>导演点击「应用已接受」时触发，编排器据此落场景。</summary>
        public event System.Action OnApplyRequested;

        void OnGUI()
        {
            if (!Visible || Proposal == null) return;

            float panelH = Mathf.Min(Screen.height - 24f, 720f);
            var panel = new Rect(12f, 70f, PanelW, panelH);
            GUI.Box(panel, "", GUI.skin.box);
            GUI.BeginGroup(panel);

            float x = Margin;
            float w = PanelW - Margin * 2;
            float y = 6f;

            GUI.Label(new Rect(x, y, w, 22f), "镜头提案");
            y += 24f;
            GUI.Label(new Rect(x, y, w, 16f), "逐条确认：接受后进入走位，拒绝跳过该条");
            y += 18f;
            GUI.Label(new Rect(x, y, w, 16f), Proposal.sceneTitle);
            y += 18f;

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(w), GUILayout.Height(panelH - 128f));
            foreach (var item in Proposal.items) DrawItem(item, w);
            GUILayout.EndScrollView();

            DrawBottomBar(new Rect(x, panelH - 34f, w, 28f));
            GUI.EndGroup();
        }

        private void DrawItem(ProposalItemDto item, float w)
        {
            if (item == null) return;
            GUI.color = DecisionColor(item.decision);
            GUILayout.Label($"[{item.kind}] {item.label}", GUILayout.Width(w));
            GUI.color = Color.white;

            if (!string.IsNullOrEmpty(item.actionText))
                GUILayout.Label(item.actionText, GUILayout.Width(w));
            if (!string.IsNullOrEmpty(item.dialogue))
                GUILayout.Label("对白：" + item.dialogue, GUILayout.Width(w));

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("接受", GUILayout.Width(70f)))
                    item.decision = ProposalDecision.Accepted;
                if (GUILayout.Button("拒绝", GUILayout.Width(70f)))
                    item.decision = ProposalDecision.Rejected;

                string durText = GUILayout.TextField(item.durationSeconds.ToString("F1"), GUILayout.Width(50f));
                if (float.TryParse(durText, out float dur) && dur > 0.1f)
                {
                    if (!Mathf.Approximately(dur, item.durationSeconds))
                    {
                        item.durationSeconds = dur;
                        item.decision = ProposalDecision.Edited;
                    }
                }
                GUILayout.Label("秒", GUILayout.Width(24f));
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("备注：", GUILayout.Width(48f));
            string note = GUILayout.TextField(item.note, GUILayout.Width(w - 48f));
            GUILayout.EndHorizontal();
            if (note != item.note)
            {
                item.note = note;
                if (!string.IsNullOrEmpty(note)) item.decision = ProposalDecision.Edited;
            }
            GUILayout.Space(6f);
        }

        private void DrawBottomBar(Rect rect)
        {
            float third = (rect.width - Margin * 2) / 3f;
            if (GUI.Button(new Rect(rect.x, rect.y, third, rect.height), "全部接受"))
            {
                foreach (var item in Proposal.items)
                    if (item != null && item.decision != ProposalDecision.Rejected)
                        item.decision = ProposalDecision.Accepted;
            }
            if (GUI.Button(new Rect(rect.x + third + Margin, rect.y, third, rect.height), "全部拒绝"))
            {
                foreach (var item in Proposal.items)
                    if (item != null) item.decision = ProposalDecision.Rejected;
            }
            if (GUI.Button(new Rect(rect.x + 2f * (third + Margin), rect.y, third, rect.height), "应用已接受"))
                OnApplyRequested?.Invoke();
        }

        private static Color DecisionColor(ProposalDecision decision)
        {
            switch (decision)
            {
                case ProposalDecision.Accepted: return new Color(0.6f, 1f, 0.6f);
                case ProposalDecision.Rejected: return new Color(1f, 0.6f, 0.6f);
                case ProposalDecision.Edited: return new Color(1f, 0.9f, 0.5f);
                default: return Color.white;
            }
        }
    }
}
