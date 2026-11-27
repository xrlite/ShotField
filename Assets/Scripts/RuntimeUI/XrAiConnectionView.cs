using ShotField.Core;
using TMPro;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public sealed partial class XrAiConnectionView : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        private GameObject _settings;

        public static bool IsAnyPanelOpen
        {
            get
            {
                var dock = FindAnyObjectByType<DirectorDockPanel>();
                if (dock != null && dock.IsOpen) return true;
                var view = FindAnyObjectByType<XrAiConnectionView>();
                if (view == null) return false;
                return (view._settings != null && view._settings.activeInHierarchy)
                    || (view._workflow != null && view._workflow.activeInHierarchy);
            }
        }

        public static XrAiConnectionView Create(RectTransform parent)
        {
            var panel = RuntimeUiFactory.CreatePanel(parent, "XrAiConnection", PanelColor,
                new Vector2(0f, 190f), new Vector2(640f, 460f));
            var view = parent.gameObject.AddComponent<XrAiConnectionView>();
            view._settings = panel;
            view.BuildWorkflow(parent);
            view.BuildSettings();
            panel.SetActive(false);
            return view;
        }

        public void Toggle()
        {
            if (_settings.activeSelf)
            {
                _settings.SetActive(false);
                if (_staged != null) _workflow.SetActive(true);
                return;
            }
            if (_staged != null)
                _workflow.SetActive(!_workflow.activeSelf);
            else OpenSettings();
        }

        private void OpenSettings()
        {
            _workflow.SetActive(false);
            _settings.SetActive(true);
            LoadDrafts();
        }
    }
}
