using System;
using System.IO;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

namespace ShotField.EditorTools
{
    public static class WorkbenchExportBinding
    {
        const string ScenePath = BuildAndroid.ScenePath;

        internal static void Attach(SpatialWorkbench wb)
        {
            if (!wb.exportBtn)
                wb.exportBtn = RuntimeUiFactory.CreateTmpButton(wb.transform, "ExportBtn",
                    "Export ZIP", new Vector2(-110f, -310f));
            if (Bound(wb)) return;
            UnityEventTools.AddPersistentListener(wb.exportBtn.onClick, wb.ExportReferencePackage);
            var click = wb.exportBtn.onClick;
            click.SetPersistentListenerState(click.GetPersistentEventCount()-1, UnityEventCallState.EditorAndRuntime);
        }

        static bool Bound(SpatialWorkbench wb)
        {
            if (!wb || !wb.isActiveAndEnabled || !wb.exportBtn || !wb.exportBtn.isActiveAndEnabled ||
                !wb.exportBtn.IsInteractable() || !wb.exportBtn.transform.IsChildOf(wb.transform) ||
                !wb.library || !wb.monitor || !wb.monitor.sourceCamera) return false;
            var click = wb.exportBtn.onClick;
            for (int i = 0; i < click.GetPersistentEventCount(); i++)
                if (click.GetPersistentTarget(i) == wb &&
                    click.GetPersistentMethodName(i) == nameof(wb.ExportReferencePackage) &&
                    click.GetPersistentListenerState(i) != UnityEventCallState.Off) return true;
            return false;
        }

        static SpatialWorkbench Open()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in scene.GetRootGameObjects())
            {
                var wb = root.GetComponentInChildren<SpatialWorkbench>();
                if (wb) return wb;
            }
            throw new InvalidOperationException("SC01 workbench missing.");
        }

        internal static bool SavedBindingExists() => Bound(Open());

        public static void WireAndExport()
        {
            try
            {
                var wb = Open();
                Attach(wb);
                if (!EditorSceneManager.SaveScene(wb.gameObject.scene))
                    throw new IOException("SC01 save failed.");
                wb = Open();
                if (!Bound(wb)) throw new InvalidOperationException("Saved binding missing.");
                if (wb.SelectedShot == null) wb.library.CreateShot(SceneCaptureMode.Current, "SC01 director view");
                wb.exportBtn.onClick.Invoke();
                if (!File.Exists(wb.LastExportPath)) throw new IOException("Button produced no archive.");
                Debug.Log("WORKBENCH-EXPORT-RESULT=PASS path=" + wb.LastExportPath);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("WORKBENCH-EXPORT-RESULT=FAIL " + ex);
                EditorApplication.Exit(1);
            }
        }
    }
}
