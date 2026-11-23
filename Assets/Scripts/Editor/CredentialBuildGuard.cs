using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 构建期凭据硬拦截：出包前扫描 Assets 下全部文本文件（跳过二进制与 .meta），
    /// 命中已知 key 模式或明文 secret/token 赋值即 <see cref="BuildFailedException"/>，
    /// 让带 key 的包根本构建不出来。
    ///
    /// 无论通过或拦截，每次扫描都把结果落一份 gate_logs（扫描文件数、命中数、
    /// 命中项文件:行号；绝不回显命中值），保证每次出包有可追溯证据。
    /// 只在 Editor 程序集内编译，不进 Player；模式与巡检脚本
    /// <c>04_Project/巡检/secret_scan.ps1</c> 对齐，两边口径不得分叉。
    /// </summary>
    public sealed class CredentialBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static readonly Regex[] Patterns =
        {
            new Regex(@"sk-[A-Za-z0-9_\-]{20,}"),
            new Regex(@"tsk_[A-Za-z0-9_\-]{20,}"),
            new Regex(@"ghp_[A-Za-z0-9_\-]{20,}"),
            new Regex(@"github_pat_[A-Za-z0-9_\-]{20,}"),
            new Regex(@"xoxb-[A-Za-z0-9_\-]{20,}"),
            new Regex(@"AIza[0-9A-Za-z_\-]{30,}"),
            new Regex(@"ark-[a-f0-9]{24,}"),
            new Regex(@"Bearer\s+[A-Za-z0-9_\-\.]{20,}"),
            // JWT：header.payload 两段 base64url，带点号，由格式模式兜住（下面的赋值条刻意排除点号）。
            new Regex(@"eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}"),
            // 赋值条：引号字面量直接命中；无引号时只认不含 `.()空白`、长度≥16 且含数字（或长度≥32）的紧凑 token，
            // 方法调用/标识符 RHS（如 string.IsNullOrEmpty(...)）不算凭据；关键词要求独立成词，
            // 排除 PublicKeyToken、TitleSecret 这类复合字段名。与巡检 secret_scan.ps1 口径一致。
            new Regex(@"(?i)(?<![A-Za-z])(secret|token|apikey|api_key|authorization)[ \t]*[:=][ \t]*(?:""[^""\s]{16,}""|'[^'\s]{16,}'|(?=[A-Za-z0-9_-]*\d)[A-Za-z0-9_-]{16,31}|[A-Za-z0-9_-]{32,})")
        };

        private static readonly HashSet<string> BinaryExtensions = new HashSet<string>
        {
            ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".mp4", ".mov", ".avi",
            ".wav", ".ogg", ".mp3", ".fbx", ".glb", ".gltf", ".dll", ".so",
            ".ttf", ".otf", ".zip", ".bundle", ".font", ".fontsettings"
        };

        public void OnPreprocessBuild(BuildReport report)
        {
            var hits = new List<string>();
            int scanned = 0;

            var assetsRoot = Application.dataPath;
            if (Directory.Exists(assetsRoot))
            {
                foreach (var file in Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta")) continue;
                    if (BinaryExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
                    scanned++;
                    ScanFile(file, hits);
                }
            }

            WriteGateLog(scanned, hits);

            if (hits.Count == 0) return;

            var detail = string.Join("\n", hits);
            throw new BuildFailedException(
                $"[CredentialBuildGuard] 构建输入中检出凭据模式，已阻止出包（共 {hits.Count} 处）：\n" +
                detail + "\n命中值按规则屏蔽，未回显明文。");
        }

        private static void ScanFile(string path, List<string> hits)
        {
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return; }

            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var rx in Patterns)
                {
                    if (rx.IsMatch(lines[i]))
                    {
                        hits.Add($"{path}:{i + 1}");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 先从 dataPath 向上定位含 04_* 工程目录的工作区根，再取其直属 05_* 证据目录；
        /// 避开 04_Project 下可能残留的嵌套 05_ 目录。找不到退化到 persistentDataPath。
        /// </summary>
        private static string ResolveGateLogDir()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            DirectoryInfo workspaceRoot = null;
            while (dir != null)
            {
                foreach (var child in dir.GetDirectories())
                {
                    if (child.Name.StartsWith("04_", StringComparison.Ordinal)) { workspaceRoot = dir; break; }
                }
                if (workspaceRoot != null) break;
                dir = dir.Parent;
            }

            if (workspaceRoot != null)
            {
                foreach (var sub in workspaceRoot.GetDirectories())
                {
                    if (sub.Name.StartsWith("05_", StringComparison.Ordinal))
                    {
                        return Path.Combine(sub.FullName, DateTime.Now.ToString("yyyy-MM"), "gate_logs");
                    }
                }
            }

            Debug.LogWarning("[CredentialBuildGuard] 未找到 05_* 证据目录，gate 日志退化到 persistentDataPath。");
            return Path.Combine(Application.persistentDataPath, "gate_logs");
        }

        /// <summary>扫描结果落 gate_logs；写日志失败只报错，不阻断构建判定。</summary>
        private static void WriteGateLog(int scanned, List<string> hits)
        {
            try
            {
                var gateDir = ResolveGateLogDir();
                Directory.CreateDirectory(gateDir);
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var path = Path.Combine(gateDir, $"credential_build_guard_{stamp}.log");

                var sb = new StringBuilder();
                sb.AppendLine($"time={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"scannedFiles={scanned}");
                sb.AppendLine($"hitCount={hits.Count}");
                foreach (var h in hits) sb.AppendLine($"  {h}");
                sb.AppendLine($"CREDENTIAL-BUILD-GUARD-RESULT={(hits.Count == 0 ? "PASS" : "FAIL")}");

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CredentialBuildGuard] gate log write failed: {ex.Message}");
            }
        }
    }
}
