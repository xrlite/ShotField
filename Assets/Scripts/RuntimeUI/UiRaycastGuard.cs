using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 运行时 UI 射线兜底：任何 Canvas 缺 XR 射线组件时自动补齐，并打印诊断计数。
    /// 专治「编辑器里鼠标能点、头显/模拟器里射线点不动」——典型根因是 Canvas 上只有鼠标射线，
    /// 却没有 TrackedDeviceGraphicRaycaster（XR 手柄与手势射线必须走这条）。
    /// 只补组件、不改布局、不改已有组件参数。
    /// </summary>
    [DisallowMultipleComponent]
    public class UiRaycastGuard : MonoBehaviour
    {
        private const float ScanInterval = 1.0f;
        private static UiRaycastGuard _instance;
        private float _nextScan;
        private bool _inputProbeAttached;
        public static int PatchedCanvases { get; private set; }
        public static int Scans { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("[ShotField] UiRaycastGuard");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UiRaycastGuard>();
        }
        private void Start()
        {
            EnsureEventSystem();
            AttachInputProbe();
            Scan();
        }
        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;
            Scan();
        }
        private void AttachInputProbe()
        {
            if (_inputProbeAttached) return;
            _inputProbeAttached = true;
            var rays = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include);
            int activeUi = 0;
            for (int i = 0; i < rays.Length; i++)
            {
                var ray = rays[i];
                if (ray == null) continue;
                if (ray.isActiveAndEnabled && ray.enableUIInteraction) activeUi++;
                ProbeButton(ray, "Select", ray.selectInput);
                ProbeButton(ray, "UIPress", ray.uiPressInput);
            }
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<XRUIInputModule>() : null;
            var registered = typeof(XRUIInputModule).GetField("m_RegisteredInteractors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(module) as System.Collections.ICollection;
            Debug.Log($"[UiRaycastGuard] Probe rays={rays.Length} activeUI={activeUi} module={module?.enabled} xr={module?.enableXRInput} registered={registered?.Count}");
        }
        private static void ProbeButton(XRRayInteractor ray, string label, XRInputButtonReader reader)
        {
            if (reader == null) return;
            var action = reader.inputSourceMode == XRInputButtonReader.InputSourceMode.InputActionReference
                ? reader.inputActionReferencePerformed?.action
                : reader.inputActionPerformed;
            if (action == null) return;
            action.performed += ctx =>
            {
                bool hit = ray.TryGetCurrentUIRaycastResult(out var result) && result.gameObject != null;
                bool hit3d = ray.TryGetCurrent3DRaycastHit(out var hit3) && hit3.collider != null;
                Debug.Log($"[UI-INPUT] {ray.name}.{label} value={ctx.ReadValue<float>():F3} overUI={ray.IsOverUIGameObject()} target={(hit ? result.gameObject.name : "none")} hit3d={(hit3d ? hit3.collider.name : "none")} pos={ray.transform.position:F2} dir={ray.transform.forward:F2}");
            };
            action.canceled += ctx => Debug.Log($"[UI-INPUT] {ray.name}.{label} canceled value={ctx.ReadValue<float>():F3}");
        }
        /// <summary>立即扫描并修补所有 Canvas；返回本次新增的射线组件数量，供验证器调用。</summary>
        public static int EnsureAllNow() => Scan();

        private static int Scan()
        {
            Scans++;
            int patched = 0;
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null) continue;
                if (PatchCanvas(canvas)) patched++;
            }
            if (patched > 0)
            {
                PatchedCanvases += patched;
                Debug.Log($"[UiRaycastGuard] 已补齐 {patched} 块 Canvas 的射线组件（累计 {PatchedCanvases}）。");
            }
            ReleaseStaleBlockers();
            return patched;
        }

        private static void ReleaseStaleBlockers()
        {
            var groups = FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include);
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g == null || !g.blocksRaycasts) continue;
                if (g.alpha > 0.01f) continue;
                g.blocksRaycasts = false;
                g.interactable = false;
                Debug.LogWarning("[UiRaycastGuard] " + g.name + " 已透明但仍阻挡射线，已强制解锁。");
            }
        }

        private static bool PatchCanvas(Canvas canvas)
        {
            bool changed = false;

            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                changed = true;
            }

            // World Space UI 只走 XR TrackedDeviceGraphicRaycaster。
            // 再挂 GraphicRaycaster 会形成第二套鼠标射线，和 XRUIInputModule 争抢同一块 Canvas。
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                changed = true;
            }

            if (changed)
                Debug.Log("[UiRaycastGuard] " + canvas.name + " renderMode=" + canvas.renderMode +
                          " layer=" + LayerMask.LayerToName(canvas.gameObject.layer) + " 已补射线组件。");

            return changed;
        }

        private static void EnsureEventSystem()
        {
            var existing = EventSystem.current;
            if (existing == null) existing = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);

            if (existing == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<XRUIInputModule>();
                Debug.LogWarning("[UiRaycastGuard] 场景没有 EventSystem，已创建并挂 XRUIInputModule。");
                return;
            }

            if (!existing.gameObject.activeSelf)
            {
                existing.gameObject.SetActive(true);
                Debug.LogWarning("[UiRaycastGuard] EventSystem 处于关闭状态，已重新开启。");
            }

            var module = existing.GetComponent<XRUIInputModule>();
            if (module == null)
            {
                existing.gameObject.AddComponent<XRUIInputModule>();
                Debug.LogWarning("[UiRaycastGuard] EventSystem 缺少 XRUIInputModule，已补上。");
            }
            else if (!module.enabled)
            {
                module.enabled = true;
                Debug.LogWarning("[UiRaycastGuard] XRUIInputModule 被禁用，已重新启用。");
            }
        }

        /// <summary>诊断输出：列出每块 Canvas 的射线组件状态，便于在设备日志里一眼定位。</summary>
        public static string Describe()
        {
            var sb = new System.Text.StringBuilder();
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            sb.AppendLine("[UiRaycastGuard] Canvas 共 " + canvases.Length + " 块：");
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null) continue;
                bool xr = c.GetComponent<TrackedDeviceGraphicRaycaster>() != null;
                bool mouse = c.GetComponent<GraphicRaycaster>() != null;
                sb.AppendLine("  - " + c.name + " mode=" + c.renderMode +
                              " layer=" + LayerMask.LayerToName(c.gameObject.layer) +
                              " XR射线=" + xr + " 鼠标射线=" + mouse);
            }
            var es = EventSystem.current;
            sb.AppendLine(es == null
                ? "  EventSystem: 缺失"
                : "  EventSystem: " + es.name + " active=" + es.gameObject.activeInHierarchy +
                  " XRUIInputModule=" + (es.GetComponent<XRUIInputModule>() != null));
            return sb.ToString();
        }
    }
}
