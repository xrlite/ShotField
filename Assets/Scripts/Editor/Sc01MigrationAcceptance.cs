#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShotField.EditorTools
{
    [InitializeOnLoad]
    internal static class Sc01MigrationAcceptance
    {
        static Sc01MigrationAcceptance()
        {
            if (!Application.isBatchMode) EditorApplication.delayCall += Verify;
        }

        private static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null) return;

            var path = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../05_验证记录/2026-11/sc01_migration_review_20261120.log"));
            if (File.Exists(path)) return;


            void Record(string message, string stack, LogType type)
            {
                File.AppendAllText(path, $"[{type}] {message}{Environment.NewLine}");
            }

            File.WriteAllText(path, "SC01 screenshot export started" + Environment.NewLine);
            Application.logMessageReceived += Record;
            try
            {
                var preview = EditorSceneManager.OpenPreviewScene(BuildAndroid.ScenePath);
                try { BatchEvidence.ExportSceneViews(preview); }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= Record;
            }
        }
    }
}
#endif
