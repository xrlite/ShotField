// 色板一致性验证器：拿磁盘上的 .mat / .prefab 文本与 PrevisPalette 对账。
// 刻意不读内存里的 Material 对象，也不复用构建脚本的常量——这样它证明的是资产文件，
// 而不是「代码自己写的值」。门禁 verify_changed.ps1 依赖本条日志行。
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using ShotField.Core;

namespace ShotField.EditorTools
{
    public static class PrevisPaletteVerifier
    {
        private const string LogTag = "PALETTE-VERIFIER";
        private static readonly Regex BaseColorRx = new Regex(@"_BaseColor:\s*\{r:\s*([0-9.eE+-]+),\s*g:\s*([0-9.eE+-]+),\s*b:\s*([0-9.eE+-]+)", RegexOptions.Compiled);
        private static readonly Regex GuidRx = new Regex(@"guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);

        [MenuItem("ShotField/Verifiers/Run Previs Palette Verifier")]
        public static void Run()
        {
            var problems = new List<string>();
            var notes = new StringBuilder();

            var guidToKey = new Dictionary<string, string>();
            for (var i = 0; i < PrevisPalette.Keys.Count; i++)
            {
                var key = PrevisPalette.KeyAt(i);
                var matPath = PrevisPalette.MaterialPathAt(i);
                var metaPath = matPath + ".meta";
                if (!File.Exists(metaPath))
                {
                    problems.Add("缺 meta：" + metaPath);
                    continue;
                }
                var m = GuidRx.Match(File.ReadAllText(metaPath));
                if (!m.Success)
                {
                    problems.Add(matPath + " 的 meta 里没有 guid");
                    continue;
                }
                guidToKey[m.Groups[1].Value] = key;

                if (!File.Exists(matPath))
                {
                    problems.Add("缺材质：" + matPath);
                    continue;
                }
                var text = File.ReadAllText(matPath);
                var c = BaseColorRx.Match(text);
                if (!c.Success)
                {
                    problems.Add(matPath + " 里找不到 _BaseColor（可能不是 URP/Lit）");
                    continue;
                }
                var want = PrevisPalette.ColorOf(key);
                var got = new Vector3(float.Parse(c.Groups[1].Value), float.Parse(c.Groups[2].Value), float.Parse(c.Groups[3].Value));
                var drift = Mathf.Max(Mathf.Abs(want.r - got.x), Mathf.Max(Mathf.Abs(want.g - got.y), Mathf.Abs(want.b - got.z)));
                if (drift > 0.02f)
                {
                    problems.Add(key + " 材质与色板不符：色板 " + PrevisPalette.HexOf(key) + " 实测 rgb(" + got.x.ToString("F3") + "," + got.y.ToString("F3") + "," + got.z.ToString("F3") + ")");
                }
            }

            for (var i = 0; i < PrevisPalette.Keys.Count; i++)
            {
                var key = PrevisPalette.KeyAt(i);
                var prefabPath = PrevisPalette.PrefabPathAt(i);
                if (!File.Exists(prefabPath))
                {
                    problems.Add("缺 prefab：" + prefabPath);
                    continue;
                }
                var used = new HashSet<string>();
                foreach (Match g in GuidRx.Matches(File.ReadAllText(prefabPath)))
                {
                    if (guidToKey.TryGetValue(g.Groups[1].Value, out var ownerKey))
                    {
                        used.Add(ownerKey);
                    }
                }
                if (used.Count == 0)
                {
                    problems.Add(key + " prefab 没有引用任何色板材质");
                }
                else if (used.Count > 1 || !used.Contains(key))
                {
                    problems.Add(key + " prefab 串色：引用了 " + string.Join("/", used));
                }
            }

            var separation = PrevisPalette.NarrowestHueSeparation();
            if (separation < PrevisPalette.MinHueSeparationDegrees)
            {
                problems.Add("色相间距只有 " + separation.ToString("F1") + "°，低于 " + PrevisPalette.MinHueSeparationDegrees + "°");
            }
            var rgbGap = PrevisPalette.NarrowestRgbDistance();
            if (rgbGap < PrevisPalette.MinRgbDistance)
            {
                problems.Add("RGB 距离只有 " + rgbGap.ToString("F3") + "，低于 " + PrevisPalette.MinRgbDistance);
            }
            notes.Append(" hueMin=" + separation.ToString("F1") + "° rgbMin=" + rgbGap.ToString("F3") + " mats=" + guidToKey.Count + "/6");

            if (problems.Count == 0)
            {
                Debug.Log(LogTag + " PASS 六色材质与 prefab 均与 PrevisPalette 一致。" + notes);
            }
            else
            {
                Debug.LogError(LogTag + " FAIL " + problems.Count + " 项：" + string.Join(" | ", problems) + notes);
            }
        }
    }
}
