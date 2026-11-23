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
    /// 构建期凭据硬拦截：出包前扫描 Assets/Resources/** 与 Assets/StreamingAssets/**，
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

        // gate_logs 在仓库外（F:\pico 内），按口径用绝对路径写，UTF8 无 BOM。
        private const string GateLogDir = @"F:\pico\05_验证记录\2026-11\gate_logs";

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
            new Regex(@"(?i)(secret|token|apikey|api_key|authorization)\s*[:=]\s*[""']?[A-Za-z0-9_\-\.]{16,}")
        };

        private static readonly HashSet<string> BinaryExtensions = new HashSet<string>
        {
            ".png", ".jpg", ".jpeg", ".mp4", ".wav", ".ogg", ".mp3", ".fbx",
            ".glb", ".gltf", ".dll", ".so", ".ttf", ".otf", ".zip", ".bundle"
        };

        public void OnPreprocessBuild(BuildReport report)
        {
            var roots = new[]
            {
                Path.Combine(Application.dataPath, "Resources"),
                Path.Combine(Application.dataPath, "StreamingAssets")
            };

            var hits = new List<string>();
            int scanned = 0;
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
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

        /// <summary>扫描结果落 gate_logs；写日志失败只报错，不阻断构建判定。</summary>
        private static void WriteGateLog(int scanned, List<string> hits)
        {
            try
            {
                Directory.CreateDirectory(GateLogDir);
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var path = Path.Combine(GateLogDir, $"credential_build_guard_{stamp}.log");

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
