namespace ShotField.RuntimeUI
{
    /// <summary>掌心菜单可触发的动作类型。呈现层只负责把按钮映射回这里。</summary>
    public enum PalmMenuActionKind
    {
        SelectActor,
        SpawnProp,
        SaveSnapshot,
        ReloadSnapshot,
        ToggleMode
    }

    /// <summary>菜单内部分栏：演员 / 道具 / 场景（快照与模式切换）。</summary>
    public enum PalmMenuTab
    {
        Actors,
        Props,
        Scene
    }

    /// <summary>一条菜单项；Actor 仅 SelectActor 用，PropId 仅 SpawnProp 用。</summary>
    public sealed class PalmMenuEntry
    {
        public PalmMenuActionKind Kind;
        public string Label;
        public string PropId;
        public ActorNavigator Actor;
        public bool Active;
    }
}
