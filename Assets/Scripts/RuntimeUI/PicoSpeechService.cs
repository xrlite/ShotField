using System;
using ByteDance.PICO.Platform;
using ByteDance.PICO.Platform.Framework;
using ByteDance.PICO.Platform.Models;
using ByteDance.PICO.Platform.Service;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public class PicoSpeechService : MonoBehaviour, ISpeechService
    {
        [SerializeField] private bool isMockMode = false;

        private bool _isListening;
        private bool _isNativeInitialized;

        public bool IsListening => _isListening;
        public bool IsSupported => IsMockMode || Application.platform == RuntimePlatform.Android;
        public bool IsMockMode
        {
            get => isMockMode || Application.isEditor || Application.platform != RuntimePlatform.Android;
            set => isMockMode = value;
        }

        public event Action<string, bool> OnTranscriptionResult;
        public event Action<string> OnSpeechErrorNotice;

        private void Awake()
        {
            InitializePlatformService();
        }

        public void InitializePlatformService()
        {
            if (IsMockMode) return;

#if !UNITY_EDITOR && UNITY_ANDROID
            try
            {
                // AsrEngineInitResult 定义在 ByteDance.PICO.Platform.CLIB 命名空间，不在 Service/Models 下
                var initRes = SpeechService.InitAsrEngine();
                if (initRes == ByteDance.PICO.Platform.CLIB.AsrEngineInitResult.Success
                    || initRes == ByteDance.PICO.Platform.CLIB.AsrEngineInitResult.AlreadyInitialized)
                {
                    SpeechService.SetOnAsrResultCallback(OnNativeAsrResult);
                    SpeechService.SetOnSpeechErrorCallback(OnNativeSpeechError);
                    _isNativeInitialized = true;
                }
                else
                {
                    Debug.LogWarning($"[PicoSpeechService] ASR engine init failed: {initRes}, fallback to mock.");
                    isMockMode = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PicoSpeechService] Platform ASR exception: {ex.Message}, fallback to mock.");
                isMockMode = true;
            }
#endif
        }

        public void StartListening(int maxSeconds = 60)
        {
            _isListening = true;
            if (IsMockMode) return;

#if !UNITY_EDITOR && UNITY_ANDROID
            if (_isNativeInitialized)
            {
                SpeechService.StartAsr(false, true, maxSeconds);
            }
#endif
        }

        public void StopListening()
        {
            _isListening = false;
            if (IsMockMode) return;

#if !UNITY_EDITOR && UNITY_ANDROID
            if (_isNativeInitialized)
            {
                SpeechService.StopAsr();
            }
#endif
        }

        private void OnDestroy()
        {
            if (_isListening)
            {
                StopListening();
            }
        }

        private void OnNativeAsrResult(Message<AsrResult> message)
        {
            if (message != null && !message.IsError && message.Data != null)
            {
                var data = message.Data;
                OnTranscriptionResult?.Invoke(data.Text, data.IsFinalResult);
                if (data.IsFinalResult) _isListening = false;
            }
        }

        private void OnNativeSpeechError(Message<SpeechError> message)
        {
            _isListening = false;
            string detail = message?.Data != null ? $"{message.Data.Code}: {message.Data.Message}" : "Unknown";
            Debug.LogWarning($"[PicoSpeechService] Native Speech Error: {detail}");
            OnSpeechErrorNotice?.Invoke("请导演再说一遍（语音网络波动）");
        }

        public void SimulateSpeechResult(string text, bool isFinal)
        {
            if (isFinal) _isListening = false;
            OnTranscriptionResult?.Invoke(text, isFinal);
        }

        public void SimulateSpeechError(int code, string message)
        {
            _isListening = false;
            Debug.LogWarning($"[PicoSpeechService] Simulated Speech Error ({code}): {message}");
            OnSpeechErrorNotice?.Invoke("请导演再说一遍（语音网络波动）");
        }
    }
}
