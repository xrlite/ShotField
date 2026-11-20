using System;

namespace ShotField.Core
{
    public enum DirectorWorkflowStep
    {
        SelectScript = 0,
        FrameCamera = 1,
        ExportSnapshot = 2
    }

    public class DirectorWorkflowState
    {
        public static DirectorWorkflowState Instance { get; set; } = new DirectorWorkflowState();

        public DirectorWorkflowStep CurrentStep { get; private set; } = DirectorWorkflowStep.SelectScript;

        public event Action<DirectorWorkflowStep> OnStepChanged;

        public void SetStep(DirectorWorkflowStep step)
        {
            if (CurrentStep == step) return;
            CurrentStep = step;
            OnStepChanged?.Invoke(CurrentStep);
        }

        public void AdvanceStep()
        {
            switch (CurrentStep)
            {
                case DirectorWorkflowStep.SelectScript:
                    SetStep(DirectorWorkflowStep.FrameCamera);
                    break;
                case DirectorWorkflowStep.FrameCamera:
                    SetStep(DirectorWorkflowStep.ExportSnapshot);
                    break;
                case DirectorWorkflowStep.ExportSnapshot:
                    SetStep(DirectorWorkflowStep.SelectScript);
                    break;
            }
        }

        public void Reset()
        {
            SetStep(DirectorWorkflowStep.SelectScript);
        }
    }
}
