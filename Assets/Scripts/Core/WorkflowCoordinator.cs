using System;

namespace ShotField.Core
{
    /// <summary>
    /// P1 工作流状态协调器：纯状态机，不依赖场景、网络与 UI。
    /// 只回答「现在处于哪一阶段、能否前进」；各阶段的实际动作仍由既有
    /// ChainOrchestrator 等 RuntimeUI 服务承接，本类不直接驱动场景对象。
    /// 线性前进，到达 Export 后不再推进；回退起点只能走 <see cref="Reset"/>。
    /// </summary>
    public sealed class WorkflowCoordinator
    {
        public const int StageCount = 6;

        public WorkflowStage CurrentStage { get; private set; } = WorkflowStage.Import;

        public bool IsFinished => CurrentStage == WorkflowStage.Export;

        public bool CanAdvance => CurrentStage != WorkflowStage.Export;

        /// <summary>阶段变更事件，参数为变更后的阶段。</summary>
        public event Action<WorkflowStage> OnStageChanged;

        /// <summary>前进到下一阶段；已在 Export 时保持原地并返回当前阶段。</summary>
        public WorkflowStage Advance()
        {
            if (!CanAdvance) return CurrentStage;
            SetStage((WorkflowStage)(CurrentStage + 1));
            return CurrentStage;
        }

        /// <summary>回退到导入阶段，重新走完整流程。</summary>
        public void Reset()
        {
            SetStage(WorkflowStage.Import);
        }

        private void SetStage(WorkflowStage stage)
        {
            if (CurrentStage == stage) return;
            CurrentStage = stage;
            OnStageChanged?.Invoke(CurrentStage);
        }
    }
}
