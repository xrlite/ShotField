using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 走位震动独立模块：沙盘给演员下达走位目标时，对应手柄短促震动反馈。
    /// 订阅 <see cref="ActorNavigator.OnDestinationSet"/>；无 XR 设备（编辑器 / 模拟器）
    /// 时只记日志，不抛异常。模块自身不触碰导航与选中逻辑。
    /// 触感走 XRI <see cref="HapticImpulsePlayer.SendHapticImpulse"/>，不自造震动通道。
    /// </summary>
    public class ActorWalkHaptics : MonoBehaviour
    {
        [Range(0f, 1f)] public float amplitude = 0.5f;
        public float durationSeconds = 0.1f;

        /// <summary>手柄触感播放器引用；留空 Start 自动在场景里找任一 HapticImpulsePlayer。</summary>
        public HapticImpulsePlayer controller;

        private readonly List<ActorNavigator> _bound = new List<ActorNavigator>();

        void Start()
        {
            if (controller == null) controller = FindAnyObjectByType<HapticImpulsePlayer>();
            BindAll();
            ActorSelection.Changed += OnSelectionChanged;
        }

        void OnDestroy()
        {
            ActorSelection.Changed -= OnSelectionChanged;
            UnbindAll();
        }

        private void OnSelectionChanged(ActorNavigator _) => BindAll();

        private void BindAll()
        {
            UnbindAll();
            var all = ActorSelection.All;
            if (all == null) return;
            foreach (var nav in all)
            {
                if (nav == null || _bound.Contains(nav)) continue;
                nav.OnDestinationSet += HandleDestinationSet;
                _bound.Add(nav);
            }
        }

        private void UnbindAll()
        {
            foreach (var nav in _bound)
            {
                if (nav != null) nav.OnDestinationSet -= HandleDestinationSet;
            }
            _bound.Clear();
        }

        private void HandleDestinationSet(Vector3 _) => Pulse();

        /// <summary>触发一次手柄震动；控制器缺失或不支持脉冲时静默降级。</summary>
        public void Pulse()
        {
            if (controller == null)
            {
                Debug.Log("[ActorWalkHaptics] 未找到 XR 控制器，跳过震动");
                return;
            }
            controller.SendHapticImpulse(amplitude, durationSeconds);
        }
    }
}
