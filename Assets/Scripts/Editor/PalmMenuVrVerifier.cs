#if UNITY_EDITOR
using System;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// P5 掌心菜单 VR 侧契约验收。
    ///
    /// 验证范围（诚实边界，别当功能验收用）：
    /// 本验证器只能证明**规格常量与状态机契约成立**，不能证明菜单在头显里看得见、
    /// 射线点得中 —— 那些必须装机后截图。按项目口径，这里的结论是
    /// 「契约 PASS / 运行时 pending-真机」，不得写成「掌心菜单已可用」。
    /// </summary>
    public static class PalmMenuVrVerifier
    {
        // 4 条尺寸规格 + 6 条状态机契约 = 10
        private const int ExpectedAssertions = 10;

        [MenuItem("ShotField/Verifiers/Run Palm Menu VR Verifier")]
        [MenuItem("镜场 ShotField/验证/验证掌心菜单 PalmMenuVR")]
        public static void Run()
        {
            int passCount = 0;
            void Assert(string name, bool condition)
            {
                if (condition)
                {
                    passCount++;
                    Debug.Log($"[PalmMenuVrVerifier] PASS: {name}");
                }
                else
                {
                    Debug.LogError($"[PalmMenuVrVerifier] FAIL: {name}");
                    throw new Exception("Assertion failed: " + name);
                }
            }

            // —— 尺寸规格：必须与 P5 版式规范 §0.1 一致 ——
            Assert("板宽 240 px（世界 0.24 m）", Mathf.Approximately(PalmMenuUiFactory.PanelWidth, 240f));
            Assert("板高 340 px（世界 0.34 m）", Mathf.Approximately(PalmMenuUiFactory.PanelHeight, 340f));
            Assert("像素基准 1000 px/m（1 px = 1 mm）",
                Mathf.Approximately(PalmMenuUiFactory.PixelsPerMeter, 1000f));
            Assert("条目按钮高 44 px，不低于 poke 直触 30 mm 下限",
                PalmMenuUiFactory.ButtonHeight >= 30f);

            // —— 状态机契约：纯逻辑，可离线跑 ——
            var state = new PalmMenuState();
            Assert("新建状态默认收起", !state.IsOpen);

            state.SetOpen(true);
            bool opened = state.IsOpen;
            state.SetOpen(false);
            Assert("SetOpen 可开可合", opened && !state.IsOpen);

            var changedHits = 0;
            Action handler = () => changedHits++;
            state.Changed += handler;
            state.SelectTab(PalmMenuTab.Props);
            state.Changed -= handler;
            Assert("切换分栏会通知呈现层刷新", changedHits > 0);
            Assert("切换后 Tab 指向目标分栏", state.Tab == PalmMenuTab.Props);

            bool allTabsBuildable = true;
            foreach (PalmMenuTab tab in Enum.GetValues(typeof(PalmMenuTab)))
            {
                state.SelectTab(tab);
                try
                {
                    if (state.BuildEntries() == null) allTabsBuildable = false;
                }
                catch
                {
                    allTabsBuildable = false;
                }
            }
            Assert("三个分栏在无绑定对象时都能构建条目且不抛异常", allTabsBuildable);

            Assert("空视觉效果被判为无效（IsValid=false）",
                !default(PalmMenuVisuals).IsValid);

            if (passCount != ExpectedAssertions)
            {
                Debug.LogError($"PALM-MENU-VR-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"PALM-MENU-VR-RESULT=PASS ({passCount}/{ExpectedAssertions}) 运行时表现 pending-真机");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
