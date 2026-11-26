using ByteDance.PICO.OpenXR;
using ByteDance.PICO.XR;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Visual-only director hall behavior. The opaque hall yields to PICO VST;
    /// the emulator's virtual room remains the passthrough environment.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class WarmHallVisualController : MonoBehaviour
    {
        public Transform hallEnvironment;
        public Transform directorTable;
        public GameObject[] vstHiddenObjects;
        public TabletopDirector tabletop;
        public GameObject[] legacyStageRoots;
        public bool followTabletopAnchor = true;
        public bool usePassthroughState = true;

        private bool _lastHallVisible = true;
        private bool _dupSofaHidden;

        // PICO Emulator (x86_64/Houdini) delivers no OpenXR passthrough frames and keeps
        // immersive focus on systemui while VST is requested; keep the hall there instead.
        private static bool IsEmulator =>
            SystemInfo.deviceModel.Contains("x86") || SystemInfo.processorType.Contains("x86");

        private void Awake()
        {
            if (tabletop == null) tabletop = FindAnyObjectByType<TabletopDirector>();
            if (!Application.isEditor)
            {
                bool vst = !IsEmulator;
                PassthroughFeature.EnableVideoSeeThrough = vst;
                PXR_Manager.EnableVideoSeeThrough = vst;
            }
            HideLegacyStage();
        }

        private void HideLegacyStage()
        {
            if (!_dupSofaHidden && GameObject.Find("sofa_1") != null)
            {
                // 去重方向此前是反的：sofa_1 是运行时拼出的重复沙发，默认位姿悬在
                // Y=1.13 且横在观众位正前方 1.7 m；场景里的 SC01_Blockout/Sofa 位置正确。
                // 保留场景沙发、收起重复件，1:1 首帧才不会糊住茶几与信封。
                var dupSofa = GameObject.Find("sofa_1");
                if (dupSofa != null)
                {
                    dupSofa.SetActive(false);
                    _dupSofaHidden = true;
                }
            }
            if (legacyStageRoots == null) return;
            foreach (var go in legacyStageRoots)
            {
                if (go == null || !go.activeSelf) continue;
                // TabletopDirector owns the same room root in scenes built before WarmHall.
                if (tabletop != null && go.transform == tabletop.sceneRoot1to1) continue;
                go.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            HideLegacyStage();
            if (followTabletopAnchor && tabletop != null)
            {
                var anchor = tabletop.TabletopAnchor.position;
                var p = transform.position;
                p.x = anchor.x;
                p.z = anchor.z;
                transform.position = p;
            }

            bool requested = usePassthroughState && !Application.isEditor &&
                             PassthroughFeature.EnableVideoSeeThrough;
            bool extensionReady = !Application.isEditor && PassthroughFeature.isExtensionEnable;
            bool passthrough = requested && extensionReady && !IsEmulator;
            bool tabletopMode = tabletop == null || tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini;
            bool visible = tabletopMode && !passthrough;
            if (hallEnvironment != null && hallEnvironment.gameObject.activeSelf != visible)
                hallEnvironment.gameObject.SetActive(visible);
            if (directorTable != null && tabletop != null)
            {
                bool tableVisible = tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini;
                if (directorTable.gameObject.activeSelf != tableVisible)
                    directorTable.gameObject.SetActive(tableVisible);
            }
            if (vstHiddenObjects != null)
                foreach (var go in vstHiddenObjects)
                    if (go != null && go.activeSelf != visible) go.SetActive(visible);
            if (visible != _lastHallVisible)
            {
                _lastHallVisible = visible;
                RenderSettings.fog = visible;
                Debug.Log($"[WarmHall] environment={(visible ? "VR" : tabletopMode ? "VST" : "STUDIO")} requested={requested} extensionReady={extensionReady}");
            }
        }
    }
}
