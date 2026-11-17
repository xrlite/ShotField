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

            BuildScene();

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

            // ---------- A：安全机位（信封背面一侧，正面背对镜头）----------
            PlaceCamera(cam, new Vector3(0f, 1.20f, 2.00f), new Vector3(0f, 0.55f, 0f));
            var s01 = lib.CreateShot(false, "S01 安全机位：从信封背面拍，正面向下不可见", true);
            Line("");
            Line("[S01] 机位 z=+2.00（信封背面一侧），意图：" + s01.intent);
            var r1 = EvalFirst("信封正面不可见", s01, null);
            Assert("A. 安全机位下「信封正面不可见」= 通过", r1, CheckStatus.Pass);

            // ---------- B：危险机位（信封正面一侧，正面正对镜头）----------
            PlaceCamera(cam, new Vector3(0f, 1.20f, -2.00f), new Vector3(0f, 0.55f, 0f));
            var s02 = lib.CreateShot(true, "S02 危险机位：绕到信封正面一侧", true);
            Vector3 snapPosA = Vector3.zero, snapPosB = Vector3.zero;
            var stA = s02.snapshot.Find("actorA");
            if (stA != null) snapPosA = stA.position;
            var stB = s02.snapshot.Find("envelope");
            if (stB != null) snapPosB = stB.position;

            Line("");
            Line("[S02] 机位 z=-2.00（信封正面一侧），继承 S01 场景");
            DiagnoseEnvelope(s02);

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
            var all = Object.FindObjectsByType<ShotConstraint>(FindObjectsSortMode.None);
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

            // 日志在 EditorApplication.Exit 时可能来不及 flush，另存一份结果文件
            try
            {
                var dir = "F:/pico/05_验证记录/2026-11";
                if (System.IO.Directory.Exists(dir))
                {
                    System.IO.File.WriteAllText(dir + "/passport_smoke_result_20261117.txt", text, new System.Text.UTF8Encoding(false));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SMOKE] 结果文件写入失败：" + e.Message);
            }

            EditorApplication.Exit(fail == 0 ? 0 : 1);
        }

        private static ConstraintResult EvalFirst(string ruleName, ShotData shot, ShotData prev)
        {
            var all = Object.FindObjectsByType<ShotConstraint>(FindObjectsSortMode.None);
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

        private static void BuildScene()
        {
            var existing = Object.FindObjectsByType<SemanticMarker>(FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                Line("场景里已有标记对象，直接复用");
                return;
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f);

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.localScale = new Vector3(1.6f, 0.05f, 0.8f);
            table.transform.position = new Vector3(0f, 0.55f, 0f);

            var a = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            a.name = "ActorA";
            a.transform.localScale = new Vector3(0.32f, 0.45f, 0.32f);
            a.transform.position = new Vector3(-1.1f, 0.9f, 0.6f);
            var mA = a.AddComponent<SemanticMarker>();
            mA.kind = SemanticMarker.Kind.ActorPrimary; mA.markerId = "actorA"; mA.displayName = "沈默";

            var b = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            b.name = "ActorB";
            b.transform.localScale = new Vector3(0.32f, 0.45f, 0.32f);
            b.transform.position = new Vector3(1.1f, 0.9f, 0.6f);
            var mB = b.AddComponent<SemanticMarker>();
            mB.kind = SemanticMarker.Kind.ActorSecondary; mB.markerId = "actorB"; mB.displayName = "苏明";

            var env = GameObject.CreatePrimitive(PrimitiveType.Cube);
            env.name = "Envelope";
            env.transform.localScale = new Vector3(0.16f, 0.011f, 0.11f);
            env.transform.position = new Vector3(0f, 0.583f, 0f);
            env.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 局部 +Z → 世界 -Z
            var mE = env.AddComponent<SemanticMarker>();
            mE.kind = SemanticMarker.Kind.KeyProp; mE.markerId = "envelope"; mE.displayName = "信封";
            mE.criticalFaceNormal = Vector3.forward;
            mE.criticalFaceMustStayHidden = true;
            mE.criticalFaceSize = new Vector2(0.16f, 0.11f);

            var host = new GameObject("Constraints");
            var vis = host.AddComponent<VisibilityConstraint>(); vis.ruleName = "信封正面不可见"; vis.targetMarkerId = "";
            var ax = host.AddComponent<AxisConstraint>(); ax.ruleName = "摄影机不得越轴"; ax.actorAId = "actorA"; ax.actorBId = "actorB";
            var fr = host.AddComponent<FramingConstraint>(); fr.ruleName = "主角A在画面左侧"; fr.subjectId = "actorA"; fr.minViewportX = 0f; fr.maxViewportX = 0.5f;

            // 运行时创建的 Collider 必须同步一次，否则 Raycast 打不到
            Physics.SyncTransforms();

            Line("已构建测试场景：地板 / 桌子 / 沈默 / 苏明 / 信封（正面朝世界 -Z）");
        }

        private static void PlaceCamera(Camera cam, Vector3 pos, Vector3 lookAt)
        {
            cam.transform.position = pos;
            // 注意：Transform.LookAt 让物体的 +Z 指向目标，但 Unity 相机渲染用的是 -Z。
            // 直接用 LookAt 会让相机实际看向目标的反方向。必须让 +Z 背离目标。
            var back = (pos - lookAt).normalized;
            cam.transform.rotation = Quaternion.LookRotation(back, Vector3.up);
            Physics.SyncTransforms();
            Line(string.Format("  [CAM] pos={0} lookAt={1} back={2} euler={3} forward={4}",
                pos.ToString("F2"), lookAt.ToString("F2"), back.ToString("F3"),
                cam.transform.rotation.eulerAngles.ToString("F1"),
                cam.transform.forward.ToString("F3")));
        }

        /// <summary>把「信封正面不可见」这条规则的中间量全部打印出来，定位卡在哪一步。</summary>
        private static void DiagnoseEnvelope(ShotData shot)
        {
            var marker = SemanticMarker.Find("envelope");
            if (marker == null) { Line("  诊断：找不到信封 marker"); return; }

            var cam = shot.camera;
            Line(string.Format("  诊断 cam.pos={0} euler={1} fov={2}",
                cam.position.ToString("F2"), cam.rotation.eulerAngles.ToString("F1"), cam.fieldOfView));
            Line(string.Format("  诊断 faceCenter={0} normalWorld={1}",
                marker.CriticalFaceCenter.ToString("F2"), marker.CriticalFaceNormalWorld.ToString("F2")));

            var toCam = cam.position - marker.CriticalFaceCenter;
            float dot = Vector3.Dot(marker.CriticalFaceNormalWorld, toCam.normalized);
            Line(string.Format("  诊断 朝向dot={0:F3}（>0 才算正面朝镜头）", dot));

            float aspect = 16f / 9f;
            var samples = marker.SampleCriticalFace();
            for (int i = 0; i < samples.Length; i++)
            {
                var local = cam.ViewMatrix.MultiplyPoint3x4(samples[i]);
                var proj = cam.ProjectionMatrix(aspect, true);
                var clip = proj * new Vector4(local.x, local.y, local.z, 1f);
                float ndcX = clip.w > 0f ? clip.x / clip.w : -999f;
                float ndcY = clip.w > 0f ? clip.y / clip.w : -999f;
                bool inside = clip.w > 0f && ndcX >= -1f && ndcX <= 1f && ndcY >= -1f && ndcY <= 1f;

                bool occluded = false;
                string occluder = "";
                var dir = samples[i] - cam.position;
                float dist = dir.magnitude;
                var hits = Physics.RaycastAll(cam.position, dir.normalized, Mathf.Max(0.01f, dist - 0.02f), ~0, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hits.Length; h++)
                {
                    var t = hits[h].collider != null ? hits[h].collider.transform : null;
                    if (t == null) continue;
                    if (t == marker.transform || t.IsChildOf(marker.transform)) { occluder = "(自身已忽略)"; continue; }
                    occluded = true;
                    occluder = t.name;
                    break;
                }

                Line(string.Format("    点{0} world={1} localZ={2:F2} w={3:F2} ndc=({4:F2},{5:F2}) 视锥内={6} 遮挡={7} {8}",
                    i, samples[i].ToString("F2"), local.z, clip.w, ndcX, ndcY, inside, occluded, occluder));
            }
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
