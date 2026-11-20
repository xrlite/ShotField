#if UNITY_EDITOR
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class ThreeStepGuideVerifier
    {
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run Three-Step Guide Verifier")]
        public static void Run()
        {
            int passed = 0;
            var workflow = DirectorWorkflowState.Instance;
            workflow.Reset();

            var board = ThreeStepGuideBoard.CreateGuideBoard();
            try
            {
                VerifyAssert.Require("Guide board instantiated with WorldSpace Canvas",
                    board != null && board.canvas != null && board.canvas.renderMode == RenderMode.WorldSpace, ref passed);

                VerifyAssert.Require("Guide board transform matches director ceiling mount specs",
                    Vector3.Distance(board.transform.position, ThreeStepGuideBoard.DefaultPosition) < 0.01f &&
                    board.cardImages != null && board.cardImages.Length == 3, ref passed);

                board.UpdateStepHighlight(workflow.CurrentStep);
                VerifyAssert.Require("Step 1 (SelectScript): Card 0 glowing blue, Cards 1 & 2 dimmed",
                    ColorMatch(board.cardImages[0].color, ThreeStepGuideBoard.ActiveColor) &&
                    ColorMatch(board.cardImages[1].color, ThreeStepGuideBoard.InactiveColor) &&
                    ColorMatch(board.cardImages[2].color, ThreeStepGuideBoard.InactiveColor), ref passed);

                DirectorWorkflowStep recordedStep = DirectorWorkflowStep.SelectScript;
                workflow.OnStepChanged += s => recordedStep = s;

                workflow.AdvanceStep();
                board.UpdateStepHighlight(workflow.CurrentStep);
                VerifyAssert.Require("Step 2 (FrameCamera): Card 1 glowing blue, Cards 0 & 2 dimmed",
                    workflow.CurrentStep == DirectorWorkflowStep.FrameCamera &&
                    recordedStep == DirectorWorkflowStep.FrameCamera &&
                    ColorMatch(board.cardImages[1].color, ThreeStepGuideBoard.ActiveColor) &&
                    ColorMatch(board.cardImages[0].color, ThreeStepGuideBoard.InactiveColor), ref passed);

                workflow.AdvanceStep();
                board.UpdateStepHighlight(workflow.CurrentStep);
                VerifyAssert.Require("Step 3 (ExportSnapshot): Card 2 glowing blue, Cards 0 & 1 dimmed",
                    workflow.CurrentStep == DirectorWorkflowStep.ExportSnapshot &&
                    recordedStep == DirectorWorkflowStep.ExportSnapshot &&
                    ColorMatch(board.cardImages[2].color, ThreeStepGuideBoard.ActiveColor) &&
                    ColorMatch(board.cardImages[1].color, ThreeStepGuideBoard.InactiveColor), ref passed);

                workflow.AdvanceStep();
                board.UpdateStepHighlight(workflow.CurrentStep);
                VerifyAssert.Require("Workflow loops back to SelectScript on advance from last step",
                    workflow.CurrentStep == DirectorWorkflowStep.SelectScript &&
                    ColorMatch(board.cardImages[0].color, ThreeStepGuideBoard.ActiveColor), ref passed);
            }
            finally
            {
                workflow.Reset();
                if (board != null) Object.DestroyImmediate(board.gameObject);
            }

            VerifyAssert.Finish("THREE-STEP-GUIDE-RESULT", passed, Expected);
        }

        private static bool ColorMatch(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f &&
            Mathf.Abs(a.b - b.b) < 0.02f && Mathf.Abs(a.a - b.a) < 0.02f;
    }
}
#endif
