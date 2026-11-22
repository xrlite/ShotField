#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShotField.Import;
using UnityEditor;
using UnityEngine;

namespace ShotField.Import.EditorTools
{
    /// <summary>
    /// P2 四格式剧本导入验证器：跑 txt / md / docx / pdf 四份同内容样本，
    /// 比对标题 / 人物数 / 对白数 / 场景数 / 摘要；并覆盖 .doc 拒绝与无文本层 PDF 归 OCR 两条负路径。
    /// 日志落 05_验证记录/2026-11/gate_logs/script_import_<时间戳>.log。
    /// </summary>
    public static class ScriptImportVerifier
    {
        private const string Tag = "SCRIPT-IMPORT-RESULT";

        private static readonly string[] Formats = { "txt", "md", "docx", "pdf" };

        [MenuItem("ShotField/Verifiers/Run Script Import Verifier")]
        public static void Run()
        {
            var router = new ScriptImporterRouter();
            var results = new Dictionary<string, ScriptImportResult>();
            var log = new StringBuilder();
            int passed = 0;

            log.AppendLine($"ShotField script import verify {DateTimeOffset.Now:yyyyMMdd_HHmmss}");
            log.AppendLine($"project={Application.dataPath}");
            log.AppendLine();

            foreach (var format in Formats)
            {
                string path = ResolveSamplePath(format);
                ScriptImportResult result;
                try
                {
                    result = router.Import(path);
                }
                catch (Exception ex)
                {
                    result = ScriptImportResult.Fail(ScriptImportStatus.Failed, $"未捕获异常：{ex.Message}", path);
                }

                results[format] = result;
                bool ok = result.Status == ScriptImportStatus.PendingConfirmation && MatchesGolden(result);
                if (ok) passed++;

                log.AppendLine($"[{format}] {Path.GetFileName(path)}");
                log.AppendLine($"  status={result.Status} success={result.Success} reason={result.FailureReason}");
                log.AppendLine($"  title={result.Title}");
                log.AppendLine($"  characters=({result.Characters.Count}) {string.Join("/", result.Characters)}");
                log.AppendLine($"  dialogue={result.DialogueCount} scenes={result.SceneCount}");
                log.AppendLine($"  summary={result.Summary}");
                log.AppendLine($"  matchGolden={ok}");
                log.AppendLine();
            }

            bool crossConsistent = CheckCrossConsistent(results, log);
            bool docRejected = CheckDocRejection(router, log);
            bool emptyPdfNeedsOcr = CheckEmptyPdfNeedsOcr(log);
            bool gbRoundTrip = CheckGb18030RoundTrip(log);

            int total = Formats.Length;
            bool allPass = passed == total && crossConsistent && docRejected && emptyPdfNeedsOcr && gbRoundTrip;
            string verdict = allPass ? "PASS" : "FAIL";

            log.AppendLine("=== 四格式比对表 ===");
            log.AppendLine("format | title | chars | dialogue | scenes | summary");
            foreach (var format in Formats)
            {
                var r = results[format];
                log.AppendLine($"{format} | {r.Title} | {r.Characters.Count} | {r.DialogueCount} | {r.SceneCount} | {r.Summary}");
            }
            log.AppendLine();
            log.AppendLine($"crossConsistent={crossConsistent} docRejected={docRejected} emptyPdfNeedsOcr={emptyPdfNeedsOcr} gbRoundTrip={gbRoundTrip}");
            log.AppendLine($"{Tag}={verdict} ({passed}/{total})");

            WriteLog(log.ToString());
            Debug.Log($"{Tag}={verdict} ({passed}/{total})");
            if (Application.isBatchMode) EditorApplication.Exit(allPass ? 0 : 1);
        }

        private static string ResolveSamplePath(string format)
        {
            if (format == "txt" || format == "md")
            {
                return Path.Combine(Application.dataPath, "Scripts", "Import", "Samples", $"sample.{format}");
            }
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../06_素材/ImportSamples"));
            return Path.Combine(dir, $"sample.{format}");
        }

