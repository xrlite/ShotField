#if UNITY_EDITOR
using System;
using System.Linq;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// P1 工作流集成语门禁：反射核对六阶段枚举顺序、协调器线性前进/复位语义，
    /// 以及协调器类型留在 Core 程序集（不向 RuntimeUI / Editor 泄漏依赖方向）。
    /// </summary>
    public static class WorkflowAssemblyVerifier
    {
        private static readonly string[] ExpectedStageNames =
        {
            "Import", "Proposal", "Assets", "Rehearsal", "Edit", "Export"
        };

        [MenuItem("ShotField/Verifiers/Run Workflow Assembly Verifier")]
        public static void Run()
        {
            int passed = 0;

            var stageNames = Enum.GetNames(typeof(WorkflowStage));
            VerifyAssert.Require("六阶段枚举按 Import/Proposal/Assets/Rehearsal/Edit/Export 顺序声明",
                stageNames.SequenceEqual(ExpectedStageNames), ref passed);

            var stageValues = Enum.GetValues(typeof(WorkflowStage)).Cast<int>().ToArray();
            bool valuesOrdered = stageValues.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 });
            VerifyAssert.Require("枚举数值 0..5 与阶段顺序一致", valuesOrdered, ref passed);

            var coordinator = new WorkflowCoordinator();
            VerifyAssert.Require("协调器初始阶段为 Import",
                coordinator.CurrentStage == WorkflowStage.Import, ref passed);

            for (int i = 0; i < WorkflowCoordinator.StageCount - 1; i++) coordinator.Advance();
            VerifyAssert.Require("五次线性前进后到达 Export",
                coordinator.CurrentStage == WorkflowStage.Export && coordinator.IsFinished, ref passed);

            coordinator.Advance();
            VerifyAssert.Require("到达 Export 后继续前进保持原地",
                coordinator.CurrentStage == WorkflowStage.Export && !coordinator.CanAdvance, ref passed);

            coordinator.Reset();
            string asmName = coordinator.GetType().Assembly.GetName().Name;
            bool coreOnly = asmName == "ShotField.Core";
            VerifyAssert.Require($"协调器归属 Core 程序集（实际 {asmName}，无 RuntimeUI/Editor 泄漏）",
                coordinator.CurrentStage == WorkflowStage.Import && coreOnly, ref passed);

            VerifyAssert.Finish("WORKFLOW-ASSEMBLY-RESULT", passed, ExpectedStageNames.Length);
        }
    }
}
#endif
