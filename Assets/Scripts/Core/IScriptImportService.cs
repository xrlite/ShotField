using System;

namespace ShotField.Core
{
    public interface IScriptImportService
    {
        bool ImportScript(string rawScript, string sourceInfo = "External");
        string LastImportedScript { get; }
        ScriptAnalysisResult LastParsedResult { get; }
        event Action<string, ScriptAnalysisResult> OnScriptImported;
    }
}
