using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.RuntimeUI
{
    [Serializable]
    public sealed class PropPrefabBinding
    {
        public string propId;
        public string displayName;
        public GameObject prefab;
    }

    /// <summary>
    /// 资产库可视化拾取面板（Task A1）：
    /// 展示 SC01 真实绑定 Prefab，点击即在片场锚点生成可抓取道具。
    /// </summary>
    public class PropBrowserUI : MonoBehaviour
    {
        public List<PropPrefabBinding> bindings = new List<PropPrefabBinding>();
        public Transform spawnAnchor;
        public Transform sceneRoot;
        public Text statusText;
        public event Action<GameObject> OnPropSpawned;

        private readonly List<GameObject> spawnedProps = new List<GameObject>();

        public int BindingCount => bindings != null ? bindings.Count : 0;
        public int SpawnedCount => spawnedProps.Count;

        public GameObject SpawnProp(string propId)
        {
            Vector3 pos = spawnAnchor != null
                ? spawnAnchor.position + spawnAnchor.forward * 1.2f
                : new Vector3(1.2f, 0.0f, 1.8f);
            pos.y = Mathf.Max(0f, pos.y);
            return SpawnProp(propId, pos);
        }

        public GameObject SpawnProp(string propId, Vector3 worldPos)
        {
            var binding = FindBinding(propId);
            if (binding == null || binding.prefab == null)
            {
                SetStatus($"未绑定资产: {propId}");
                return null;
            }

            Transform parent = sceneRoot != null ? sceneRoot : null;
            var instance = Instantiate(binding.prefab, worldPos, Quaternion.identity, parent);
            instance.name = $"{binding.propId}_{spawnedProps.Count + 1}";

            EnsureGrabReady(instance, binding.propId);
            spawnedProps.Add(instance);

            var entry = PropLibrary.Find(binding.propId);
            string label = entry != null ? entry.displayName : binding.displayName;
            SetStatus($"已放置: {label}");
            OnPropSpawned?.Invoke(instance);
            return instance;
        }

        public void BindButton(Button btn, string propId)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => SpawnProp(propId));
        }

        private PropPrefabBinding FindBinding(string propId)
        {
            if (string.IsNullOrEmpty(propId) || bindings == null) return null;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i] != null && bindings[i].propId == propId) return bindings[i];
            }
            return null;
        }

        private static void EnsureGrabReady(GameObject go, string propId)
        {
            if (go.GetComponentInChildren<Collider>() == null)
            {
                go.AddComponent<BoxCollider>();
            }
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (go.GetComponent<XRGrabInteractable>() == null)
            {
                go.AddComponent<XRGrabInteractable>();
            }
            if (go.GetComponent<ScenePropGrab>() == null)
            {
                go.AddComponent<ScenePropGrab>();
            }
            if (go.GetComponent<SemanticMarker>() == null)
            {
                var marker = go.AddComponent<SemanticMarker>();
                marker.markerId = go.name;
                var entry = PropLibrary.Find(propId);
                if (entry != null) marker.kind = entry.semanticKind;
            }
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }
    }
}
