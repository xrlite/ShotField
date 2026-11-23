#if UNITY_EDITOR
using System;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// P5 积木平铺布局验收。
    ///
    /// 这个验证器守的是一条**几何结论**，不是代码风格：
    /// 在 0.70 m 眼距下，一块 0.62 m 宽的板占 47.8°，已经吃满 ±25° 水平舒适区（50°），
    /// 两块并排要 85.8°，超出 ±35° 可接受上限（70°）。所以"多板并排"物理不成立，
    /// 只能走弧面 + 转头。任何人把板宽改大、或把角间距改小，这里会立刻炸。
    ///
    /// 边界：板实例的默认值在编辑模式下用临时 GameObject 读取（测的是脚本默认，
    /// 不是场景装配结果），读完立即销毁，不污染场景。
    /// </summary>
    public static class BlockLayoutVerifier
    {
        // 6 条几何 + 4 条默认值 = 10
        private const int ExpectedAssertions = 10;

        private const float EyeDistance = 0.70f;
        private const float LargePanelWidth = 0.62f;
        private const float SmallPanelWidth = 0.36f;

        [MenuItem("ShotField/Verifiers/Run Block Layout Verifier")]
        [MenuItem("镜场 ShotField/验证/验证积木平铺 BlockLayout")]
        public static void Run()
        {
            int passCount = 0;
            void Assert(string name, bool condition)
            {
                if (condition)
                {
                    passCount++;
                    Debug.Log($"[BlockLayoutVerifier] PASS: {name}");
                }
                else
                {
                    Debug.LogError($"[BlockLayoutVerifier] FAIL: {name}");
                    throw new Exception("Assertion failed: " + name);
                }
            }

            // —— 几何：视锥占用 ——
            float largeAngle = AngleOf(LargePanelWidth, EyeDistance);
            float smallAngle = AngleOf(SmallPanelWidth, EyeDistance);
            Assert($"大板占角约 47.8°（实测 {largeAngle:F1}°）", Mathf.Abs(largeAngle - 47.8f) < 0.5f);
            Assert($"小板占角约 28.8°（实测 {smallAngle:F1}°）", Mathf.Abs(smallAngle - 28.8f) < 0.5f);

            // —— 几何：并排可行性 ——
            float twoLarge = AngleOf(LargePanelWidth * 2f + 0.06f, EyeDistance);
            float twoSmall = AngleOf(SmallPanelWidth * 2f + 0.06f, EyeDistance);
            Assert($"两块大板并排 85.8° 超 ±35° 上限，故禁止并排（实测 {twoLarge:F1}°）",
                twoLarge > 70f);
            Assert($"两块小板并排 58.2° 仍在可接受区（实测 {twoSmall:F1}°）",
                twoSmall < 70f);

            // —— 默认值：角间距必须大于大板占角，否则相邻板重叠 ——
            var go = new GameObject("[BlockLayoutVerifier]");
            try
            {
                var registry = go.AddComponent<BlockPanelRegistry>();
                var slot = go.AddComponent<BlockPanelSlot>();

                Assert($"默认角间距 {registry.panelSpacingDeg}° 大于大板占角 47.8°，相邻不重叠",
                    registry.panelSpacingDeg > largeAngle);

                Assert("默认距离档为 0.60 / 0.70 / 0.80 三档递增",
                    registry.distanceSteps != null &&
                    registry.distanceSteps.Length == 3 &&
                    Mathf.Approximately(registry.distanceSteps[0], 0.60f) &&
                    Mathf.Approximately(registry.distanceSteps[1], 0.70f) &&
                    Mathf.Approximately(registry.distanceSteps[2], 0.80f));

                Assert("默认档索引落在数组范围内",
                    registry.currentStepIndex >= 0 &&
                    registry.currentStepIndex < registry.distanceSteps.Length);

                Assert("默认板尺寸为大板 0.62×0.42",
                    Mathf.Approximately(slot.sizeMeters.x, 0.62f) &&
                    Mathf.Approximately(slot.sizeMeters.y, 0.42f));

                Assert("默认板下沉量 0.10 m，低于视线但不脱出视野",
                    Mathf.Approximately(registry.verticalDrop, 0.10f));

                Assert("默认档距离等于 0.70 m",
                    Mathf.Approximately(registry.CurrentDistance, 0.70f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            if (passCount != ExpectedAssertions)
            {
                Debug.LogError($"BLOCK-LAYOUT-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"BLOCK-LAYOUT-RESULT=PASS ({passCount}/{ExpectedAssertions})");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>宽度为 w 的板在距离 d 处所占的水平张角（度）。</summary>
        private static float AngleOf(float width, float distance) =>
            2f * Mathf.Atan(width * 0.5f / distance) * Mathf.Rad2Deg;
    }
}
#endif
