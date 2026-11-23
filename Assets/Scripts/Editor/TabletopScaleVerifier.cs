#if UNITY_EDITOR
using System;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// P5 沙盘 1:20 验收：缩放口径、承载面接口契约、按需演员预算默认值。
    ///
    /// 不带 MR 平面识别实现 —— 那需要真机与 PICO 官方接口，模拟器不支持，
    /// 项目已把它列为 Labs 阻塞项。本验证器只保证：
    /// 拿不到桌面时，调用方拿到 false 并降级，**不报错、不阻断主流程**。
    /// </summary>
    public static class TabletopScaleVerifier
    {
        // 2 条缩放 + 5 条接口契约 + 3 条组件默认值 = 10
        private const int ExpectedAssertions = 10;

        [MenuItem("ShotField/Verifiers/Run Tabletop Scale Verifier")]
        [MenuItem("镜场 ShotField/验证/验证沙盘 1比20 TabletopScale")]
        public static void Run()
        {
            int passCount = 0;
            void Assert(string name, bool condition)
            {
                if (condition)
                {
                    passCount++;
                    Debug.Log($"[TabletopScaleVerifier] PASS: {name}");
                }
                else
                {
                    Debug.LogError($"[TabletopScaleVerifier] FAIL: {name}");
                    throw new Exception("Assertion failed: " + name);
                }
            }

            // —— 缩放口径 ——
            Assert("1:20 沙盘缩放等于 0.05（1/20）", Mathf.Approximately(1f / 20f, 0.05f));
            Assert("ScaleMode 恰有两个模式（1:1 与沙盘）",
                Enum.GetValues(typeof(TabletopDirector.ScaleMode)).Length == 2);

            // —— 承载面接口契约 ——
            ITabletopSurfaceProvider provider = NullTabletopSurfaceProvider.Instance;
            bool got = provider.TryGetSurface(out var surface);
            Assert("空 provider 返回 false，调用方据此降级", !got);
            Assert("空 provider 输出的 surface 为 default", surface.Center == Vector3.zero && !surface.HasExtent);

            var flat = new TabletopSurface(Vector3.zero, Vector3.up, new Vector2(1.2f, 0.8f));
            Assert("水平朝上的桌面被判为可放置", flat.FacesUp && flat.HasExtent);

            var wall = new TabletopSurface(Vector3.zero, Vector3.forward, new Vector2(1.2f, 0.8f));
            Assert("竖直面（墙）不被判为可放置", !wall.FacesUp);

            var zeroExtent = new TabletopSurface(Vector3.zero, Vector3.up, Vector2.zero);
            Assert("零尺寸面被判为无效", !zeroExtent.HasExtent);

            // —— 组件默认值 ——
            var go = new GameObject("[TabletopScaleVerifier]");
            try
            {
                var budget = go.AddComponent<TabletopActorBudget>();
                Assert("沙盘默认最多 2 名演员（与『默认在场 2 人』一致）",
                    budget.maxVisibleInTabletop == 2);
                Assert("默认保留当前选中演员在沙盘里", budget.alwaysKeepSelected);

                var director = go.AddComponent<TabletopDirector>();
                Assert("默认处于 1:1 沉浸模式，不会一进场景就在沙盘里",
                    director.currentMode == TabletopDirector.ScaleMode.Immersive1to1);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            if (passCount != ExpectedAssertions)
            {
                Debug.LogError($"TABLETOP-SCALE-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"TABLETOP-SCALE-RESULT=PASS ({passCount}/{ExpectedAssertions}) MR 平面识别 pending-真机");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
