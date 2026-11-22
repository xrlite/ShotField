using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    public partial class AiScriptParser
    {
        /// <summary>显式离线文本结构化：仅提取分句与景别建议，不产生在线产物。</summary>
        public ScriptAnalysisResult ParseOfflineStructured(string rawScript)
        {
            if (string.IsNullOrWhiteSpace(rawScript)) return new ScriptAnalysisResult();

            var lines = rawScript.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string title = lines.Length > 0 ? lines[0].Trim() : "SC01_客厅对峙";
            var res = new ScriptAnalysisResult
            {
                sceneTitle = title,
                conflictSummary = lines.Length > 1 ? lines[1].Trim() : title
            };
            res.characters.Add("ActorA");

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (i == 0 && (line.StartsWith("内景") || line.StartsWith("外景"))) continue;
                var clauses = line.Split(new[] { '，', ',', '；', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var subBeats = clauses.Length > 1 ? clauses : new[] { line };
                foreach (var clause in subBeats)
                {
                    string p = clause.Trim();
                    if (string.IsNullOrEmpty(p)) continue;
                    string shotType = p.Contains("信") || p.Contains("特写") ? "CloseUp_PropFocus"
                        : (p.Contains("远") || p.Contains("全景") ? "Wide_Establishing" : "MediumShot_LowAngle");
                    res.beats.Add(new ScriptBeat
                    {
                        character = p.Contains("导演") ? "Director" : "ActorA",
                        action = p,
                        dialogue = string.Empty,
                        suggestedShotType = shotType
                    });
                }
            }
            return res;
        }
    }
}
