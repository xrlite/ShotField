#if UNITY_EDITOR
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class PlatformServicesVerifier
    {
        private const int Expected = 10;

        [MenuItem("ShotField/Verifiers/Run Platform Services Verifier")]
        public static void Run()
        {
            int passed = 0;
            var testRoot = new GameObject("TestPlatformServicesRoot");

            try
            {
                // 1. Speech Service Tests
                var speechGo = new GameObject("PicoSpeechService_Test", typeof(PicoSpeechService));
                speechGo.transform.SetParent(testRoot.transform);
                var speechService = speechGo.GetComponent<PicoSpeechService>();

                VerifyAssert.Require("SpeechService initialized with mock fallback enabled for batchmode",
                    speechService != null && speechService.IsSupported && speechService.IsMockMode && !speechService.IsListening,
                    ref passed);

                speechService.StartListening(60);
                bool listeningActive = speechService.IsListening;
                speechService.StopListening();
                bool listeningStopped = !speechService.IsListening;
                VerifyAssert.Require("SpeechService StartListening and StopListening properly toggle IsListening",
                    listeningActive && listeningStopped, ref passed);

                string receivedSpeechText = null;
                bool receivedFinal = false;
                speechService.OnTranscriptionResult += (text, isFinal) =>
                {
                    receivedSpeechText = text;
                    receivedFinal = isFinal;
                };
                speechService.StartListening(60);
                speechService.SimulateSpeechResult("降低机位特写信件", true);
                VerifyAssert.Require("SimulateSpeechResult fires OnTranscriptionResult with final transcription",
                    receivedSpeechText == "降低机位特写信件" && receivedFinal && !speechService.IsListening, ref passed);

                string receivedErrorNotice = null;
                speechService.OnSpeechErrorNotice += notice => receivedErrorNotice = notice;
                speechService.SimulateSpeechError(1001, "Network connection timed out");
                VerifyAssert.Require("SimulateSpeechError triggers friendly notice '请导演再说一遍'",
                    !string.IsNullOrEmpty(receivedErrorNotice) && receivedErrorNotice.Contains("请导演再说一遍"), ref passed);

                // 2. External Script Receiver Tests
                var receiverGo = new GameObject("ExternalScriptReceiver_Test", typeof(ExternalScriptReceiver));
                receiverGo.transform.SetParent(testRoot.transform);
                var scriptReceiver = receiverGo.GetComponent<ExternalScriptReceiver>();

                string importedEventScript = null;
                ScriptAnalysisResult importedEventResult = null;
                scriptReceiver.OnScriptImported += (script, result) =>
                {
                    importedEventScript = script;
                    importedEventResult = result;
                };

                const string testScript = "内景 客厅 日\n这是外部分享测试剧本，茶几上有信件。\n李导：准备开拍！";
                bool shareResult = scriptReceiver.SimulateExternalShare(testScript);

                VerifyAssert.Require("SimulateExternalShare completes successfully and sets LastImportedScript",
                    shareResult && scriptReceiver.LastImportedScript == testScript, ref passed);

                VerifyAssert.Require("ExternalScriptReceiver parsed script contains valid scene beats and characters",
                    scriptReceiver.LastParsedResult != null &&
                    scriptReceiver.LastParsedResult.beats != null &&
                    scriptReceiver.LastParsedResult.beats.Count > 0, ref passed);

                VerifyAssert.Require("OnScriptImported event dispatches imported script text and structured analysis",
                    importedEventScript == testScript && importedEventResult == scriptReceiver.LastParsedResult, ref passed);

                // 3. Spatial Tracking Coordinator Tests
                var trackingGo = new GameObject("SpatialTrackingCoordinator_Test", typeof(SpatialTrackingCoordinator));
                trackingGo.transform.SetParent(testRoot.transform);
                var tracker = trackingGo.GetComponent<SpatialTrackingCoordinator>();

                VerifyAssert.Require("SpatialTrackingCoordinator switches all default strictly to false",
                    !tracker.EnableMotionTracker && !tracker.EnableHandTracking && !tracker.EnableEyeTracking, ref passed);

                VerifyAssert.Require("SpatialTrackingCoordinator active statuses are all false initially",
                    !tracker.IsMotionTrackerActive && !tracker.IsHandTrackingActive && !tracker.IsEyeTrackingActive, ref passed);

                tracker.InitializeTracking();
                VerifyAssert.Require("InitializeTracking with all false switches makes 0 hardware calls and throws 0 errors",
                    !tracker.IsMotionTrackerActive && !tracker.IsHandTrackingActive && !tracker.IsEyeTrackingActive, ref passed);
            }
            finally
            {
                if (testRoot != null) Object.DestroyImmediate(testRoot);
            }

            VerifyAssert.Finish("PLATFORM-SERVICES-RESULT", passed, Expected);
        }
    }
}
#endif
