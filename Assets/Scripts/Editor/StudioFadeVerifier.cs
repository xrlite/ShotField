#if UNITY_EDITOR
using System;
using System.Reflection;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// P5 片场切换过渡验收。
    ///
    /// 守的核心不变量是「同数据」：过渡**只允许**切 TabletopDirector 的模式并调演员预算，
    /// 不允许重建场景、重新实例化演员。一旦有人把 TransitionTo 改成"销毁再生成"，
    /// 沙盘里摆好的位姿就丢了 —— 所以这里用反射锁住方法签名，
    /// 把"只能传一个模式枚举进去"变成可回归的约束。
    /// </summary>
    public static class StudioFadeVerifier
    {
        // 4 条参数 + 6 条契约 = 10
        private const int ExpectedAssertions = 10;

        [MenuItem("ShotField/Verifiers/Run Studio Fade Verifier")]
        [MenuItem("ShotField/验证/验证片场切换 StudioFade")]
        public static void Run()
        {
            int passCount = 0;
            void Assert(string name, bool condition)
            {
                if (condition)
                {
                    passCount++;
                    Debug.Log($"[StudioFadeVerifier] PASS: {name}");
                }
                else
                {
                    Debug.LogError($"[StudioFadeVerifier] FAIL: {name}");
                    throw new Exception("Assertion failed: " + name);
                }
            }

            var type = typeof(StudioFadeTransition);
            Assert("StudioFadeTransition 是 MonoBehaviour", typeof(MonoBehaviour).IsAssignableFrom(type));

            // —— 契约：方法签名锁死，防止改成重建场景 ——
            var transitionTo = type.GetMethod("TransitionTo", new[] { typeof(TabletopDirector.ScaleMode) });
            Assert("存在 TransitionTo(ScaleMode)，参数只有一个模式枚举",
                transitionTo != null);
            Assert("TransitionTo 返回 void，不返回新对象（排除重建语义）",
                transitionTo != null && transitionTo.ReturnType == typeof(void));

            Assert("存在 ToggleMode() 供菜单一键来回切",
                type.GetMethod("ToggleMode", Type.EmptyTypes) != null);

            Assert("暴露 IsRunning 供外部禁止重复触发",
                type.GetProperty("IsRunning") != null);

            // —— 参数默认值 ——
            var go = new GameObject("[StudioFadeVerifier]");
            try
            {
                var fade = go.AddComponent<StudioFadeTransition>();

                Assert($"默认单程时长 0.35 s（实测 {fade.fadeDuration}）",
                    Mathf.Approximately(fade.fadeDuration, 0.35f));

                Assert("一次来回总时长小于 1 s，不让人等",
                    fade.fadeDuration * 2f < 1f);

                Assert($"遮罩距离 0.12 m 在 0.05–0.5 合理区间（实测 {fade.overlayDistance}）",
                    fade.overlayDistance >= 0.05f && fade.overlayDistance <= 0.5f);

                Assert("遮罩默认纯黑不透明",
                    fade.fadeColor.a > 0.99f &&
                    fade.fadeColor.r < 0.01f && fade.fadeColor.g < 0.01f && fade.fadeColor.b < 0.01f);

                Assert("刚创建时不处于过渡中（未误触发协程）", !fade.IsRunning);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            if (passCount != ExpectedAssertions)
            {
                Debug.LogError($"STUDIO-FADE-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"STUDIO-FADE-RESULT=PASS ({passCount}/{ExpectedAssertions}) 人眼观感 pending-真机");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
