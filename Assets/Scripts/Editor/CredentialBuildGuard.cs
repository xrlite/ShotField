using System.Collections.Generic;
using System.IO;
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
    /// 只在 Editor 程序集内编译，不进 Player；命中日志只报文件与行号，绝不回显命中值。
    /// 模式与巡检脚本 <c>04_Project/巡检/secret_scan.ps1</c> 对齐，两边口径不得分叉。
    /// </summary>
    public sealed class CredentialBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static readonly Regex[] Patterns =
        {
            new Regex(@"sk-[A-Za-z0-9]{24,}"),
            new Regex(@"sk-proj-[A-Za-z0-9_\-]{20,}"),
            new Regex(@"tsk_[A-Za-z0-9_\-]{20,}"),
            new Regex(@"AIza[0-9A-Za-z_\-]{30,}"),
            new Regex(@"ark-[a-f0-9]{24,}"),
            new Regex(@"Bearer\s+[A-Za-z0-9_\-\.]{32,}"),
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
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta")) continue;
                    if (BinaryExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
                    ScanFile(file, hits);
                }
            }

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
    }
}
