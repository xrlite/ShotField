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
    public partial class PropBrowserUI : MonoBehaviour
    {
        public List<PropPrefabBinding> bindings = new List<PropPrefabBinding>();
        public Transform spawnAnchor;
        public Transform sceneRoot;
        public TextMeshProUGUI statusText;
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
            var instance = CreateProp(propId, worldPos);
            if (instance != null) OnPropSpawned?.Invoke(instance);
            return instance;
        }

        /// <summary>事务式生成：即使外部回调抛异常，也把已创建实例交回调用方处理。</summary>
        public bool TrySpawnProp(string propId, Vector3 worldPos, out GameObject instance, out string error)
        {
            instance = null; error = "";
            GameObject created = null;
            try
            {
                created = CreateProp(propId, worldPos, value => created = value);
                instance = created;
                if (instance == null) { error = "未绑定资产: " + propId; return false; }
            }
            catch (Exception ex)
            {
                if (created != null)
                {
                    RemoveSpawnedProp(created);
                    if (Application.isPlaying) Destroy(created); else DestroyImmediate(created);
                    instance = null;
                }
                error = ex.Message; return false;
            }
            try { OnPropSpawned?.Invoke(instance); }
            catch (Exception ex) { error = ex.Message; }
            return true;
        }

        private GameObject CreateProp(string propId, Vector3 worldPos, Action<GameObject> onCreated = null)
        {
            var binding = FindBinding(propId);
            if (binding == null || binding.prefab == null)
            {
                RuntimeUiFactory.SetStatusTmp(statusText, $"未绑定资产: {propId}");
                return null;
            }

            Transform parent = sceneRoot != null ? sceneRoot : null;
            var instance = Instantiate(binding.prefab, worldPos, Quaternion.identity, parent);
            onCreated?.Invoke(instance);
            instance.name = $"{binding.propId}_{spawnedProps.Count + 1}";

            EnsureGrabReady(instance, binding.propId);
            spawnedProps.Add(instance);

            var entry = PropLibrary.Find(binding.propId);
            string label = entry != null ? entry.displayName : binding.displayName;
            RuntimeUiFactory.SetStatusTmp(statusText, $"已放置: {label}");
            return instance;
        }

        public bool RemoveSpawnedProp(GameObject instance) => instance != null && spawnedProps.Remove(instance);

        public void BindButton(Button btn, string propId)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => SpawnProp(propId));
        }

        /// <summary>
        /// 取某条目的绑定 Prefab（只读，不实例化）。放置交互器用它拿预览源，
        /// 避免重复维护一份"哪个 id 对应哪个 prefab"的映射。
        /// </summary>
        public GameObject FindPrefab(string propId)
        {
            var binding = FindBinding(propId);
            return binding != null ? binding.prefab : null;
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

        /// <summary>
        /// 给实例补齐抓取三件套（非 trigger Collider + Rigidbody + XRGrabInteractable）
        /// 并挂 ScenePropGrab / SemanticMarker。放置交互器确认落地时复用同一套。
        /// </summary>
        public static void EnsureGrabReady(GameObject go, string propId)
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
            var marker = go.GetComponent<SemanticMarker>();
            if (marker == null) marker = go.AddComponent<SemanticMarker>();
            string baseId = string.IsNullOrEmpty(go.name) ? propId : go.name;
            string uniqueId = baseId;
            int suffix = 2;
            var other = SemanticMarker.Find(uniqueId, FindObjectsInactive.Include);
            while (other != null && other != marker)
            {
                uniqueId = $"{baseId}_{suffix++}";
                other = SemanticMarker.Find(uniqueId, FindObjectsInactive.Include);
            }
            marker.RebindId(uniqueId);
            var entry = PropLibrary.Find(propId);
            if (entry != null) marker.kind = entry.semanticKind;
        }
    }
}
