using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.RuntimeUI
{
    [Serializable]
    public sealed class SceneObjectEntry
    {
        public string id;
        public string displayName;
        public GameObject target;
        public Vector3 initialPos;
        public Quaternion initialRot;
        public bool isLocked;
        public bool isVisible = true;
    }

    /// <summary>
    /// 场景对象列表与层级面板（Task A2）：
    /// 支持选择聚焦、显示/隐藏、位置锁定（防误触）及一键复位。
    /// </summary>
    public class SceneObjectInspector : MonoBehaviour
    {
        public TMP_Text selectedLabel;
        public TMP_Text statusText;
        public Transform focusMarker;
        public readonly List<SceneObjectEntry> objects = new List<SceneObjectEntry>();
        public string SelectedId { get; private set; }

        void Awake()
        {
            if (objects.Count == 0) RefreshSceneObjects();
        }

        public void RefreshSceneObjects()
        {
            objects.Clear();
            var markers = FindObjectsByType<SemanticMarker>(FindObjectsInactive.Include);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null) continue;
                string id = string.IsNullOrEmpty(m.markerId) ? m.gameObject.name : m.markerId;
                objects.Add(new SceneObjectEntry
                {
                    id = id,
                    displayName = m.gameObject.name,
                    target = m.gameObject,
                    initialPos = m.transform.position,
                    initialRot = m.transform.rotation,
                    isLocked = IsGrabDisabled(m.gameObject),
                    isVisible = m.gameObject.activeSelf
                });
            }
            if (objects.Count > 0 && string.IsNullOrEmpty(SelectedId))
            {
                SelectObject(objects[0].id);
            }
        }

        public bool SelectObject(string id)
        {
            var entry = Find(id);
            if (entry == null || entry.target == null) return false;
            SelectedId = entry.id;
            if (selectedLabel != null) selectedLabel.text = $"选中: {entry.displayName}";
            if (focusMarker != null) focusMarker.position = entry.target.transform.position;
            return true;
        }

        public bool ToggleVisibility(string id)
        {
            var entry = Find(id);
            if (entry == null || entry.target == null) return false;
            entry.isVisible = !entry.isVisible;
            entry.target.SetActive(entry.isVisible);
            RuntimeUiFactory.SetStatusTmp(statusText, $"{entry.displayName}: {(entry.isVisible ? "显示" : "隐藏")}");
            return true;
        }

        public bool ToggleLock(string id)
        {
            var entry = Find(id);
            if (entry == null || entry.target == null) return false;
            entry.isLocked = !entry.isLocked;
            var grab = entry.target.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = !entry.isLocked;
            RuntimeUiFactory.SetStatusTmp(statusText, $"{entry.displayName}: {(entry.isLocked ? "已锁定" : "已解锁")}");
            return true;
        }

        public bool ResetObject(string id)
        {
            var entry = Find(id);
            if (entry == null || entry.target == null) return false;
            entry.target.transform.SetPositionAndRotation(entry.initialPos, entry.initialRot);
            if (!entry.target.activeSelf)
            {
                entry.isVisible = true;
                entry.target.SetActive(true);
            }
            RuntimeUiFactory.SetStatusTmp(statusText, $"{entry.displayName}: 已复位");
            return true;
        }

        public void ToggleSelectedVisibility() => ToggleVisibility(SelectedId);
        public void ToggleSelectedLock() => ToggleLock(SelectedId);
        public void ResetSelected() => ResetObject(SelectedId);

        public void CycleSelected()
        {
            if (objects.Count == 0) RefreshSceneObjects();
            if (objects.Count == 0) return;
            int idx = objects.FindIndex(x => x.id == SelectedId);
            int next = (idx + 1) % objects.Count;
            SelectObject(objects[next].id);
        }

        public SceneObjectEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].id == id) return objects[i];
            }
            return null;
        }

        private static bool IsGrabDisabled(GameObject go)
        {
            var grab = go.GetComponent<XRGrabInteractable>();
            return grab != null && !grab.enabled;
        }
    }
}
