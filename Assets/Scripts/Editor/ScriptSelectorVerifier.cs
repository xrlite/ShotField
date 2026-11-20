#if UNITY_EDITOR
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class ScriptSelectorVerifier
    {
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run Script Selector Verifier")]
        public static void Run()
        {
            int passed = 0;
            var workflow = DirectorWorkflowState.Instance;
            workflow.Reset();

            var canvasGo = new GameObject("TestWorkbenchCanvas", typeof(RectTransform), typeof(Canvas));
            var panel = ScriptSelectionPanel.AttachToCanvas(canvasGo);
            panel.EnsureUI();
            panel.RefreshUI();

            try
            {
                var rt = panel.GetComponent<RectTransform>();
                VerifyAssert.Require("Panel docked at (0, 345) with standard size (660, 85)",
                    rt != null && rt.anchoredPosition == ScriptSelectionPanel.DefaultPosition &&
                    rt.sizeDelta == ScriptSelectionPanel.DefaultSize, ref passed);

                VerifyAssert.Require("Act 1/5 initial state formatted with Chinese title",
                    panel.CurrentActIndex == 0 &&
                    panel.indicatorText.text.Contains("1/5") &&
                    panel.indicatorText.text.Contains("第一幕"), ref passed);

                panel.NextAct();
                VerifyAssert.Require("Act 2/5 navigation updates indicator and conflict summary",
                    panel.CurrentActIndex == 1 &&
                    panel.indicatorText.text.Contains("2/5") &&
                    panel.indicatorText.text.Contains("第二幕") &&
                    !string.IsNullOrEmpty(panel.conflictText.text), ref passed);

                panel.SelectAct(4);
                bool act5Ok = panel.CurrentActIndex == 4 && panel.indicatorText.text.Contains("5/5") && panel.indicatorText.text.Contains("第五幕");
                panel.NextAct();
                bool wrapOk = panel.CurrentActIndex == 0 && panel.indicatorText.text.Contains("1/5");
                VerifyAssert.Require("5-act navigation covers Act 5 and wraps around cleanly",
                    act5Ok && wrapOk, ref passed);

                VerifyAssert.Require("Tag text explicitly displays user-replaceable placeholder note",
                    panel.tagText != null && panel.tagText.text == ScriptSelectionPanel.ScriptTag, ref passed);

                ProductionAct callbackAct = ProductionAct.Act1_Connection;
                ActSceneDescriptor callbackDesc = null;
                panel.OnActSelected += (act, desc) => { callbackAct = act; callbackDesc = desc; };

                workflow.SetStep(DirectorWorkflowStep.SelectScript);
                panel.SelectAct(2);
                VerifyAssert.Require("Act selection invokes callback and advances workflow to FrameCamera",
                    callbackAct == ProductionAct.Act3_Verification &&
                    callbackDesc != null &&
                    callbackDesc.act == ProductionAct.Act3_Verification &&
                    workflow.CurrentStep == DirectorWorkflowStep.FrameCamera, ref passed);
            }
            finally
            {
                workflow.Reset();
                if (canvasGo != null) Object.DestroyImmediate(canvasGo);
            }

            VerifyAssert.Finish("SCRIPT-SELECTOR-RESULT", passed, Expected);
        }
    }
}
#endif
