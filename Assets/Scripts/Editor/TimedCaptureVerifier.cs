#if UNITY_EDITOR
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class TimedCaptureVerifier
    {
        private const int Expected = 4;

        [MenuItem("ShotField/Verifiers/Run Timed Capture Verifier")]
        public static void Run()
        {
            int passed = 0;
            var clock = new TimedCaptureClock();

            clock.BeginRecording(10.0f, 10);
            bool early = clock.PollSample(10.05f, 10, out _);
            VerifyAssert.Require("TimedCaptureClock suppresses sample before interval", !early, ref passed);

            bool onTime = clock.PollSample(10.12f, 10, out float elapsed);
            VerifyAssert.Require("TimedCaptureClock triggers sample on interval with accurate elapsed",
                onTime && Mathf.Abs(elapsed - 0.12f) < 0.001f, ref passed);

            float total = clock.EndRecording(10.0f);
            VerifyAssert.Require("TimedCaptureClock clamps zero-length duration safely",
                total >= 0.001f, ref passed);

            clock.BeginPlayback(20.0f);
            VerifyAssert.Require("TimedCaptureClock computes playback elapsed cleanly",
                Mathf.Abs(clock.PlaybackElapsed(22.5f) - 2.5f) < 0.001f, ref passed);

            VerifyAssert.Finish("TIMED-CAPTURE-RESULT", passed, Expected);
        }
    }
}
#endif
