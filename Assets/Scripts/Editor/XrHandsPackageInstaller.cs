using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class XrHandsPackageInstaller
    {
        private static readonly string[] PackagesToAdd = { "com.unity.xr.hands@1.7.3" };
        private const double TimeoutSeconds = 600;
        private static AddAndRemoveRequest _request;
        private static double _deadline;

        public static void Install()
        {
            Debug.Log("[XrHandsPackageInstaller] Adding com.unity.xr.hands@1.7.3");
            _request = Client.AddAndRemove(packagesToAdd: PackagesToAdd);
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (_request == null) return;
            if (!_request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[XrHandsPackageInstaller] Timed out waiting for UPM.");
                    EditorApplication.Exit(2);
                }
                return;
            }
            EditorApplication.update -= Poll;
            if (_request.Status == StatusCode.Success)
            {
                var names = _request.Result.Select(p => $"{p.name}@{p.version}");
                Debug.Log($"[XrHandsPackageInstaller] Resolved: {string.Join(", ", names)}");
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[XrHandsPackageInstaller] Failed: {_request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
