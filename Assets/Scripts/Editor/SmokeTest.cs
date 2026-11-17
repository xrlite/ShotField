#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 端到端冒烟测试（batchmode 用）。
    /// 不是"类型存在"级别的检查，而是真的建场景、建镜头、跑三条约束、比对预期。
    ///
    /// 断言：
    ///   A. 安全机位（在信封背面一侧）→ 「信封正面不可见」应判 通过
    ///   B. 危险机位（在信封正面一侧）→ 「信封正面不可见」应判 违规
    ///   C. 继承镜头 → S02 的场景快照应与 S01 相同，但快照对象互不引用
    /// </summary>
    public static class SmokeTest
    {
        private static StringBuilder sb = new StringBuilder();
        private static int pass = 0;
        private static int fail = 0;

        public static void Run()
        {
            sb = new StringBuilder();
            pass = 0;
            fail = 0;

            Line("=== ShotField 端到端冒烟测试 ===");

            SmokeScene.Build(Line);

            // ---------- 准备 ----------
            var libGo = new GameObject("ShotLibrary");
            var lib = libGo.AddComponent<ShotLibrary>();
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("SmokeCam");
                cam = camGo.AddComponent<Camera>();
            }
            lib.ShotCamera = cam;
            cam.aspect = ShotCameraState.DefaultAspect;

            // ---------- A：安全机位（信封背面一侧，正面背对镜头）----------
            SmokeScene.PlaceCamera(Line, cam, new Vector3(0f, 1.20f, 2.00f), new Vector3(0f, 0.55f, 0f));
            var s01 = lib.CreateShot(false, "S01 安全机位：从信封背面拍，正面向下不可见", true);
            Line("");
            Line("[S01] 机位 z=+2.00（信封背面一侧），意图：" + s01.intent);
            var r1 = EvalFirst("信封正面不可见", s01, null);
            Assert("A. 安全机位下「信封正面不可见」= 通过", r1, CheckStatus.Pass);

            // ---------- B：危险机位（信封正面一侧，正面正对镜头）----------
            SmokeScene.PlaceCamera(Line, cam, new Vector3(0f, 1.20f, -2.00f), new Vector3(0f, 0.55f, 0f));
            cam.aspect = ShotCameraState.PortraitAspect;
            var s02 = lib.CreateShot(true, "S02 危险机位：绕到信封正面一侧", true);
            Vector3 snapPosA = Vector3.zero, snapPosB = Vector3.zero;
            var stA = s02.snapshot.Find("actorA");
            if (stA != null) snapPosA = stA.position;
            var stB = s02.snapshot.Find("envelope");
            if (stB != null) snapPosB = stB.position;

            Line("");
            Line("[S02] 机位 z=-2.00（信封正面一侧），继承 S01 场景");
            SmokeScene.DiagnoseEnvelope(s02, Line);

            // 断言 C：继承后的快照与 S01 相同，但对象不共享
            var s01StA = s01.snapshot.Find("actorA");
            bool inheritedOk = (s01StA != null) && (snapPosA == s01StA.position);
            bool independent = (s01.snapshot != s02.snapshot) && (s01StA != s02.snapshot.Find("actorA"));
            if (inheritedOk && independent) Pass("C1. S02 继承了 S01 的人物位置");
            else Fail("C1. 继承失败或值不一致");

            // 篡改 S02 快照的"副本"，验证不污染 S01（不直接改 S02，避免影响后面的越轴检查）
            var probe = s02.snapshot.Clone();
            var probeA = probe.Find("actorA");
            if (probeA != null) probeA.position = new Vector3(999f, 999f, 999f);
            var s01After = s01.snapshot.Find("actorA");
            if (s01After != null && s01After.position.x < 900f) Pass("C2. 改副本没有污染 S01（深拷贝成立）");
            else Fail("C2. S01 被污染了 —— 深拷贝失效");

            var r2 = EvalFirst("信封正面不可见", s02, s01);
            Assert("B. 危险机位下「信封正面不可见」= 违规", r2, CheckStatus.Violation);

            // ---------- D：越轴 ----------
            var r3 = EvalFirst("摄影机不得越轴", s02, s01);
            if (r3 != null)
            {
                Line("");
                Line("[D] 越轴检查：" + r3.status + " — " + r3.detail);
                if (r3.status != CheckStatus.Pending) Pass("D. 越轴规则有明确判定（非待确认）");
                else Fail("D. 越轴规则未能判定");
            }

            // ---------- E：所有规则都跑得到 ----------
            var all = Object.FindObjectsByType<ShotConstraint>(FindObjectsInactive.Exclude);
            if (all.Length >= 3) Pass("E. 场景中挂载了 " + all.Length + " 条约束规则");
            else Fail("E. 约束规则数量不足：" + all.Length);

            try
            {
                PassportSmokeTest.Verify(lib, s01, s02, r1, r2, all);
                Line("[PASSPORT] PASS: saved and reloaded both shots, verified history and applied camera.");
            }
            catch (System.Exception e)
            {
                Fail("护照写盘与读回失败：" + e);
            }

            // ---------- 汇总 ----------
            Line("");
            Line("=== 通过 " + pass + " 项，失败 " + fail + " 项 ===");
            Line(fail == 0 ? "SMOKE-RESULT=PASS" : "SMOKE-RESULT=FAIL");

            var text = sb.ToString();
            Debug.Log("[SMOKE]\n" + text);

            try
            {
                var now = System.DateTimeOffset.UtcNow.ToOffset(System.TimeSpan.FromHours(8));
                var dir = System.IO.Path.Combine(Application.dataPath, "../../../05_验证记录", now.ToString("yyyy-MM"));
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, $"passport_smoke_result_{now:yyyyMMdd_HHmmss_fff}.txt");
                using var output = new System.IO.FileStream(path, System.IO.FileMode.CreateNew);
                using var writer = new System.IO.StreamWriter(output, new UTF8Encoding(false));
                writer.Write(text);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SMOKE] 结果文件写入失败：" + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(fail == 0 ? 0 : 1);
            }
        }

        private static ConstraintResult EvalFirst(string ruleName, ShotData shot, ShotData prev)
        {
            var all = Object.FindObjectsByType<ShotConstraint>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].ruleName == ruleName)
                {
                    return all[i].Evaluate(shot, prev);
                }
            }
            Line("!! 找不到规则 " + ruleName);
            return null;
        }

        private static void Assert(string label, ConstraintResult r, CheckStatus expect)
        {
            if (r == null) { Fail(label + "（结果为 null）"); return; }
            string got = r.status + " / " + r.detail;
            if (r.status == expect) Pass(label + "　→ " + got);
            else Fail(label + "　→ 期望 " + expect + "，实际 " + got);
        }

        private static void Pass(string s) { pass++; sb.AppendLine("  [PASS] " + s); }
        private static void Fail(string s) { fail++; sb.AppendLine("  [FAIL] " + s); }
        private static void Line(string s) { sb.AppendLine(s); }
    }

    /// <summary>
    /// batchmode 下的自动触发器。
    /// 原因：用 -executeMethod 时，只要本次有脚本变更需要编译，-quit 会在编译后抢先退出，
    /// 导致方法根本执行不到。改成域重载后自己触发，绕开这个时序问题。
    /// 触发条件：环境变量 SHOTFIELD_SMOKE=1
    /// </summary>
    [InitializeOnLoad]
    internal static class SmokeAutoRunner
    {
        static SmokeAutoRunner()
        {
            if (!Application.isBatchMode) return;
            if (System.Environment.GetEnvironmentVariable("SHOTFIELD_SMOKE") != "1") return;
            EditorApplication.delayCall += SmokeTest.Run;
        }
    }
}
#endif
