using System.Collections.Generic;

namespace ShotField.RuntimeUI
{
    public sealed partial class PalmMenuState
    {
        private void BuildCardEntries(List<PalmMenuEntry> entries)
        {
            if (Cards == null) return;
            AddCardEntry(entries, ThreeCardWorkspace.MonitorId, "监看板");
            AddCardEntry(entries, ThreeCardWorkspace.TimelineId, "时间轴");
            AddCardEntry(entries, ThreeCardWorkspace.DrawerId, "剧本 / 资产");
        }

        private void AddCardEntry(List<PalmMenuEntry> entries, string id, string label)
        {
            bool open = Cards.IsOpen(id);
            entries.Add(new PalmMenuEntry
            {
                Kind = PalmMenuActionKind.ToggleCard,
                CardId = id,
                Label = (open ? "收纳 " : "呼出 ") + label,
                Active = open
            });
        }

        private void ToggleCard(string id)
        {
            if (Cards == null) { SetStatus("三卡工作区未绑定"); return; }
            Cards.ToggleCard(id);
            SetStatus(Cards.LastNote);
        }

        private void ToggleNetwork()
        {
            if (Network == null) { SetStatus("联机入口未绑定"); return; }
            Network.Toggle();
            SetStatus(Network.Visible ? "联机面板已呼出" : "联机面板已关闭");
        }
    }
}
