#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// Console 归零校验：统计当前 Console 内 Error / Warning 条目数，
    /// 输出 CONSOLE-CLEAN-RESULT=PASS Errors=0 Warnings=0（有残留则 FAIL 并列出条目）。
    /// 不做白名单豁免；遗留红/黄必须要么修掉，要么在报告里列处置方案。
    /// 协议时序：清 Console → Domain Reload → 进 Play → 操作提案/摆位/时间轴 → 跑满 5 分钟 → 本校验器读数。
    /// 注：UnityEditor.LogEntries 在本版本为 internal，走反射取公开静态方法。
    /// </summary>
    public static class ConsoleCleanVerifier
    {
        public const string Tag = "CONSOLE-CLEAN-RESULT";

        [MenuItem("ShotField/Verifiers/Run Console Clean Verifier")]
        public static void Run()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
            if (type == null)
            {
                Debug.LogError("[ConsoleClean] 找不到 UnityEditor.LogEntries 类型");
                Debug.Log($"{Tag}=FAIL Errors=-1 Warnings=-1");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var countsArgs = new object[] { 0, 0, 0 };
            type.GetMethod("GetCountsByType", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, countsArgs);
            int errors = (int)countsArgs[0];
            int warnings = (int)countsArgs[1];
            int logs = (int)countsArgs[2];

            int total = (int)type.GetMethod("GetCount", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            var residual = new List<string>();
            var lineMethod = type.GetMethod("GetLinesAndModeFromEntryInternal",
                BindingFlags.Public | BindingFlags.Static);
            for (int row = 0; row < total && residual.Count < 30; row++)
            {
                var rowArgs = new object[] { row, 1, 0, "" };
                lineMethod.Invoke(null, rowArgs);
                var body = ((string)rowArgs[3] ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                if (!string.IsNullOrEmpty(body)) residual.Add(Truncate(body));
            }

            var report = new StringBuilder();
            report.AppendLine($"[ConsoleClean] errors={errors} warnings={warnings} logs={logs} entries={total}");
            foreach (var row in residual) report.AppendLine("  RESIDUAL " + row);
            Debug.Log(report.ToString());

            bool pass = errors == 0 && warnings == 0;
            string verdict = pass ? "PASS" : "FAIL";
            Debug.Log($"{Tag}={verdict} Errors={errors} Warnings={warnings}");
            if (Application.isBatchMode) EditorApplication.Exit(pass ? 0 : 1);
        }

        private static string Truncate(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length > 160 ? s.Substring(0, 160) : s;
        }
    }
}
#endif
