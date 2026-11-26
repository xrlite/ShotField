using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 1:1 真人演绎与基本动作关键帧面板逻辑：
    /// 1) 附身头手 30Hz 录制与回放（ActorPossessionController）；
    /// 2) 常用表演关键帧打点（走近 / 转身 / 拿取 / 对话），写入关键帧列表并驱动自然动画。
    /// </summary>
    public sealed class ActorPerformancePanel : MonoBehaviour
    {
        public string LastNote { get; private set; } = "就绪 · 可录制附身动作或打关键帧";
        public readonly List<ActorKeyframe> Keyframes = new List<ActorKeyframe>();

        private ActorPossessionController _possession;
        private int _clipSeq = 1;

        private void Resolve()
        {
            if (_possession == null) _possession = FindAnyObjectByType<ActorPossessionController>();
        }

        public string TogglePossessRecord()
        {
            Resolve();
            var actor = ResolveTargetActor();
            if (_possession == null || actor == null)
                return LastNote = "未找到可附身演员或 Possession 控制器";

            if (_possession.IsRecording)
            {
                _possession.StopRecording();
                if (_possession.activeClip != null && _possession.activeClip.Count > 0)
                    _possession.StartReplay(_possession.activeClip);
                return LastNote = $"已停止录制并回放 ({_possession.activeClip?.Count ?? 0} 帧)";
            }

            var cam = Camera.main != null ? Camera.main.transform : null;
            var left = GameObject.Find("XRController_Left")?.transform ?? cam;
            var right = GameObject.Find("XRController_Right")?.transform ?? cam;
            _possession.Possess(actor, cam, left, right);
            _possession.StartRecording($"Perf_{_clipSeq++:00}");
            return LastNote = $"正在 1:1 附身录制 [{actor.name}]… 再次点击结束并回放";
        }

        public string TriggerActionKeyframe(string actionName)
        {
            var actor = ResolveTargetActor();
            if (actor == null) return LastNote = "请先在沙盘或场内选中一个演员";

            var nav = actor.GetComponent<ActorNavigator>();
            float t = Time.time;
            if (actionName == "walk" && nav != null)
            {
                Vector3 dst = actor.transform.position + actor.transform.forward * 1.1f;
                nav.MoveTo(dst, false);
            }
            else if (actionName == "turn")
            {
                actor.transform.Rotate(0f, 90f, 0f);
            }
            else
            {
                var anim = actor.GetComponentInChildren<Animator>();
                if (anim != null && anim.HasState(0, Animator.StringToHash("Idle")))
                    anim.CrossFade("Idle", 0.15f);
            }

            Keyframes.Add(new ActorKeyframe(t, actor.transform.position, actor.transform.rotation, actionName));
            return LastNote = $"已记录动作关键帧 [{actionName}] (累计 {Keyframes.Count} 点)";
        }

        private static GameObject ResolveTargetActor()
        {
            if (ActorSelection.Current != null) return ActorSelection.Current.gameObject;
            var all = ActorSelection.All;
            return all != null && all.Count > 0 && all[0] != null ? all[0].gameObject : GameObject.Find("ActorA");
        }
    }
}
