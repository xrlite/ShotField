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
        private Light[] _hallLights;
        private bool[] _authoredLightsEnabled;
        private Light _rimLight;

        // PICO Emulator (x86_64/Houdini) delivers no OpenXR passthrough frames and keeps
        // immersive focus on systemui while VST is requested; keep the hall there instead.
        private static bool IsEmulator =>
            SystemInfo.deviceModel.Contains("x86") || SystemInfo.processorType.Contains("x86");

        private void Awake()
        {
            if (tabletop == null) tabletop = FindAnyObjectByType<TabletopDirector>();
            _hallLights = GetComponentsInChildren<Light>(true);
            _authoredLightsEnabled = System.Array.ConvertAll(_hallLights, light => light.enabled);
            if (!Application.isEditor)
            {
                bool vst = !IsEmulator;
                PassthroughFeature.EnableVideoSeeThrough = vst;
                PXR_Manager.EnableVideoSeeThrough = vst;
            }
            HideLegacyStage();
            EnsureRimLight();
        }

        private void EnsureRimLight()
        {
            if (_rimLight != null) return;
            var go = new GameObject("WarmHall_TabletopRimLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.4f, 2.35f, 1.35f);
            go.transform.LookAt(transform.position + Vector3.up * 0.85f);
            _rimLight = go.AddComponent<Light>();
            _rimLight.type = LightType.Spot;
            _rimLight.color = new Color(0.68f, 0.80f, 1.0f, 1f);
            _rimLight.intensity = 2.2f;
            _rimLight.range = 6.5f;
            _rimLight.spotAngle = 52f;
            _rimLight.shadows = LightShadows.None;
        }

        private void HideLegacyStage()
        {
            if (!_dupSofaHidden && GameObject.Find("sofa_1") != null)
            {
                // 去重方向此前是反的：sofa_1 是运行时拼出的重复沙发，默认位姿悬在
                // Y=1.13 且横在观众位正前方 1.7 m；场景里的 SC01_Blockout/Sofa 位置正确。
                // 保留场景沙发、收起重复件，1:1 首帧才不会糊住茶几与核心道具。
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
            for (int i = 0; i < _hallLights.Length; i++)
                if (_hallLights[i] != null)
                    _hallLights[i].enabled = visible && _authoredLightsEnabled[i];
            if (_rimLight != null) _rimLight.enabled = visible;
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
                if (visible)
                {
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = new Color(0.04f, 0.045f, 0.055f, 1f);
                    RenderSettings.fogDensity = 0.032f;
                }
                Debug.Log($"[WarmHall] environment={(visible ? "VR" : tabletopMode ? "VST" : "STUDIO")} requested={requested} extensionReady={extensionReady}");
            }
        }
    }
}
