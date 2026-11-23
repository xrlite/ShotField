using System;
using System.Collections.Generic;
using System.Linq;
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
    public class PropBrowserUI : MonoBehaviour
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
            var binding = FindBinding(propId);
            if (binding == null || binding.prefab == null)
            {
                RuntimeUiFactory.SetStatusTmp(statusText, $"未绑定资产: {propId}");
                return null;
            }

            Transform parent = sceneRoot != null ? sceneRoot : null;
            var instance = Instantiate(binding.prefab, worldPos, Quaternion.identity, parent);
            instance.name = $"{binding.propId}_{spawnedProps.Count + 1}";

            EnsureGrabReady(instance, binding.propId);
            spawnedProps.Add(instance);

            var entry = PropLibrary.Find(binding.propId);
            string label = entry != null ? entry.displayName : binding.displayName;
            RuntimeUiFactory.SetStatusTmp(statusText, $"已放置: {label}");
            OnPropSpawned?.Invoke(instance);
            return instance;
        }

        public void BindButton(Button btn, string propId)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => SpawnProp(propId));
        }

        // 六色演员条目：点击把场景内待激活的 PrevisCast 实例放行入场。
        // 注册走 ActorNavigator.Registry partial 的 OnEnable，不依赖面板手动 Bind。
        private static readonly string[] ActorIds =
        {
            "actorRed", "actorBlue", "actorYellow",
            "actorGreen", "actorViolet", "actorMagenta"
        };

        private static readonly Dictionary<string, string> ActorSceneName = new Dictionary<string, string>
        {
            ["actorRed"] = "PrevisCast_A_Red",
            ["actorBlue"] = "PrevisCast_B_Blue",
            ["actorYellow"] = "PrevisCast_C_Yellow",
            ["actorGreen"] = "PrevisCast_D_Green",
            ["actorViolet"] = "PrevisCast_E_Violet",
            ["actorMagenta"] = "PrevisCast_F_Magenta"
        };

        private bool actorButtonsBuilt;

        private void Start() => BuildActorButtons();

        private void BuildActorButtons()
        {
            if (actorButtonsBuilt) return;
            actorButtonsBuilt = true;
            var panel = statusText != null ? statusText.transform.parent : transform;
            for (var i = 0; i < ActorIds.Length; i++)
            {
                var entry = PropLibrary.Find(ActorIds[i]);
                var label = entry != null ? entry.displayName : ActorIds[i];
                var x = i % 2 == 0 ? -48f : 48f;
                var y = -45f - (i / 2) * 58f;
                var btn = RuntimeUiFactory.CreateTmpButton(panel, $"Spawn_{ActorIds[i]}", label,
                    new Vector2(x, y), new Vector2(88f, 44f));
                var id = ActorIds[i];
                btn.onClick.AddListener(() => ActivateActor(id));
            }
        }

        /// <summary>放行场景内待激活的指定演员；已在场则直接返回。返回是否成功。</summary>
        public bool ActivateActor(string actorId)
        {
            if (!ActorSceneName.TryGetValue(actorId, out var sceneName)) return false;
            var nav = UnityEngine.Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include)
                .FirstOrDefault(n => n.name == sceneName);
            if (nav == null)
            {
                RuntimeUiFactory.SetStatusTmp(statusText, $"未找到演员: {sceneName}");
                return false;
            }
            if (!nav.gameObject.activeSelf) nav.gameObject.SetActive(true);
            RuntimeUiFactory.SetStatusTmp(statusText, $"已加入: {nav.name}");
            OnPropSpawned?.Invoke(nav.gameObject);
            return true;
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
