using System;

namespace ShotField.Core
{
    public interface ISpeechService
    {
        bool IsListening { get; }
        bool IsSupported { get; }
        void StartListening(int maxSeconds = 60);
        void StopListening();
        event Action<string, bool> OnTranscriptionResult;
        event Action<string> OnSpeechErrorNotice;
    }
}
