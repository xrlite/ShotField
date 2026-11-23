namespace ShotField.Core
{
    /// <summary>
    /// P1 创作工作流六阶段：导入剧本 → 提案确认 → 资产落场 → 排练预演 → 剪辑质检 → 导出镜头包。
    /// 枚举数值即编排顺序；协调器只允许沿此顺序线性前进，回退只能走 Reset。
    /// </summary>
    public enum WorkflowStage
    {
        Import = 0,
        Proposal = 1,
        Assets = 2,
        Rehearsal = 3,
        Edit = 4,
        Export = 5
    }
}
