using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 手柄触感封装：呼出 / 收起 / 选中 / 确认四档脉冲。
    /// 控制器引用缺失（编辑器无头显、模拟器无触觉回传）时静默降级，不报错、不抛异常。
    /// 走 XRI 官方 <see cref="HapticImpulsePlayer.SendHapticImpulse"/>，不自造震动通道。
    /// </summary>
    public sealed class PalmMenuHaptics
    {
        private readonly HapticImpulsePlayer _left;
        private readonly HapticImpulsePlayer _right;

        public PalmMenuHaptics(HapticImpulsePlayer left, HapticImpulsePlayer right)
        {
            _left = left;
            _right = right;
        }

        /// <summary>菜单呼出：左手短脉冲提示。</summary>
        public void PulseOpen() => Send(_left, 0.4f, 0.08f);

        /// <summary>菜单收起：左手更弱的短脉冲。</summary>
        public void PulseClose() => Send(_left, 0.2f, 0.05f);

        /// <summary>条目悬停/选中：右手轻脉冲。</summary>
        public void PulseSelect() => Send(_right, 0.5f, 0.06f);

        /// <summary>命令确认（落位/保存/切模式）：右手稍强脉冲。</summary>
        public void PulseConfirm() => Send(_right, 0.6f, 0.12f);

        private static void Send(HapticImpulsePlayer controller, float amplitude, float duration)
        {
            if (controller == null) return;
            controller.SendHapticImpulse(amplitude, duration);
        }
    }
}
