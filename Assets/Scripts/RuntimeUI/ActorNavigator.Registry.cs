using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 选择表生命周期：演员激活即登记进 ActorSelection，失活/销毁即摘除。
    /// 道具库新加载或从待激活状态放行的演员靠 OnEnable 自动入网，不依赖面板手动 Bind。
    /// </summary>
    public partial class ActorNavigator
    {
        private void OnEnable()
        {
            ActorSelection.Register(this);
            ScheduleGroundCheck();
        }

        private void OnDisable() => ActorSelection.Unregister(this);
    }
}
