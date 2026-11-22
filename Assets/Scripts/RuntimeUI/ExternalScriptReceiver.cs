using System;
using System.Threading.Tasks;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public class ExternalScriptReceiver : MonoBehaviour, IScriptImportService
    {
        private AiScriptParser _parser;

        public string LastImportedScript { get; private set; } = string.Empty;
        public ScriptAnalysisResult LastParsedResult { get; private set; }

        public event Action<string, ScriptAnalysisResult> OnScriptImported;

        private void Awake()
        {
            _parser = DeepSeekConfig.CreateParser(LabFeatureFlags.OfflineDemo);
        }

        private void Start()
        {
            CheckAndroidIntent();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                CheckAndroidIntent();
            }
        }

        public void CheckAndroidIntent()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (currentActivity == null) return;

                using var intent = currentActivity.Call<AndroidJavaObject>("getIntent");
                if (intent == null) return;

                string action = intent.Call<string>("getAction");
                if (action == "android.intent.action.SEND")
                {
                    string extraText = intent.Call<string>("getStringExtra", "android.intent.extra.TEXT");
                    if (!string.IsNullOrEmpty(extraText))
                    {
                        ImportScript(extraText, "AndroidIntent:ACTION_SEND");
                        try
                        {
                            intent.Call("removeExtra", "android.intent.extra.TEXT");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[ExternalScriptReceiver] removeExtra failed: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ExternalScriptReceiver] JNI Intent Check failed: {ex.Message}");
            }
#endif
        }

        public bool ImportScript(string rawScript, string sourceInfo = "External")
        {
            if (string.IsNullOrWhiteSpace(rawScript)) return false;

            LastImportedScript = rawScript;
            if (_parser == null) _parser = DeepSeekConfig.CreateParser(LabFeatureFlags.OfflineDemo);

            bool offline = LabFeatureFlags.OfflineDemo || !DeepSeekConfig.Load().IsConfigured;
            if (offline)
            {
                LastParsedResult = ScriptTaskRunner.RunSync(_parser.ParseScriptAsync(rawScript));
                OnScriptImported?.Invoke(LastImportedScript, LastParsedResult);
            }
            else
            {
                _ = ExecuteAsyncImport(rawScript);
            }
            return true;
        }

        private async Task ExecuteAsyncImport(string script)
        {
            try
            {
                LastParsedResult = await _parser.ParseScriptAsync(script);
                OnScriptImported?.Invoke(LastImportedScript, LastParsedResult);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ExternalScriptReceiver] Async import failed: {ex.Message}");
            }
        }

        public bool SimulateExternalShare(string scriptText)
        {
            return ImportScript(scriptText, "SimulatedShare");
        }
    }
}
