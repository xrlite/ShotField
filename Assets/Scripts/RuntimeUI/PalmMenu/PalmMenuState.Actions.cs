using System.Collections.Generic;
using UnityEngine;

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
            if (Network == null) Network = Object.FindAnyObjectByType<VrNetworkPanel>();
            if (Network == null) { SetStatus("联机入口未绑定"); return; }
            Network.Toggle();
            SetStatus(Network.Visible ? "联机面板已呼出" : "联机面板已关闭");
        }

        private void ToggleCommunity()
        {
            if (Community == null) Community = Object.FindAnyObjectByType<VrCommunityPanel>();
            if (Community == null)
                Community = new GameObject("[VrCommunityPanel]").AddComponent<VrCommunityPanel>();
            Community.Toggle();
            SetStatus(Community.Visible ? "社区案例库已呼出" : "社区案例库已关闭");
        }

        private void ToggleActorPerform()
        {
            if (Possession == null) Possession = Object.FindAnyObjectByType<ActorPossessionController>();
            if (Possession == null)
                Possession = new GameObject("[ActorPossessionController]").AddComponent<ActorPossessionController>();

            if (Possession.IsRecording)
            {
                Possession.StopRecording();
                Possession.StartReplay(Possession.activeClip);
                SetStatus("已停止录制，正在回放 3 点 IK Take");
                return;
            }

            GameObject actor = ActorSelection.Current != null ? ActorSelection.Current.gameObject : GameObject.Find("ActorA");
            if (actor == null) { SetStatus("未找到可附身演员 ActorA"); return; }

            Transform head = Camera.main != null ? Camera.main.transform : actor.transform;
            var leftGo = GameObject.Find("XRController_Left");
            var rightGo = GameObject.Find("XRController_Right");
            Possession.Possess(actor, head, leftGo != null ? leftGo.transform : head, rightGo != null ? rightGo.transform : head);
            Possession.StartRecording("PalmTake_01");
            SetStatus($"已附身 {actor.name}，正在录制 3 点 IK Take");
        }
    }
}