        private static bool MatchesGolden(ScriptImportResult r)
        {
            return r.Title == "夜归人"
                && r.Characters.Count == 2
                && r.Characters.Contains("林岚")
                && r.Characters.Contains("陈屿")
                && r.DialogueCount == 5
                && r.SceneCount == 2
                && r.Summary == "雨夜末班车上，两人因一把旧伞相认。";
        }

        private static bool CheckCrossConsistent(Dictionary<string, ScriptImportResult> results, StringBuilder log)
        {
            string title = null; int chars = -1, dialogue = -1, scenes = -1; string summary = null;
            bool consistent = true;
            foreach (var r in results.Values)
            {
                if (title == null) { title = r.Title; chars = r.Characters.Count; dialogue = r.DialogueCount; scenes = r.SceneCount; summary = r.Summary; }
                else if (title != r.Title || chars != r.Characters.Count || dialogue != r.DialogueCount ||
                         scenes != r.SceneCount || summary != r.Summary)
                {
                    consistent = false;
                }
            }
            log.AppendLine($"[cross] title={title} chars={chars} dialogue={dialogue} scenes={scenes} summary={summary} consistent={consistent}");
            return consistent;
        }

        private static bool CheckDocRejection(ScriptImporterRouter router, StringBuilder log)
        {
            string tmp = Path.Combine(Application.temporaryCachePath, "script_import_reject.doc");
            File.WriteAllText(tmp, "old binary doc body");
            var result = router.Import(tmp);
            bool ok = result.Status == ScriptImportStatus.UnsupportedFormat &&
                      result.FailureReason.Contains(".docx");
            log.AppendLine($"[negative .doc] status={result.Status} reason={result.FailureReason} rejected={ok}");
            return ok;
        }

        private static bool CheckEmptyPdfNeedsOcr(StringBuilder log)
        {
            string tmp = Path.Combine(Application.temporaryCachePath, "script_import_empty.pdf");
            File.WriteAllBytes(tmp, Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n"));
            var result = new PdfImporter().Import(tmp);
            bool ok = result.Status == ScriptImportStatus.NeedsOcr;
            log.AppendLine($"[negative empty-pdf] status={result.Status} reason={result.FailureReason} needsOcr={ok}");
            return ok;
        }

        private static bool CheckGb18030RoundTrip(StringBuilder log)
        {
            const string source = "雨夜末班车上，两人因一把旧伞相认。";
            try
            {
                byte[] bytes = Encoding.GetEncoding(936).GetBytes(source);
                var (text, encoding) = PlainTextImporter.Decode(bytes);
                bool ok = text == source && encoding == "GB18030";
                log.AppendLine($"[gb18030] encoding={encoding} roundTrip={ok}");
                return ok;
            }
            catch (Exception ex)
            {
                log.AppendLine($"[gb18030] unavailable: {ex.Message}");
                return false;
            }
        }

        private static void WriteLog(string content)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../05_验证记录/2026-11/gate_logs"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"script_import_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.log");
            File.WriteAllText(path, content, Encoding.UTF8);
            Debug.Log($"[ScriptImportVerifier] log={path}");
        }

        /// <summary>
        /// batchmode 自动触发器：环境变量 SHOTFIELD_SCRIPT_IMPORT=1 时，域重载后延迟执行，
        /// 绕开 -executeMethod 在脚本变更编译后被 -quit 抢先退出的时序问题。
        /// </summary>
        [InitializeOnLoad]
        internal static class ImportAutoRunner
        {
            static ImportAutoRunner()
            {
                if (!Application.isBatchMode) return;
                if (Environment.GetEnvironmentVariable("SHOTFIELD_SCRIPT_IMPORT") != "1") return;
                EditorApplication.delayCall += Run;
            }
        }
    }
}
#endif
